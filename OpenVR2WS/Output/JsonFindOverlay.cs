using OpenVR2WS.Input;
using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Output;

[ExportTsInterface]
internal class JsonFindOverlay
{
    public ulong Handle;
    public string Key;

    public JsonFindOverlay(
        DataFindOverlay data,
        ulong handle
    )
    {
        Handle = handle;
        Key = data.OverlayKey;
    }
}