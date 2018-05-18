using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TypeGap
{
    public abstract class BasicTypeInitializer : AdvancedTypeInitializer
    {
        public abstract bool CanConvertType(Type t);
        public abstract string ToTsType(string objectName);
        public abstract string FromTsType(string objectName);

        public override string GetGroupNameIfCanConvert(Type t)
        {
            if (CanConvertType(t))
            {
                string guid;
                using (MD5 md5 = MD5.Create())
                    guid = string.Join(string.Empty, md5.ComputeHash(Encoding.UTF8.GetBytes(t.AssemblyQualifiedName)).Select(b => b.ToString("x2")));
                return guid;
            }
            return null;
        }

        public override string ToTsType(Type t, string objectName)
        {
            return $"return {ToTsType(objectName)};";
        }

        public override string FromTsType(Type t, string objectName)
        {
            return $"return {FromTsType(objectName)};";
        }
    }

    public abstract class AdvancedTypeInitializer
    {
        public abstract string GetGroupNameIfCanConvert(Type t);
        public abstract string ToTsType(Type t, string objectName);
        public abstract string FromTsType(Type t, string objectName);
    }
}
