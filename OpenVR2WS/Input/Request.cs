using System;
using System.ComponentModel;
using System.Text.Json;
using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Input;

[ExportTsClass]
internal class Request
{
    public RequestKeyEnum Key = RequestKeyEnum.None;
    public dynamic? Data;
    public string? Password = null;
    public string? Nonce = null;
}