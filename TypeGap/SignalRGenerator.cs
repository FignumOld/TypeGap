using System;
using System.Linq;
using System.Reflection;
using TypeGap.Extensions;
using TypeGap.Util;
using TypeLite;

namespace TypeGap
{
    public class SignalRHubDesc
    {
        public SignalRHubDesc(Type hubType)
        {
            HubType = hubType;
            HubClassName = HubType.Name;
        }

        public Type HubType { get; private set; }

        public string HubClassName { get; set; }

        public string HubSignalRName
        {
            get
            {
                var attribute = HubType.GetDnxCompatible().GetCustomAttributes().Where(ca => ca.GetType().FullName == "Microsoft.AspNet.SignalR.Hubs.HubNameAttribute").FirstOrDefault();
                if (attribute == null)
                    return null;
                var propInfo = attribute.GetType().GetDnxCompatible().GetProperty("HubName");
                return propInfo.GetValue(attribute) as string;
            }
        }
    }

    public class SignalRGenerator
    {
        internal const string HUB_TYPE = "Microsoft.AspNet.SignalR.Hub";

        private string GenerateHubs(Assembly assembly, TypeConverter converter)
        {
            var hubs = assembly.GetTypes()
                .Where(t => t.GetDnxCompatible().BaseType != null && t.GetDnxCompatible().BaseType.FullName != null && t.GetDnxCompatible().BaseType.FullName.Contains(HUB_TYPE))
                .OrderBy(t => t.FullName)
                .ToList();

            if (!hubs.Any()) return "";

            var scriptBuilder = new ScriptBuilder("    ");
            // Output signalR style promise interface:
            scriptBuilder.AppendLine("interface ISignalRPromise<T> {");
            using (scriptBuilder.IncreaseIndentation())
            {
                scriptBuilder.AppendLineIndented("done(cb: (result: T) => any): ISignalRPromise<T>;");
                scriptBuilder.AppendLineIndented("error(cb: (error: any) => any): ISignalRPromise<T>;");
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
            hubs.ForEach(h => GenerateHubInterfaces(new SignalRHubDesc(h), scriptBuilder, converter));
            // Generate client connection interfaces
            scriptBuilder.AppendLineIndented("interface SignalR {");
            using (scriptBuilder.IncreaseIndentation())
            {
                hubs.ForEach(h => scriptBuilder.AppendLineIndented(h.Name.ToCamelCase() + ": I" + h.Name + "Proxy;"));
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
            return scriptBuilder.ToString();
        }

        public void WriteHubs(SignalRHubDesc[] hubs, TypeConverter converter, CustomIndentedTextWriter writer)
        {
            var hubList = hubs.ToList();
            var scriptBuilder = new ScriptBuilder("    ");
            // Output signalR style promise interface:
            scriptBuilder.AppendLine("interface ISignalRPromise<T> {");
            using (scriptBuilder.IncreaseIndentation())
            {
                scriptBuilder.AppendLineIndented("done(cb: (result: T) => any): ISignalRPromise<T>;");
                scriptBuilder.AppendLineIndented("error(cb: (error: any) => any): ISignalRPromise<T>;");
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
            hubList.ForEach(h => GenerateHubInterfaces(h, scriptBuilder, converter));
            // Generate client connection interfaces
            scriptBuilder.AppendLineIndented("interface SignalR {");
            using (scriptBuilder.IncreaseIndentation())
            {
                hubList.ForEach(h => scriptBuilder.AppendLineIndented(h.HubClassName.ToCamelCase() + ": I" + h.HubClassName + "Proxy;"));
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();

            writer.WriteLine(scriptBuilder.ToString());
        }

        private void GenerateHubInterfaces(SignalRHubDesc hub, ScriptBuilder scriptBuilder, TypeConverter converter)
        {
            if (!hub.HubType.GetDnxCompatible().BaseType.FullName.Contains(HUB_TYPE)) throw new ArgumentException("The supplied type does not appear to be a SignalR hub.", "hubType");
            // Build the client interface
            scriptBuilder.AppendLineIndented(string.Format("export interface I{0}Client {{", hub.HubClassName));
            using (scriptBuilder.IncreaseIndentation())
            {
                if (!hub.HubType.GetDnxCompatible().BaseType.GetDnxCompatible().IsGenericType)
                {
                    scriptBuilder.AppendLineIndented("/* Client interface not generated as hub doesn't derive from Hub<T> */");
                }
                else
                {
                    GenerateMethods(scriptBuilder, hub, converter, true, (mi, tc) => GenerateMethodDeclaration(mi, tc, hub, true));
                }
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();

            // Build the function for creating a hub proxy
            if (string.IsNullOrEmpty(hub.HubSignalRName))
            {
                scriptBuilder.AppendLineIndented($"/* function {hub.HubClassName}Client_CreateHubProxy not generated as hub type {hub.HubType.FullName} does not have a HubNameAttribute or the hub name is an empty string. */");
            }
            else
            {
                scriptBuilder.AppendLineIndented($"export function {hub.HubClassName}Client_CreateHubProxy(connection: SignalR.Hub.Connection): SignalR.Hub.Proxy {{");
                using (scriptBuilder.IncreaseIndentation())
                    scriptBuilder.AppendLineIndented($"return connection.createHubProxy('{hub.HubSignalRName}');");
                scriptBuilder.AppendLineIndented("}");
            }
            scriptBuilder.AppendLine();

            // Build the function for wiring up a proxy to a client interface
            if (!hub.HubType.GetDnxCompatible().BaseType.GetDnxCompatible().IsGenericType)
            {
                scriptBuilder.AppendLineIndented($"/* function {hub.HubClassName}Client_BindProxy not generated as hub doesn't derive from Hub<T> */");
            }
            else
            {
                scriptBuilder.AppendLineIndented($"export function {hub.HubClassName}Client_BindProxy(proxy: SignalR.Hub.Proxy, client: I{hub.HubClassName}Client): void {{");
                using (scriptBuilder.IncreaseIndentation())
                {
                    GenerateMethods(scriptBuilder, hub, converter, true, (mi, tc) => GenerateMethodProxyBinding(mi, tc, hub));
                }
                scriptBuilder.AppendLineIndented("}");
            }
            scriptBuilder.AppendLine();

            // Build the interface containing the SERVER methods
            scriptBuilder.AppendLineIndented($"interface I{hub.HubClassName} {{");
            using (scriptBuilder.IncreaseIndentation())
            {
                GenerateMethods(scriptBuilder, hub, converter, false, (mi, tc) => GenerateMethodDeclaration(mi, tc, hub, false));
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
            // Build the proxy class (represents the proxy generated by signalR).
            scriptBuilder.AppendLineIndented(string.Format("interface I{0}Proxy {{", hub.HubClassName));
            using (scriptBuilder.IncreaseIndentation())
            {
                scriptBuilder.AppendLineIndented("server: I" + hub.HubClassName + ";");
                scriptBuilder.AppendLineIndented("client: I" + hub.HubClassName + "Client;");
            }
            scriptBuilder.AppendLineIndented("}");
            scriptBuilder.AppendLine();
        }

        private void GenerateMethods(ScriptBuilder scriptBuilder, SignalRHubDesc hub, TypeConverter converter, bool isClient, Func<MethodInfo, TypeConverter, string> generator)
        {
            var type = isClient ? hub.HubType.GetDnxCompatible().BaseType.GenericTypeArguments.First() : hub.HubType;
            type.GetDnxCompatible().GetMethods()
                .Where(mi => !mi.IsStatic && mi.GetBaseDefinition().DeclaringType == type)
                .OrderBy(mi => mi.Name)
                .ToList()
                .ForEach(m => scriptBuilder.AppendLineIndented(generator(m, converter)));
        }

        private string GenerateMethodDeclaration(MethodInfo methodInfo, TypeConverter converter, SignalRHubDesc hub, bool isClient)
        {
            var result = methodInfo.Name.ToCamelCase() + "(";
            result += string.Join(", ", methodInfo.GetParameters().Select(param => param.Name + ": " + converter.GetTypeScriptName(param.ParameterType)));

            var returnTypeName = converter.GetTypeScriptName(methodInfo.ReturnType);
            returnTypeName = (isClient || returnTypeName == "void") ? "void" : "ISignalRPromise<" + returnTypeName + ">";
            result += "): " + returnTypeName + ";";
            return result;
        }

        private string GenerateMethodProxyBinding(MethodInfo methodInfo, TypeConverter converter, SignalRHubDesc hub)
        {
            var paramList = string.Join(", ", methodInfo.GetParameters().Select(param => param.Name));
            return $@"proxy.on(""{methodInfo.Name}"", ({paramList}) => client.{methodInfo.Name.ToCamelCase()}({paramList}));";
        }
    }
}
