using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TypeGap.Extensions;

namespace TypeGap.Util
{
    internal class ObjectToDescriptionConverter
    {
        public static ApiControllerDesc Convert(Type type)
        {
            var typeInfo = type.GetDnxCompatible();

            var dec = new ApiControllerDesc();

            dec.ControllerName = type.Name;

            var routeAttr = GetAttributes(type, "Route").FirstOrDefault();
            if (routeAttr != null)
                dec.RouteTemplate = TryGetBestPrivateMember(routeAttr, "Template", "Name").ToString();

            var methods = typeInfo.GetMethods()
               .Where(m => m.IsPublic)
               .Where(m => !m.IsSpecialName)
               .Where(m => m.DeclaringType == type)
               .OrderBy(m => m.Name)
               .ToArray();

            foreach (var m in methods)
            {
                // [NonAction]
                if (GetAttributes(m, "NonAction").Length > 0)
                    continue;

                // [HttpPut, HttpGet, etc..]
                var method = ApiMethod.Get;
                if (GetAttributes(m, "HttpPost").Length > 0)
                    method = ApiMethod.Post;
                else if (GetAttributes(m, "HttpDelete").Length > 0)
                    method = ApiMethod.Delete;
                else if (GetAttributes(m, "HttpPut").Length > 0)
                    method = ApiMethod.Put;
                else if (GetAttributes(m, "HttpPatch").Length > 0)
                    method = ApiMethod.Patch;

                var action = dec.AddAction(m.Name, m.ReturnType, method);

                // [RouteAttribute]
                var actionRouteAttr = GetAttributes(m, "Route").FirstOrDefault();
                if (actionRouteAttr != null)
                    action.RouteTemplate = TryGetBestPrivateMember(actionRouteAttr, "Template", "Name").ToString();

                // [ProducesResponseTypeAttribute]
                var responses = GetAttributes(type, "ProducesResponseType").Select(p => new
                {
                    Type = (Type)TryGetBestPrivateMember(p, "Type"),
                    StatusCode = (int)TryGetBestPrivateMember(p, "StatusCode"),
                });
                var bestResponse = responses.OrderBy(r => r.StatusCode).FirstOrDefault(r => r.StatusCode.ToString().StartsWith("2"));
                if (bestResponse != null)
                {
                    action.ReturnType = bestResponse.Type;
                }

                var parameters = m.GetParameters();
                foreach (var pmInfo in parameters)
                {
                    var p = new ApiParamDesc();
                    p.IsOptional = pmInfo.IsOptional;
                    p.ParameterName = pmInfo.Name;
                    p.ParameterType = pmInfo.ParameterType;
                    if (GetAttributes(pmInfo, "FromUri").Length > 0)
                        p.Mode = ApiParameterMode.FromUri;
                    else if (GetAttributes(pmInfo, "FromBody").Length > 0)
                        p.Mode = ApiParameterMode.FromBody;

                    action.Parameters.Add(p);
                }
            }

            return dec;
        }


        private static object[] GetAttributes(Type type, string attrName)
        {
            var typeInfo = type.GetDnxCompatible();
            return typeInfo.GetCustomAttributes()
                .Select(a => (object)a)
                .Where(a => a.GetType().Name.EndsWith(attrName) || a.GetType().Name.EndsWith(attrName + "Attribute"))
                .ToArray();
        }

        private static object[] GetAttributes(ICustomAttributeProvider typeInfo, string attrName)
        {
            return typeInfo.GetCustomAttributes(false)
                .Where(a => a.GetType().Name.EndsWith(attrName) || a.GetType().Name.EndsWith(attrName + "Attribute"))
                .ToArray();
        }

        private static object TryGetBestPrivateMember(object obj, params string[] names)
        {
            var type = obj.GetType();
            var typeInfo = type.GetDnxCompatible();
            var members = typeInfo.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            foreach (var n in names)
            {
                var member = members.FirstOrDefault(me => me.Name == n);
                if (member != null)
                {
                    //if (member is FieldInfo field)
                    //{
                    //    return field.GetValue(obj);
                    //}
                    if (member is PropertyInfo property)
                    {
                        return property.GetValue(obj);
                    }
                }
            }

            return null;
        }
    }
}
