using System.Data;
using TypeGen.Core.TypeAnnotations;
using Valve.VR;

namespace OpenVR2WS.Output;

[ExportTsInterface]
internal class OutputDataSkeletonSummary
{
    public float FingerCurlThumb = 0f;
    public float FingerCurlIndex = 0f;
    public float FingerCurlMiddle = 0f;
    public float FingerCurlRing = 0f;
    public float FingerCurlPinky = 0f;
    public float FingerSplayThumbIndex = 0f;
    public float FingerSplayIndexMiddle = 0f;
    public float FingerSplayMiddleRing = 0f;
    public float FingerSplayRingPinky = 0f;

    public OutputDataSkeletonSummary(VRSkeletalSummaryData_t skeletonData)
    {
        Update(skeletonData);
    }
    
    public void Update(VRSkeletalSummaryData_t data)
    {
        FingerCurlThumb = data.flFingerCurl0;
        FingerCurlIndex = data.flFingerCurl1;
        FingerCurlMiddle = data.flFingerCurl2;
        FingerCurlRing = data.flFingerCurl3;
        FingerCurlPinky = data.flFingerCurl4;
        FingerSplayThumbIndex = data.flFingerSplay0;
        FingerSplayIndexMiddle = data.flFingerSplay1;
        FingerSplayMiddleRing = data.flFingerSplay2;
        FingerSplayRingPinky = data.flFingerSplay3;
    }
}