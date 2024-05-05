using System;

namespace OpenVR2WS;

static class Utils
{
    public static double NowMs()
    {
        return DateTime.Now.Ticks / (double)TimeSpan.TicksPerMillisecond;
    }

    public static double NowUTCMs()
    {
        return DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerMillisecond;
    }

    public static Int32 NowUnixUTC()
    {
        return (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
    }

    public static Int32 NowUnixMs()
    {
        return (Int32)(DateTime.Now.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
    }
}