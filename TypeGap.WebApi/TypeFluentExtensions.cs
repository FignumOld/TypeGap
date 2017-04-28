using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TypeGap.WebApi;

namespace TypeGap
{
    public static class TypeFluentExtensions
    {
        public static TypeFluent AddWebApi(this TypeFluent builder, Type t)
        {
            if (!typeof(System.Web.Http.ApiController).IsAssignableFrom(t))
                throw new ArgumentException("Type must be assignable to System.Web.Http.ApiController");

            builder.AddIntegrationItem<WebApiIntegration>(t);
            return builder;
        }

        public static TypeFluent AddWebApi<T>(this TypeFluent builder)
        {
            return AddWebApi(builder, typeof(T));
        }

        public static TypeFluent WithAjaxHelper(this TypeFluent builder, bool value = true)
        {
            var i = builder.GetIntegration<WebApiIntegration>();
            i.WithAjaxServices = value;
            return builder;
        }
    }
}
