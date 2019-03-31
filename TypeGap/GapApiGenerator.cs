using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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

        public string RouteTemplate { get; set; }

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

    public class SupportedType
    {
        public string TsType { get; set; }
        public Type MainType { get; set; }
    }

    public class GapApiGeneratorOptions
    {
        public Func<ApiActionDesc, string> FnActionName { get; set; } = (action) =>
        {
            var v = action.ActionName;
            v = v.Substring(0, 1).ToLower() + v.Substring(1);
            if (v.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
                v = v.Substring(0, v.Length - 5);
            return v;
        };

        public Func<AjaxExecContext, string> FnAjaxExecute { get; set; } = (c) => $"this.{c.Ajax}.{c.HttpMethod}({c.Url}, {c.Post}, {c.Options})";

        public string PromiseType { get; set; } = "Promise";
        public string EssentialsImportPath { get; set; } = "./Ajax";
        public string HeaderText { get; set; }
        public string FooterText { get; set; }
        public string AjaxClassName { get; set; } = "Ajax";
        public string ControllerBaseClass { get; set; }
        public bool HideActionsWithNoReturn { get; set; } = true;
        public string DefaultRouteTemplate { get; set; } = "[controller]/[action]";
        public bool EnableDeepParameterCloning { get; set; } = true;
        public string OptionsClassName { get; set; } = "IExtendedAjaxSettings";
        public List<AdvancedTypeInitializer> TypeInitializers { get; set; } = new List<AdvancedTypeInitializer>();
        public Dictionary<Type, string> SupportedTypes { get; set; } = new Dictionary<Type, string>();
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

        protected Dictionary<string, string> bodyLookup = new Dictionary<string, string>();

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

            writer.WriteLine(Resx.AjaxHelpers);
            writer.WriteLine();

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
            var actions = controller.Actions.Select(a => ParseAction(controller, a)).ToArray();

            var duplicateRoute = actions.GroupBy(a => a.Action.Method + a.Template).FirstOrDefault(g => g.Count() > 1);
            if (duplicateRoute != null)
                throw new Exception($"Two actions ('{duplicateRoute.First().Action.ActionName}', '{duplicateRoute.Skip(1).First().Action.ActionName}') " +
                    $"in controller '{controller.ControllerName}' share the same route '{duplicateRoute.First().Template}' and method '{duplicateRoute.First().Action.Method}'.");

            var baseClass = String.IsNullOrWhiteSpace(_options.ControllerBaseClass) ? "" : $" extends {_options.ControllerBaseClass}";

            var controllerName = $"{controller.ControllerName}Service";

            writer.WriteLine($"export class {controllerName}{baseClass} {{");
            writer.Indent++;
            writer.WriteLine("protected _basePath: string;");
            writer.WriteLine($"protected {ajaxVariableName}: {_options.AjaxClassName};");

            writer.WriteLine();
            writer.WriteLine($"public endpoints = {{");
            foreach (var a in actions)
            {
                writer.Indent++;
                WriteEndpoint(a, writer);
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
            foreach (var a in actions)
                writer.WriteLine($"this.{a.NameString} = this.{a.NameString}.bind(this);");
            writer.Indent--;
            writer.WriteLine("}");

            foreach (var a in actions)
            {
                writer.WriteLine();
                WriteMethod(a, writer);
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        protected virtual ParsedApiDesc ParseAction(ApiControllerDesc controller, ApiActionDesc action)
        {
            var actionRegex = @"(?:\[action\]|\{action\})";
            var controllerRegex = @"(?:\[controller\]|\{controller\})";
            string template = _options.DefaultRouteTemplate;

            if (controller.RouteTemplate != null || action.RouteTemplate != null)
            {
                if (controller.RouteTemplate != null)
                {
                    template = controller.RouteTemplate;
                }
                if (action.RouteTemplate != null)
                {
                    var art = action.RouteTemplate.TrimStart('~');
                    var absolute = art.StartsWith("/");
                    template = absolute ? art : JoinUrls(template, art);
                }
            }

            template = Regex.Replace(template, actionRegex, action.ActionName, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, controllerRegex, controller.ControllerName, RegexOptions.IgnoreCase);

            ApiParamDesc postParameter = null;
            ApiParamDesc modelParameter = null;
            ApiParamDesc[] getParameters = ValidateParameters(action.Parameters, action.Method, template, out postParameter, out modelParameter);

            string GetParamString(IEnumerable<ApiParamDesc> aa) => String.Join(", ", aa.Select(p => $"{p.ParameterName}{(p.IsOptional ? "?" : "")}: {_converter.GetTypeScriptName(p.ParameterType)}"));

            var path = BuildUrlString(action, template, getParameters, modelParameter);

            return new ParsedApiDesc
            {
                EndpointParamString = GetParamString(modelParameter != null ? new[] { modelParameter } : getParameters),
                ParamString = GetParamString(action.Parameters),
                PathString = path,
                PostParameter = postParameter,
                ModelParameter = modelParameter,
                GetParameters = getParameters,
                NameString = _options.FnActionName(action),
                Action = action,
                Template = template,
            };
        }

        protected virtual void WriteEndpoint(ParsedApiDesc desc, CustomIndentedTextWriter writer)
        {
            var line = $"{desc.NameString}: ({desc.EndpointParamString}): string => {desc.PathString},";
            writer.WriteLine(line.Replace(" + \"\",", ","));
        }

        protected virtual void WriteMethod(ParsedApiDesc desc, CustomIndentedTextWriter writer)
        {
            string returnString;
            if (desc.Action.ReturnType == null)
                returnString = "any";
            else
                returnString = _converter.GetTypeScriptName(desc.Action.ReturnType);

            var paramString = desc.ParamString;

            if (!string.IsNullOrWhiteSpace(paramString))
                paramString += ", ";

            string filesString = "";
            if (desc.PostParameter != null && desc.PostParameter.ParameterType.Name == "IFormCollection")
                filesString = "_files?: File[], ";

            var modifier = "public";
            if (_options.HideActionsWithNoReturn && returnString.StartsWith("any"))
            {
                writer.WriteLine("// This method is hidden because it's return type is not specified and HideActionsWithNoReturn=True");
                writer.WriteLine("// Either add a return type (check [ProducesResponseType]) or set HideActionsWithNoReturn to False");
                modifier = "protected";
            }

            writer.WriteLine($"{modifier} {desc.NameString}({paramString}{filesString}{optionsVariableName}?: {_options.OptionsClassName}): {_options.PromiseType}<{returnString}> {{");
            writer.Indent++;

            if (desc.PostParameter != null)
            {
                if (String.IsNullOrWhiteSpace(filesString))
                {
                    var pinit = this.CreateTypeInitializerMethod(desc.PostParameter.ParameterType);
                    if (!String.IsNullOrWhiteSpace(pinit))
                        writer.WriteLine($"{desc.PostParameter.ParameterName} = from_{pinit}({desc.PostParameter.ParameterName});");
                }
                else
                {
                    writer.WriteLine($"if ({checkRealFn}(_files))");
                    writer.Indent++;
                    writer.WriteLine($"for (const _f of _files) {{ {desc.PostParameter.ParameterName}.append(\"files[]\", _f); }}");
                    writer.Indent--;
                }
            }

            var excParams = desc.ModelParameter == null ? desc.GetParameters : new[] { desc.ModelParameter };

            writer.WriteLine($"var url = this.endpoints.{desc.NameString}({String.Join(", ", excParams.Select(p => p.ParameterName))});");

            var ajaxCtx = new AjaxExecContext { Ajax = ajaxVariableName, HttpMethod = desc.Action.Method.ToString().ToLower(), Options = optionsVariableName, Post = desc.PostParameter?.ParameterName ?? "null", Url = "url" };
            writer.Write("return " + _options.FnAjaxExecute(ajaxCtx).TrimEnd(';'));

            var initializer = this.CreateTypeInitializerMethod(desc.Action.ReturnType);
            if (String.IsNullOrWhiteSpace(initializer))
                writer.WriteLine(";");
            else
                writer.WriteLine($".then(to_{initializer});");

            writer.Indent--;
            writer.WriteLine("}");
        }

        protected virtual ApiParamDesc[] ValidateParameters(List<ApiParamDesc> parameters, ApiMethod httpMethod, string routeTemplate, out ApiParamDesc postParam, out ApiParamDesc modelParam)
        {
            var canHavePost = new[] { ApiMethod.Patch, ApiMethod.Post, ApiMethod.Put }.Contains(httpMethod);

            var postPossibilities = parameters
                .Where(p => !IsRouteParameter(p.ParameterName, routeTemplate))
                .Where(p => p.Mode != ApiParameterMode.FromUri)
                .Where(p => p.Mode == ApiParameterMode.FromBody || TypeConverter.IsComplexType(p.ParameterType))
                .ToArray();

            if (postPossibilities.Length > 1)
                throw new InvalidOperationException($"Invalid action, can't have more than one candidate for post parameters. Try using [FromBody] or [FromUri] to provide additional context. (at {routeTemplate}");

            var post = postPossibilities.FirstOrDefault();

            if (!canHavePost && post != null)
            {
                // if there's only a single parameter in a get method, mvc will bind query parameters into it.
                if (post.Mode != ApiParameterMode.FromBody && parameters.Count == 1 && !IsRouteParameter(post.ParameterName, routeTemplate))
                {
                    var m = GetMembersAsParams(post.ParameterType).Select(kvp => new ApiParamDesc
                    {
                        ParameterName = kvp.Key,
                        ParameterType = kvp.Value,
                        IsOptional = true,
                        Mode = ApiParameterMode.FromUri,
                    }).ToList();

                    var inner = ValidateParameters(m, httpMethod, routeTemplate, out postParam, out modelParam);
                    modelParam = post;
                    postParam = null;
                    return inner;
                }

                throw new InvalidOperationException($"Invalid action, unable to map complex parameter '{post.ParameterName}' to {httpMethod} request as a message body is not allowed. (at {routeTemplate})");
            }

            postParam = post;
            modelParam = null;

            return (post == null ?
                    parameters :
                    parameters.Except(new[] { Nullable.GetUnderlyingType(post.ParameterType) != null ? null : post }))
                .ToArray();
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

        protected virtual string BuildUrlString(ApiActionDesc action, string template, ApiParamDesc[] getParameters, ApiParamDesc modelParameter)
        {
            List<string> routeJs = new List<string>();
            List<string> queryJs = new List<string>();
            List<ApiParamDesc> routeParameters = new List<ApiParamDesc>();

            bool seenOptionalRoute = false;

            string GetParamExecString(ApiParamDesc p)
            {
                var name = modelParameter == null ? p.ParameterName : $"{modelParameter.ParameterName}.{p.ParameterName}";
                var imm = CreateTypeInitializerMethod(p.ParameterType);
                if (imm != null)
                    return $"from_{imm}({name})";
                return name;
            }

            var parts = template.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!part.StartsWith("{"))
                {
                    routeJs.Add("\"" + part + "\"");
                    continue;
                }

                part = part.Substring(1, part.Length - 2);

                var typeIndex = part.IndexOf(":");
                if (typeIndex > 0)
                    part = part.Substring(0, typeIndex);

                if (part.StartsWith("*"))
                    part = part.Substring(1);

                var templateOptional = part.EndsWith("?");
                part = part.TrimEnd('?');

                if (templateOptional)
                    seenOptionalRoute = true;
                else if (seenOptionalRoute == true)
                    throw new Exception($"In action '{action.ActionName}', required route parameter must not come after an optional route parameter in template: '{template}'.");

                var get = getParameters.FirstOrDefault(g => g.ParameterName.Equals(part));
                if (get == null)
                {
                    throw new Exception($"In action '{action.ActionName}', route parameter `{part}` does not match any available method parameters. " +
                                        $"Please check your route template: '{template}'." +
                                        $"Parameters: [{String.Join(", ", action.Parameters.Select(s => s.ParameterName))}]");
                }

                if (templateOptional != get.IsOptional)
                {
                    bool canBeNull = !get.ParameterType.GetDnxCompatible().IsValueType || (Nullable.GetUnderlyingType(get.ParameterType) != null);
                    if (!canBeNull)
                        throw new Exception($"In action '{action.ActionName}', route parameter `{part}` is marked optional={get.IsOptional}, but the route " +
                                            $"template '{template}' is marked optional={templateOptional} and the type is non-nullable so is required.");
                }

                var typeCode = Type.GetTypeCode(get.ParameterType);
                switch (typeCode)
                {
                    case TypeCode.DateTime:
                    case TypeCode.Object:
                        throw new Exception($"In action '{action.ActionName}' parameter type '{get.ParameterType.Name}' is not suitable " +
                                            $"as a route parameter for template '{template}'. (Type code: {typeCode})");
                }

                routeParameters.Add(get);
                routeJs.Add($"[{GetParamExecString(get)}, \"{get.ParameterName}\"]");
            }

            foreach (var q in getParameters.Except(routeParameters))
            {
                queryJs.Add($"[{GetParamExecString(q)}, \"{q.ParameterName}\", {(q.IsOptional || Nullable.GetUnderlyingType(q.ParameterType) != null).ToString().ToLower()}]");
            }

            return $"_build_url(this._basePath, [{String.Join(", ", routeJs)}], [{String.Join(", ", queryJs)}])";
        }

        protected virtual Dictionary<string, Type> GetMembersAsParams(Type t)
        {
            var rpro = t.GetDnxCompatible()
                 .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                 .ToDictionary(k => k.Name, k => k.PropertyType)
                 .Concat(t.GetDnxCompatible().GetFields(BindingFlags.Instance | BindingFlags.Public).ToDictionary(k => k.Name, k => k.FieldType));
            return rpro.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        protected virtual string CreateTypeInitializerMethod(Type t)
        {
            t = _converter.UnwrapType(t);

            var tsName = _converter.GetTypeScriptName(t);
            var clrName = _converter.PrettyClrTypeName(t);

            if (tsName.StartsWith("any")) // we won't be able to process any types that we don't understand.
                return null;

            var itList = _options.TypeInitializers.Where(g => g.GetGroupNameIfCanConvert(t) != null).ToArray();
            if (itList.Length > 1)
                throw new Exception($"Type '{t.FullName}' is currently handled by more than one type initializer. This is not supported. (current initializers: {String.Join(", ", itList.Select(i => i.GetType().Name))})");

            var it = itList.FirstOrDefault();
            var guid = it?.GetGroupNameIfCanConvert(t);

            if (guid == null)
                using (MD5 md5 = MD5.Create())
                    guid = string.Join(string.Empty, md5.ComputeHash(Encoding.UTF8.GetBytes(t.AssemblyQualifiedName)).Select(b => b.ToString("x2")));

            if (it != null && bodyLookup.ContainsKey(guid))
                return guid;

            StringBuilder sb = new StringBuilder();

            if (it == null && Type.GetTypeCode(t) != TypeCode.Object)
                return null;

            string GenInitMethod(string prefix, string body)
            {
                if (String.IsNullOrWhiteSpace(body))
                    return null;

                StringBuilder inner = new StringBuilder();
                inner.AppendLine($"function {prefix}_{guid}({initVariableName}: any): any {{");
                inner.AppendLine($"{initIndent}// {tsName} - ({clrName})");
                inner.AppendLine($"{initIndent}if (!{checkRealFn}({initVariableName})) return {initVariableName};");
                inner.AppendLine($"{initIndent}" + body.Replace("\n", "\n" + initIndent));
                //inner.AppendLine($"{initIndent}return {initVariableName};");
                inner.AppendLine($"}}");
                return inner.ToString().Trim();
            }

            string GenInitBody(string prefix, bool clone)
            {
                StringBuilder inner = new StringBuilder();
                int count = 0;

                if (clone)
                {
                    inner.AppendLine("const cloned: any = { };");
                }

                foreach (var prop in GetMembersAsParams(t))
                {
                    var pmm = CreateTypeInitializerMethod(prop.Value);
                    if (pmm != null)
                    {
                        count++;
                        if (clone)
                            inner.AppendLine($"cloned.{prop.Key} = {prefix}_{pmm}({initVariableName}.{prop.Key});");
                        else
                            inner.AppendLine($"{initVariableName}.{prop.Key} = {prefix}_{pmm}({initVariableName}.{prop.Key});");
                    }
                    else if (clone)
                    {
                        inner.AppendLine($"cloned.{prop.Key} = {initVariableName}.{prop.Key};");
                    }
                }

                if (count == 0)
                    return null;

                inner.AppendLine("return " + (clone ? "cloned;" : initVariableName + ";"));

                return GenInitMethod(prefix, inner.ToString().Trim());
            }

            string GenArrayBody(string prefix)
            {
                Type elementType = null;
                Type[] interfaces = t.GetDnxCompatible().GetInterfaces().Concat(new[] { t }).ToArray();

                if (t.IsIDictionary())
                {
                    foreach (Type i in interfaces)
                        if (i.GetDnxCompatible().IsGenericType && i.GetGenericTypeDefinition().Equals(typeof(IDictionary<,>)))
                            elementType = i.GetDnxCompatible().GetGenericArguments()[1];
                }
                else
                {
                    foreach (Type i in interfaces)
                        if (i.GetDnxCompatible().IsGenericType && i.GetGenericTypeDefinition().Equals(typeof(IEnumerable<>)))
                            elementType = i.GetDnxCompatible().GetGenericArguments()[0];
                }

                if (elementType == null)
                    throw new Exception("Unknown error occurred parsing type: " + t.FullName + ". Considered an enumerable, but element type could not be found.");

                var pmm = CreateTypeInitializerMethod(elementType);
                if (pmm == null)
                    return null;

                StringBuilder inner = new StringBuilder();

                if (t.IsIDictionary())
                {
                    inner.AppendLine("const cloned: any = { };");
                    inner.AppendLine($"for (let key in {initVariableName})");
                }
                else
                {
                    inner.AppendLine("const cloned: any[] = [];");
                    inner.AppendLine($"for (let key: number = 0; key < {initVariableName}.length; key++)");
                }

                inner.AppendLine($"{initIndent}cloned[key] = {prefix}_{pmm}({initVariableName}[key]);");
                inner.AppendLine("return cloned;");
                return GenInitMethod(prefix, inner.ToString().Trim());
            }

            string GenBasic(string prefix, string v)
            {
                StringBuilder inner = new StringBuilder();
                inner.AppendLine(v);
                return GenInitMethod(prefix, inner.ToString().Trim());
            }

            if (it != null)
            {
                sb.AppendLine(GenBasic("from", it.FromTsType(t, initVariableName)));
                sb.AppendLine();
                sb.AppendLine(GenBasic("to", it.ToTsType(t, initVariableName)));
            }
            else if (typeof(IEnumerable).GetDnxCompatible().IsAssignableFrom(t))
            {
                sb.AppendLine(GenArrayBody("from"));
                sb.AppendLine();
                sb.AppendLine(GenArrayBody("to"));
            }
            else
            {
                sb.AppendLine(GenInitBody("from", _options.EnableDeepParameterCloning));
                sb.AppendLine();
                sb.AppendLine(GenInitBody("to", false));
            }

            var result = sb.ToString();
            if (String.IsNullOrWhiteSpace(result))
                return null;

            bodyLookup[guid] = result;
            return guid;
        }

        protected class ParsedApiDesc
        {
            public ApiActionDesc Action { get; set; }
            public ApiParamDesc PostParameter { get; set; }
            public ApiParamDesc ModelParameter { get; set; }
            public ApiParamDesc[] GetParameters { get; set; }
            public string Template { get; set; }
            public string PathString { get; set; }
            public string ParamString { get; set; }
            public string EndpointParamString { get; set; }
            public string NameString { get; set; }
        }
    }
}
