using System.Collections.Generic;
using EasyOpenVR;
using Valve.VR;

namespace OpenVR2WS.Output;

public class JsonDeviceIds(Dictionary<ETrackedDeviceClass, HashSet<uint>> deviceToIndex, Dictionary<EasyOpenVRSingleton.InputSource, int> sourceToIndex)
{
    public Dictionary<ETrackedDeviceClass, HashSet<uint>> DeviceToIndex = deviceToIndex;
    public Dictionary<EasyOpenVRSingleton.InputSource, int> SourceToIndex = sourceToIndex;
}