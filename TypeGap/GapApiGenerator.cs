using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TypeGap.Extensions;
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

    public class AjaxExecContext
    {
        public string Ajax { get; set; }
        public string Url { get; set; }
        public string Post { get; set; }
        public string HttpMethod { get; set; }
        public string Options { get; set; }
    }

    public class GapApiGeneratorOptions
    {
        public Func<string, string> UrlRewriter { get; set; } = (v) => v;
        public Func<string, string> FunctionNameRewriter { get; set; } = (v) => v.Substring(0, 1).ToLower() + v.Substring(1);
        public string PromiseType { get; set; } = "Promise";
        public string EssentialsImportPath { get; set; } = "./Ajax";
        public string HeaderText { get; set; }
        public string FooterText { get; set; }
        public string AjaxClassName { get; set; } = "Ajax";
        public string ControllerBaseClass { get; set; }
        public string OptionsClassName { get; set; } = "IExtendedAjaxSettings";
        public Func<AjaxExecContext, string> AjaxExecFn { get; set; } = (c) => $"this.{c.Ajax}.execute({c.Url}, {c.HttpMethod}, {c.Post}, {c.Options})";
        public List<GapInitializer> TypeInitializers { get; set; } = new List<GapInitializer>();
    }

    public class GapApiGenerator
    {
        private readonly TypeConverter _converter;
        private readonly GapApiGeneratorOptions _options;
        private readonly string checkRealFn = "_is_real";
        private readonly string initVariableName = "value";
        private readonly string initIndent = "    ";
        private readonly string ajaxVariableName = "_ajax";

        private readonly string optionsVariableName = "ajaxOptions";

        public GapApiGenerator(TypeConverter converter, string indent, GapApiGeneratorOptions options)
        {
            _converter = converter;
            _options = options;
            initIndent = indent;
        }

        public virtual void WriteServices(ApiControllerDesc[] controllers, CustomIndentedTextWriter writer)
        {
            var controllerNames = controllers.Select(d => d.ControllerName).ToArray();

            var baseClass = String.IsNullOrWhiteSpace(_options.ControllerBaseClass) ? "" : $", {_options.ControllerBaseClass}";

            writer.WriteLine($"import {{ {_options.AjaxClassName}, {_options.OptionsClassName}{baseClass} }} from \"{_options.EssentialsImportPath}\";");

            if (!String.IsNullOrEmpty(_options.HeaderText))
            {
                writer.WriteLine("// === BEGIN CUSTOM HEADER CODE ===");
                writer.WriteLine(_options.HeaderText);
                writer.WriteLine("// === END CUSTOM HEADER CODE ===");
            }

            writer.WriteLine();

            WriteStaticHelper(controllerNames, writer);
            writer.WriteLine();

            foreach (var d in controllers)
            {
                WriteController(d, writer);
                writer.WriteLine();
            }

            if (!String.IsNullOrEmpty(_options.FooterText))
            {
                writer.WriteLine("// === BEGIN CUSTOM FOOTER CODE ===");
                writer.WriteLine(_options.FooterText);
                writer.WriteLine("// === END CUSTOM FOOTER CODE ===");
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

            writer.WriteLine($"public constructor(hostname: string, {optionsVariableName}?: {_options.OptionsClassName}) {{");
            writer.Indent++;
            foreach (var n in names)
            {
                writer.WriteLine($"this.{n} = new {n}Service(hostname, {optionsVariableName});");
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

            var baseClass = String.IsNullOrWhiteSpace(_options.ControllerBaseClass) ? "" : $" extends {_options.ControllerBaseClass}";

            var controllerName = $"{controller.ControllerName}Service";

            writer.WriteLine($"export class {controllerName}{baseClass} {{");
            writer.Indent++;
            writer.WriteLine("protected _basePath: string;");
            writer.WriteLine($"protected {ajaxVariableName}: {_options.AjaxClassName};");

            writer.WriteLine();
            writer.WriteLine($"public static Endpoints = {{");
            foreach (var action in controller.Actions)
            {
                writer.Indent++;
                WriteEndpoint(writer, ParseAction(template, action));
                writer.Indent--;
            }
            writer.WriteLine($"}}");
            writer.WriteLine();

            writer.WriteLine($"public constructor(basePath?: string, {optionsVariableName}?: {_options.OptionsClassName}) {{");
            writer.Indent++;

            if (!String.IsNullOrEmpty(baseClass))
                writer.WriteLine("super();");

            writer.WriteLine("basePath = (basePath || \"\");");
            writer.WriteLine("this._basePath = (basePath.substr(-1) == \"/\") ? basePath.substr(0, basePath.length - 1) : basePath;");
            writer.WriteLine($"this.{ajaxVariableName} = new {_options.AjaxClassName}({optionsVariableName});");
            writer.Indent--;
            writer.WriteLine("}");

            foreach (var action in controller.Actions)
            {
                writer.WriteLine();
                WriteMethod(controllerName, action, writer, ParseAction(template, action));
            }

            if (bodyLookup.Values.Any())
            {
                writer.WriteLine();
                writer.WriteLine("// ======================================================================================");
                writer.WriteLine("// The code below is generated by the chosen type initializer settings. It will serialize");
                writer.WriteLine("// and deserialize types as described when they cross the http boundry.");
                writer.WriteLine("// ======================================================================================");
                writer.WriteLine();

                foreach (var helper in bodyLookup.Values.SelectMany(v => v.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)))
                {
                    writer.WriteLine(helper);
                }

                writer.WriteLine($"private {checkRealFn}({initVariableName}: any): boolean {{");
                writer.Indent++;
                writer.WriteLine($"return {initVariableName} !== \"\" && {initVariableName} !== undefined && {initVariableName} !== null && !(Array.isArray({initVariableName}) && {initVariableName}.length === 0);");
                writer.Indent--;
                writer.WriteLine("}");
            }

            // we need to regen lookups for each controller that needs it
            keyLookup.Clear();
            bodyLookup.Clear();

            writer.Indent--;
            writer.WriteLine("}");
        }

        protected class ParsedApiDesc
        {
            public ApiParamDesc PostParameter { get; set; }
            public ApiParamDesc[] GetParameters { get; set; }
            public string PathString { get; set; }
            public string ParamString { get; set; }
            public string EndpointParamString { get; set; }
            public string MethodString { get; set; }
            public string NameString { get; set; }
        }

        protected virtual ParsedApiDesc ParseAction(string template, ApiActionDesc action)
        {
            var regex = @"(?:\[action\]|\{action\})";
            var exists = Regex.IsMatch(template, regex, RegexOptions.IgnoreCase);

            if (!exists && action.RouteTemplate == null)
                action.RouteTemplate = "[action]";

            template = JoinUrls(template, action.RouteTemplate);
            template = Regex.Replace(template, regex, action.ActionName, RegexOptions.IgnoreCase);

            var httpMethod = action.Method.ToString().ToLower();

            ApiParamDesc postParameter = null;
            ApiParamDesc[] getParameters = ValidateParameters(action.Parameters, httpMethod, template, out postParameter);

            string GetParamString(IEnumerable<ApiParamDesc> aa) => String.Join(", ", aa.Select(p => $"{p.ParameterName}{(p.IsOptional ? "?" : "")}: {_converter.GetTypeScriptName(p.ParameterType)}"));

            var path = BuildUrlString(action, template, getParameters);

            return new ParsedApiDesc
            {
                EndpointParamString = GetParamString(getParameters),
                ParamString = GetParamString(action.Parameters),
                MethodString = httpMethod,
                PathString = path,
                PostParameter = postParameter,
                GetParameters = getParameters,
                NameString = _options.FunctionNameRewriter(action.ActionName),
            };
        }

        protected virtual void WriteEndpoint(CustomIndentedTextWriter writer, ParsedApiDesc desc)
        {
            var line = $"{desc.NameString}: ({desc.EndpointParamString}): string => {desc.PathString},";
            writer.WriteLine(line.Replace(" + \"\",", ","));
        }

        protected virtual void WriteMethod(string controllerName, ApiActionDesc action, CustomIndentedTextWriter writer, ParsedApiDesc desc)
        {
            string returnString;
            if (action.ReturnType == null)
                returnString = "any";
            else if (action.ReturnType.Name == "IActionResult")
                returnString = "any /* IActionResult */";
            else
                returnString = _converter.GetTypeScriptName(action.ReturnType);

            var paramString = desc.ParamString;

            if (!string.IsNullOrWhiteSpace(paramString))
                paramString += ", ";

            writer.WriteLine($"public {desc.NameString}({paramString}{optionsVariableName}?: {_options.OptionsClassName}): {_options.PromiseType}<{returnString}> {{");
            writer.Indent++;

            if (desc.PostParameter != null)
            {
                var pinit = this.CreateTypeInitializerMethod(desc.PostParameter.ParameterType);
                if (!String.IsNullOrWhiteSpace(pinit))
                    writer.WriteLine($"{desc.PostParameter.ParameterName} = this.from_{pinit}({desc.PostParameter.ParameterName});");
            }

            writer.WriteLine($"var url = this._basePath + {controllerName}.Endpoints.{desc.NameString}({String.Join(", ", desc.GetParameters.Select(p => p.ParameterName))});");

            var ajaxCtx = new AjaxExecContext { Ajax = ajaxVariableName, HttpMethod = desc.MethodString, Options = optionsVariableName, Post = desc.PostParameter?.ParameterName ?? "null", Url = "url" };
            writer.Write("return " + _options.AjaxExecFn(ajaxCtx).TrimEnd(';'));

            var initializer = this.CreateTypeInitializerMethod(action.ReturnType);
            if (String.IsNullOrWhiteSpace(initializer))
                writer.WriteLine(";");
            else
                writer.WriteLine($".then(this.to_{initializer});");

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

        protected virtual bool IsRouteParameter(string name, string template)
        {
            return Regex.IsMatch(template, @"\{\*?" + name, RegexOptions.IgnoreCase);
        }

        protected virtual string BuildUrlString(ApiActionDesc action, string template, ApiParamDesc[] getParameters)
        {
            List<string> routeParameters = new List<string>();
            StringBuilder url = new StringBuilder();
            bool writtenOptionalRoute = false;

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

                if (part.EndsWith("?"))
                    writtenOptionalRoute = true;
                else if (writtenOptionalRoute == true)
                    throw new Exception($"In action '{action.ActionName}', required route parameter must not come after an optional route parameter in template: '{template}'.");

                routeParameters.Add(part);

                url.Append($"/\" + encodeURIComponent({part.TrimEnd('?')} as any) + \"");
            }

            foreach (var rtemplate in routeParameters)
            {
                var r = rtemplate;
                var templateMarkedOptional = r.EndsWith("?");
                r = r.TrimEnd('?');

                var get = getParameters.FirstOrDefault(g => g.ParameterName.Equals(r));
                if (get == null)
                {
                    throw new Exception($"In action '{action.ActionName}', route parameter `{r}` does not match any available method parameters. " +
                                        $"Please check your route template: '{template}'." +
                                        $"Parameters: [{String.Join(", ", action.Parameters.Select(s => s.ParameterName))}]");
                }

                if (templateMarkedOptional != get.IsOptional)
                {
                    throw new Exception($"In action '{action.ActionName}', route parameter `{r}` is marked optional={get.IsOptional}, but the route " +
                                        $"template '{template}' is marked optional={templateMarkedOptional}");
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

            var finalGetParameters = getParameters.Where(p => routeParameters.All(r => !r.TrimEnd('?').Equals(p.ParameterName, StringComparison.OrdinalIgnoreCase)));
            if (finalGetParameters.Any())
            {
                url.Append("?");
                url.Append(String.Join("&", finalGetParameters.Select(p => $"{p.ParameterName}=\" + encodeURIComponent({p.ParameterName} as any) + \"")));
            }

            return "\"" + _options.UrlRewriter("/" + url.ToString().TrimStart('/')) + "\"";
        }

        protected Dictionary<Type, string> keyLookup = new Dictionary<Type, string>();
        protected Dictionary<string, string> bodyLookup = new Dictionary<string, string>();

        protected virtual string CreateTypeInitializerMethod(Type t)
        {
            if (keyLookup.ContainsKey(t))
                return keyLookup[t];

            var it = _options.TypeInitializers.SingleOrDefault(g => g.CanConvertType(t));

            var guid = Guid.NewGuid().ToString().ToLower().Replace("-", "");
            StringBuilder sb = new StringBuilder();

            if (it == null && Type.GetTypeCode(t) != TypeCode.Object)
                return null;

            string GenInitMethod(string prefix, string body)
            {
                StringBuilder inner = new StringBuilder();
                inner.AppendLine($"private {prefix}_{guid}({initVariableName}: any): any {{");
                inner.AppendLine($"{initIndent}if (!this.{checkRealFn}({initVariableName})) return {initVariableName};");
                inner.AppendLine($"{initIndent}" + body.Replace("\n", "\n" + initIndent));
                inner.AppendLine($"{initIndent}return {initVariableName};");
                inner.AppendLine($"}}");
                return inner.ToString().Trim();
            }

            string GenInitBody(string prefix)
            {
                StringBuilder inner = new StringBuilder();
                var rpro = t.GetDnxCompatible()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .ToDictionary(k => k.Name, k => k.PropertyType)
                    .Concat(t.GetDnxCompatible().GetFields(BindingFlags.Instance | BindingFlags.Public).ToDictionary(k => k.Name, k => k.FieldType));

                foreach (var prop in rpro)
                {
                    var pmm = CreateTypeInitializerMethod(prop.Value);
                    if (pmm != null)
                    {
                        //inner.AppendLine($"if (this.{checkRealFn}({initVariableName}))");
                        inner.AppendLine($"{initVariableName}.{prop.Key} = this.{prefix}_{pmm}({initVariableName}.{prop.Key});");
                    }
                }
                return GenInitMethod(prefix, inner.ToString().Trim());
            }

            string GenArrayBody(string prefix)
            {
                Type elementType = null;
                Type[] interfaces = t.GetDnxCompatible().GetInterfaces();
                foreach (Type i in interfaces)
                    if (i.GetDnxCompatible().IsGenericType && i.GetGenericTypeDefinition().Equals(typeof(IEnumerable<>)))
                        elementType = i.GetDnxCompatible().GetGenericArguments()[0];

                if (elementType == null)
                    throw new Exception("Unknown error occurred parsing type: " + t.FullName + ". Considered an enumerable, but element type could not be found.");

                var pmm = CreateTypeInitializerMethod(elementType);
                if (pmm == null)
                    return null;

                StringBuilder inner = new StringBuilder();
                inner.AppendLine($"for (let i = 0; i < {initVariableName}.length; i++)");
                //inner.AppendLine($"{initIndent}if (this.{checkRealFn}({initVariableName}[i]))");
                inner.AppendLine($"{initIndent}{initVariableName}[i] = this.{prefix}_{pmm}({initVariableName}[i]);");
                return GenInitMethod(prefix, inner.ToString().Trim());
            }

            string GenBasic(string prefix, string v)
            {
                StringBuilder inner = new StringBuilder();
                //inner.AppendLine($"if (this.{checkRealFn}({initVariableName}))");
                inner.AppendLine($"{initVariableName} = {v};");
                return GenInitMethod(prefix, inner.ToString().Trim());
            }

            if (it != null)
            {
                sb.AppendLine(GenBasic("from", it.FromTsType(initVariableName)));
                sb.AppendLine();
                sb.AppendLine(GenBasic("to", it.ToTsType(initVariableName)));
            }
            else if (typeof(IEnumerable).GetDnxCompatible().IsAssignableFrom(t))
            {
                sb.AppendLine(GenArrayBody("from"));
                sb.AppendLine();
                sb.AppendLine(GenArrayBody("to"));
            }
            else
            {
                sb.AppendLine(GenInitBody("from"));
                sb.AppendLine();
                sb.AppendLine(GenInitBody("to"));
            }

            keyLookup[t] = guid;
            bodyLookup[guid] = sb.ToString();

            return guid;
        }
    }
}
