using TypeGen.Core.TypeAnnotations;
using Valve.VR;

namespace OpenVR2WS.Input;

[ExportTsInterface]
internal class InputDataDeviceProperty
{
    public int DeviceIndex = -1;
    public ETrackedDeviceProperty Property = ETrackedDeviceProperty.Prop_Invalid;

    public static InputDataDeviceProperty CreateFromEvent(
        VREvent_t data
    )
    {
        var output = new InputDataDeviceProperty
        {
            DeviceIndex = (int)data.trackedDeviceIndex,
            Property = data.data.property.prop
        };
        return output;
    }
}