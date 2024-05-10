using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenVR2WS;

internal class JsonOptions
{
    private static readonly JsonSerializerOptions Instance = new() { IncludeFields = true };
    private static bool _initDone;

    internal static JsonSerializerOptions get()
    {
        if (!_initDone)
        {
            Instance.Converters.Add(new JsonStringEnumConverter());
            // TODO: I suspect the below was only needed when the data was faulty, but not entirely sure.
            // Instance.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
            _initDone = true;
        }

        return Instance;
    }
}