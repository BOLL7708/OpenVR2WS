using EasyOpenVR.Utils;
using TypeGen.Core.TypeAnnotations;
using Valve.VR;

namespace OpenVR2WS.Input;

[ExportTsInterface]
public class InputDataMoveSpace
{
    public int DurationMs = 0;
    public EasingUtils.EasingType EaseInType = EasingUtils.EasingType.Linear;
    public EasingUtils.EasingMode EaseInMode = EasingUtils.EasingMode.Out;
    public int EaseInMs = 0;
    public EasingUtils.EasingType EaseOutType = EasingUtils.EasingType.Linear;
    public EasingUtils.EasingMode EaseOutMode = EasingUtils.EasingMode.Out;
    public int EaseOutMs = 0;
    public bool ResetSpaceBeforeRun = false;
    public bool ResetOffsetAfterRun = false;
    public Correction Correction = Correction.PlaySpace;
    public DataMoveSpaceEntry[] Entries = [];

    public static InputDataMoveSpace BuildEmpty(bool withChild = false)
    {
        return withChild
            ? new InputDataMoveSpace { Entries = [new DataMoveSpaceEntry()] }
            : new InputDataMoveSpace();
    }
}

public enum Correction
{
    PlaySpace,
    Hmd,
    HmdYaw,
    HmdPitch
}

[ExportTsInterface]
public class DataMoveSpaceEntry
{
    public EasingUtils.EasingType EaseType = EasingUtils.EasingType.Linear;
    public EasingUtils.EasingMode EaseMode = EasingUtils.EasingMode.In;
    public float OffsetX = 0;
    public float OffsetY = 0;
    public float OffsetZ = 0;
    public float Rotate = 0;
    public int StartOffsetMs = 0;
    public int EndOffsetMs = 0;
    public bool PingPong = false;
    public int Repeat = 0;
    public bool Accumulate = false;

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