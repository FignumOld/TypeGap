using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TypeGap.Extensions;
using TypeGap.Util;
using TypeLite;
using TypeLite.TsModels;

namespace TypeGap
{
    public enum EnumValueMode
    {
        Number,
        String,
    }

    public class TypeFluent
    {
        //private List<Type> _hubs = new List<Type>();
        private List<Type> _general = new List<Type>();
        private string _namespace;
        private List<ApiControllerDesc> _apis = new List<ApiControllerDesc>();
        private string _promiseType = "Promise";
        private Func<string, string> _urlRewriter = (u) => u;
        private string _ajaxName = "./Ajax";
        private bool _generateNotice = true;
        private GapEnumGenerator _enumGenerator = new RegularEnumGenerator(false, EnumValueMode.Number);
        private string _indent = "    ";
        private ITsModelVisitor _modelVisitor;

        public TypeFluent Add(Type t)
        {
            _general.Add(t);
            return this;
        }

        public TypeFluent Add<T>()
        {
            return Add(typeof(T));
        }

        //public TypeFluent AddSignalRHub<T>()
        //{
        //    return AddSignalRHub(typeof(T));
        //}

        //public TypeFluent AddSignalRHub(Type t)
        //{
        //    if (t.BaseType == null || t.BaseType.FullName == null || !t.BaseType.FullName.Contains(SignalRGenerator.HUB_TYPE))
        //        throw new ArgumentException("Type must directly derive from the Hub type.");

        //    _hubs.Add(t);
        //    return this;
        //}

        public ApiControllerDesc AddApiDescription(string name, string route = null)
        {
            var api = new ApiControllerDesc();
            api.ControllerName = name;

            if (!String.IsNullOrWhiteSpace(route))
                api.RouteTemplate = route;

            _apis.Add(api);
            return api;
        }

        public ApiControllerDesc AddApiObject(Type type)
        {
            var api = ObjectToDescriptionConverter.Convert(type);
            _apis.Add(api);
            return api;
        }

        public TypeFluidOutput Build()
        {
            var services = new StringWriter();
            var servicesWriter = new CustomIndentedTextWriter(services, _indent);

            var enums = new StringWriter();
            var enumsWriter = new CustomIndentedTextWriter(enums, _indent);

            var definitions = new StringWriter();
            var definitionsWriter = new CustomIndentedTextWriter(definitions, _indent);

            TypeScriptFluent fluent = new TypeScriptFluent();
            fluent.WithConvertor<Guid>(c => "string");
            fluent.WithIndentation(_indent);
            fluent.WithModelVisitor(_modelVisitor);

            var converter = new TypeConverter(_namespace, fluent);
            fluent.WithDictionaryMemberFormatter(converter);

            if (!string.IsNullOrEmpty(_namespace))
                fluent.WithModuleNameFormatter(m => _namespace);

            var apiGen = new GapApiGenerator(converter, _urlRewriter, _promiseType, _ajaxName);
            apiGen.WriteServices(_apis.ToArray(), servicesWriter);

            //var signalr = new SignalRGenerator();
            //signalr.WriteHubs(_hubs.ToArray(), converter, servicesWriter);

            ProcessTypes(_general, fluent);

            var tsClassDefinitions = fluent.Generate(TsGeneratorOutput.Properties | TsGeneratorOutput.Fields);
            definitionsWriter.Write(tsClassDefinitions);

            _enumGenerator.WriteEnums(enumsWriter, definitionsWriter, fluent.ModelBuilder.Build().Enums, converter);

            string prepended = _generateNotice ? Resx.GeneratedNotice + "\r\n\r\n" : "";

            return new TypeFluidOutput
            {
                DefinitionTS = prepended + definitions.GetStringBuilder(),
                EnumsTS = prepended + enums.GetStringBuilder(),
                ServicesTS = prepended + services.GetStringBuilder(),
            };
        }

        public TypeFluent WithGlobalNamespace(string @namespace)
        {
            _namespace = @namespace;
            return this;
        }

        public TypeFluent WithPromiseType(string promise)
        {
            _promiseType = promise;
            return this;
        }

        public TypeFluent WithUrlRewriter(Func<string, string> rewriter)
        {
            _urlRewriter = rewriter;
            return this;
        }

        public TypeFluent WithGeneratedNotice(bool generateNotice = true)
        {
            _generateNotice = generateNotice;
            return this;
        }

        public TypeFluent WithAjaxServicePath(string path)
        {
            _ajaxName = path;
            return this;
        }

        public TypeFluent WithIndent(string indent)
        {
            _indent = indent;
            return this;
        }

        public TypeFluent WithEnumGenerator(GapEnumGenerator generator)
        {
            _enumGenerator = generator;
            return this;
        }

        public TypeFluent WithModelVisitor(ITsModelVisitor visitor)
        {
            _modelVisitor = visitor;
            return this;
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
                if (typeof(Task).GetDnxCompatible().IsAssignableFrom(clrTypeToUse))
                {
                    if (clrTypeToUse.GetDnxCompatible().IsGenericType)
                    {
                        clrTypeToUse = clrTypeToUse.GetDnxCompatible().GetGenericArguments()[0];
                    }
                    else continue; // Ignore non-generic Task as we can't know what type it will really be
                }

                if (clrTypeToUse.IsNullable())
                    clrTypeToUse = clrTypeToUse.GetUnderlyingNullableType();

                // Ignore compiler generated types
                if (clrTypeToUse.GetDnxCompatible().GetCustomAttribute(typeof(CompilerGeneratedAttribute)) != null)
                    continue;

                if (clrTypeToUse.Namespace.StartsWith("System"))
                    continue;

                if (clrTypeToUse.IsIDictionary())
                    continue;

                if (clrTypeToUse == typeof(string) || clrTypeToUse.GetDnxCompatible().IsPrimitive || clrTypeToUse == typeof(object)) continue;


                bool isClassOrArray = clrTypeToUse.GetDnxCompatible().IsClass || clrTypeToUse.GetDnxCompatible().IsInterface;
                TsModuleMember member = null;
                if (clrTypeToUse.IsArray)
                {
                    ProcessTypes(new[] { clrTypeToUse.GetElementType() }, generator);
                }
                else if (clrTypeToUse.GetDnxCompatible().IsGenericType)
                {
                    ProcessTypes(clrTypeToUse.GetDnxCompatible().GetGenericArguments(), generator);
                    bool isEnumerable = typeof(IEnumerable).GetDnxCompatible().IsAssignableFrom(clrTypeToUse);
                    if (!isEnumerable)
                    {
                        member = generator.ModelBuilder.Add(clrTypeToUse, !isClassOrArray);
                    }
                }
                else
                {
                    member = generator.ModelBuilder.Add(clrTypeToUse, !isClassOrArray);
                }

                var classModel = member as TsClass;
                if (isClassOrArray && classModel != null)
                {
                    var references = classModel.Properties
                        .Where(model => !model.IsIgnored)
                        .Select(m => m.PropertyType)
                        .Concat(classModel.GenericArguments)
                        .Select(m => m.Type)
                        .Where(t => !t.IsIDictionary())
                        .ToArray();

                    ProcessTypes(references, generator);
                }
            }
        }

        private void WriteFile(string path, string content)
        {
            path = Path.GetFullPath(path);
            var dir = Path.GetDirectoryName(path);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, content);
        }
    }

    public class TypeFluidOutput
    {
        public string DefinitionTS { get; set; }
        public string ServicesTS { get; set; }
        public string EnumsTS { get; set; }
    }
}
