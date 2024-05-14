using EasyOpenVR;
using EasyOpenVR.Utils;
using Valve.VR;

namespace OpenVR2WS.Output;

internal class JsonPose
{
    // public HmdMatrix34_t matrix = new HmdMatrix34_t(); // TODO: Figure out what to do with this...
    public float[] RotationMatrix = new float[9];
    public Vec3 Position = new Vec3();
    public Vec3 Velocity = new Vec3();
    public Vec3 AngularVelocity = new Vec3();
    public Vec3 Orientation = new Vec3();
    public bool IsConnected = false;
    public bool IsTracking = false;

    public JsonPose(TrackedDevicePose_t poseData)
    {
        Update(poseData);
    }

    public void Update(TrackedDevicePose_t poseData)
    {
        RotationMatrix[0] = poseData.mDeviceToAbsoluteTracking.m0;
        RotationMatrix[1] = poseData.mDeviceToAbsoluteTracking.m1;
        RotationMatrix[2] = poseData.mDeviceToAbsoluteTracking.m2;
        RotationMatrix[3] = poseData.mDeviceToAbsoluteTracking.m4;
        RotationMatrix[4] = poseData.mDeviceToAbsoluteTracking.m5;
        RotationMatrix[5] = poseData.mDeviceToAbsoluteTracking.m6;
        RotationMatrix[6] = poseData.mDeviceToAbsoluteTracking.m8;
        RotationMatrix[7] = poseData.mDeviceToAbsoluteTracking.m9;
        RotationMatrix[8] = poseData.mDeviceToAbsoluteTracking.m10;

        var orientation = GeneralUtils.RotationMatrixToYPR(poseData.mDeviceToAbsoluteTracking);
        Orientation.X = orientation.pitch;
        Orientation.Y = orientation.yaw;
        Orientation.Z = orientation.roll;

        Position.X = poseData.mDeviceToAbsoluteTracking.m3;
        Position.Y = poseData.mDeviceToAbsoluteTracking.m7;
        Position.Z = poseData.mDeviceToAbsoluteTracking.m11;

        Velocity.X = poseData.vVelocity.v0;
        Velocity.Y = poseData.vVelocity.v1;
        Velocity.Z = poseData.vVelocity.v2;

        AngularVelocity.X = poseData.vAngularVelocity.v0;
        AngularVelocity.Y = poseData.vAngularVelocity.v1;
        AngularVelocity.Z = poseData.vAngularVelocity.v2;

        IsConnected = poseData.bDeviceIsConnected;
        IsTracking = poseData.bPoseIsValid;
    }
}