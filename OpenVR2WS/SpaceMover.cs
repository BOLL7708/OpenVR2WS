using System;
using System.Linq;
using System.Threading;
using EasyOpenVR;
using EasyOpenVR.Extensions;
using EasyOpenVR.Utils;
using OpenVR2WS.Input;
using Valve.VR;

namespace OpenVR2WS;

public static class SpaceMover
{
    public static void MoveSpace(DataMoveSpace data)
    {
        var thread = new Thread(() => AnimationWorker(data));
        if (!thread.IsAlive) thread.Start();
    }
    
    private static void AnimationWorker(DataMoveSpace data)
    {
        /*
         * 1. Allow animations to run in parallel, with different durations and delays.
         * 2. Every animation should have the following properties:
         *  - Duration, ms that the animation should run over
         *  - Delay, ms before starting the animation.
         *  - Repeat, will cycle the animation this many times.
         *  - PingPong, will make the value go in the other direction after done. 
         *  - Easing type, e.g. linear, sine, quad, cubic, quart, quint, expo, circ, back, elastic, bounce
         *  - Easing mode, e.g. in, out, inOut
         *  - Offset, the actual point in space the VR space should move to.
         * 3. The whole sequence should have a few settings too.
         *  - Reset space after animation
         */
        var vr = EasyOpenVRSingleton.Instance;
        if (data.ResetBeforeRun) vr.ResetUniverse();
        
        var move = data.Moves.First();
        HmdVector3_t offset = move.GetOffset();
        EasingUtils.EasingType easingType = move.EasingType;
        EasingUtils.EasingMode easingMode = move.EasingMode;
        int duration = move.DurationMs;
        
        Thread.CurrentThread.IsBackground = true;
        var hz = vr.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float);
        var msPerFrame = 1000 / hz;
        var frameCount = (int)Math.Ceiling(duration / msPerFrame);
        var standingPose = new HmdMatrix34_t();
        OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref standingPose);
        var sittingPose = new HmdMatrix34_t();
        OpenVR.ChaperoneSetup.GetWorkingSeatedZeroPoseToRawTrackingPose(ref sittingPose);
        OpenVR.ChaperoneSetup.GetWorkingCollisionBoundsInfo(out var physQuad);

        var timeLoopStarted = 0L;
        var timeSpent = 0.0;
        var value = 0.0;
        var easeFunc = EasingUtils.Get(easingType, easingMode);
        var easeValue = 0.0;
        for (var i = 0; i < frameCount; i++)
        {
            timeLoopStarted = DateTime.Now.Ticks;
            value = (double)i / frameCount;
            easeValue = easeFunc(value);
            vr.TranslateUniverse(offset.Multiply((float)easeValue), standingPose, sittingPose, physQuad);
            timeSpent = (double)(DateTime.Now.Ticks - timeLoopStarted) / TimeSpan.TicksPerMillisecond;
            Thread.Sleep((int)Math.Round(Math.Max(1.0, msPerFrame - timeSpent))); // Animation time per frame adjusted by the time it took to animate.
        }

        if (data.ResetAfterRun) vr.ResetUniverse();
    }
}