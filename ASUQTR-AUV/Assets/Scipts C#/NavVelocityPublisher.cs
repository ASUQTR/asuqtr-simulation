using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Publie la vitesse DVL simulee pour le mode RealisticSensors.
///
/// Topic ROS2:
///   /dvl/velocities
///
/// Type ROS2:
///   geometry_msgs/TwistWithCovarianceStamped
///
/// Frame output:
/// - ROS body FLU: X=forward, Y=left, Z=up.
/// - Unity body: X=right, Y=up, Z=forward.
/// </summary>
public class NavVelocityPublisher : MonoBehaviour
{
    [Tooltip("Activer en mode RealisticSensors. Desactiver en mode FastDebugOdometry.")]
    public bool publishRealisticSensorTopic = false;

    [Tooltip("Frequence de publication DVL en Hz.")]
    public float publishRateHz = 30f;

    [Tooltip("Frame ROS du DVL, coherent avec l'URDF.")]
    public string frameId = "dvl_link";

    private Rigidbody rb;
    private float timer = 0f;
    private bool advertised = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
            Debug.LogError("[NavVelocityPublisher] Rigidbody manquant.");
    }

    void FixedUpdate()
    {
        if (!publishRealisticSensorTopic)
            return;

        if (SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected || rb == null)
            return;

        if (!advertised)
        {
            SimpleRosSocket.Instance.Advertise(SimpleRosSocket.DvlVelocityTopic, SimpleRosSocket.DvlVelocityType);
            advertised = true;
        }

        timer += Time.fixedDeltaTime;
        if (timer < 1f / Mathf.Max(1f, publishRateHz))
            return;

        timer = 0f;
        PublishVelocity();
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

    static string TwistCovariance36()
    {
        return "[" +
            "0.0004,0,0,0,0,0," +
            "0,0.0004,0,0,0,0," +
            "0,0,0.0004,0,0,0," +
            "0,0,0,999999,0,0," +
            "0,0,0,0,999999,0," +
            "0,0,0,0,0,999999" +
        "]";
    }

    void PublishVelocity()
    {
        Vector3 bodyVelocityUnity = transform.InverseTransformDirection(rb.linearVelocity);
        Vector3 rosLinear = FrameConversions.UnityBodyToRosFlu(bodyVelocityUnity);

        RosStamp(out int sec, out uint nanosec);

        string msg =
            "{\"op\":\"publish\"," +
             "\"topic\":\"" + SimpleRosSocket.DvlVelocityTopic + "\"," +
             "\"msg\":{" +
               "\"header\":{" +
                 "\"stamp\":{\"sec\":" + sec + ",\"nanosec\":" + nanosec + "}," +
                 "\"frame_id\":\"" + frameId + "\"" +
               "}," +
               "\"twist\":{" +
                 "\"twist\":{" +
                   "\"linear\":{" +
                     "\"x\":" + F(rosLinear.x) + "," +
                     "\"y\":" + F(rosLinear.y) + "," +
                     "\"z\":" + F(rosLinear.z) +
                   "}," +
                   "\"angular\":{\"x\":0.000000,\"y\":0.000000,\"z\":0.000000}" +
                 "}," +
                 "\"covariance\":" + TwistCovariance36() +
               "}" +
             "}}";

        SimpleRosSocket.Instance.Send(msg);
    }
}
