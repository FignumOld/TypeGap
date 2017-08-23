using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TypeGap.Util;

namespace TypeGap
{

    public enum ApiParameterMode
    {
        Unspecified = 0,
        FromBody,
        FromUri,
    }

    public enum ApiMethod
    {
        Get,
        Post,
        Delete,
        Patch,
        Put,
    }

    public class ApiControllerDesc
    {
        private string _controllerName;

        public string ControllerName
        {
            get { return _controllerName; }
            set
            {
                _controllerName = value.Replace("Controller", "");
            }
        }

        public string RouteTemplate { get; set; } = "api/[controller]";
        public List<ApiActionDesc> Actions { get; set; } = new List<ApiActionDesc>();

        public ApiActionDesc AddAction(string name, Type returnType, ApiMethod method = ApiMethod.Get, string route = null, IEnumerable<ApiParamDesc> parameters = null)
        {
            var action = new ApiActionDesc();
            action.ActionName = name;
            action.ReturnType = returnType;
            action.Method = method;

            if (!String.IsNullOrWhiteSpace(route))
                action.RouteTemplate = route;

            if (parameters != null)
                action.Parameters.AddRange(parameters);

            Actions.Add(action);
            return action;
        }
    }

    public class ApiActionDesc
    {
        public string ActionName { get; set; }
        public string RouteTemplate { get; set; } = "[action]";
        public ApiMethod Method { get; set; }
        public List<ApiParamDesc> Parameters { get; set; } = new List<ApiParamDesc>();
        public Type ReturnType { get; set; }
    }

    public class ApiParamDesc
    {
        public string ParameterName { get; set; }
        public Type ParameterType { get; set; }
        public bool IsOptional { get; set; }
        public ApiParameterMode Mode { get; set; }
    }

    internal class ApiGenerator
    {
        private readonly TypeConverter _converter;
        private readonly Func<string, string> _rewriter;
        private readonly string _promiseType;
        private readonly string _ajaxPath;

        public ApiGenerator(TypeConverter converter, Func<string, string> rewriter, string promiseType, string ajaxPath)
        {
            _converter = converter;
            _rewriter = rewriter;
            _promiseType = promiseType;
            _ajaxPath = ajaxPath;
        }

        public void WriteServices(ApiControllerDesc[] controllers, IndentedTextWriter writer)
        {
            var controllerNames = controllers.Select(d => d.ControllerName).ToArray();

            writer.WriteLine($"import {{ Ajax, IExtendedAjaxSettings }} from \"{_ajaxPath}\";");

            writer.WriteLine();

            WriteStaticHelper(controllerNames, writer);
            writer.WriteLine();

            foreach (var d in controllers)
            {
                WriteController(d, writer);
                writer.WriteLine();
            }
        }

        private void WriteStaticHelper(string[] names, IndentedTextWriter writer)
        {
            writer.WriteLine($"export class Services {{");
            writer.Indent++;
            foreach (var n in names)
            {
                writer.WriteLine($"public readonly {n}: {n}Service;");
            }

            writer.WriteLine("public constructor(hostname: string, ajaxDefaults?: IExtendedAjaxSettings) {");
            writer.Indent++;
            foreach (var n in names)
            {
                writer.WriteLine($"this.{n} = new {n}Service(hostname, ajaxDefaults);");
            }
            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");
        }

