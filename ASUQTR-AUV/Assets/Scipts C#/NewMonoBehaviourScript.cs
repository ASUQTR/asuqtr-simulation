using UnityEngine;
using System.Globalization;

/// <summary>
/// Publie la position d'un objet Target vers ROS comme cible absolue NED.
/// Le contrôleur LQR va essayer d'atteindre cette position.
/// </summary>
public class TargetPositionPublisher : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("L'objet Transform que le sous-marin doit suivre")]
    public Transform targetObject;

    [Header("ROS Settings")]
    [Tooltip("Topic ROS pour la position cible absolue")]
    public string rosTopic = "/control/abs_ned_pos_target";

    [Tooltip("Fréquence de publication en Hz")]
    public float publishRateHz = 10f;

    private float timer = 0f;

    void Update()
    {
        if (SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected)
            return;

        if (targetObject == null)
        {
            Debug.LogWarning("[TargetPositionPublisher] Aucun targetObject assigné!");
            return;
        }

        timer += Time.deltaTime;
        if (timer < 1f / publishRateHz)
            return;

        timer = 0f;
        PublishTargetPosition();
    }

    void PublishTargetPosition()
    {
        // Position Unity ? NED (même conversion que NavPositionPublisher)
        Vector3 pos = targetObject.position;

        float x_ned = pos.z;   // X_ned = avant  = Z_unity
        float y_ned = pos.x;   // Y_ned = droite = X_unity
        float z_ned = -pos.y;  // Z_ned = bas    = -Y_unity

        // Message ROS (format geometry_msgs/Point)
        string msg =
            "{" +
                "\"op\": \"publish\"," +
                "\"topic\": \"" + rosTopic + "\"," +
                "\"msg\": {" +
                    "\"x\": " + x_ned.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    "\"y\": " + y_ned.ToString("F6", CultureInfo.InvariantCulture) + "," +
                    "\"z\": " + z_ned.ToString("F6", CultureInfo.InvariantCulture) +
                "}" +
            "}";

        SimpleRosSocket.Instance.Send(msg);
    }
}