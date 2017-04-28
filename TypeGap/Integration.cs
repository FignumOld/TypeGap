using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeGap
{
    internal abstract class Integration
    {
        public List<Type> Types { get; } = new List<Type>();

        public abstract void WriteServices(TypeConverter converter, IndentedTextWriter writer);
    }
}
