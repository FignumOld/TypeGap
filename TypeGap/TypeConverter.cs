using TypeGap.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TypeLite;

namespace TypeGap
{
    public class TypeConverter
    {
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

        public static bool IsComplexType(Type clrType)
        {
            return !_cache.ContainsKey(clrType);
        }

        public static string GetTypeScriptName(Type clrType, TypeScriptFluent fluent)
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

            // Dictionaries -- these should come before IEnumerables, because they also implement IEnumerable
            if (clrType.IsIDictionary())
            {
                return $"{{ [key: {GetTypeScriptName(clrType.GetGenericArguments()[0], fluent)}]: {GetTypeScriptName(clrType.GetGenericArguments()[1], fluent)} }}";
            }

            if (clrType.IsArray)
            {
                return GetTypeScriptName(clrType.GetElementType(), fluent) + "[]";
            }

            if (typeof(IEnumerable).IsAssignableFrom(clrType))
            {
                if (clrType.IsGenericType)
                {
                    return GetTypeScriptName(clrType.GetGenericArguments()[0], fluent) + "[]";
                }
                return "any[]";
            }

            if (clrType.Namespace.StartsWith("System."))
                return "any";

            if (clrType.IsEnum)
            {
                fluent.ModelBuilder.Add(clrType);
                return clrType.FullName;
            }

            if (clrType.IsClass || clrType.IsInterface)
            {
                var name = clrType.FullName;
                if (clrType.IsGenericType)
                {
                    name = clrType.FullName.Remove(clrType.FullName.IndexOf('`')) + "<";
                    var count = 0;
                    foreach (var genericArgument in clrType.GetGenericArguments())
                    {
                        if (count++ != 0) name += ", ";
                        name += GetTypeScriptName(genericArgument, fluent);
                    }
                    name += ">";
                }
                fluent.ModelBuilder.Add(clrType);
                return name;
            }

            Console.WriteLine("WARNING: Unknown conversion for type: " + clrType.FullName);
            return "any";
        }
    }
}
