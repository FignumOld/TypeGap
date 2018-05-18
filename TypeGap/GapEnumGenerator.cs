using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TypeGap.Util;
using TypeLite;
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

        protected string GetEnumValue(TsEnumValue e)
        {
            return ValueMode == EnumValueMode.Number ? e.Value : "\"" + e.Name + "\"";
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
                enumWriter.WriteLine($"{value.Name} = {GetEnumValue(value)},");
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
            enumWriter.WriteLine($"type {e.Name} = {String.Join(" | ", e.Enum.Values.Select(GetEnumValue))};");
            globalTypeName = null;
        }
    }
}
