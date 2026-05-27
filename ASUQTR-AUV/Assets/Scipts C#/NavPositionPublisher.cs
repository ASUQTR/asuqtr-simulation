using System;
using System.Globalization;
using UnityEngine;

public enum PositionPublishMode
{
    Disabled,
    FastDebugOdometry,
    RealisticDepth
}

/// <summary>
/// Publie les informations de position issues du Rigidbody.
///
/// FastDebugOdometry:
///   Unity physics -> /odometry/filtered (nav_msgs/Odometry) -> control_node
///
/// RealisticDepth:
///   Unity physics -> /depth (nav_msgs/Odometry) -> robot_localization
///
/// Conversion frame:
/// - Unity: X=right, Y=up, Z=forward.
/// - ROS Odometry standard: world ENU, body FLU.
/// - control_node.py reconvertit ensuite ENU/FLU vers son etat interne NED/FRD.
/// </summary>
public class NavPositionPublisher : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Rigidbody du sous-marin dont l'etat est publie vers ROS2.")]
    public Rigidbody rb;

    [Header("Mode")]
    public PositionPublishMode publishMode = PositionPublishMode.FastDebugOdometry;

    [Tooltip("Frequence de publication en Hz.")]
    public float publishRateHz = 30.0f;

    [Header("Frames")]
    public string odomFrameId = "odom";
    public string baseFrameId = "base_link";
    public string depthSensorFrameId = "bar30_link";

    private Vector3 originUnity;
    private float timer = 0.0f;
    private bool advertised = false;

    void Awake()
    {
        if (!rb)
            rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (rb == null)
        {
            Debug.LogError("[NavPositionPublisher] Rigidbody manquant.");
            return;
        }

        CaptureOrigin();
    }

    public void ResetOrigin()
    {
        CaptureOrigin();
        Debug.Log("[NavPositionPublisher] Origin reset.");
    }

    void Update()
    {
        if (publishMode == PositionPublishMode.Disabled)
            return;

        if (SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected || rb == null)
            return;

        AdvertiseIfNeeded();

        timer += Time.deltaTime;
        float period = 1.0f / Mathf.Max(1.0f, publishRateHz);
        if (timer < period)
            return;

        timer = 0.0f;

        if (publishMode == PositionPublishMode.FastDebugOdometry)
            PublishFastDebugOdometry();
        else if (publishMode == PositionPublishMode.RealisticDepth)
            PublishDepthOdometry();
    }

    void CaptureOrigin()
    {
        originUnity = rb.position;
    }

    void AdvertiseIfNeeded()
    {
        if (advertised)
            return;

        if (publishMode == PositionPublishMode.FastDebugOdometry)
            SimpleRosSocket.Instance.Advertise(SimpleRosSocket.OdometryTopic, SimpleRosSocket.OdometryType);
        else if (publishMode == PositionPublishMode.RealisticDepth)
            SimpleRosSocket.Instance.Advertise(SimpleRosSocket.DepthTopic, SimpleRosSocket.DepthType);

        advertised = true;
    }

    static string F(float value)
    {
        return value.ToString("F6", CultureInfo.InvariantCulture);
    }

    static string D(double value)
    {
        return value.ToString("F6", CultureInfo.InvariantCulture);
    }

    static string Covariance36(double diagonal)
    {
        string d = D(diagonal);
        string z = "0.000000";
        string[] values = new string[36];

        for (int i = 0; i < values.Length; i++)
            values[i] = z;

        values[0] = d;
        values[7] = d;
        values[14] = d;
        values[21] = d;
        values[28] = d;
        values[35] = d;

        return "[" + string.Join(",", values) + "]";
    }

    static void RosStamp(out int sec, out uint nanosec)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        sec = (int)Math.Floor(now);
        nanosec = (uint)((now - sec) * 1e9);
    }

    void PublishFastDebugOdometry()
    {
        Vector3 relativeUnityPosition = rb.position - originUnity;
        Vector3 rosPosition = FrameConversions.UnityWorldToRosEnu(relativeUnityPosition);
        Quaternion rosOrientation = FrameConversions.UnityRotationToRosEnuFlu(transform.rotation);

        Vector3 bodyLinearUnity = transform.InverseTransformDirection(rb.linearVelocity);
        Vector3 rosLinear = FrameConversions.UnityBodyToRosFlu(bodyLinearUnity);

        Vector3 bodyAngularUnity = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 rosAngular = FrameConversions.UnityBodyToRosFlu(bodyAngularUnity);

        RosStamp(out int sec, out uint nanosec);

        string poseCovariance = Covariance36(0.0001);
        string twistCovariance = Covariance36(0.001);

        string msg =
            "{\"op\":\"publish\"," +
             "\"topic\":\"" + SimpleRosSocket.OdometryTopic + "\"," +
             "\"msg\":{" +
               "\"header\":{" +
                 "\"stamp\":{\"sec\":" + sec + ",\"nanosec\":" + nanosec + "}," +
                 "\"frame_id\":\"" + odomFrameId + "\"" +
               "}," +
               "\"child_frame_id\":\"" + baseFrameId + "\"," +
               "\"pose\":{" +
                 "\"pose\":{" +
                   "\"position\":{" +
                     "\"x\":" + F(rosPosition.x) + "," +
                     "\"y\":" + F(rosPosition.y) + "," +
                     "\"z\":" + F(rosPosition.z) +
                   "}," +
                   "\"orientation\":{" +
                     "\"x\":" + F(rosOrientation.x) + "," +
                     "\"y\":" + F(rosOrientation.y) + "," +
                     "\"z\":" + F(rosOrientation.z) + "," +
                     "\"w\":" + F(rosOrientation.w) +
                   "}" +
                 "}," +
                 "\"covariance\":" + poseCovariance +
               "}," +
               "\"twist\":{" +
                 "\"twist\":{" +
                   "\"linear\":{" +
                     "\"x\":" + F(rosLinear.x) + "," +
                     "\"y\":" + F(rosLinear.y) + "," +
                     "\"z\":" + F(rosLinear.z) +
                   "}," +
                   "\"angular\":{" +
                     "\"x\":" + F(rosAngular.x) + "," +
                     "\"y\":" + F(rosAngular.y) + "," +
                     "\"z\":" + F(rosAngular.z) +
                   "}" +
                 "}," +
                 "\"covariance\":" + twistCovariance +
               "}" +
             "}}";

        SimpleRosSocket.Instance.Send(msg);
    }

    void PublishDepthOdometry()
    {
        Vector3 relativeUnityPosition = rb.position - originUnity;
        Vector3 nedPosition = FrameConversions.UnityWorldToNed(relativeUnityPosition);

        // The physical depth node publishes nav_msgs/Odometry on /depth with
        // pose.pose.position.z = -depth. ROS Z is up; marine depth is positive down.
        float rosZ = -nedPosition.z;
        RosStamp(out int sec, out uint nanosec);

        string msg =
            "{\"op\":\"publish\"," +
             "\"topic\":\"" + SimpleRosSocket.DepthTopic + "\"," +
             "\"msg\":{" +
               "\"header\":{" +
                 "\"stamp\":{\"sec\":" + sec + ",\"nanosec\":" + nanosec + "}," +
                 "\"frame_id\":\"" + odomFrameId + "\"" +
               "}," +
               "\"child_frame_id\":\"" + depthSensorFrameId + "\"," +
               "\"pose\":{" +
                 "\"pose\":{" +
                   "\"position\":{\"x\":0.000000,\"y\":0.000000,\"z\":" + F(rosZ) + "}," +
                   "\"orientation\":{\"x\":0.000000,\"y\":0.000000,\"z\":0.000000,\"w\":1.000000}" +
                 "}," +
                 "\"covariance\":" + Covariance36(0.0001) +
               "}," +
               "\"twist\":{" +
                 "\"twist\":{" +
                   "\"linear\":{\"x\":0.000000,\"y\":0.000000,\"z\":0.000000}," +
                   "\"angular\":{\"x\":0.000000,\"y\":0.000000,\"z\":0.000000}" +
                 "}," +
                 "\"covariance\":" + Covariance36(0.0) +
               "}" +
             "}}";

        SimpleRosSocket.Instance.Send(msg);
    }
}
