using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Input;

[ExportTsClass]
internal class DataSetting
{
    public string Section = "";
    public string Setting = "";
    public Output.TypeEnum Type = Output.TypeEnum.None;
}