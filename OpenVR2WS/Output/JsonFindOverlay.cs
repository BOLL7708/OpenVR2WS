using OpenVR2WS.Input;

namespace OpenVR2WS.Output;

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