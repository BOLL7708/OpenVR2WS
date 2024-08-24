using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Input;

[ExportTsInterface]
internal class InputDataSetting
{
    public string Section = "";
    public string Setting = "";
    public Output.OutputEnumType Type = Output.OutputEnumType.None;
}