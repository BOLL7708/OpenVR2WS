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
            [ETrackedDeviceClass.HMD] = new HashSet<uint>(),
            [ETrackedDeviceClass.Controller] = new HashSet<uint>(),
            [ETrackedDeviceClass.TrackingReference] = new HashSet<uint>(),
            [ETrackedDeviceClass.GenericTracker] = new HashSet<uint>(),
            [ETrackedDeviceClass.DisplayRedirect] = new HashSet<uint>()
        };
        public static ConcurrentDictionary<uint, ETrackedDeviceClass> indexToDevice = new ConcurrentDictionary<uint, ETrackedDeviceClass>(Environment.ProcessorCount, (int)OpenVR.k_unMaxTrackedDeviceCount);
        public static ConcurrentDictionary<ulong, InputSource> handleToSource = new ConcurrentDictionary<ulong, InputSource>(Environment.ProcessorCount, (int)OpenVR.k_unMaxTrackedDeviceCount);
        public static ConcurrentDictionary<InputSource, ulong> sourceToHandle = new ConcurrentDictionary<InputSource, ulong>();
        public static ConcurrentDictionary<InputSource, ConcurrentDictionary<string, Vec3>> analogInputActionData = new ConcurrentDictionary<InputSource, ConcurrentDictionary<string, Vec3>>();
        public static ConcurrentDictionary<InputSource, Pose> poseInputActionData = new ConcurrentDictionary<InputSource, Pose>();
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
            indexToDevice[index] = deviceClass;
            deviceToIndex[deviceClass].Add(index);
            indexToDevice[index] = deviceClass;
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

            void GetInputHandle(InputSource source)
            {
                var handle = Instance.GetInputSourceHandle(source);
                handleToSource[handle] = source;
                sourceToHandle[source] = handle;
                var info = Instance.GetOriginTrackedDeviceInfo(handle);
                indexToSource[(int) info.trackedDeviceIndex] = source;
                sourceToIndex[source] = (int) info.trackedDeviceIndex;
            }
        }

        public static void UpdateOrAddAnalogInputActionData(InputAnalogActionData_t data, InputActionInfo info)
        {
            var source = handleToSource[info.sourceHandle];
            if (!analogInputActionData.ContainsKey(source)) analogInputActionData[source] = new ConcurrentDictionary<string, Vec3>();
            analogInputActionData[source][info.pathEnd] = new Vec3() { x=data.x, y=data.y, z=data.z };
        }

        public static void UpdateOrAddPoseInputActionData(InputPoseActionData_t data, InputActionInfo info)
        {
            var source = handleToSource[info.sourceHandle];
            poseInputActionData[source] = new Pose(data.pose);
        }
    }
}
