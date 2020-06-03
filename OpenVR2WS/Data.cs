using BOLL7708;
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
        public static ConcurrentDictionary<dynamic, dynamic> deviceToIndex = new ConcurrentDictionary<dynamic, dynamic>()
        {
            [ETrackedDeviceClass.HMD] = null,
            [ETrackedDeviceClass.Controller] = new List<uint>(),
            [ETrackedControllerRole.LeftHand] = null,
            [ETrackedControllerRole.RightHand] = null,
            [ETrackedDeviceClass.TrackingReference] = new List<uint>(),
            [ETrackedDeviceClass.GenericTracker] = new List<uint>()
        };
        public static ConcurrentDictionary<uint, ETrackedDeviceClass> indexToDevice = new ConcurrentDictionary<uint, ETrackedDeviceClass>(Environment.ProcessorCount, (int)OpenVR.k_unMaxTrackedDeviceCount);
        public static ConcurrentDictionary<uint, ETrackedControllerRole> controllerRoles = new ConcurrentDictionary<uint, ETrackedControllerRole>();
        public static ConcurrentDictionary<ulong, InputSource> handleToSource = new ConcurrentDictionary<ulong, InputSource>(Environment.ProcessorCount, (int)OpenVR.k_unMaxTrackedDeviceCount);
        public static ConcurrentDictionary<InputSource, ulong> sourceToHandle = new ConcurrentDictionary<InputSource, ulong>();
        public static ConcurrentDictionary<string, dynamic> analogInputActionData = new ConcurrentDictionary<string, dynamic>();
        public static ConcurrentDictionary<InputSource, dynamic> poseInputActionData = new ConcurrentDictionary<InputSource, dynamic>();

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
            indexToDevice[index] = deviceClass;
            switch (deviceClass)
            {
                case ETrackedDeviceClass.HMD:
                    deviceToIndex[ETrackedDeviceClass.HMD] = index;
                    break;
                case ETrackedDeviceClass.Controller:
                    if (!deviceToIndex[ETrackedDeviceClass.Controller].Contains(index))
                    {
                        deviceToIndex[ETrackedDeviceClass.Controller].Add(index);
                    }
                    if (controllerRoles.ContainsKey(index))
                    {
                        var role = controllerRoles[index];
                        deviceToIndex[role] = index;
                    }
                    break;
                case ETrackedDeviceClass.TrackingReference:
                    if (!deviceToIndex[ETrackedDeviceClass.TrackingReference].Contains(index))
                    {
                        deviceToIndex[ETrackedDeviceClass.TrackingReference].Add(index);
                    }
                    break;
                case ETrackedDeviceClass.GenericTracker:
                    if (!deviceToIndex[ETrackedDeviceClass.GenericTracker].Contains(index))
                    {
                        deviceToIndex[ETrackedDeviceClass.GenericTracker].Add(index);
                    }
                    break;
            }
        }

        public static void UpdateControllerRoles()
        {
            var leftIndex = Instance.GetIndexForControllerRole(ETrackedControllerRole.LeftHand);
            if (leftIndex != uint.MaxValue)
            {
                controllerRoles[leftIndex] = ETrackedControllerRole.LeftHand;
                SaveDeviceClass(ETrackedDeviceClass.Controller, leftIndex);
            }
            var rightIndex = Instance.GetIndexForControllerRole(ETrackedControllerRole.RightHand);
            if (rightIndex != uint.MaxValue)
            {
                controllerRoles[rightIndex] = ETrackedControllerRole.RightHand;
                SaveDeviceClass(ETrackedDeviceClass.Controller, rightIndex);
            }
        }

        public static void UpdateInputDeviceHandles()
        {
            GetInputHandle(InputSource.Chest);
            GetInputHandle(InputSource.Head);
            GetInputHandle(InputSource.LeftFoot);
            GetInputHandle(InputSource.LeftHand);
            GetInputHandle(InputSource.LeftShoulder);
            GetInputHandle(InputSource.RightFoot);
            GetInputHandle(InputSource.RightHand);
            GetInputHandle(InputSource.RightShoulder);
            GetInputHandle(InputSource.Waist);

            void GetInputHandle(InputSource source)
            {
                var handle = Instance.GetInputSourceHandle(source);
                handleToSource[handle] = source;
                sourceToHandle[source] = handle;
            }
        }

        public static void UpdateOrAddAnalogInputActionData(InputAnalogActionData_t data, InputActionInfo info)
        {
            var source = handleToSource[info.sourceHandle];
            var key = $"{source}:{info.pathEnd}";
            analogInputActionData[key] = new Vec3() { x=data.x, y=data.y, z=data.z };
        }

        public static void UpdateOrAddPoseInputActionData(InputPoseActionData_t data, InputActionInfo info)
        {
            var source = handleToSource[info.sourceHandle];
            poseInputActionData[source] = data.pose; // TODO: Convert this to something nice looking
        }
    }
}
