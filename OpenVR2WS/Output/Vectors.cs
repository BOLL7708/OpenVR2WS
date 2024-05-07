using Valve.VR;

namespace OpenVR2WS.Output;

// ReSharper disable InconsistentNaming
internal class Vec2
{
    public double x = 0;
    public double y = 0;
}

internal class Vec3
{
    public Vec3(HmdVector3_t point = new HmdVector3_t())
    {
        x = point.v0;
        y = point.v1;
        z = point.v2;
    }

    public double x = 0;
    public double y = 0;
    public double z = 0;
}