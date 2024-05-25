using TypeGen.Core.TypeAnnotations;
using Valve.VR;

namespace OpenVR2WS.Output;

[ExportTsInterface]
internal class Vec2
{
    public double X = 0;
    public double Y = 0;
}

[ExportTsInterface]
internal class Vec3
{
    public Vec3(HmdVector3_t point = new HmdVector3_t())
    {
        X = point.v0;
        Y = point.v1;
        Z = point.v2;
    }

    public double X = 0;
    public double Y = 0;
    public double Z = 0;
}