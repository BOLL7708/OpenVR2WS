﻿using BOLL7708;
using OpenVR2WS.Output;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;
using static BOLL7708.EasyOpenVRSingleton;

namespace OpenVR2WS
{
    static class Data
    {
        public static ConcurrentDictionary<ETrackedDeviceClass, HashSet<uint>> deviceToIndex = new ConcurrentDictionary<ETrackedDeviceClass, HashSet<uint>>();
        public static ConcurrentDictionary<ulong, InputSource> handleToSource = new ConcurrentDictionary<ulong, InputSource>(Environment.ProcessorCount, (int)OpenVR.k_unMaxTrackedDeviceCount);
        public static ConcurrentDictionary<InputSource, ulong> sourceToHandle = new ConcurrentDictionary<InputSource, ulong>();
        public static ConcurrentDictionary<InputSource, ConcurrentDictionary<string, Vec3>> analogInputActionData = new ConcurrentDictionary<InputSource, ConcurrentDictionary<string, Vec3>>();
        public static ConcurrentDictionary<InputSource, ConcurrentDictionary<string, Pose>> poseInputActionData = new ConcurrentDictionary<InputSource, ConcurrentDictionary<string, Pose>>();
        public static ConcurrentDictionary<InputSource, int> sourceToIndex = new ConcurrentDictionary<InputSource, int>();
        public static ConcurrentDictionary<int, InputSource> indexToSource = new ConcurrentDictionary<int, InputSource>();

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

            void GetInputHandle(InputSource source)
            {
                var handle = Instance.GetInputSourceHandle(source);
                handleToSource[handle] = source;
                sourceToHandle[source] = handle;
                var info = Instance.GetOriginTrackedDeviceInfo(handle);
                if (info.trackedDeviceIndex != uint.MaxValue)
                {
                    var index = (int)info.trackedDeviceIndex;
                    // Only a headset gets index 0, but it's also the default N/A when loading info.
                    if (source != InputSource.Head && index == 0) index = -1;
                    sourceToIndex[source] = index;
                    indexToSource[index] = source;
                }
            }
        }

        public static void UpdateOrAddAnalogInputActionData(InputAnalogActionData_t data, InputActionInfo info)
        {
            var source = handleToSource[info.sourceHandle];
            if (!analogInputActionData.ContainsKey(source)) analogInputActionData[source] = new ConcurrentDictionary<string, Vec3>();
            analogInputActionData[source][info.pathEnd] = new Vec3() { x = data.x, y = data.y, z = data.z };
        }

        public static void UpdateOrAddPoseInputActionData(InputPoseActionData_t data, InputActionInfo info)
        {
            var source = handleToSource[info.sourceHandle];
            if (!poseInputActionData.ContainsKey(source)) poseInputActionData[source] = new ConcurrentDictionary<string, Pose>();
            poseInputActionData[source][info.pathEnd] = new Pose(data.pose);
        }
        public static void UpdateOrAddPoseData(TrackedDevicePose_t pose, int deviceIndex)
        {
            if(indexToSource.TryGetValue(deviceIndex, out var source))
            {
                if (!poseInputActionData.ContainsKey(source)) poseInputActionData[source] = new ConcurrentDictionary<string, Pose>();
                poseInputActionData[source]["Pose"] = new Pose(pose);
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
}
