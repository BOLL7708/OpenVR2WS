using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Input;

[ExportTsInterface]
internal class InputDataRemoteSetting
{
    public string Section = "";
    public string Setting = "";
    public string Value = "";
    public InputEnumValueType Type = InputEnumValueType.None;
}