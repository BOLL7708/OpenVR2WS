namespace OpenVR2WS.Output;

internal class JsonSetting(
    string section,
    string setting,
    dynamic? value
)
{
    public string Section = section;
    public string Setting = setting;
    public dynamic? Value = value;
}