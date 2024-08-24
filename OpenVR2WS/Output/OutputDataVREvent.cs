using System;
using System.Diagnostics;
using TypeGen.Core.TypeAnnotations;
using Valve.VR;

namespace OpenVR2WS.Output;

[ExportTsInterface]
internal class OutputDataVREvent
{
    public string TypeFull;
    public string Type;
    public OutputDataVREvent (EVREventType eventType)
    {
        TypeFull = string.Empty;
        try
        {
            var enumName = Enum.GetName(typeof(EVREventType), eventType);
            if (enumName != null)
            {
                TypeFull = enumName;
            }
        } catch (Exception e)
        {
            Debug.WriteLine($"Failed to get name of enum: {e.Message}");
        }
        Type = TypeFull.Replace("VREvent_", ""); 
    }
}