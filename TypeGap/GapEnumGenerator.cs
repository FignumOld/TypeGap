using System;
using System.Collections.Generic;
using System.Linq;
using TypeGap.Util;
using TypeLite.TsModels;

namespace TypeGap
{
    public abstract class GapEnumGenerator
    {
        public EnumValueMode ValueMode { get; }

        public class EnumGroup
        {
            public TsEnum Enum { get; set; }
            public string Name { get; set; }
            public string Namespace { get; set; }
        }

        public GapEnumGenerator(EnumValueMode valueMode)
        {
            ValueMode = valueMode;
        }

        public void WriteEnums(CustomIndentedTextWriter enumsWriter, CustomIndentedTextWriter globalsWriter, CustomIndentedTextWriter definitionsWriter, ISet<TsEnum> enums, TypeConverter converter)
        {
            var enumGroup = enums
                .Select(e => new { Original = e, Path = converter.GetTypeScriptName(e.Type).Split('.') })
                .Select(e => new EnumGroup { Enum = e.Original, Name = e.Path.Last(), Namespace = String.Join(".", e.Path.Take(e.Path.Length - 1)) })
                .GroupBy(e => e.Namespace)
                .ToArray();

            Dictionary<string, EnumGroup> globals = new Dictionary<string, EnumGroup>();

            foreach (var grp in enumGroup)
            {
                enumsWriter.WriteLine();
                enumsWriter.WriteLine($"namespace {grp.Key} {{");
                enumsWriter.Indent++;

                definitionsWriter.WriteLine();
                definitionsWriter.WriteLine($"namespace {grp.Key} {{");
                definitionsWriter.Indent++;

                foreach (var e in grp)
                {
                    GenerateEnum(enumsWriter, definitionsWriter, e, out var globalTypeName);
                    if (!String.IsNullOrWhiteSpace(globalTypeName))
                        globals.Add(globalTypeName, e);
                }

                enumsWriter.Indent--;
                enumsWriter.WriteLine("}");

                definitionsWriter.Indent--;
                definitionsWriter.WriteLine("}");
            }

            if (globals.Any())
            {
                var namespaces = globals.Select(g => g.Value.Namespace)
                    .Distinct()
                    .ToArray();

                globalsWriter.WriteLine("const wnd: any = window;");
                foreach (var space in namespaces)
                    globalsWriter.WriteLine($"wnd.{space} = wnd.{space} || {{}};");

                foreach (var n_gr in globals)
                    globalsWriter.WriteLine($"wnd.{n_gr.Value.Namespace}.{n_gr.Key} = {n_gr.Value.Namespace}.{n_gr.Key};");
            }
        }

        /// <summary>
        /// Obtains the string value for the given enum field (<paramref name="enumValue"/>).
        /// The value obtained takes into account the <see cref="ValueMode"/> specified as the parameter in the constructor.
        /// </summary>
        /// <param name="enumGroup">The definitions for the type containing the enum field.</param>
        /// <param name="enumValue">The field/option of the enum that the values needs to be obtained from.</param>
        protected virtual string GetEnumValue(EnumGroup enumGroup, TsEnumValue enumValue)
        {
            return GapEnumGenerator.GetEnumValue(ValueMode, enumValue);
        }

        /// <summary>
        /// Obtains the string value for the given enum field (<paramref name="enumValue"/>).
        /// The value obtained depends on the <paramref name="mode"/> specified.
        /// </summary>
        /// <param name="mode">The mode used to generate the enum value, it can be the enum field value or the enum field name.</param>
        /// <param name="enumValue">The field/option of the enum that the values needs to be obtained from.</param>
        protected static string GetEnumValue(EnumValueMode mode, TsEnumValue enumValue)
        {
            return mode == EnumValueMode.Number ? enumValue.Value : "\"" + enumValue.Name + "\"";
        }

        public abstract void GenerateEnum(CustomIndentedTextWriter enumWriter, CustomIndentedTextWriter definitionsWriter, EnumGroup enumObj, out string globalTypeName);
    }

    public class NonConstEnumInitializer : BasicTypeInitializer
    {
        public override bool CanConvertType(Type t)
        {
            throw new NotImplementedException();
        }

        public override string FromTsType(string objectName)
        {
            throw new NotImplementedException();
        }

        public override string ToTsType(string objectName)
        {
            throw new NotImplementedException();
        }
    }

    public class RegularEnumGenerator : GapEnumGenerator
    {
        public bool ConstEnums { get; }

        public RegularEnumGenerator(bool constEnums, EnumValueMode valueMode) : base(valueMode)
        {
            ConstEnums = constEnums;
        }

        public override void GenerateEnum(CustomIndentedTextWriter enumWriter, CustomIndentedTextWriter definitionsWriter, EnumGroup e, out string globalTypeName)
        {
            enumWriter.WriteLine($"{(ConstEnums ? "const " : "")}enum {e.Name} {{");
            foreach (var value in e.Enum.Values)
            {
                enumWriter.Indent++;
                enumWriter.WriteLine($"{value.Name} = {this.GetEnumValue(e, value)},");
                enumWriter.Indent--;
            }
            enumWriter.WriteLine("}");
            globalTypeName = ConstEnums ? null : e.Name;
        }
    }

    public class TypeEnumGenerator : GapEnumGenerator
    {
        public TypeEnumGenerator(EnumValueMode valueMode) : base(valueMode)
        {

        }

        public override void GenerateEnum(CustomIndentedTextWriter enumWriter, CustomIndentedTextWriter definitionsWriter, EnumGroup e, out string globalTypeName)
        {
            enumWriter.WriteLine($"type {e.Name} = {String.Join(" | ", e.Enum.Values.Select(enumValue => GetEnumValue(e, enumValue)))};");
            globalTypeName = null;
        }
    }
}
