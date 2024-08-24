using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Output;

[ExportTsInterface]
internal class OutputDataApplicationInfo(string appId, double sessionStart)
{
    public string AppId = appId;
    public double SessionStart = sessionStart;
}