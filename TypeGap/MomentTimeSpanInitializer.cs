using System;
using System.Reflection;

namespace TypeGap
{
    public class MomentTimeSpanInitializer : BasicTypeInitializer
    {
        public override bool CanConvertType(Type t)
        {
            return typeof(TimeSpan).GetTypeInfo().IsAssignableFrom(t);
        }

        public override string ToTsType(string objectName)
        {
            return $"moment.utc({objectName}, \"HH:mm:ss\")";
        }

        public override string FromTsType(string objectName)
        {
            return $"moment({objectName}).format(\"HH:mm:ss\")";
        }
    }
}
