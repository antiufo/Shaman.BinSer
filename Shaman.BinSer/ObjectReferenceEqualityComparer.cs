using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shaman.Runtime.Serialization
{
    class ObjectReferenceEqualityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y)
        {
            if (x != null && x.GetType() == typeof(string))
            {
                return (string)x == y as string;
            }
            return object.ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            if (obj.GetType() == typeof(string)) return obj.GetHashCode();
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

    }
}
