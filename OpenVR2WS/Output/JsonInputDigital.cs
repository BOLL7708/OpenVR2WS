using EasyOpenVR;
using Valve.VR;

namespace OpenVR2WS.Output;

internal class JsonInputDigital(
    EasyOpenVRSingleton.InputSource source,
    InputDigitalActionData_t data,
    EasyOpenVRSingleton.InputActionInfo info
)
{
    public EasyOpenVRSingleton.InputSource Source = source;
    public string Input = info.pathEnd;
    public bool State = data.bState;
}