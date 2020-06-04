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
        public int[] matrix = null; // TODO: Figure out what to do with this...
        public Vec3 position = new Vec3();
        public Vec3 velocity = new Vec3();
        public Vec3 angularVelocity = new Vec3();

        public Pose(TrackedDevicePose_t poseData)
        {
            Update(poseData);
        }

        public void Update(TrackedDevicePose_t poseData) {
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
