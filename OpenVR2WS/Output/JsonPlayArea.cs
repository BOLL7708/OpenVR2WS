using Valve.VR;

namespace OpenVR2WS.Output;

internal class JsonPlayArea
{
    public Vec3[] Corners = new Vec3[8];
    public Vec3 Size = new Vec3();

    public JsonPlayArea(HmdQuad_t rect = new HmdQuad_t(), HmdVector2_t size = new HmdVector2_t(), float height = 0)
    {
        Update(rect, size, height);
    }

    public void Update(HmdQuad_t rect, HmdVector2_t newSize, float height)
    {
        Corners[0] = new Vec3(rect.vCorners0);
        Corners[1] = new Vec3(rect.vCorners1);
        Corners[2] = new Vec3(rect.vCorners2);
        Corners[3] = new Vec3(rect.vCorners3);

        Corners[4] = new Vec3(rect.vCorners0);
        Corners[5] = new Vec3(rect.vCorners1);
        Corners[6] = new Vec3(rect.vCorners2);
        Corners[7] = new Vec3(rect.vCorners3);

        Corners[4].Y = height;
        Corners[5].Y = height;
        Corners[6].Y = height;
        Corners[7].Y = height;

        Size.X = newSize.v0;
        Size.Y = height;
        Size.Z = newSize.v1;
    }
}