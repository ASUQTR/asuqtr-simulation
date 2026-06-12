using System.Globalization;
using UnityEngine;

/// <summary>
/// Publie l'odométrie du sous-marin vers le topic ROS configuré (ex: /odometry/filtered).
///
/// Rôle :
///— Fournir une publication périodique de l'état du Rigidbody en nav_msgs/Odometry,
///— Émuler le comportement de reset d'odom (origine capturée au démarrage, réinitialisable).
///
/// Conventions d'axes et intégration Unity → ROS (très important) :
/// - Unity (repère monde) :  X = droite, Y = haut, Z = avant
/// - Convention NED utilisée par les nodes ROS de ce projet :
///     X_ned (north) = Unity.z  (forward)
///     Y_ned (east)  = Unity.x  (right)
///     Z_ned (down)  = -Unity.y (down)
/// - Cette conversion doit rester cohérente avec les autres publishers (IMU, target, etc.).
///   Toute modification de cette convention DOIT être faite en un point central pour éviter
///   des incohérences côté contrôleur (LQR).
///
/// Origine relative (comportement) :
/// - À l'appel de Start() l'origine NED est capturée (position initiale du sub).
/// - Les positions publiées sont relatées à cette origine (pos - origin), comme le driver VectorNav
///   qui expose initial_position / service reset_odom.
/// - Méthode publique ResetOrigin() permet de recapturer l'origine à tout moment.
///
/// Message publié (format rosbridge) :
/// - JSON équivalent à nav_msgs/msg/Odometry sur /odometry/filtered.
/// - Fréquence contrôlée par publishInterval (en secondes).
///
/// Remarques :
/// - Ce composant n'altère pas la physique : il lit simplement rb.position.
/// - Assurez-vous que le Rigidbody référencé est bien celui du sous-marin (ou du rigidbody racine).
/// </summary>
public class NavPositionPublisher : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Rigidbody du sous-marin (position lue pour la publication).")]
    public Rigidbody rb;

    [Header("ROS")]
    [Tooltip("Topic rosbridge qui recevra l'odométrie nav_msgs/Odometry.")]
    public string topic = "/odometry/filtered";

    [Tooltip("Frame ROS parent de l'odométrie.")]
    public string frameId = "odom";

    [Tooltip("Frame ROS enfant du sous-marin.")]
    public string childFrameId = "base_link";

    [Tooltip("Intervalle entre deux publications (s). Par exemple 0.1 = 10 Hz.")]
    public float publishInterval = 0.1f;

    // Origine NED capturée au démarrage (équivalent à initial_position / reset_odom)
    private Vector3 originNED;
    private float timer = 0f;
    private bool topicAdvertised = false;

    void Awake()
    {
        // Récupération automatique du Rigidbody si non assigné dans l'Inspector
        if (!rb) rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        if (rb == null)
        {
            Debug.LogError("[NavPositionPublisher] Rigidbody manquant !");
            return;
        }

        TryAdvertiseTopic();

        // Capture l'origine initiale (position de référence)
        CaptureOrigin();
    }

    /// <summary>
    /// Réinitialise l'origine NED à la position courante du Rigidbody.
    /// Usage : équivalent au service reset_odom du driver VectorNav physique.
    /// </summary>
    public void ResetOrigin()
    {
        CaptureOrigin();
        Debug.Log("[NavPositionPublisher] Origine NED réinitialisée.");
    }

    /// <summary>
    /// Conversion centralisée Unity position -> NED position.
    /// Garder cette fonction comme unique point de vérité pour la conversion d'axes.
    /// Mapping : x_ned = Unity.z, y_ned = Unity.x, z_ned = -Unity.y
    /// </summary>
    static Vector3 UnityPositionToNED(Vector3 unityPos)
    {
        return new Vector3(unityPos.z, unityPos.x, -unityPos.y);
    }

    static Vector3 UnityWorldToRosENU(Vector3 unityVector)
    {
        return new Vector3(unityVector.x, unityVector.z, unityVector.y);
    }

    static Quaternion RotationMatrixToQuaternion(Vector3 c0, Vector3 c1, Vector3 c2)
    {
        float m00 = c0.x, m01 = c1.x, m02 = c2.x;
        float m10 = c0.y, m11 = c1.y, m12 = c2.y;
        float m20 = c0.z, m21 = c1.z, m22 = c2.z;

        float trace = m00 + m11 + m22;
        if (trace > 0f)
        {
            float s = Mathf.Sqrt(trace + 1f) * 2f;
            return new Quaternion(
                (m21 - m12) / s,
                (m02 - m20) / s,
                (m10 - m01) / s,
                0.25f * s
            );
        }

        if (m00 > m11 && m00 > m22)
        {
            float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
            return new Quaternion(
                0.25f * s,
                (m01 + m10) / s,
                (m02 + m20) / s,
                (m21 - m12) / s
            );
        }

        if (m11 > m22)
        {
            float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
            return new Quaternion(
                (m01 + m10) / s,
                0.25f * s,
                (m12 + m21) / s,
                (m02 - m20) / s
            );
        }

        float s2 = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
        return new Quaternion(
            (m02 + m20) / s2,
            (m12 + m21) / s2,
            0.25f * s2,
            (m10 - m01) / s2
        );
    }

    Quaternion UnityRotationToRosENUFLU(Quaternion unityRotation)
    {
        Vector3 forwardENU = UnityWorldToRosENU(unityRotation * Vector3.forward);
        Vector3 leftENU = UnityWorldToRosENU(unityRotation * -Vector3.right);
        Vector3 upENU = UnityWorldToRosENU(unityRotation * Vector3.up);
        return RotationMatrixToQuaternion(forwardENU, leftENU, upENU);
    }

    void CaptureOrigin()
    {
        Vector3 pos = rb.position;
        originNED = UnityPositionToNED(pos);
        Debug.Log($"[NavPositionPublisher] Origine NED capturée : {originNED}");
    }

    void Update()
    {
        TryAdvertiseTopic();

        // Vérifications : socket ROS active et Rigidbody présent
        if (SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected || rb == null)
            return;

        // Contrôle de fréquence
        timer += Time.deltaTime;
        if (timer < publishInterval) return;
        timer = 0f;

        PublishPosition();
    }

    void TryAdvertiseTopic()
    {
        if (topicAdvertised)
            return;

        if (SimpleRosSocket.Instance != null)
        {
            SimpleRosSocket.Instance.AdvertiseTopic(topic, "nav_msgs/Odometry");
            topicAdvertised = true;
        }
    }

    void PublishPosition()
    {
        Vector3 pos = rb.position;

        // Conversion Unity -> NED via l'utilitaire centralisé
        Vector3 ned = UnityPositionToNED(pos);
        float x_ned = ned.x;
        float y_ned = ned.y;
        float z_ned = ned.z;

        // Position relative à l'origine (pos - origin) comme le driver VectorNav
        float dx = x_ned - originNED.x;
        float dy = y_ned - originNED.y;
        float dz = z_ned - originNED.z;

        Vector3 linearLocalUnity = rb.transform.InverseTransformDirection(rb.linearVelocity);
        Vector3 angularLocalUnity = rb.transform.InverseTransformDirection(rb.angularVelocity);
        Quaternion qRos = UnityRotationToRosENUFLU(rb.rotation);

        // Position ENU relative, car control_node convertit ensuite ENU -> NED.
        float enuX = dy;
        float enuY = dx;
        float enuZ = -dz;

        // Twist en repère body FLU, convention ROS standard.
        float linearX = linearLocalUnity.z;
        float linearY = -linearLocalUnity.x;
        float linearZ = linearLocalUnity.y;
        float angularX = angularLocalUnity.z;
        float angularY = -angularLocalUnity.x;
        float angularZ = angularLocalUnity.y;

        // Construction JSON rosbridge nav_msgs/Odometry.
        string msg =
            "{\"op\":\"publish\"," +
	         "\"topic\":\"" + topic + "\"," +
	         "\"msg\":{" +
	           "\"header\":{" +
                 "\"frame_id\":\"" + frameId + "\"" +
	           "}," +
               "\"child_frame_id\":\"" + childFrameId + "\"," +
               "\"pose\":{\"pose\":{" +
                 "\"position\":{" +
	               "\"x\":" + enuX.ToString("F6", CultureInfo.InvariantCulture) + "," +
	               "\"y\":" + enuY.ToString("F6", CultureInfo.InvariantCulture) + "," +
	               "\"z\":" + enuZ.ToString("F6", CultureInfo.InvariantCulture) +
                 "}," +
                 "\"orientation\":{" +
                   "\"x\":" + qRos.x.ToString("F6", CultureInfo.InvariantCulture) + "," +
                   "\"y\":" + qRos.y.ToString("F6", CultureInfo.InvariantCulture) + "," +
                   "\"z\":" + qRos.z.ToString("F6", CultureInfo.InvariantCulture) + "," +
                   "\"w\":" + qRos.w.ToString("F6", CultureInfo.InvariantCulture) +
                 "}" +
               "}}," +
               "\"twist\":{\"twist\":{" +
                 "\"linear\":{" +
                   "\"x\":" + linearX.ToString("F6", CultureInfo.InvariantCulture) + "," +
                   "\"y\":" + linearY.ToString("F6", CultureInfo.InvariantCulture) + "," +
                   "\"z\":" + linearZ.ToString("F6", CultureInfo.InvariantCulture) +
                 "}," +
                 "\"angular\":{" +
                   "\"x\":" + angularX.ToString("F6", CultureInfo.InvariantCulture) + "," +
                   "\"y\":" + angularY.ToString("F6", CultureInfo.InvariantCulture) + "," +
                   "\"z\":" + angularZ.ToString("F6", CultureInfo.InvariantCulture) +
                 "}" +
               "}}" +
	         "}}";

        SimpleRosSocket.Instance.Send(msg);
        Debug.Log($"[NavPositionPublisher] publishing odom pos=({enuX:F3},{enuY:F3},{enuZ:F3}) lin=({linearX:F3},{linearY:F3},{linearZ:F3})");
    }
}
