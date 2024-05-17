using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public static void MoveSpace(DataMoveSpace data, Action<string> callback)
    {
        var thread = new Thread(() => AnimationWorker(data, callback));
        if (!thread.IsAlive) thread.Start();
    }

    private static void AnimationWorker(DataMoveSpace data, Action<string> callback)
    {
        Thread.CurrentThread.IsBackground = true;
        var vr = EasyOpenVRSingleton.Instance;
        if (data.ResetBeforeRun)
        {
            vr.ResetUniverse();
            Thread.Sleep(1);
        }
        // Get animation rate
        var hz = vr.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float);
        var msPerFrame = 1000 / hz;

        // Generate data to loop over
        List<SpaceMoverEntry> entries = [];
        var maxFrameCount = 1;
        foreach (var entry in data.Entries)
        {
            var easeFunc = EasingUtils.Get(entry.EasingType, entry.EasingMode);
            var delay = (int)Math.Round(entry.DelayMs / msPerFrame);
            var duration = (int)Math.Round(entry.DurationMs / msPerFrame);
            maxFrameCount = Math.Max(maxFrameCount, delay + duration);
            entries.Add(new SpaceMoverEntry(entry.GetOffset(), easeFunc, entry, delay, duration));
        }

        // Fetch current setup
        var standingPose = new HmdMatrix34_t();
        OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref standingPose);
        var sittingPose = new HmdMatrix34_t();
        OpenVR.ChaperoneSetup.GetWorkingSeatedZeroPoseToRawTrackingPose(ref sittingPose);
        OpenVR.ChaperoneSetup.GetWorkingCollisionBoundsInfo(out var physQuad);

        // Loop the animation
        var timeLoopStarted = 0L;
        var timeSpent = 0.0;
        var value = 0.0;

        // Animation loop
        for (var currentFrame = 0; currentFrame < maxFrameCount; currentFrame++)
        {
            timeLoopStarted = DateTime.Now.Ticks;
            var offset = new HmdVector3_t();

            // Apply all offsets to the final offset and apply it to the play space
            foreach (var entry in entries)
            {
                value = AnimationProgress(maxFrameCount, currentFrame, entry);
                offset = offset.Add(
                    entry.Offset.Multiply(
                        (float)entry.EaseFunc(value)
                    )
                );
            }

            vr.TranslateUniverse(offset, standingPose, sittingPose, physQuad);

            timeSpent = (double)(DateTime.Now.Ticks - timeLoopStarted) / TimeSpan.TicksPerMillisecond;
            Thread.Sleep((int)Math.Round(Math.Max(1.0, msPerFrame - timeSpent))); // Animation time per frame adjusted by the time it took to animate.
        }

        if (data.ResetAfterRun) vr.ResetUniverse();
        callback($"Moved play space over {maxFrameCount} frames.");
    }

    private static double AnimationProgress(int totalFrames, int currentFrame, SpaceMoverEntry entry)
    {
        var totalAnimationFrames = entry.DelayFrames + entry.DurationFrames;
        var repeatCount = Math.Max(1, entry.Entry.Repeat);
        if (currentFrame >= totalAnimationFrames) return entry.Entry.PingPong ? 0.0 : 1.0;
        if (currentFrame < entry.DelayFrames) return 0.0;

        var cycleFrameCount = Math.Round((double)entry.DurationFrames / repeatCount);
        var currentCycleFrame = (currentFrame - entry.DelayFrames) % cycleFrameCount;
        var progress = currentCycleFrame / cycleFrameCount;
        if (entry.Entry.PingPong)
        {
            if (progress > 0.5) progress = (1.0 - progress);
            progress *= 2;
        }

        return progress;
    }

    struct SpaceMoverEntry(
        HmdVector3_t offset,
        Func<double, double> easeFunc,
        DataMoveSpaceEntry move,
        int delayFrames,
        int durationFrames)
    {
        public HmdVector3_t Offset = offset;
        public Func<double, double> EaseFunc = easeFunc;
        public DataMoveSpaceEntry Entry = move;
        public int DelayFrames = delayFrames;
        public int DurationFrames = durationFrames;
    }
}