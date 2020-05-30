using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVR2WS
{
    static class Utils
    {
        public static double NowMs()
        {
            return DateTime.Now.Ticks / (double)TimeSpan.TicksPerMillisecond;
        }
    }
}