        private void WriteController(ApiControllerDesc controller, IndentedTextWriter writer)
        {
            string urlPrefix = controller.RouteTemplate;
            urlPrefix = urlPrefix.Replace("[controller]", controller.ControllerName);

            writer.WriteLine($"export class {controller.ControllerName}Service {{");
            writer.Indent++;
            writer.WriteLine("private _hostname: string;");
            writer.WriteLine("private _ajax: Ajax;");
            writer.WriteLine("public constructor(hostname: string, ajaxDefaults?: IExtendedAjaxSettings) {");
            writer.Indent++;
            writer.WriteLine("this._hostname = (hostname.substr(-1) == \"/\") ? hostname : hostname + \"/\";");
            writer.WriteLine("this._ajax = new Ajax(ajaxDefaults);");
            writer.Indent--;
            writer.WriteLine("}");

            foreach (var action in controller.Actions)
            {
                writer.WriteLine();
                WriteMethod(action, writer, urlPrefix);
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private void WriteMethod(ApiActionDesc action, IndentedTextWriter writer, string urlPrefix)
        {
            var template = JoinUrls(urlPrefix, action.RouteTemplate);
            template = template.Replace("[action]", action.ActionName);

            var returnString = _converter.GetTypeScriptName(action.ReturnType);

            var httpMethod = action.Method.ToString().ToLower();

            ApiParamDesc postParameter = null;
            ApiParamDesc[] getParameters = ValidateParameters(action.Parameters, httpMethod, template, out postParameter);

            var paramString = BuildMethodParameters(action);
            if (!string.IsNullOrWhiteSpace(paramString))
                paramString += ", ";

            writer.WriteLine($"public {action.ActionName}({paramString}ajaxOptions?: IExtendedAjaxSettings): {_promiseType}<{returnString}> {{");
            writer.Indent++;
            writer.WriteLine("var url = this._hostname + " + BuildUrlString(template, getParameters) + ";");
            writer.WriteLine($"return this._ajax.{httpMethod}(url, {postParameter?.ParameterName ?? "null"}, ajaxOptions);");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private ApiParamDesc[] ValidateParameters(List<ApiParamDesc> parameters, string httpMethod, string routeTemplate, out ApiParamDesc postParam)
        {
            // ReSharper disable once ReplaceWithSingleCallToSingleOrDefault

            var postPossibilities = parameters
                .Where(p => !IsRouteParameter(p.ParameterName, routeTemplate))
                .Where(p => p.Mode != ApiParameterMode.FromUri)
                .Where(p => p.Mode == ApiParameterMode.FromBody || _converter.IsComplexType(p.ParameterType))
                .ToArray();

            if (postPossibilities.Length > 1)
                throw new InvalidOperationException($"Invalid action, can't have more than one candidate for post parameters. Try using [FromBody] or [FromUri] to provide additional context. (at {routeTemplate}");

            var post = postPossibilities.FirstOrDefault();

            if (httpMethod == "get" && post != null)
                throw new InvalidOperationException($"Invalid action, get method can't take complex type in message body. (at {routeTemplate})");

            postParam = post;
            return parameters.Except(new[] { post }).ToArray();
        }

        private string JoinUrls(params string[] url)
        {
            StringBuilder output = new StringBuilder(100);
            foreach (var u in url)
                output.Append("/" + u.Trim('/'));

            return output.ToString().Trim('/');
        }

        private string BuildMethodParameters(ApiActionDesc method)
        {
            var param = method.Parameters;
            return String.Join(", ", param.Select(p => $"{p.ParameterName}{(p.IsOptional ? "?" : "")}: {_converter.GetTypeScriptName(p.ParameterType)}"));
        }

        private bool IsRouteParameter(string name, string template)
        {
            return template.IndexOf("{" + name, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildUrlString(string template, ApiParamDesc[] getParameters)
        {
            List<string> routeParameters = new List<string>();
            StringBuilder url = new StringBuilder();

            var parts = template.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!part.StartsWith("{"))
                {
                    url.Append("/" + part);
                    continue;
                }

                part = part.Substring(1, part.Length - 2);

                var typeIndex = part.IndexOf(":");
                if (typeIndex > 0)
                    part = part.Substring(0, typeIndex);

                routeParameters.Add(part);
                url.Append($"/\" + {part} + \"");
            }

            var finalGetParameters = getParameters.Where(p => routeParameters.All(r => !r.Equals(p.ParameterName, StringComparison.OrdinalIgnoreCase)));
            if (finalGetParameters.Any())
            {
                url.Append("?");
                url.Append(String.Join("&", finalGetParameters.Select(p => $"{p.ParameterName}=\" + {p.ParameterName} + \"")));
            }

            return "\"" + _rewriter(url.ToString().TrimStart('/')) + "\"";
        }
    }
}
