using System.Globalization;
using UnityEngine;


/// <summary>
/// Simule un capteur IMU VectorNav et publie un message JSON compatible rosbridge.
/// 
/// But :
///— Fournir une publication IMU ROS2 standard cohérente avec sensor_msgs/Imu.
///— Permettre d'alimenter le contrôleur ROS (LQR / control_node) avec orientation,
///  vitesses angulaires (p, q, r) et accélération linéaire corps (LINEARACCELBODY).
///
/// Conventions et intégration Unity -> ROS
/// - Répères Unity (monde/body) : X = droite, Y = haut, Z = avant.
/// - Repère ROS monde : ENU (X = East, Y = North, Z = Up).
/// - Repère ROS body : FLU (X = Forward, Y = Left, Z = Up).
/// - Il est impératif d'utiliser la même transformation pour :
///     * la conversion d'orientation (quaternion),
///     * la conversion des vitesses angulaires (body rates),
///     * la conversion des accélérations linéaires.
///   Toute incohérence casse les boucles de contrôle (LQR) — centralisez la conversion Unity→NED.
///
/// Remarques importantes sur les choix implémentés :
/// - Orientation : reconstruite en ENU/FLU via `UnityRotToRosENUFLU(Quaternion)` (méthode robuste).
/// - Montage IMU (option face-down) : appliqué comme rotation fixe body->IMU (180° autour de X).
///   Cette rotation est appliquée à l'orientation, aux vitesses angulaires et aux accélérations.
/// - Vitesses angulaires : on transforme d'abord rb.angularVelocity en repère corps Unity,
///   puis on applique la rotation de montage IMU. Enfin on permute/sign-flip pour ROS FLU :
///     x = omega_imu.z   (forward)
///     y = -omega_imu.x  (left)
///     z = omega_imu.y   (up)
///   Cette permutation correspond exactement à la reconstruction d'axes utilisée pour le quaternion.
/// - Accélération linéaire : calculée comme dv/dt sur rb.linearVelocity (monde) puis transformée
///   en repère corps Unity, application de la rotation de montage IMU et mapping Unity->VectorNav.
///   NOTE : on n'enlève PAS Physics.gravity ici — dv/dt reflète la vraie accélération du Rigidbody.
///   Soustraire Physics.gravity provoque un biais constant si la gravité Unity n'est pas active
///   pour le sous-marin (ex : flottabilité qui compense), ce qui génère de faux signaux pour le LQR.
///
/// Horodatage, covariances et format :
/// - Le message publié suit le format JSON rosbridge (topic "/vectornav/imu") avec :
///   orientation (quaternion ENU/FLU), angular_velocity (rad/s), linear_acceleration (m/s²).
/// - Des covariances par défaut sont fournies en-chaîne (modifiable selon besoin / capteur).
///
/// Recommandation de maintenance :
/// - Conserver une fonction centrale / utilitaire pour Unity→ROS (position + quaternion),
///   appeler la même logique depuis NavImuPublisher, NavPositionPublisher, TargetStatePublisher, etc.
/// - Documenter toute modification de convention d'axes dans le README du projet.
/// </summary>
public class NavImuPublisher : MonoBehaviour
{
    [Header("ROS")]
    [Tooltip("Topic ROS2 sensor_msgs/Imu. Utiliser le même nom que les nodes ROS2 attendent.")]
    public string topic = "/vectornav/imu";

    [Tooltip("Frame id utilisée dans le header IMU.")]
    public string frameId = "vectornav_imu";

    [Tooltip("Fréquence de publication IMU en Hz (driver VectorNav : 40 Hz par défaut)")]
    public float publishRateHz = 50f;

    [Header("IMU mounting")]
    [Tooltip("Si vrai, l'IMU simulée est montée 'face-down' (rotation 180° autour de X).")]
    public bool imuFaceDown = false;

