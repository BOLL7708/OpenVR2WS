using OpenVR2WS.Input;

namespace OpenVR2WS.Output;

// ReSharper disable InconsistentNaming
internal class Response
{
    public ResponseEnum Type = ResponseEnum.Undefined;
    public CommandEnum Command = CommandEnum.None;
    public string Message = "";
    public dynamic? Data = null;

    public static Response CreateError(string message)
    {
        return new Response
        {
            Type = ResponseEnum.Error,
            Message = message
        };
    }

    public static Response CreateMessage(string message)
    {
        return new Response
        {
            Type = ResponseEnum.Message,
            Message = message
        };
    }

    public static Response CreateCommand(CommandEnum command, dynamic data)
    {
        return new Response
        {
            Type = ResponseEnum.Command,
            Command = command,
            Data = data
        };
    }

    public static Response CreateVREvent(dynamic data)
    {
        return new Response
        {
            Type = ResponseEnum.VREvent,
            Data = data
        };
    }

    public static Response CreateInput(dynamic data)
    {
        return new Response
        {
            Type = ResponseEnum.Input,
            Data = data
        };
    }
}