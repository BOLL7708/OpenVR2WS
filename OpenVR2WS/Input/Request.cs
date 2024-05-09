using System;
using System.Text.Json;

namespace OpenVR2WS.Input;

internal class Request
{
    public CommandEnum Command = CommandEnum.None;
    public JsonElement? Data;
    public string? Nonce = null;
}