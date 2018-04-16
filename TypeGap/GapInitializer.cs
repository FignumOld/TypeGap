using System;
using System.Collections.Generic;
using System.Text;

namespace TypeGap
{
    public abstract class GapInitializer
    {
        public abstract bool CanConvertType(Type t);
        public abstract string ToTsType(string objectName);
        public abstract string FromTsType(string objectName);
    }
}
