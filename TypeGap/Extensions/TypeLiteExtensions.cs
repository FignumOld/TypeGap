using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TypeLite;
using TypeLite.TsModels;

namespace TypeGap.Extensions
{
    public static class TypeLiteExtensions
    {
        public static void RegisterDictionaryMemberFormatter(this TsGenerator tsGenerator, TypeConverter conv)
        {
            tsGenerator.SetMemberTypeFormatter((tsProperty, memberTypeName) =>
            {
                var dictionaryInterface =
                    tsProperty.PropertyType.Type.GetDnxCompatible().GetInterface(typeof(IDictionary<,>).Name) ??
                    tsProperty.PropertyType.Type.GetDnxCompatible().GetInterface(typeof(IDictionary).Name);

                if (dictionaryInterface != null)
                {
                    if (dictionaryInterface.GetDnxCompatible().IsGenericType)
                    {
                        var args = dictionaryInterface.GetDnxCompatible().GetGenericArguments();
                        var t1 = conv.GetTypeScriptName(args[0]);
                        var t2 = conv.GetTypeScriptName(args[1]);
                        return $"{{ [key: {t1}]: {t2} }}";
                    }
                    else
                    {
                        return "{ [key: string]: any }";
                    }

                    return tsGenerator.GetFullyQualifiedTypeName(new TsClass(dictionaryInterface));
                }
                else
                {
                    return tsGenerator.DefaultMemberTypeFormatter(tsProperty, memberTypeName);
                }
            });
        }

        public static TypeScriptFluent WithDictionaryMemberFormatter(this TypeScriptFluent typeScriptFluent, TypeConverter conv)
        {
            typeScriptFluent.ScriptGenerator.RegisterDictionaryMemberFormatter(conv);
            return typeScriptFluent;
        }
    }
}
