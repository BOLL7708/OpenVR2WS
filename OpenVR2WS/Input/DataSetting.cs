using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Input;

[ExportTsInterface]
internal class DataSetting
{
    public string Section = "";
    public string Setting = "";
    public Output.TypeEnum Type = Output.TypeEnum.None;
}