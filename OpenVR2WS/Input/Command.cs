using System;
using System.Text.Json;

namespace OpenVR2WS.Input;

internal class Command
{
    public CommandEnum Key = CommandEnum.None;
    public JsonElement? Data;
}