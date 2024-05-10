using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using OpenVR2WS.Output;
using Valve.VR;
using static EasyOpenVR.EasyOpenVRSingleton;

namespace OpenVR2WS;

// ReSharper disable InconsistentNaming
internal static class DataStore
{
    public static readonly ConcurrentDictionary<ETrackedDeviceClass, HashSet<uint>> deviceToIndex = new();
    public static readonly ConcurrentDictionary<ulong, InputSource> handleToSource =
        new (Environment.ProcessorCount, (int)OpenVR.k_unMaxTrackedDeviceCount);
    public static readonly ConcurrentDictionary<InputSource, ulong> sourceToHandle = new();
    public static readonly ConcurrentDictionary<InputSource, ConcurrentDictionary<string, Vec3>> analogInputActionData = new();
    public static readonly ConcurrentDictionary<InputSource, ConcurrentDictionary<string, JsonPose>> poseInputActionData = new();
    public static readonly ConcurrentDictionary<InputSource, int> sourceToIndex = new();
    public static readonly ConcurrentDictionary<int, InputSource> indexToSource = new();

    /*
     * This will update the device class for an index what was just connected.
     * Or all of the valid indexes if nothing is supplied.
     */
    public static void UpdateDeviceIndices(uint index = uint.MaxValue)
    {
        if (index != uint.MaxValue)
        {
            // Only update this one index.
            var deviceClass = OpenVR.System.GetTrackedDeviceClass(index);
            SaveDeviceClass(deviceClass, index);
        }
        else
        {
            // This loop is run at init, just to find all existing devices.
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                var deviceClass = OpenVR.System.GetTrackedDeviceClass(i);
                if (deviceClass != ETrackedDeviceClass.Invalid)
                {
                    SaveDeviceClass(deviceClass, i);
                }
            }
        }
    }

    /*
     * This should be up and running.
     */
    private static void SaveDeviceClass(ETrackedDeviceClass deviceClass, uint index)
    {
        deviceToIndex[deviceClass].Add(index);
    }

    public static void UpdateInputDeviceHandles()
    {
        GetInputHandle(InputSource.LeftHand);
        GetInputHandle(InputSource.LeftElbow);
        GetInputHandle(InputSource.LeftShoulder);
        GetInputHandle(InputSource.LeftKnee);
        GetInputHandle(InputSource.LeftFoot);

        GetInputHandle(InputSource.RightHand);
        GetInputHandle(InputSource.RightElbow);
        GetInputHandle(InputSource.RightShoulder);
        GetInputHandle(InputSource.RightKnee);
        GetInputHandle(InputSource.RightFoot);

        GetInputHandle(InputSource.Head);
        GetInputHandle(InputSource.Chest);
        GetInputHandle(InputSource.Waist);

        GetInputHandle(InputSource.Gamepad);
        GetInputHandle(InputSource.Camera);
        return;

        void GetInputHandle(InputSource source)
        {
            var handle = Instance.GetInputSourceHandle(source);
            handleToSource[handle] = source;
            sourceToHandle[source] = handle;
            var info = Instance.GetOriginTrackedDeviceInfo(handle);
            if (info.trackedDeviceIndex == uint.MaxValue) return;
            var index = (int)info.trackedDeviceIndex;
            // Only a headset gets index 0, but it's also the default N/A when loading info.
            if (source != InputSource.Head && index == 0) index = -1;
            sourceToIndex[source] = index;
            indexToSource[index] = source;
        }
    }

    public static void UpdateOrAddAnalogInputActionData(InputAnalogActionData_t data, InputActionInfo info)
    {
        var source = handleToSource[info.sourceHandle];
        if (!analogInputActionData.ContainsKey(source))
            analogInputActionData[source] = new ConcurrentDictionary<string, Vec3>();
        analogInputActionData[source][info.pathEnd] = new Vec3() { X = data.x, Y = data.y, Z = data.z };
    }

    public static void UpdateOrAddPoseInputActionData(InputPoseActionData_t data, InputActionInfo info)
    {
        var source = handleToSource[info.sourceHandle];
        if (!poseInputActionData.ContainsKey(source))
            poseInputActionData[source] = new ConcurrentDictionary<string, JsonPose>();
        poseInputActionData[source][info.pathEnd] = new JsonPose(data.pose);
    }

    public static void UpdateOrAddPoseData(TrackedDevicePose_t pose, int deviceIndex)
    {
        if (indexToSource.TryGetValue(deviceIndex, out var source))
        {
            if (!poseInputActionData.ContainsKey(source))
                poseInputActionData[source] = new ConcurrentDictionary<string, JsonPose>();
            poseInputActionData[source]["Pose"] = new JsonPose(pose);
        }
    }

    public static void Reset()
    {
        deviceToIndex[ETrackedDeviceClass.HMD] = new HashSet<uint>();
        deviceToIndex[ETrackedDeviceClass.Controller] = new HashSet<uint>();
        deviceToIndex[ETrackedDeviceClass.TrackingReference] = new HashSet<uint>();
        deviceToIndex[ETrackedDeviceClass.GenericTracker] = new HashSet<uint>();
        deviceToIndex[ETrackedDeviceClass.DisplayRedirect] = new HashSet<uint>();
        handleToSource.Clear();
        sourceToHandle.Clear();
        analogInputActionData.Clear();
        poseInputActionData.Clear();
        sourceToIndex.Clear();
        indexToSource.Clear();
    }
}