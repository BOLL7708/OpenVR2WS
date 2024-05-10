using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenVR2WS;

internal class JsonOptions
{
    private static readonly JsonSerializerOptions _instance = new() { IncludeFields = true };
    private static bool _initDone;

    internal static JsonSerializerOptions get()
    {
        if (!_initDone)
        {
            _instance.Converters.Add(new JsonStringEnumConverter());
            _initDone = true;
        }

        return _instance;
    }
}