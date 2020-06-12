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
        private List<SignalRHubDesc> _hubs = new List<SignalRHubDesc>();
        private List<Type> _general = new List<Type>();
        private string _namespace;
        private List<ApiControllerDesc> _apis = new List<ApiControllerDesc>();
        private bool _generateNotice = true;
        private GapEnumGenerator _enumGenerator = new RegularEnumGenerator(false, EnumValueMode.Number);
        private string _indent = "    ";
        private ITsModelVisitor _modelVisitor;
        private Dictionary<Type, string> _typeConversions;

        public TypeFluent Add(Type t)
        {
            _general.Add(t);
            return this;
        }

        public TypeFluent Add<T>()
        {
            return Add(typeof(T));
        }

        public SignalRHubDesc AddSignalRHub<T>()
        {
            return AddSignalRHub(typeof(T));
        }

        public SignalRHubDesc AddSignalRHub(Type t)
        {
            if (t.GetDnxCompatible().BaseType == null || t.GetDnxCompatible().BaseType.FullName == null ||
                !(t.GetDnxCompatible().BaseType.FullName.Contains(SignalRGenerator.HUB_TYPE) || t.GetDnxCompatible().BaseType.FullName.Contains(SignalRGenerator.HUB_TYPE_CORE)))
                throw new ArgumentException("Type must directly derive from the Hub type.");

            var desc = new SignalRHubDesc(t);
            _hubs.Add(desc);
            return desc;
        }

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

        public string Build(GapApiGeneratorOptions options = null)
        {
            var services = new StringWriter();
            var servicesWriter = new CustomIndentedTextWriter(services, _indent);

            var enums = new StringWriter();
            var enumsWriter = new CustomIndentedTextWriter(enums, _indent);

            var globals = new StringWriter();
            var globalsWriter = new CustomIndentedTextWriter(globals, _indent);

            var definitions = new StringWriter();
            var definitionsWriter = new CustomIndentedTextWriter(definitions, _indent);

            TypeScriptFluent fluent = new TypeScriptFluent();
            fluent.WithConvertor<Guid>(c => "string");

            if (_typeConversions != null)
                foreach (var conversion in _typeConversions)
                    fluent.WithConvertor(conversion.Key, t => conversion.Value);

            fluent.WithIndentation(_indent);
            fluent.WithModelVisitor(_modelVisitor);

            var converter = new TypeConverter(_namespace, fluent, _typeConversions);
            fluent.WithDictionaryMemberFormatter(converter);

            if (!string.IsNullOrEmpty(_namespace))
                fluent.WithModuleNameFormatter(m => _namespace);

            ProcessTypes(_general, fluent);
            var model = fluent.ModelBuilder.Build(); // this is to fix up manually added types before GapApiGenerator

            var apiGen = new GapApiGenerator(converter, _indent, options ?? new GapApiGeneratorOptions());
            apiGen.WriteServices(_apis.ToArray(), servicesWriter);

            var signalr = new SignalRGenerator();
            signalr.WriteHubs(_hubs.ToArray(), converter, servicesWriter);

            var tsClassDefinitions = fluent.Generate(TsGeneratorOutput.Properties | TsGeneratorOutput.Fields);
            definitionsWriter.Write(tsClassDefinitions);

            _enumGenerator.WriteEnums(enumsWriter, globalsWriter, definitionsWriter, model.Enums, converter);

            string prepended = _generateNotice ? Resx.GeneratedNotice + "\r\n\r\n" : "";
            prepended +=
                services.GetStringBuilder() + Environment.NewLine +
                "declare global {" +
                (definitions.GetStringBuilder().ToString() + enums.GetStringBuilder()).Replace("declare namespace", "namespace").Replace("\n", "\n    ").TrimEnd() + Environment.NewLine +
                "}" + Environment.NewLine + globals.GetStringBuilder();
            return prepended;
        }

        public TypeFluent WithGlobalNamespace(string @namespace)
        {
            _namespace = @namespace;
            return this;
        }

        public TypeFluent WithGeneratedNotice(bool generateNotice = true)
        {
            _generateNotice = generateNotice;
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

        public TypeFluent WithTypeConversions(Dictionary<Type, string> typeConversions)
        {
            _typeConversions = typeConversions;
            return this;
        }

        public void Build(string outputPath, GapApiGeneratorOptions options = null)
        {
            WriteFile(outputPath, Build(options));
        }

        private static void ProcessTypes(IEnumerable<Type> types, TypeScriptFluent generator)
        {
            foreach (var clrType in types.Where(t => t != typeof(void)))
            {
                if (generator.ModelBuilder.ContainsType(clrType))
                    continue;

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

                // No need to add types which TypeLite considers built-in
                if (TsType.GetTypeFamily(clrTypeToUse) == TsTypeFamily.System)
                    continue;

                // Skip all other System types unless a custom converter is registered for it
                if (clrTypeToUse.Namespace.StartsWith("System"))
                    if (!generator.IsTypeConvertorRegistered(clrTypeToUse))
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
