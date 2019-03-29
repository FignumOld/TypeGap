using System;
using System.Reflection;

namespace TypeGap
{
    public class MomentDateTimeInitializer : BasicTypeInitializer
    {
        public override bool CanConvertType(Type t)
        {
            return typeof(DateTime).GetTypeInfo().IsAssignableFrom(t) || typeof(DateTimeOffset).GetTypeInfo().IsAssignableFrom(t);
        }

        public override string ToTsType(string objectName)
        {
            return $"moment.utc({objectName})";
        }

        public override string FromTsType(string objectName)
        {
            return $"moment({objectName}).toISOString()";
        }
    }
}
