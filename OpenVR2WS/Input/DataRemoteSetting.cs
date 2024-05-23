using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Input;

[ExportTsInterface]
internal class DataRemoteSetting
{
    public string Section = "";
    public string Setting = "";
    public string Value = "";
    public TypeEnum Type = TypeEnum.None;
}