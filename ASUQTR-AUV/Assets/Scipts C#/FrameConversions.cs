using System;
using UnityEngine;

/// <summary>
/// Shared frame conversions for the ASUQTR Unity simulator.
///
/// Unity uses X=right, Y=up, Z=forward. Its scene space and rotations can feel
/// left-handed compared with ROS/marine math, so every ROS-facing script should
/// call this helper instead of re-implementing signs locally.
///
/// ROS standard frames used here:
/// - World ENU: X=east/right, Y=north/forward, Z=up
/// - Body FLU:  X=forward, Y=left, Z=up
///
/// Marine/control internal frames documented for clarity:
/// - World NED: X=north/forward, Y=east/right, Z=down
/// - Body FRD:  X=forward, Y=right, Z=down
/// </summary>
public static class FrameConversions
{
    public static Vector3 UnityWorldToNed(Vector3 unityWorld)
    {
        return new Vector3(
            unityWorld.z,
            unityWorld.x,
            -unityWorld.y
        );
    }

    public static Vector3 UnityWorldToRosEnu(Vector3 unityWorld)
    {
        return new Vector3(
            unityWorld.x,
            unityWorld.z,
            unityWorld.y
        );
    }

    public static Vector3 UnityBodyToFrd(Vector3 unityBody)
    {
        return new Vector3(
            unityBody.z,
            unityBody.x,
            -unityBody.y
        );
    }

    public static Vector3 UnityBodyToRosFlu(Vector3 unityBody)
    {
        return new Vector3(
            unityBody.z,
            -unityBody.x,
            unityBody.y
        );
    }

    public static Quaternion UnityRotationToRosEnuFlu(Quaternion unityRotation)
    {
        // Build a right-handed ROS rotation matrix from Unity basis vectors.
        // Columns are ROS body axes (FLU) expressed in ROS world coordinates (ENU).
        Vector3 rosForward = UnityWorldToRosEnu(unityRotation * Vector3.forward);
        Vector3 rosLeft = UnityWorldToRosEnu(unityRotation * -Vector3.right);
        Vector3 rosUp = UnityWorldToRosEnu(unityRotation * Vector3.up);

        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(rosForward.x, rosForward.y, rosForward.z, 0.0f));
        m.SetColumn(1, new Vector4(rosLeft.x, rosLeft.y, rosLeft.z, 0.0f));
        m.SetColumn(2, new Vector4(rosUp.x, rosUp.y, rosUp.z, 0.0f));

        Quaternion q = m.rotation;
        q.Normalize();
        return q;
    }

    public static Vector3 QuaternionToEulerRad(Quaternion q)
    {
        double x = q.x;
        double y = q.y;
        double z = q.z;
        double w = q.w;

        double t0 = 2.0 * (w * x + y * z);
        double t1 = 1.0 - 2.0 * (x * x + y * y);
        double roll = Math.Atan2(t0, t1);

        double t2 = 2.0 * (w * y - z * x);
        t2 = Math.Max(-1.0, Math.Min(1.0, t2));
        double pitch = Math.Asin(t2);

        double t3 = 2.0 * (w * z + x * y);
        double t4 = 1.0 - 2.0 * (y * y + z * z);
        double yaw = Math.Atan2(t3, t4);

        return new Vector3((float)roll, (float)pitch, (float)yaw);
    }
}
