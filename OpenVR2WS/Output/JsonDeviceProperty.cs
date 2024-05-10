namespace OpenVR2WS.Output;

public class JsonDeviceProperty(int deviceIndex, string propName, dynamic? propertyValue, string dataType)
{
    public int DeviceIndex = deviceIndex;
    public string PropName = propName;
    public dynamic? PropertyValue = propertyValue;
    public string DataType = dataType;
}