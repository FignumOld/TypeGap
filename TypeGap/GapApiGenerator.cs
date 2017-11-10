using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
        public string RouteTemplate { get; set; }
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

    public class GapApiGenerator
    {
        private readonly TypeConverter _converter;
        private readonly Func<string, string> _rewriter;
        private readonly string _promiseType;
        private readonly string _ajaxPath;

        public GapApiGenerator(TypeConverter converter, Func<string, string> rewriter, string promiseType, string ajaxPath)
        {
            _converter = converter;
            _rewriter = rewriter;
            _promiseType = promiseType;
            _ajaxPath = ajaxPath;
        }

        public virtual void WriteServices(ApiControllerDesc[] controllers, CustomIndentedTextWriter writer)
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

        protected virtual void WriteStaticHelper(string[] names, CustomIndentedTextWriter writer)
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

        protected virtual void WriteController(ApiControllerDesc controller, CustomIndentedTextWriter writer)
        {
            string template = controller.RouteTemplate;
            template = Regex.Replace(template, @"(?:\[controller\]|\{controller\})", controller.ControllerName, RegexOptions.IgnoreCase);

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
                WriteMethod(action, writer, template);
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        protected virtual void WriteMethod(ApiActionDesc action, CustomIndentedTextWriter writer, string template)
        {
            var regex = @"(?:\[action\]|\{action\})";
            var exists = Regex.IsMatch(template, regex, RegexOptions.IgnoreCase);

            if (!exists && action.RouteTemplate == null)
                action.RouteTemplate = "[action]";

            template = JoinUrls(template, action.RouteTemplate);
            template = Regex.Replace(template, regex, action.ActionName, RegexOptions.IgnoreCase);

            var returnString = _converter.GetTypeScriptName(action.ReturnType);

            var httpMethod = action.Method.ToString().ToLower();

            ApiParamDesc postParameter = null;
            ApiParamDesc[] getParameters = ValidateParameters(action.Parameters, httpMethod, template, out postParameter);

            var paramString = BuildMethodParameters(action);
            if (!string.IsNullOrWhiteSpace(paramString))
                paramString += ", ";

            writer.WriteLine($"public {action.ActionName}({paramString}ajaxOptions?: IExtendedAjaxSettings): {_promiseType}<{returnString}> {{");
            writer.Indent++;
            writer.WriteLine("var url = this._hostname + " + BuildUrlString(action, template, getParameters) + ";");
            writer.WriteLine($"return this._ajax.{httpMethod}(url, {postParameter?.ParameterName ?? "null"}, ajaxOptions);");
            writer.Indent--;
            writer.WriteLine("}");
        }

        protected virtual ApiParamDesc[] ValidateParameters(List<ApiParamDesc> parameters, string httpMethod, string routeTemplate, out ApiParamDesc postParam)
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

        protected virtual string JoinUrls(params string[] url)
        {
            StringBuilder output = new StringBuilder(100);
            foreach (var u in url.Where(x => !String.IsNullOrWhiteSpace(x)))
                output.Append("/" + u.Trim('/'));

            return output.ToString().Trim('/');
        }

        protected virtual string BuildMethodParameters(ApiActionDesc method)
        {
            var param = method.Parameters;
            return String.Join(", ", param.Select(p => $"{p.ParameterName}{(p.IsOptional ? "?" : "")}: {_converter.GetTypeScriptName(p.ParameterType)}"));
        }

        protected virtual bool IsRouteParameter(string name, string template)
        {
            return Regex.IsMatch(template, @"\{\*?" + name, RegexOptions.IgnoreCase);
        }

        protected virtual string BuildUrlString(ApiActionDesc action, string template, ApiParamDesc[] getParameters)
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

                if (part.StartsWith("*"))
                    part = part.Substring(1);

                routeParameters.Add(part);
                url.Append($"/\" + encodeURIComponent({part}) + \"");
            }

            foreach (var r in routeParameters)
            {
                var get = getParameters.FirstOrDefault(g => g.ParameterName.Equals(r));
                if (get == null)
                {
                    throw new Exception($"In action '{action.ActionName}', route parameter `{r}` does not match any available method parameters. " +
                                        $"Please check your route template: '{template}'." +
                                        $"Parameters: [{String.Join(", ", action.Parameters.Select(s => s.ParameterName))}]");
                }
                var typeCode = Type.GetTypeCode(get.ParameterType);
                switch (typeCode)
                {
                    case TypeCode.DateTime:
                    case TypeCode.Object:
                        throw new Exception($"In action '{action.ActionName}' parameter type '{get.ParameterType.Name}' is not suitable " +
                                            $"as a route parameter for template '{template}'. (Type code: {typeCode})");

                }
            }

            var finalGetParameters = getParameters.Where(p => routeParameters.All(r => !r.Equals(p.ParameterName, StringComparison.OrdinalIgnoreCase)));
            if (finalGetParameters.Any())
            {
                url.Append("?");
                url.Append(String.Join("&", finalGetParameters.Select(p => $"{p.ParameterName}=\" + encodeURIComponent({p.ParameterName}) + \"")));
            }

            return "\"" + _rewriter(url.ToString().TrimStart('/')) + "\"";
        }
    }
}
