using TypeGen.Core.TypeAnnotations;
using Valve.VR;

namespace OpenVR2WS.Output;

[ExportTsInterface]
internal class OutputDataCumulativeStats
{
    public double SystemTimeMs; // Time when we fetched the statistics

    public long
        FramesPresented, // Total frames presented, includes reprojected
        FramesDropped, // Total frames repeated without reprojection
        FramesReprojected, // Total frames repeated with reprojection
        FramesLoading, // Total frames presented during loading
        FramesTimedOut; // Total frames that timeout, drops to compositor (2.5 repeated frames)

    public OutputDataCumulativeStats(Compositor_CumulativeStats stats = new Compositor_CumulativeStats())
    {
        Update(stats);
    }

    public void Update(Compositor_CumulativeStats stats)
    {
        SystemTimeMs = Utils.NowMs();
        FramesPresented = stats.m_nNumFramePresents;
        FramesDropped = stats.m_nNumDroppedFrames;
        FramesReprojected = stats.m_nNumReprojectedFrames;
        FramesLoading = stats.m_nNumLoading;
        FramesTimedOut = stats.m_nNumTimedOut;
    }
}