    private Rigidbody rb;
    private float timer = 0f;
    private uint seq = 0;
    private Vector3 lastVelocity;
    private bool firstFrame = true;
    private bool topicAdvertised = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[NavImuPublisher] Rigidbody manquant !");
            return;
        }

        // Initialisation de la vitesse précédente pour le calcul dv/dt
        lastVelocity = rb.linearVelocity;

        TryAdvertiseTopic();

        Debug.Log($"[NavImuPublisher] Prêt — publication IMU simulée ROS ENU/FLU sur {topic}.");
    }

    void FixedUpdate()
    {
        if (SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected || rb == null)
            return;

        TryAdvertiseTopic();

        timer += Time.fixedDeltaTime;
        if (timer < 1f / publishRateHz) return;
        timer = 0f;

        PublishImu();
    }

    void TryAdvertiseTopic()
    {
        if (topicAdvertised)
            return;

        if (SimpleRosSocket.Instance != null)
        {
            SimpleRosSocket.Instance.AdvertiseTopic(topic, "sensor_msgs/Imu");
            topicAdvertised = true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CONVERSIONS UTILITAIRES (centraliser si besoin)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Convertit un vecteur exprimé en repère Unity world vers ROS ENU world.
    /// Mapping : x_enu = x_unity, y_enu = z_unity, z_enu = y_unity.
    /// </summary>
    static Vector3 WorldUnityToRosENU(Vector3 v)
    {
        return new Vector3(v.x, v.z, v.y);
    }

    /// <summary>
    /// Convertit une rotation Unity (body->world) en quaternion ROS ENU/FLU.
    /// Méthode robuste : reconstruit les axes body ROS dans le monde ROS.
    /// </summary>
    static Quaternion UnityRotToRosENUFLU(Quaternion qUnity)
    {
        Vector3 forwardENU = WorldUnityToRosENU(qUnity * Vector3.forward);
        Vector3 leftENU = WorldUnityToRosENU(qUnity * -Vector3.right);
        Vector3 upENU = WorldUnityToRosENU(qUnity * Vector3.up);

        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(forwardENU.x, forwardENU.y, forwardENU.z, 0f));
        m.SetColumn(1, new Vector4(leftENU.x, leftENU.y, leftENU.z, 0f));
        m.SetColumn(2, new Vector4(upENU.x, upENU.y, upENU.z, 0f));

        Quaternion q = m.rotation;
        q.Normalize();
        return q;
    }

    /// <summary>
    /// Rotation fixe représentant le montage IMU dans le repère corps.
    /// Ex : faceDown = rotation 180° autour de X (retourne l'IMU).
    /// </summary>
    static Quaternion GetImuMountingRotation(bool faceDown)
    {
        return faceDown ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLICATION IMU
    // ─────────────────────────────────────────────────────────────────────────

    void PublishImu()
    {
        Quaternion qMount = GetImuMountingRotation(imuFaceDown);

        // Orientation : appliquer la rotation de montage puis convertir en quaternion ROS ENU/FLU
        Quaternion qUnity = transform.rotation;
        Quaternion qImuUnity = qUnity * qMount;
        Quaternion qRos = UnityRotToRosENUFLU(qImuUnity);

        // Vitesse angulaire : rb.angularVelocity (monde) -> body Unity -> body IMU
        Vector3 omegaBody = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 omegaImu = qMount * omegaBody;

        // Mappage cohérent Unity/IMU -> ROS body FLU
        float angularX = omegaImu.z;   // forward
        float angularY = -omegaImu.x;  // left
        float angularZ = omegaImu.y;   // up

        // Accélération linéaire : calcul dv/dt à partir de rb.linearVelocity (monde)
        // Remarque importante : on n'enlève pas Physics.gravity ici. dv/dt reflète la vraie
        // accélération du Rigidbody dans la simulation (utile quand la flottabilité compense la gravité).
        Vector3 accWorld = Vector3.zero;
        if (!firstFrame)
        {
            accWorld = (rb.linearVelocity - lastVelocity) / Time.fixedDeltaTime;
        }
        else
        {
            firstFrame = false;
        }
        lastVelocity = rb.linearVelocity;

        // Accélération en repère corps Unity
        Vector3 accBodyUnity = transform.InverseTransformDirection(accWorld);

        // Appliquer la rotation de montage IMU (body -> imu)
        Vector3 accImu = qMount * accBodyUnity;

        // Mapper Unity/IMU axes -> ROS body FLU
        float ax = accImu.z;
        float ay = -accImu.x;
        float az = accImu.y;

        // Timestamp
        double now = Time.timeAsDouble;
        uint secs = (uint)now;
        uint nsecs = (uint)((now - secs) * 1e9);

        // Covariances (valeurs par défaut, ajustables selon bruit/simu)
        string orientation_cov = "[0.0001,0,0,0,0.0001,0,0,0,0.0001]";
        string angular_cov = "[0.0001,0,0,0,0.0001,0,0,0,0.0001]";
        string accel_cov = "[0.001,0,0,0,0.001,0,0,0,0.001]";

        // Construction JSON rosbridge
        string msg =
        "{\"op\":\"publish\"," +
         "\"topic\":\"" + topic + "\"," +
         "\"msg\":{" +
           "\"header\":{" +
             "\"stamp\":{\"sec\":" + secs + ",\"nanosec\":" + nsecs + "}," +
             "\"frame_id\":\"" + frameId + "\"" +
           "}," +
           "\"orientation\":{" +
             "\"x\":" + qRos.x.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"y\":" + qRos.y.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"z\":" + qRos.z.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"w\":" + qRos.w.ToString("F6", CultureInfo.InvariantCulture) +
           "}," +
           "\"orientation_covariance\":" + orientation_cov + "," +
           "\"angular_velocity\":{" +
             "\"x\":" + angularX.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"y\":" + angularY.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"z\":" + angularZ.ToString("F6", CultureInfo.InvariantCulture) +
           "}," +
           "\"angular_velocity_covariance\":" + angular_cov + "," +
           "\"linear_acceleration\":{" +
             "\"x\":" + ax.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"y\":" + ay.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"z\":" + az.ToString("F6", CultureInfo.InvariantCulture) +
           "}," +
           "\"linear_acceleration_covariance\":" + accel_cov +
         "}}";

        SimpleRosSocket.Instance.Send(msg);
    }
}
