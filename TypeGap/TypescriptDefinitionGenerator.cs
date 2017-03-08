//using TypeBridge.Extensions;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using System.Runtime.CompilerServices;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading.Tasks;
//using System.Web.Http;
//using TypeLite;

//namespace TypeBridge.Generation
//{
//    public class TypescriptDefinitionGenerator
//    {
//        public static void ProcessAssembly(Assembly assy, string[] namespaces, string outputDir)
//        {
//            Directory.CreateDirectory(outputDir);
//            GenerateTypeScriptContracts(new[] { assy }, namespaces, outputDir);
//        }

//        private static void GenerateTypeScriptContracts(Assembly[] assemblies, string[] namespaces, string outDir)
//        {
//            var generator = new TypeScriptFluent()
//                .WithConvertor<Guid>(c => "string");

//            foreach (var assembly in assemblies)
//            {
//                // Get the WebAPI controllers...
//                var controllers = assembly.GetTypes().Where(t => typeof(ApiController).IsAssignableFrom(t));
//                GenerateWebApiActions(controllers.ToArray(), namespaces, outDir, true);

//                // Get the return types...
//                var actions = controllers
//                    .SelectMany(c => c.GetMethods()
//                        .Where(m => m.IsPublic)
//                        .Where(m => !m.IsSpecialName)
//                        .Where(m => m.DeclaringType == c));

//                ProcessMethods(actions, generator, namespaces);

//                //var signalrHubs = assembly.GetTypes().Where(t => typeof(IHub).IsAssignableFrom(t));
//                //var methods = signalrHubs
//                //    .SelectMany(h => h.GetMethods()
//                //        .Where(m => m.IsPublic)
//                //        .Where(m => m.GetBaseDefinition().DeclaringType == h));
//                //ProcessMethods(methods, generator);

//                //var clientInterfaceTypes = signalrHubs.Where(t => t.BaseType.IsGenericType)
//                //    .Select(t => t.BaseType.GetGenericArguments()[0]);
//                //var clientMethods = clientInterfaceTypes
//                //    .SelectMany(h => h.GetMethods()
//                //        .Where(m => m.IsPublic)
//                //        .Where(m => m.DeclaringType == h));
//                //ProcessMethods(clientMethods, generator);

//                // Add all classes that are declared inside the specified namespace
//                if (namespaces != null && namespaces.Any())
//                {
//                    var types = assembly.GetTypes().Where(t => IncludedNamespace(namespaces, t)).Except(controllers);
//                    ProcessTypes(types, generator, namespaces);
//                }

//                generator.AsConstEnums(false);
//            }

//            var tsEnumDefinitions = generator.Generate(TsGeneratorOutput.Enums);
//            File.WriteAllText(Path.Combine(outDir, "enums.ts"), tsEnumDefinitions);

//            //Generate interface definitions for all classes
//            var tsClassDefinitions = generator.Generate(TsGeneratorOutput.Properties | TsGeneratorOutput.Fields);
//            File.WriteAllText(Path.Combine(outDir, "classes.d.ts"), tsClassDefinitions);
//        }

//        private static bool IncludedNamespace(string[] namespaces, Type t)
//        {
//            return namespaces.Any(n => Regex.IsMatch((t.Namespace ?? ""), WildcardToRegex(n)));
//        }

//        private static string WildcardToRegex(string pattern)
//        {
//            return "^" + Regex.Escape(pattern).Replace(@"%", ".*") + "$";
//        }

//        private static void ProcessMethods(IEnumerable<MethodInfo> methods, TypeScriptFluent generator, string[] namespaces)
//        {
//            var returnTypes = methods.Select(m => m.ReturnType);
//            ProcessTypes(returnTypes, generator, namespaces);
//            var inputTypes = methods.SelectMany(m => m.GetParameters()).Select(p => p.ParameterType);
//            ProcessTypes(inputTypes, generator, namespaces);
//        }

//        //private static void GenerateSignalrHubs(Options options)
//        //{
//        //    var allOutput = new StringBuilder();
//        //    foreach (var assemblyName in options.Assemblies)
//        //    {
//        //        var assembly = Assembly.LoadFrom(assemblyName);
//        //        allOutput.Append(new SignalRGenerator().GenerateHubs(assembly));
//        //    }
//        //    // Don't create the output if we don't have any hubs!
//        //    if (allOutput.Length == 0) return;

