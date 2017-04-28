using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeGap.WebApi
{
    internal class WebApiIntegration : Integration
    {
        public bool WithAjaxServices { get; set; }

        public override void WriteServices(TypeConverter converter, IndentedTextWriter writer)
        {
            var gen = new WebApiGenerator(converter);
            gen.WriteServices(Types.ToArray(), writer, WithAjaxServices);
        }
    }
}
