using Shaman.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Shaman
{
#if REFLECTION_EXTENSIONS_INTERNAL
    internal
#else
    public
#endif
    static partial class ReflectionExtensions
    {

        
        public static Assembly Assembly(this Type type)
        {
            return type.GetTypeInfo().Assembly;
        }


        /// <summary>
        /// Determines if <paramref name="type"/> is a subclass of <paramref name="baseType"/>.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="baseType">The potential base class.</param>
        /// <returns>Whether the check is positive.</returns>
        public static bool Is(this Type type, Type baseType)
        {
            return baseType.IsAssignableFrom(type);
        }

        /// <summary>
        /// Determines if <paramref name="type"/> is a subclass of the specified type.
        /// </summary>
        /// <typeparam name="T">The potential base class.</typeparam>
        /// <param name="type">The type.</param>
        /// <returns>Whether the check is positive.</returns>
        public static bool Is<T>(this Type type)
        {
            return typeof(T).IsAssignableFrom(type);
        }

        [RestrictedAccess]
        public static bool IsInterface(this Type type)
        {
            return type.GetTypeInfo().IsInterface;
        }
        [RestrictedAccess]
        public static bool IsPrimitive(this Type type)
        {
            return type.GetTypeInfo().IsPrimitive;
        }
        [RestrictedAccess]
        public static Type BaseType(this Type type)
        {
            return type.GetTypeInfo().BaseType;
        }

        [RestrictedAccess]
        public static bool IsGenericType(this Type type)
        {
            return type.GetTypeInfo().IsGenericType;
        }
        [RestrictedAccess]
        public static bool IsValueType(this Type type)
        {
            return type.GetTypeInfo().IsValueType;
        }
        [RestrictedAccess]
        internal static Type[] GetGenericArguments(this Type type)
        {
            return type.GetTypeInfo().GenericTypeArguments;
        }
        [RestrictedAccess]
        internal static bool IsEnum(this Type type)
        {
            return type.GetTypeInfo().IsEnum;
        }
    }
}
