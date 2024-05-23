using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Input;

[ExportTsEnum]
internal enum RequestKeyEnum
{
    None,
    CumulativeStats,
    PlayArea,
    ApplicationInfo,
    DeviceIds,
    DeviceProperty,
    InputAnalog,
    InputPose,
    Setting,
    RemoteSetting,
    FindOverlay,
    MoveSpace
}