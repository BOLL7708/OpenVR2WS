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
        public float[] matrix = new float[12];
        public Vec3 position = new Vec3();
        public Vec3 velocity = new Vec3();
        public Vec3 angularVelocity = new Vec3();
        public Vec3 orientation = new Vec3();

        public Pose(TrackedDevicePose_t poseData)
        {
            Update(poseData);
        }

        public void Update(TrackedDevicePose_t poseData) {
            matrix[0] = poseData.mDeviceToAbsoluteTracking.m0;
            matrix[1] = poseData.mDeviceToAbsoluteTracking.m1;
            matrix[2] = poseData.mDeviceToAbsoluteTracking.m2;
            matrix[3] = poseData.mDeviceToAbsoluteTracking.m3;
            matrix[4] = poseData.mDeviceToAbsoluteTracking.m4;
            matrix[5] = poseData.mDeviceToAbsoluteTracking.m5;
            matrix[6] = poseData.mDeviceToAbsoluteTracking.m6;
            matrix[7] = poseData.mDeviceToAbsoluteTracking.m7;
            matrix[8] = poseData.mDeviceToAbsoluteTracking.m8;
            matrix[9] = poseData.mDeviceToAbsoluteTracking.m9;
            matrix[10] = poseData.mDeviceToAbsoluteTracking.m10;
            matrix[11] = poseData.mDeviceToAbsoluteTracking.m11;

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
        }
    }


}
