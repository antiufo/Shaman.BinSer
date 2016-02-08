#if CORECLR
using Shaman;
using Shaman.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace System
{
    internal class AppDomain
    {
        static AppDomain()
        {
            var mscorlib = typeof(string).GetTypeInfo().Assembly;
            AppDomain_CurrentDomain = ReflectionHelper.GetWrapper<Func<object>>(mscorlib, "System.AppDomain", "get_CurrentDomain");
            AppDomain_GetAssemblies = ReflectionHelper.GetWrapper<Func<object, Assembly[]>>(mscorlib, "System.AppDomain", "GetAssemblies");
        }

        private AppDomain() { }

        public static AppDomain CurrentDomain { get; } = new AppDomain();


        private static Func<object, Assembly[]> AppDomain_GetAssemblies;
        private static Func<object> AppDomain_CurrentDomain;

        internal IEnumerable<Assembly> GetAssemblies()
        {
            return AppDomain_GetAssemblies(AppDomain_CurrentDomain());
        }
    }
}
#endif