using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TypeLite.Extensions;

namespace TypeLite.TsModels {
    /// <summary>
    /// Represents a type in the code model.
    /// </summary>
    [DebuggerDisplay("TsType - Type: {Type}")]
    public class TsType {
        /// <summary>
        /// Gets the CLR type represented by this instance of the TsType.
        /// </summary>
        public Type Type { get; private set; }

        /// <summary>
        /// Initializes a new instance of the TsType class with the specific CLR type.
        /// </summary>
        /// <param name="type">The CLR type represented by this instance of the TsType.</param>
        public TsType(Type type) {
            if (type.IsNullable()) {
                type = type.GetNullableValueType();
            }

            this.Type = type;
        }

        /// <summary>
        /// Represents the TsType for the object CLR type.
        /// </summary>
        public static readonly TsType Any = new TsType(typeof(object));     

        /// <summary>
        /// Returns true if this property is collection
        /// </summary>
        /// <returns></returns>
        public bool IsCollection() {
            return GetTypeFamily(this.Type) == TsTypeFamily.Collection;
        }



        /// <summary>
        /// Gets TsTypeFamily of the CLR type.
        /// </summary>
        /// <param name="type">The CLR type to get TsTypeFamily of</param>
        /// <returns>TsTypeFamily of the CLR type</returns>
        internal static TsTypeFamily GetTypeFamily(System.Type type) {
            if (type.IsNullable()) {
                return TsType.GetTypeFamily(type.GetNullableValueType());
            }

            var isString = (type == typeof(string));
            var isEnumerable = typeof(IEnumerable).GetDnxCompatible().IsAssignableFrom(type);

            // surprisingly  Decimal isn't a primitive type
            if (isString || type.GetDnxCompatible().IsPrimitive || type.FullName == "System.Decimal" || type.FullName == "System.DateTime" || type.FullName == "System.DateTimeOffset" || type.FullName == "System.SByte") {
                return TsTypeFamily.System;
            } else if (isEnumerable) {
                return TsTypeFamily.Collection;
            }

            if (type.GetDnxCompatible().IsEnum) {
                return TsTypeFamily.Enum;
            }

            if ((type.GetDnxCompatible().IsClass && type.FullName != "System.Object") || type.GetDnxCompatible().IsValueType /* structures */ || type.GetDnxCompatible().IsInterface) {
                return TsTypeFamily.Class;
            }

            return TsTypeFamily.Type;
        }

        /// <summary>
        /// Factory method so that the correct TsType can be created for a given CLR type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static TsType Create(System.Type type) {
            var family = GetTypeFamily(type);
            switch (family) {
                case TsTypeFamily.System:
                    return new TsSystemType(type);
                case TsTypeFamily.Collection:
                    return new TsCollection(type);
                case TsTypeFamily.Class:
                    return new TsClass(type);
                case TsTypeFamily.Enum:
                    return new TsEnum(type);
                default:
                    return new TsType(type);
            }
        }

        /// <summary>
        /// Gets type of items in generic version of IEnumerable.
        /// </summary>
        /// <param name="type">The IEnumerable type to get items type from</param>
        /// <returns>The type of items in the generic IEnumerable or null if the type doesn't implement the generic version of IEnumerable.</returns>
        internal static Type GetEnumerableType(Type type) {
            if (type.GetDnxCompatible().IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) {
                return type.GetDnxCompatible().GetGenericArguments()[0];
            }

            foreach (Type intType in type.GetDnxCompatible().GetInterfaces()) {
                if (intType.GetDnxCompatible().IsGenericType && intType.GetGenericTypeDefinition() == typeof(IEnumerable<>)) {
                    return intType.GetDnxCompatible().GetGenericArguments()[0];
                }
            }
            return null;
        }
    }
}
