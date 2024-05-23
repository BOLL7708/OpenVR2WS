using OpenVR2WS.Input;
using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Output;

[ExportTsEnum]
public enum ResponseTypeEnum
{
    Undefined,
    Error,
    Message,
    Result,
    VREvent,
    InputDigital
}