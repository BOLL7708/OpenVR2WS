namespace OpenVR2WS.Output;

internal class JsonApplicationInfo(string appId, double sessionStart)
{
    public string AppId = appId;
    public double SessionStart = sessionStart;
}