using TypeGap.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TypeLite;
using TypeLite.TsModels;

namespace TypeGap
{
    public class TypeConverter
    {
        private readonly string _globalNamespace;
        private readonly TypeScriptFluent _fluent;
        private static readonly Dictionary<Type, string> _cache;

        static TypeConverter()
        {
            _cache = new Dictionary<Type, string>();
            // Integral types
            _cache.Add(typeof(object), "any");
            _cache.Add(typeof(bool), "boolean");
            _cache.Add(typeof(byte), "number");
            _cache.Add(typeof(sbyte), "number");
            _cache.Add(typeof(short), "number");
            _cache.Add(typeof(ushort), "number");
            _cache.Add(typeof(int), "number");
            _cache.Add(typeof(uint), "number");
            _cache.Add(typeof(long), "number");
            _cache.Add(typeof(ulong), "number");
            _cache.Add(typeof(float), "number");
            _cache.Add(typeof(double), "number");
            _cache.Add(typeof(decimal), "number");
            _cache.Add(typeof(string), "string");
            _cache.Add(typeof(char), "string");
            _cache.Add(typeof(DateTime), "Date");
            _cache.Add(typeof(DateTimeOffset), "Date");
            _cache.Add(typeof(byte[]), "string");
            _cache.Add(typeof(Guid), "string");
            _cache.Add(typeof(Exception), "string");
            _cache.Add(typeof(void), "void");
        }

        public TypeConverter(string globalNamespace, TypeScriptFluent fluent)
        {
            _globalNamespace = globalNamespace;
            _fluent = fluent;
        }

        public bool IsComplexType(Type clrType)
        {
            return !_cache.ContainsKey(clrType);
        }

        private string GetFullName(Type clrType)
        {
            string moduleName = null;

            var tsMember = _fluent.ModelBuilder.GetType(clrType) as TsModuleMember;
            if (tsMember != null)
                moduleName = tsMember.Module.Name + "." + tsMember.Name;

            return String.IsNullOrEmpty(_globalNamespace) ? (moduleName ?? clrType.FullName) : _globalNamespace + "." + clrType.Name;
        }

        public string GetTypeScriptName(Type clrType)
        {
            string result;

            if (clrType.IsNullable())
            {
                clrType = clrType.GetUnderlyingNullableType();
            }

            if (clrType.IsGenericTask())
            {
                clrType = clrType.GetUnderlyingTaskType();
            }

            if (_cache.TryGetValue(clrType, out result))
            {
                return result;
            }

            if (clrType.Name == "IActionResult")
            {
                return "any /* IActionResult */";
            }

            // these objects generally just mean json, so 'any' is appropriate.
            if (clrType.Namespace == "Newtonsoft.Json.Linq")
            {
                return "any";
            }

            // Dictionaries -- these should come before IEnumerables, because they also implement IEnumerable
            if (clrType.IsIDictionary())
            {
                if (clrType.GenericTypeArguments.Length != 2)
                    return "{ [key: string]: any }";

                // TODO: can we use Map<> instead? this will break if the first argument is not string | number.
                return $"{{ [key: {GetTypeScriptName(clrType.GetDnxCompatible().GetGenericArguments()[0])}]: {GetTypeScriptName(clrType.GetDnxCompatible().GetGenericArguments()[1])} }}";
            }

            if (clrType.IsArray)
            {
                return GetTypeScriptName(clrType.GetElementType()) + "[]";
            }

            if (typeof(IEnumerable).GetDnxCompatible().IsAssignableFrom(clrType))
            {
                if (clrType.GetDnxCompatible().IsGenericType)
                {
                    return GetTypeScriptName(clrType.GetDnxCompatible().GetGenericArguments()[0]) + "[]";
                }
                return "any[]";
            }

            if (clrType.Namespace.StartsWith("System."))
                return "any";

            if (clrType.GetDnxCompatible().IsEnum)
            {
                _fluent.ModelBuilder.Add(clrType);
                return GetFullName(clrType);
            }

            if (clrType.GetDnxCompatible().IsClass || clrType.GetDnxCompatible().IsInterface)
            {
                var name = GetFullName(clrType);
                if (clrType.GetDnxCompatible().IsGenericType)
                {
                    name = GetFullName(clrType).Remove(GetFullName(clrType).IndexOf('`')) + "<";
                    var count = 0;
                    foreach (var genericArgument in clrType.GetDnxCompatible().GetGenericArguments())
                    {
                        if (count++ != 0) name += ", ";
                        name += GetTypeScriptName(genericArgument);
                    }
                    name += ">";
                }
                _fluent.ModelBuilder.Add(clrType);
                return name;
            }

            Console.WriteLine("WARNING: Unknown conversion for type: " + GetFullName(clrType));
            return "any";
        }

        public string PrettyClrTypeName(Type t)
        {
            if (!t.GetDnxCompatible().IsGenericType)
                return t.FullName;

            return t.Namespace + "." + string.Format(
                "{0}<{1}>",
                t.Name.Substring(0, t.Name.LastIndexOf("`", StringComparison.Ordinal)),
                string.Join(", ", t.GetDnxCompatible().GetGenericArguments().Select(PrettyClrTypeName)));
        }
    }
}
