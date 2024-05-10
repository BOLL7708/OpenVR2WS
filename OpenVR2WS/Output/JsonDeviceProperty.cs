namespace OpenVR2WS.Output;

public class JsonDeviceProperty(int deviceIndex, string propertyName, dynamic? propertyValue, string dataType)
{
    public int DeviceIndex = deviceIndex;
    public string PropertyName = propertyName;
    public dynamic? PropertyValue = propertyValue;
    public string DataType = dataType;
}