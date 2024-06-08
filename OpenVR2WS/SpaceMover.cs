using System;
using System.Collections.Generic;
using System.Threading;
using EasyOpenVR;
using EasyOpenVR.Extensions;
using EasyOpenVR.Utils;
using OpenVR2WS.Input;
using Valve.VR;

namespace OpenVR2WS;

public static class SpaceMover
{
    private static Thread? _thread;

    public static void MoveSpace(DataMoveSpace data, Action<string> callback)
    {
        if (_thread == null || !_thread.IsAlive)
        {
            _thread = new Thread(() => AnimationWorker(data, callback));
            if (!_thread.IsAlive) _thread.Start();
        }
        else
        {
            callback("Already running an animation, cannot run multiple at the same time.");
        }
    }

    private static void AnimationWorker(DataMoveSpace data, Action<string> callback)
    {
        Thread.CurrentThread.IsBackground = true;
        var vr = EasyOpenVRSingleton.Instance;
        if (data.ResetSpaceBeforeRun)
        {
            vr.ResetUniverse();
            Thread.Sleep(1);
        }

        // Get animation rate
        var hz = vr.GetFloatTrackedDeviceProperty(0, ETrackedDeviceProperty.Prop_DisplayFrequency_Float);
        if (data.DurationMs > 0 && hz <= 0) return;
        var msPerFrame = 1000 / hz;

        // Start and end easing functions
        var easeInFunc = EasingUtils.Get(data.EaseInType, data.EaseInMode);
        var easeOutFunc = EasingUtils.Get(data.EaseOutType, data.EaseOutMode);
        var easeInFrames = (int)Math.Round(data.EaseInMs / msPerFrame);
        var easeOutFrames = (int)Math.Round(data.EaseOutMs / msPerFrame);

        // Build entry data to loop over
        var totalFrames = (int)Math.Round(data.DurationMs / msPerFrame);
        List<SpaceMoverEntry> entries = [];
        foreach (var entry in data.Entries)
        {
            var easeFunc = EasingUtils.Get(entry.EaseType, entry.EaseMode);
            var startOffsetFrames = (int)Math.Round(entry.StartOffsetMs / msPerFrame);
            var endOffsetFrames = (int)Math.Round(entry.EndOffsetMs / msPerFrame);
            entries.Add(new SpaceMoverEntry(
                entry.GetOffset(), 
                entry.Rotate, 
                easeFunc, 
                entry, 
                startOffsetFrames, 
                endOffsetFrames)
            );
        }

        // Fetch current setup
        var originPose = vr.GetOriginPose();
        var poses = vr.GetDeviceToAbsoluteTrackingPose();
        var hmdPose = poses[0].mDeviceToAbsoluteTracking;
        var hmdAnchorEuler = hmdPose.EulerAngles();
        if (data.Correction != Correction.HmdYaw) hmdAnchorEuler.v1 = 0;
        if (data.Correction != Correction.HmdPitch) hmdAnchorEuler.v0 = 0;
        var correctionPose = (data.Correction switch
        {
            Correction.PlaySpace => originPose,
            Correction.Hmd => hmdPose,
            _ => hmdPose.FromEuler(hmdAnchorEuler).Translate(hmdPose.GetPosition())
        });
        
        // Loop the animation
        var timeLoopStarted = 0L;
        var timeSpent = 0.0;
        var easeValue = 0.0;
        var valueResult = new Progress();
        var repetition = 0.0;

        // Animation loop
        if (totalFrames == 0 && data.Entries.Length > 0) // No duration is instant
        {
            var offset = new HmdVector3_t();
            var rotate = 0f;
            foreach (var entry in entries)
            {
                offset = offset.Add(entry.Offset);
                rotate += entry.Rotate;
            }
            vr.ModifyUniverse(offset, rotate, originPose, correctionPose, true);
        }
        else // With duration so we animate 
        {
            for (var currentFrame = 0; currentFrame < totalFrames; currentFrame++)
            {
                timeLoopStarted = DateTime.Now.Ticks;
                var offset = new HmdVector3_t();
                var rotate = 0f;

                // Apply all offsets to the final offset and apply it to the play space
                foreach (var entry in entries)
                {
                    valueResult = AnimationProgress(totalFrames, currentFrame, entry);
                    easeValue = StartEndEase(totalFrames, currentFrame, easeInFrames, easeInFunc, easeOutFrames, easeOutFunc);
                    repetition = entry.Entry.Accumulate ? (double)valueResult.Repetition : 0.0;
                    offset = offset.Add(
                        entry.Offset.Multiply(
                            (float)(entry.EaseFunc(valueResult.Value) * easeValue + repetition)
                        )
                    );
                    rotate += entry.Rotate * (float)(entry.EaseFunc(valueResult.Value) * easeValue);
                }
                vr.ModifyUniverse(offset, rotate, originPose, correctionPose, true);

                timeSpent = (double)(DateTime.Now.Ticks - timeLoopStarted) / TimeSpan.TicksPerMillisecond;
                Thread.Sleep((int)Math.Round(Math.Max(1.0, msPerFrame - timeSpent))); // Animation time per frame adjusted by the time it took to animate.
            }
        }

        /*
         * A full vr.ResetUniverse() here actually resets the position to the wrong place, it acts like doing the standing location reset from the dashboard.
         * Due to this, we simply translate the space to a zero offset, to go back to where we started this sequence.
         * This would seem natural as resetting at the start would need to be a global reset, while this just resets the current one.
         */
        if (data.ResetOffsetAfterRun)
        {
            vr.ModifyUniverse(new HmdVector3_t(), 0f, originPose, correctionPose, true);
        }

        callback($"Moved play space over {totalFrames} frames.");
    }
    
