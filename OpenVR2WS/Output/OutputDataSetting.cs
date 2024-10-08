using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Output;

[ExportTsInterface]
internal class OutputDataSetting(
    string section,
    string setting,
    dynamic? value
)
{
    public string Section = section;
    public string Setting = setting;
    public dynamic? Value = value;
}