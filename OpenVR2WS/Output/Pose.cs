using BOLL7708;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;

namespace OpenVR2WS.Output
{
    class Pose
    {
        // public HmdMatrix34_t matrix = new HmdMatrix34_t(); // TODO: Figure out what to do with this...
        public float[] rotationMatrix = new float[9];
        public Vec3 position = new Vec3();
        public Vec3 velocity = new Vec3();
        public Vec3 angularVelocity = new Vec3();
        public Vec3 orientation = new Vec3();
        public bool isConnected = false;
        public bool isTracking = false;

        public Pose(TrackedDevicePose_t poseData)
        {
            Update(poseData);
        }

        public void Update(TrackedDevicePose_t poseData) {
            rotationMatrix[0] = poseData.mDeviceToAbsoluteTracking.m0;
            rotationMatrix[1] = poseData.mDeviceToAbsoluteTracking.m1;
            rotationMatrix[2] = poseData.mDeviceToAbsoluteTracking.m2;
            rotationMatrix[3] = poseData.mDeviceToAbsoluteTracking.m4;
            rotationMatrix[4] = poseData.mDeviceToAbsoluteTracking.m5;
            rotationMatrix[5] = poseData.mDeviceToAbsoluteTracking.m6;
            rotationMatrix[6] = poseData.mDeviceToAbsoluteTracking.m8;
            rotationMatrix[7] = poseData.mDeviceToAbsoluteTracking.m9;
            rotationMatrix[8] = poseData.mDeviceToAbsoluteTracking.m10;

            var orientation = EasyOpenVRSingleton.Utils.RotationMatrixToYPR(poseData.mDeviceToAbsoluteTracking);
            this.orientation.x = orientation.pitch;
            this.orientation.y = orientation.yaw;
            this.orientation.z = orientation.roll;

            position.x = poseData.mDeviceToAbsoluteTracking.m3;
            position.y = poseData.mDeviceToAbsoluteTracking.m7;
            position.z = poseData.mDeviceToAbsoluteTracking.m11;

            velocity.x = poseData.vVelocity.v0;
            velocity.y = poseData.vVelocity.v1;
            velocity.z = poseData.vVelocity.v2;

            angularVelocity.x = poseData.vAngularVelocity.v0;
            angularVelocity.y = poseData.vAngularVelocity.v1;
            angularVelocity.z = poseData.vAngularVelocity.v2;

            isConnected = poseData.bDeviceIsConnected;
            isTracking = poseData.bPoseIsValid;
        }
    }
}
