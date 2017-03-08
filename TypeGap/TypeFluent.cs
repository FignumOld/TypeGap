using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TypeGap.Extensions;
using TypeLite;

namespace TypeGap
{
    public class TypeFluent
    {
        private List<Type> _hubs = new List<Type>();
        private List<Type> _controllers = new List<Type>();
        private List<Type> _general = new List<Type>();

        public TypeFluent Add(Type t)
        {
            _general.Add(t);
            return this;
        }

        public TypeFluent Add<T>()
        {
            return Add(typeof(T));
        }

        public TypeFluent AddWebApi(Type t)
        {
            if (!typeof(System.Web.Http.ApiController).IsAssignableFrom(t))
                throw new ArgumentException("Type must be assignable to System.Web.Http.ApiController");

            _controllers.Add(t);
            return this;
        }

        public TypeFluent AddWebApi<T>()
        {
            return AddWebApi(typeof(T));
        }

        public TypeFluent AddSignalRHub<T>()
        {
            return AddSignalRHub(typeof(T));
        }

        public TypeFluent AddSignalRHub(Type t)
        {
            if (t.BaseType == null || t.BaseType.FullName == null || !t.BaseType.FullName.Contains(SignalRGenerator.HUB_TYPE))
                throw new ArgumentException("Type must directly derive from the Hub type.");

            _hubs.Add(t);
            return this;
        }
            
        public TypeFluidOutput Build()
        {
            var services = new StringWriter();
            var servicesWriter = new IndentedTextWriter(services, "    ");

            TypeScriptFluent fluent = new TypeScriptFluent();
            fluent.WithConvertor<Guid>(c => "string");
            fluent.AsConstEnums(false);
            fluent.WithIndentation("    ");

            var webapi = new WebApiGenerator(fluent);
            webapi.WriteServices(_controllers.ToArray(), servicesWriter);

            var signalr = new SignalRGenerator();
            signalr.WriteHubs(_hubs.ToArray(), fluent, servicesWriter);

            ProcessTypes(_general, fluent);

            var tsEnumDefinitions = fluent.Generate(TsGeneratorOutput.Enums);
            var tsClassDefinitions = fluent.Generate(TsGeneratorOutput.Properties | TsGeneratorOutput.Fields);

            return new TypeFluidOutput
            {
                DefinitionTS = tsClassDefinitions,
                EnumsTS = tsEnumDefinitions,
                ServicesTS = services.GetStringBuilder().ToString(),
            };
        }

        public void Build(string definitionPath, string servicesPath, string enumsPath)
        {
            var output = Build();
            WriteFile(definitionPath, output.DefinitionTS);
            WriteFile(servicesPath, output.ServicesTS);
            WriteFile(enumsPath, output.EnumsTS);
        }

        public void Build(string basePath, string definitionName, string servicesName, string enumsName)
        {
            Build(Path.Combine(basePath, definitionName), Path.Combine(basePath, servicesName), Path.Combine(basePath, enumsName));
        }

        public void Build(string outputDirectory)
        {
            Build(outputDirectory, "definitions.d.ts", "services.ts", "enums.ts");
        }

        private static void ProcessTypes(IEnumerable<Type> types, TypeScriptFluent generator)
        {
            foreach (var clrType in types.Where(t => t != typeof(void)))
            {
                var clrTypeToUse = clrType;
                if (typeof(Task).IsAssignableFrom(clrTypeToUse))
                {
                    if (clrTypeToUse.IsGenericType)
                    {
                        clrTypeToUse = clrTypeToUse.GetGenericArguments()[0];
                    }
                    else continue; // Ignore non-generic Task as we can't know what type it will really be
                }

                if (clrTypeToUse.IsNullable())
                    clrTypeToUse = clrTypeToUse.GetUnderlyingNullableType();

                // Ignore compiler generated types
                if (Attribute.GetCustomAttribute(clrTypeToUse, typeof(CompilerGeneratedAttribute)) != null)
                    continue;

                if (clrTypeToUse.Namespace.StartsWith("System"))
                    continue;

                if (clrTypeToUse == typeof(string) || clrTypeToUse.IsPrimitive || clrTypeToUse == typeof(object)) continue;

                if (clrTypeToUse.IsArray)
                {
                    ProcessTypes(new[] { clrTypeToUse.GetElementType() }, generator);
                }
                else if (clrTypeToUse.IsGenericType)
                {
                    ProcessTypes(clrTypeToUse.GetGenericArguments(), generator);
                    bool isEnumerable = typeof(IEnumerable).IsAssignableFrom(clrTypeToUse);
                    if (!isEnumerable)
                    {
                        generator.ModelBuilder.Add(clrTypeToUse);
                    }
                }
                else
                {
                    generator.ModelBuilder.Add(clrTypeToUse);
                }
            }
        }

        private void WriteFile(string path, string content)
        {
            path = Path.GetFullPath(path);
            var dir = Path.GetDirectoryName(path);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, Resx.GeneratedNotice + Environment.NewLine + Environment.NewLine + content);
        }
    }

    public class TypeFluidOutput
    {
        public string DefinitionTS { get; set; }
        public string ServicesTS { get; set; }
        public string EnumsTS { get; set; }
    }
}
