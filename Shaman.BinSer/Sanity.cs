using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Shaman.Runtime
{
    internal static class Sanity
    {
        public static Exception ShouldntHaveHappened()
        {
            throw new Exception("ShouldntHaveHappened");
        }

        public static void Assert(bool v)
        {
            if (!v) throw new Exception("Assert");
        }

        public static void AssertFastReadByte(Stream baseStream)
        {
        }

        public static void BreakIfAttached()
        {
            if (Debugger.IsAttached)
                Debugger.Break();
        }

        public static void ShouldntHaveHappenedButTryToContinue(Exception exception)
        {
            if (Debugger.IsAttached)
                Debugger.Break();
        }
    }
}
