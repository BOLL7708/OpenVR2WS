using OpenVR2WS.Input;
using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Output;

[ExportTsInterface]
internal class OutputMessage
{
    public OutputValueTypeEnum Type = OutputValueTypeEnum.Undefined;
    public InputMessageKeyEnum Key = InputMessageKeyEnum.None;
    public string Message = "";
    public dynamic? Data = null;
    public string? Nonce = null;

    public static OutputMessage CreateError(string message, dynamic? shape = null, string? nonce = null, InputMessageKeyEnum key = InputMessageKeyEnum.None)
    {
        return new OutputMessage
        {
            Type = OutputValueTypeEnum.Error,
            Key = key,
            Message = message,
            Data = shape,
            Nonce = nonce
        };
    }
    public static OutputMessage CreateError(string message, InputMessage inputMessage, dynamic? shape = null)
    {
        return CreateError(message, shape, inputMessage.Nonce, inputMessage.Key);
    }

    public static OutputMessage CreateMessage(string message, string? nonce = null, InputMessageKeyEnum key = InputMessageKeyEnum.None)
    {
        return new OutputMessage
        {
            Type = OutputValueTypeEnum.Message,
            Key = key,
            Message = message,
            Nonce = nonce
        };
    }
    public static OutputMessage CreateMessage(string message, InputMessage inputMessage)
    {
        return CreateMessage(message, inputMessage.Nonce, inputMessage.Key);
    }

    public static OutputMessage CreateCommand(InputMessageKeyEnum inputMessageKey, dynamic data, string? nonce = null)
    {
        return new OutputMessage
        {
            Type = OutputValueTypeEnum.Result,
            Key = inputMessageKey,
            Data = data,
            Nonce = nonce
        };
    }

    public static OutputMessage CreateVREvent(dynamic data)
    {
        return new OutputMessage
        {
            Type = OutputValueTypeEnum.VREvent,
            Data = data
        };
    }

    public static OutputMessage Create(OutputValueTypeEnum type, dynamic data, string? nonce = null)
    {
        return new OutputMessage
        {
            Type = type,
            Data = data,
            Nonce = nonce
        };
    }
}