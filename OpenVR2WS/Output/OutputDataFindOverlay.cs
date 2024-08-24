using OpenVR2WS.Input;
using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Output;

[ExportTsInterface]
internal class OutputDataFindOverlay
{
    public ulong Handle;
    public string Key;

    public OutputDataFindOverlay(
        InputDataFindOverlay data,
        ulong handle
    )
    {
        Handle = handle;
        Key = data.OverlayKey;
    }
}