//        //    File.WriteAllText(Path.Combine(options.OutputFilePath, "hubs.d.ts"), allOutput.ToString());
//        //}

//        private static void GenerateWebApiActions(Type[] controllers, string[] namespaces, string outDir, bool writeServiceCaller)
//        {
//            var output = new StringBuilder();

//            output.AppendLine("/// <reference path=\"./classes.d.ts\" />");
//            output.AppendLine("import { AjaxService, IExtendedAjaxSettings } from \"./ajaxService\";");
//            output.AppendLine();

//            //TODO: allow this is be configured
//            output.Append(_interfaces);

//            foreach (var controller in controllers)
//            {
//                var controllerName = controller.Name.Replace("Controller", "");
//                output.AppendFormat("\r\nexport class {0}Service {{\r\n", controllerName);

//                output.AppendLine("  private constructor() { }");

//                var actions = controller.GetMethods()
//                    .Where(m => m.IsPublic)
//                    .Where(m => !m.IsSpecialName)
//                    .Where(m => m.DeclaringType == controller)
//                    .OrderBy(m => m.Name);

//                // TODO: WebAPI supports multiple actions with the same name but different parameters - this doesn't!
//                foreach (var action in actions)
//                {
//                    if (NotAnAction(action)) continue;

//                    var httpMethod = GetHttpMethod(action);
//                    var actionName = GetActionName(action);
//                    var returnType = IncludedNamespace(namespaces, action.ReturnType) ? TypeConverter.GetTypeScriptName(action.ReturnType) : "any";

//                    var actionParameters = GetWebApiActionParameters(action);
//                    var routeParameters = GetRouteParameters(actionParameters);
//                    var queryStringParameters = GetQueryStringParameters(actionParameters);
//                    var dataParameter = actionParameters.FirstOrDefault(a => !a.FromUri && !a.RouteProperty);
//                    var dataParameterName = dataParameter == null ? "null" : dataParameter.Name;

//                    // allow ajax options to be passed in to override defaults
//                    output.AppendLine();
//                    output.AppendFormat("  public static {0}({1}): JQueryPromise<{2}> {{\r\n", actionName, GetMethodParameters(actionParameters), returnType);

//                    output.AppendFormat("    return AjaxService.{0}(\"api/{1}/{2}{3}{4}\", {5}, ajaxOptions);\r\n", httpMethod, controllerName, actionName, routeParameters, queryStringParameters, dataParameterName);
//                    output.AppendLine("  }");
//                }
//            }
//            output.AppendLine("}");


//            File.WriteAllText(Path.Combine(outDir, "actions.ts"), output.ToString());

//            if (writeServiceCaller)
//            {
//                // Write the default service caller
//                using (var stream = typeof(TypescriptDefinitionGenerator).Assembly.GetManifestResourceStream("TypeBridge.Resources.AjaxService.ts"))
//                using (var reader = new StreamReader(stream))
//                {
//                    File.WriteAllText(Path.Combine(outDir, "ajaxService.ts"), reader.ReadToEnd());
//                }
//            }

//        }

//        private static string GetQueryStringParameters(List<ActionParameterInfo> actionParameters)
//        {
//            var result = string.Join("&", actionParameters.Where(a => a.FromUri && !a.RouteProperty).Select(a => a.Name + "=\" + " + a.Name + " + \""));
//            if (result != "") result = "?" + result;
//            return result;
//        }

//        private static string GetRouteParameters(List<ActionParameterInfo> actionParameters)
//        {
//            var result = string.Join("/", actionParameters.Where(a => a.RouteProperty).Select(a => "\" + " + a.Name + " + \""));
//            if (result != "") result = "/" + result;
//            return result;
//        }

//        private static string GetMethodParameters(List<ActionParameterInfo> actionParameters)
//        {
//            var result = string.Join(", ", actionParameters.Select(a => a.Name + ": " + a.Type));
//            if (result != "") result += ", ";
//            result += "ajaxOptions?: IExtendedAjaxSettings";
//            return result;
//        }

