using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shaman.Runtime.Serialization
{
    public interface IHasBinSerCallbacks
    {
        void OnSerializing();
    }
}
