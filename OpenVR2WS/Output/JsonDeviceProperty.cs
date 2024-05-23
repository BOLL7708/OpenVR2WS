using TypeGen.Core.TypeAnnotations;

namespace OpenVR2WS.Output;

[ExportTsInterface]
public class JsonDeviceProperty(int deviceIndex, string propertyName, dynamic? propertyValue, string dataType)
{
    public int DeviceIndex = deviceIndex;
    public string PropertyName = propertyName;
    public dynamic? PropertyValue = propertyValue;
    public string DataType = dataType;
}