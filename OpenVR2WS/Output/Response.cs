using OpenVR2WS.Input;
using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Output;

[ExportTsInterface]
internal class Response
{
    public ResponseTypeEnum Type = ResponseTypeEnum.Undefined;
    public RequestKeyEnum Key = RequestKeyEnum.None;
    public string Message = "";
    public dynamic? Data = null;
    public string? Nonce = null;

    public static Response CreateError(string message, dynamic? shape = null, string? nonce = null)
    {
        return new Response
        {
            Type = ResponseTypeEnum.Error,
            Message = message,
            Data = shape,
            Nonce = nonce
        };
    }

    public static Response CreateMessage(string message, string? nonce = null)
    {
        return new Response
        {
            Type = ResponseTypeEnum.Message,
            Message = message,
            Nonce = nonce
        };
    }

    public static Response CreateCommand(RequestKeyEnum requestKey, dynamic data, string? nonce = null)
    {
        return new Response
        {
            Type = ResponseTypeEnum.Result,
            Key = requestKey,
            Data = data,
            Nonce = nonce
        };
    }

    public static Response CreateVREvent(dynamic data)
    {
        return new Response
        {
            Type = ResponseTypeEnum.VREvent,
            Data = data
        };
    }

    public static Response Create(ResponseTypeEnum type, dynamic data, string? nonce = null)
    {
        return new Response
        {
            Type = type,
            Data = data,
            Nonce = nonce
        };
    }
}