    /**
     * Calculate the progress of the animation, that is, depending on settings it will affect the normalized value, so it can repeat, reverse or being offset.
     *
     * @param totalFrames Total frames in the animation
     * @param currentFrame Current frame in the animation
     * @param entry The entry to calculate the progress for
     * @return The progress of the animation
     */
    private static Progress AnimationProgress(int totalFrames, int currentFrame, SpaceMoverEntry entry)
    {
        var progressResult = new Progress();
        
        // Prep
        var totalAnimationFrames = totalFrames - entry.StartOffsetFrames - entry.EndOffsetFrames;
        var currentAnimationFrame = currentFrame - entry.StartOffsetFrames;
        var repeatCount = Math.Max(1, entry.Entry.Repeat);
        var cycleFrameCount = Math.Round((double)totalAnimationFrames / repeatCount);
        var currentCycleFrame = currentAnimationFrame % cycleFrameCount;
        var progress = currentCycleFrame / cycleFrameCount;
        progressResult.Repetition = (int) Math.Floor(currentAnimationFrame / cycleFrameCount);
        
        // Progress limits
        if (currentFrame < entry.StartOffsetFrames)
        {
            progress = 0;
        }
        else if (currentFrame >= totalFrames - entry.EndOffsetFrames)
        {
            progress = entry.Entry.PingPong ? 0.0 : 1.0;
        }
        else if (entry.Entry.PingPong)
        {
            if (progress > 0.5) progress = (1.0 - progress);
            progress *= 2;
        }

        progressResult.Value = progress;

        return progressResult;
    }

    private struct Progress
    {
        public double Value = 0;
        public int Repetition = 0;

        public Progress()
        {
        }
    }

    private static double StartEndEase(int totalFrames, int currentFrame, int easeInFrames, Func<double, double> easeInFunc, int easeOutFrames, Func<double, double> easeOutFunc)
    {
        var value = 1.0;
        if (easeInFrames > 0 && currentFrame <= easeInFrames) value *= easeInFunc(currentFrame / (double)easeInFrames);
        if (easeOutFrames > 0 && currentFrame >= totalFrames - easeOutFrames) value *= easeOutFunc((totalFrames - currentFrame) / (double)easeOutFrames);
        return value;
    }

    struct SpaceMoverEntry(
        HmdVector3_t offset,
        float rotate,
        Func<double, double> easeFunc,
        DataMoveSpaceEntry move,
        int startOffsetFrames,
        int endOffsetFrames)
    {
        public HmdVector3_t Offset = offset;
        public float Rotate = rotate;
        public Func<double, double> EaseFunc = easeFunc;
        public DataMoveSpaceEntry Entry = move;
        public int StartOffsetFrames = startOffsetFrames;
        public int EndOffsetFrames = endOffsetFrames;
    }
}