using Valve.VR;

namespace OpenVR2WS.Input;

internal class DataDeviceProperty
{
    public int DeviceId = -1;
    public ETrackedDeviceProperty Property = ETrackedDeviceProperty.Prop_Invalid;

    public static DataDeviceProperty CreateFromEvent(
        VREvent_t data
    )
    {
        var output = new DataDeviceProperty
        {
            DeviceId = (int)data.trackedDeviceIndex,
            Property = data.data.property.prop
        };
        return output;
    }
}