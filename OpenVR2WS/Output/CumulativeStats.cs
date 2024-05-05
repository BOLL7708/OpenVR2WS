using Valve.VR;

namespace OpenVR2WS.Output
{
    class CumulativeStats 
    {
        public double systemTimeMs;     // Time when we fetched the statistics
        public long
            framesPresented,    // Total frames presented, includes reprojected
            framesDropped,      // Total frames reepeated without reprojection
            framesReprojected,  // Total frames repeated with reprojection
            framesLoading,      // Total frames presented during loading
            framesTimedOut;     // Total frames that timeout, drops to compositor (2.5 repeated frames)

        public CumulativeStats(Compositor_CumulativeStats stats = new Compositor_CumulativeStats())
        {
            Update(stats);
        }
        public void Update(Compositor_CumulativeStats stats)
        {
            systemTimeMs = Utils.NowMs();
            framesPresented = stats.m_nNumFramePresents;
            framesDropped = stats.m_nNumDroppedFrames;
            framesReprojected = stats.m_nNumReprojectedFrames;
            framesLoading = stats.m_nNumLoading;
            framesTimedOut = stats.m_nNumTimedOut;
        }
    }
}
