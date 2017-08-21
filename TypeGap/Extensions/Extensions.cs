using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TypeGap.Extensions
{
    internal static class ReflectionExtensions
    {
        public static bool IsNullable(this Type type)
        {
            if (type.GetDnxCompatible().IsGenericType)
                return type.GetGenericTypeDefinition() == typeof(Nullable<>);
            return false;
        }

        public static bool IsGenericTask(this Type type)
        {
            if (type.GetDnxCompatible().IsGenericType)
                return type.GetGenericTypeDefinition() == typeof(Task<>);
            return false;
        }

        public static bool IsIDictionary(this Type type)
        {
            return
                type.GetDnxCompatible().GetInterfaces().Any(t => t.GetDnxCompatible().IsGenericType && t.GetGenericTypeDefinition() == typeof(IDictionary<,>)) ||
                type.GetDnxCompatible().GetInterfaces().Any(t => t == typeof(IDictionary));
        }

        public static Type GetUnderlyingNullableType(this Type type)
        {
            if (!type.GetDnxCompatible().IsGenericType)
                return type;

            return type.GetDnxCompatible().GetGenericArguments().Single();
        }

        public static Type GetUnderlyingTaskType(this Type type)
        {
            return type.GetDnxCompatible().GetGenericArguments().Single();
        }

        public static string ToCamelCase(this string value)
        {
            return value.Substring(0, 1).ToLower() + value.Substring(1);
        }

#if NETSTANDARD1_6
        public static TypeInfo GetDnxCompatible(this Type t)
        {
            return t.GetTypeInfo();
        }
#else
        public static Type GetDnxCompatible(this Type t)
        {
            return t;
        }
#endif
    }
}
