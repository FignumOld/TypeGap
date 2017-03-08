using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using TypeLite;

namespace TypeGap
{
    public class WebApiGenerator
    {
        private TypeScriptFluent _fluent;
        public WebApiGenerator(TypeScriptFluent fluent)
        {
            _fluent = fluent;
        }

        public void WriteServices(Type[] controllers, IndentedTextWriter writer)
        {
            var config = new HttpConfiguration();

            var descriptors = controllers.Select(c => new HttpControllerDescriptor(config, c.Name.Replace("Controller", ""), c));
            var names = descriptors.Select(d => d.ControllerName).ToArray();

            writer.WriteLine(Resx.AjaxService);
            writer.WriteLine();

            WriteStaticHelper(names, writer);
            writer.WriteLine();

            foreach (var d in descriptors)
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

            writer.WriteLine("public constructor(hostname: string) {");
            writer.Indent++;
            foreach (var n in names)
            {
                writer.WriteLine($"this.{n} = new {n}Service(hostname);");
            }
            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");
        }

        private void WriteController(HttpControllerDescriptor controller, IndentedTextWriter writer)
        {
            var actions = controller.ControllerType.GetMethods()
                .Where(m => m.IsPublic)
                .Where(m => !m.IsSpecialName)
                .Where(m => m.DeclaringType == controller.ControllerType)
                .OrderBy(m => m.Name);

            var routePrefix = controller.ControllerType.GetCustomAttribute<RoutePrefixAttribute>();
            string urlPrefix = routePrefix?.Prefix ?? "api/" + controller.ControllerName;

            writer.WriteLine($"export class {controller.ControllerName}Service {{");
            writer.Indent++;
            writer.WriteLine("private hostname: string;");
            writer.WriteLine("public constructor(hostname: string) {");
            writer.Indent++;
            writer.WriteLine("this.hostname = (hostname.substr(-1) == \"/\") ? hostname : hostname + \"/\";");
            writer.Indent--;
            writer.WriteLine("}");

            foreach (var action in actions)
            {
                writer.WriteLine();
                WriteMethod(new ReflectedHttpActionDescriptor(controller, action), writer, urlPrefix);
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private void WriteMethod(ReflectedHttpActionDescriptor action, IndentedTextWriter writer, string urlPrefix)
        {
            var method = action.MethodInfo;
            var route = method.GetCustomAttribute<RouteAttribute>();

            var template = route?.Template ?? action.ActionName;
            var returnString = TypeConverter.GetTypeScriptName(method.ReturnType, _fluent);

            var httpMethod = GetHttpMethod(action);

            ParameterInfo postParameter = null;
            ParameterInfo[] getParameters = ValidateParameters(method.GetParameters(), httpMethod, template, out postParameter);

            var paramString = BuildMethodParameters(method);
            if (!string.IsNullOrWhiteSpace(paramString))
                paramString += ", ";

            writer.WriteLine($"public {method.Name}({paramString}ajaxOptions?: IExtendedAjaxSettings): JQueryPromise<{returnString}> {{");
            writer.Indent++;
            writer.WriteLine("var url = this.hostname + " + BuildUrlString(urlPrefix, template, getParameters) + ";");
            writer.WriteLine($"return Ajax.{httpMethod}(url, {postParameter?.Name ?? "null"}, ajaxOptions);");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private ParameterInfo[] ValidateParameters(ParameterInfo[] parameters, string httpMethod, string routeTemplate, out ParameterInfo postParam)
        {
            var post = parameters
                .Where(p => !IsRouteParameter(p, routeTemplate))
                .Where(p => p.GetCustomAttribute<FromUriAttribute>() == null)
                .Where(p => TypeConverter.IsComplexType(p.ParameterType) || p.GetCustomAttribute<FromBodyAttribute>() != null)
                .SingleOrDefault();

            if (httpMethod == "get" && post != null)
                throw new InvalidOperationException("Invalid action, get method can't take complex type in message body");

            postParam = post;
            return parameters.Except(new[] { post }).ToArray();
        }

        private bool NotAnAction(MethodInfo action)
        {
            return action.CustomAttributes.Any(a => a.AttributeType.Name == typeof(NonActionAttribute).Name);
        }

        private string GetHttpMethod(ReflectedHttpActionDescriptor action)
        {
            var method = action.SupportedHttpMethods.First();
            return method.Method.ToLower();
        }

        private string BuildMethodParameters(MethodInfo method)
        {
            var param = method.GetParameters();
            return String.Join(", ", param.Select(p => $"{p.Name}{(p.IsOptional ? "?" : "")}: {TypeConverter.GetTypeScriptName(p.ParameterType, _fluent)}"));
        }

        private bool IsRouteParameter(ParameterInfo param, string template)
        {
            return template.IndexOf("{" + param.Name, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildUrlString(string urlPrefix, string template, ParameterInfo[] getParameters)
        {
            List<string> routeParameters = new List<string>();
            StringBuilder url = new StringBuilder(urlPrefix);

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

            var finalGetParameters = getParameters.Where(p => routeParameters.All(r => !r.Equals(p.Name, StringComparison.OrdinalIgnoreCase)));
            if (finalGetParameters.Any())
            {

                url.Append("?");
                url.Append(String.Join("&", finalGetParameters.Select(p =>
                {
                    var uri = p.GetCustomAttribute<FromUriAttribute>();
                    var name = uri?.Name ?? p.Name;
                    return $"{name}=\" + {p.Name} + \"";
                })));
            }

            return "\"" + url.ToString().TrimStart('/') + "\"";
        }
    }
}
