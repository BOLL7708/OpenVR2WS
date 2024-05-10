using OpenVR2WS.Input;

namespace OpenVR2WS.Output;

// ReSharper disable InconsistentNaming
internal class Response
{
    public ResponseTypeEnum Type = ResponseTypeEnum.Undefined;
    public RequestKeyEnum Key = RequestKeyEnum.None;
    public string Message = "";
    public dynamic? Data = null;

    public static Response CreateError(string message, dynamic? shape = null)
    {
        return new Response
        {
            Type = ResponseTypeEnum.Error,
            Message = message,
            Data = shape
        };
    }

    public static Response CreateMessage(string message)
    {
        return new Response
        {
            Type = ResponseTypeEnum.Message,
            Message = message
        };
    }

    public static Response CreateCommand(RequestKeyEnum requestKey, dynamic data)
    {
        return new Response
        {
            Type = ResponseTypeEnum.Command,
            Key = requestKey,
            Data = data
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

    public static Response Create(ResponseTypeEnum type, dynamic data)
    {
        return new Response
        {
            Type = type,
            Data = data
        };
    }
}