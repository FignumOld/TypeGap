using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeGap.Extensions
{
    public static class StringExtensions
    {
        public static string ToCamelCase(this string value)
        {
            return value.Substring(0, 1).ToLower() + value.Substring(1);
        }
    }
}
