using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Shaman.Runtime.Serialization
{
    internal static class Compatibility
    {
        public static bool IsMono = typeof(string).GetTypeInfo().Assembly.GetType("Mono.Runtime", false, false) != null;
    }
}
