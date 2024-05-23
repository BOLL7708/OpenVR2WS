using System;
using System.Text.Json;
using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Input;

[ExportTsInterface]
internal class Request
{
    public RequestKeyEnum Key = RequestKeyEnum.None;
    public JsonElement? Data;
    public string? Password = null;
    public string? Nonce = null;
}