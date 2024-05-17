using EasyOpenVR.Utils;
using Valve.VR;

namespace OpenVR2WS.Input;

public class DataMoveSpace
{
    public bool ResetBeforeRun = false;
    public bool ResetAfterRun = false;
    public DataMoveSpaceEntry[] Entries = [];

    public static DataMoveSpace BuildEmpty(bool withChild = false)
    {
        return withChild
            ? new DataMoveSpace { Entries = [new DataMoveSpaceEntry()] }
            : new DataMoveSpace();
    }
}

public class DataMoveSpaceEntry
{
    public EasingUtils.EasingType EasingType = EasingUtils.EasingType.Linear;
    public EasingUtils.EasingMode EasingMode = EasingUtils.EasingMode.In;
    public float OffsetX = 0;
    public float OffsetY = 0;
    public float OffsetZ = 0;
    public int DurationMs = 0;
    public int DelayMs = 0;
    public bool PingPong = false;
    public int Repeat = 0;

    public HmdVector3_t GetOffset()
    {
        return new HmdVector3_t
        {
            v0 = OffsetX,
            v1 = OffsetY,
            v2 = OffsetZ
        };
    }
}