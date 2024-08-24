using System;
using System.ComponentModel;
using System.Text.Json;
using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Input;

[ExportTsInterface]
internal class InputMessage
{
    public InputMessageEnumKey Key = InputMessageEnumKey.None;
    public dynamic? Data;
    public string? Password = null;
    public string? Nonce = null;
}