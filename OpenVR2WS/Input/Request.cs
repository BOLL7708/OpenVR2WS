using System;
using System.Text.Json;

namespace OpenVR2WS.Input;

internal class Request
{
    public RequestKeyEnum Key = RequestKeyEnum.None;
    public JsonElement? Data;
    public string? Password = null;
    public string? Nonce = null;
}