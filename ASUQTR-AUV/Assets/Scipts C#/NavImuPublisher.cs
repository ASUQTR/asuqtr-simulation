using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Simule l'IMU VectorNav pour le mode RealisticSensors.
///
/// Topic ROS2:
///   /vectornav/imu
///
/// Type ROS2:
///   sensor_msgs/Imu
///
/// Frame output:
/// - ROS standard ENU/FLU, parce que robot_localization attend des messages ROS standards.
/// - Les conversions Unity -> ROS passent par FrameConversions pour éviter les inversions
///   dispersées dans le projet. Unity est X=right, Y=up, Z=forward.
/// </summary>
public class NavImuPublisher : MonoBehaviour
{
    [Tooltip("Activer en mode RealisticSensors. Desactiver en mode FastDebugOdometry.")]
    public bool publishRealisticSensorTopic = false;

    [Tooltip("Frequence de publication IMU en Hz.")]
    public float publishRateHz = 50f;

    [Header("IMU mounting")]
    [Tooltip("Si vrai, l'IMU simulee est montee face-down (rotation 180 deg autour de X local Unity).")]
    public bool imuFaceDown = false;

    [Tooltip("Frame ROS du capteur IMU, coherent avec l'URDF.")]
    public string frameId = "vectornav_imu";

    private Rigidbody rb;
    private float timer = 0f;
    private Vector3 lastVelocity;
    private bool firstFrame = true;
    private bool advertised = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[NavImuPublisher] Rigidbody manquant.");
            return;
        }

        lastVelocity = rb.linearVelocity;
    }

    void FixedUpdate()
    {
        if (!publishRealisticSensorTopic)
            return;

        if (SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected || rb == null)
            return;

        if (!advertised)
        {
            SimpleRosSocket.Instance.Advertise(SimpleRosSocket.ImuTopic, SimpleRosSocket.ImuType);
            advertised = true;
        }

        timer += Time.fixedDeltaTime;
        if (timer < 1f / Mathf.Max(1f, publishRateHz))
            return;

        timer = 0f;
        PublishImu();
    }

    static Quaternion GetImuMountingRotation(bool faceDown)
    {
        return faceDown ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity;
    }

    static string F(float value)
    {
        return value.ToString("F6", CultureInfo.InvariantCulture);
    }

    static void RosStamp(out int sec, out uint nanosec)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        sec = (int)Math.Floor(now);
        nanosec = (uint)((now - sec) * 1e9);
    }

    void PublishImu()
    {
        Quaternion qMount = GetImuMountingRotation(imuFaceDown);

        Quaternion imuUnityRotation = transform.rotation * qMount;
        Quaternion rosOrientation = FrameConversions.UnityRotationToRosEnuFlu(imuUnityRotation);

        Vector3 bodyAngularUnity = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 imuAngularUnity = qMount * bodyAngularUnity;
        Vector3 rosAngular = FrameConversions.UnityBodyToRosFlu(imuAngularUnity);

        Vector3 accWorld = Vector3.zero;
        if (!firstFrame)
            accWorld = (rb.linearVelocity - lastVelocity) / Time.fixedDeltaTime;
        else
            firstFrame = false;

        lastVelocity = rb.linearVelocity;

        Vector3 bodyAccelUnity = transform.InverseTransformDirection(accWorld);
        Vector3 imuAccelUnity = qMount * bodyAccelUnity;
        Vector3 rosAccel = FrameConversions.UnityBodyToRosFlu(imuAccelUnity);

        RosStamp(out int sec, out uint nanosec);

        string orientationCov = "[0.0001,0,0,0,0.0001,0,0,0,0.0001]";
        string angularCov = "[0.0001,0,0,0,0.0001,0,0,0,0.0001]";
        string accelCov = "[0.001,0,0,0,0.001,0,0,0,0.001]";

        string msg =
            "{\"op\":\"publish\"," +
             "\"topic\":\"" + SimpleRosSocket.ImuTopic + "\"," +
             "\"msg\":{" +
               "\"header\":{" +
                 "\"stamp\":{\"sec\":" + sec + ",\"nanosec\":" + nanosec + "}," +
                 "\"frame_id\":\"" + frameId + "\"" +
               "}," +
               "\"orientation\":{" +
                 "\"x\":" + F(rosOrientation.x) + "," +
                 "\"y\":" + F(rosOrientation.y) + "," +
                 "\"z\":" + F(rosOrientation.z) + "," +
                 "\"w\":" + F(rosOrientation.w) +
               "}," +
               "\"orientation_covariance\":" + orientationCov + "," +
               "\"angular_velocity\":{" +
                 "\"x\":" + F(rosAngular.x) + "," +
                 "\"y\":" + F(rosAngular.y) + "," +
                 "\"z\":" + F(rosAngular.z) +
               "}," +
               "\"angular_velocity_covariance\":" + angularCov + "," +
               "\"linear_acceleration\":{" +
                 "\"x\":" + F(rosAccel.x) + "," +
                 "\"y\":" + F(rosAccel.y) + "," +
                 "\"z\":" + F(rosAccel.z) +
               "}," +
               "\"linear_acceleration_covariance\":" + accelCov +
             "}}";

        SimpleRosSocket.Instance.Send(msg);
    }
}
