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

        public void WriteEnums(CustomIndentedTextWriter enumsWriter, CustomIndentedTextWriter definitionsWriter, ISet<TsEnum> enums, TypeConverter converter)
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
                definitionsWriter.WriteLine($"declare namespace {grp.Key} {{");
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
                enumsWriter.WriteLine();

                var namespaces = globals.Select(g => g.Value.Namespace)
                    .Distinct()
                    .ToArray();

                enumsWriter.WriteLine("const wnd: any = window;");
                foreach (var space in namespaces)
                    enumsWriter.WriteLine($"wnd.{space} = wnd.{space} || {{}};");

                foreach (var n_gr in globals)
                    enumsWriter.WriteLine($"wnd.{n_gr.Value.Namespace}.{n_gr.Key} = {n_gr.Key};");
            }
        }

        protected string GetEnumValue(TsEnumValue e)
        {
            return ValueMode == EnumValueMode.Number ? e.Value : "\"" + e.Name + "\"";
        }

        public abstract void GenerateEnum(CustomIndentedTextWriter enumWriter, CustomIndentedTextWriter definitionsWriter, EnumGroup enumObj, out string globalTypeName);

        //    private string GenerateEnumDefinitions(TypeScriptFluent fluent, TypeConverter converter)
        //    {
        //        var tsModel = fluent.ModelBuilder.Build();
        //        var enums = tsModel.Enums
        //            .Select(e => new { Original = e, Path = converter.GetTypeScriptName(e.Type).Split('.') })
        //            .Select(e => new { Original = e.Original, Name = e.Path.Last(), Namespace = String.Join(".", e.Path.Take(e.Path.Length - 1)) })
        //            .GroupBy(e => e.Namespace)
        //            .ToArray();

        //        var sb = new ScriptBuilder("    ");

        //        Func<TsEnumValue, string> GetEnumValue = (e) => _enumValue == EnumValueMode.Number ? e.Value : "\"" + e.Name + "\"";

        //        foreach (var grp in enums)
        //        {
        //            sb.AppendLine();
        //            sb.AppendLine($"namespace {grp.Key} {{");

        //            foreach (var e in grp)
        //            {
        //                switch (_enumOutput)
        //                {
        //                    case EnumOutputMode.Instance:
        //                    case EnumOutputMode.Const:
        //                        {
        //                            sb.AppendLine($"    export {(_enumOutput == EnumOutputMode.Const ? "const " : "")}enum {e.Name} = {{");
        //                            foreach (var value in e.Original.Values)
        //                                sb.AppendLine($"        {value.Name} = {GetEnumValue(value)},");
        //                            sb.AppendLine("    }");
        //                        }
        //                        break;
        //                    case EnumOutputMode.Type:
        //                        {
        //                            sb.AppendLine($"    export type {e.Name} = {String.Join(" | ", e.Original.Values.Select(GetEnumValue))};");
        //                        }
        //                        break;
        //                    case EnumOutputMode.Custom:
        //                        {
        //                            sb.AppendLine($"    export type {e.Name} = {String.Join(" | ", e.Original.Values.Select(GetEnumValue))};");
        //                            sb.AppendLine($"    export const {e.Name}Detail = {{");
        //                            foreach (var value in e.Original.Values)
        //                            {
        //                                string obj = $"{{ Value: {GetEnumValue(value)}, Name: \"{value.Name}\"";

        //                                var displayAttr =
        //                                    ObjectToDescriptionConverter.GetAttributes(value.Field, "Display").FirstOrDefault() ??
        //                                    ObjectToDescriptionConverter.GetAttributes(value.Field, "DisplayName").FirstOrDefault();

        //                                if (displayAttr != null)
        //                                {
        //                                    var displayText = ObjectToDescriptionConverter.TryGetBestPrivateMember(displayAttr, "Name", "DisplayName");
        //                                    if (displayText != null)
        //                                        obj += $", DisplayName: \"{displayText}\"";
        //                                }
        //                                obj += " },";

        //                                sb.AppendLine($"        {GetEnumValue(value).Trim('"')}: {obj}");
        //                                if (value.Name != GetEnumValue(value).Trim('"'))
        //                                    sb.AppendLine($"        {value.Name}: {obj}");
        //                            }
        //                            sb.AppendLine("    };");
        //                        }
        //                        break;
        //                    default:
        //                        throw new ArgumentOutOfRangeException();
        //                }
        //            }

        //            sb.AppendLine("}");
        //        }

        //        return sb.ToString();
        //    }
        //}

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
            enumWriter.WriteLine($"export {(ConstEnums ? "const " : "")}enum {e.Name} {{");
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
            enumWriter.WriteLine($"export type {e.Name} = {String.Join(" | ", e.Enum.Values.Select(GetEnumValue))};");
            globalTypeName = null;
        }
    }
}