//        private static List<ActionParameterInfo> GetWebApiActionParameters(MethodInfo action)
//        {
//            var result = new List<ActionParameterInfo>();
//            var parameters = action.GetParameters();
//            foreach (var parameterInfo in parameters)
//            {
//                var param = new ActionParameterInfo();
//                param.Name = parameterInfo.Name;
//                param.Type = TypeConverter.GetTypeScriptName(parameterInfo.ParameterType);
//                var fromUri = parameterInfo.GetCustomAttributes<FromUriAttribute>().FirstOrDefault();
//                if (fromUri != null)
//                {
//                    param.Name = fromUri.Name ?? param.Name;
//                }
//                var fromBody = parameterInfo.GetCustomAttributes<FromBodyAttribute>().FirstOrDefault();
//                // Parameters are from the URL unless specified by a [FromBody] attribute.
//                param.FromUri = fromBody == null;

//                //TODO: Support route parameters that are not 'id', might be hard as will need to parse routing setup
//                if (parameterInfo.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
//                {
//                    param.RouteProperty = true;
//                }
//                param.Name = param.Name.ToCamelCase();
//                result.Add(param);
//            }

//            return result;
//        }

//        private static string GetActionName(MethodInfo action)
//        {
//            // TODO: Support ActionNameAttribute

//            var route = action.GetCustomAttribute<RouteAttribute>();
//            if (route != null)
//                return route.Template;

//            return action.Name.ToCamelCase();
//        }

//        private static string GetHttpMethod(MethodInfo action)
//        {
//            // TODO: Support other http methods
//            if (action.CustomAttributes.Any(a => a.AttributeType.Name == typeof(HttpPostAttribute).Name)) return "post";
//            return "get";
//        }

//        private static bool NotAnAction(MethodInfo action)
//        {
//            return action.CustomAttributes.Any(a => a.AttributeType.Name == typeof(NonActionAttribute).Name);
//        }


//        private static void ProcessTypes(IEnumerable<Type> types, TypeScriptFluent generator, string[] namespaces)
//        {
//            foreach (var clrType in types.Where(t => t != typeof(void)))
//            {
//                var clrTypeToUse = clrType;
//                if (typeof(Task).IsAssignableFrom(clrTypeToUse))
//                {
//                    if (clrTypeToUse.IsGenericType)
//                    {
//                        clrTypeToUse = clrTypeToUse.GetGenericArguments()[0];
//                    }
//                    else continue; // Ignore non-generic Task as we can't know what type it will really be
//                }

//                if (clrTypeToUse.IsNullable())
//                {
//                    clrTypeToUse = clrTypeToUse.GetUnderlyingNullableType();
//                }

//                // Ignore compiler generated types
//                if (Attribute.GetCustomAttribute(clrTypeToUse, typeof(CompilerGeneratedAttribute)) != null)
//                {
//                    continue;
//                }

//                if (!IncludedNamespace(namespaces, clrType))
//                    continue;

//                Console.WriteLine("Processing Type: " + clrTypeToUse);
//                if (clrTypeToUse == typeof(string) || clrTypeToUse.IsPrimitive || clrTypeToUse == typeof(object)) continue;

//                if (clrTypeToUse.IsArray)
//                {
//                    ProcessTypes(new[] { clrTypeToUse.GetElementType() }, generator, namespaces);
//                }
//                else if (clrTypeToUse.IsGenericType)
//                {
//                    ProcessTypes(clrTypeToUse.GetGenericArguments(), generator, namespaces);
//                    bool isEnumerable = typeof(IEnumerable).IsAssignableFrom(clrTypeToUse);
//                    if (!isEnumerable)
//                    {
//                        generator.ModelBuilder.Add(clrTypeToUse);
//                    }
//                }
//                else
//                {
//                    generator.ModelBuilder.Add(clrTypeToUse);
//                }
//            }
//        }

//        private static string _interfaces =
//@"export interface IDictionary<T> {
//   [key: string]: T;
//}
//";

//        private class ActionParameterInfo
//        {
//            public string Name { get; set; }
//            public bool FromUri { get; set; }
//            public bool RouteProperty { get; set; }
//            public string Type { get; set; }
//        }
//    }
//}
