using System.Globalization;
using UnityEngine;


/// <summary>
/// Simule un capteur IMU VectorNav et publie un message JSON compatible rosbridge.
/// 
/// But :
///— Fournir une publication IMU cohérente avec le driver VectorNav C++ (tf_ned_to_enu = false).
///— Permettre d'alimenter le contrôleur ROS (LQR / control_node) avec orientation,
///  vitesses angulaires (p, q, r) et accélération linéaire corps (LINEARACCELBODY).
///
/// Conventions et intégration Unity -> ROS (NED)
/// - Répères Unity (monde/body) : X = droite, Y = haut, Z = avant.
/// - Répères VectorNav / NED attendu côté ROS :
///     x_vn (north / forward) = forward corps  = Unity Z
///     y_vn (east  / right)   = right corps    = Unity X
///     z_vn (down  )          = down (bas)     = -Unity Y
/// - Il est impératif d'utiliser la même transformation pour :
///     * la conversion d'orientation (quaternion),
///     * la conversion des vitesses angulaires (body rates),
///     * la conversion des accélérations linéaires.
///   Toute incohérence casse les boucles de contrôle (LQR) — centralisez la conversion Unity→NED.
///
/// Remarques importantes sur les choix implémentés :
/// - Orientation : reconstruite en NED via `UnityRotToNEDQuat(Quaternion)` (méthode robuste).
/// - Montage IMU (option face-down) : appliqué comme rotation fixe body->IMU (180° autour de X).
///   Cette rotation est appliquée à l'orientation, aux vitesses angulaires et aux accélérations.
/// - Vitesses angulaires : on transforme d'abord rb.angularVelocity en repère corps Unity,
///   puis on applique la rotation de montage IMU. Enfin on permute/sign-flip pour VectorNav :
///     p = omega_imu.z  (roll = forward)
///     q = omega_imu.x  (pitch = right)
///     r = -omega_imu.y (yaw = down)  <-- inversion unique requise
///   Cette permutation correspond exactement à la reconstruction d'axes utilisée pour le quaternion.
/// - Accélération linéaire : calculée comme dv/dt sur rb.linearVelocity (monde) puis transformée
///   en repère corps Unity, application de la rotation de montage IMU et mapping Unity->VectorNav.
///   NOTE : on n'enlève PAS Physics.gravity ici — dv/dt reflète la vraie accélération du Rigidbody.
///   Soustraire Physics.gravity provoque un biais constant si la gravité Unity n'est pas active
///   pour le sous-marin (ex : flottabilité qui compense), ce qui génère de faux signaux pour le LQR.
///
/// Horodatage, covariances et format :
/// - Le message publié suit le format JSON rosbridge (topic "/vectornav/IMU") avec :
///   orientation (quaternion NED), angular_velocity (p,q,r en rad/s), linear_acceleration (m/s²).
/// - Des covariances par défaut sont fournies en-chaîne (modifiable selon besoin / capteur).
///
/// Recommandation de maintenance :
/// - Conserver une fonction centrale / utilitaire pour Unity→NED (position + quaternion),
///   appeler la même logique depuis NavImuPublisher, NavPositionPublisher, TargetStatePublisher, etc.
/// - Documenter toute modification de convention d'axes dans le README du projet.
/// </summary>
public class NavImuPublisher : MonoBehaviour
{
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

        Debug.Log("[NavImuPublisher] Prêt — frame VectorNav NED body, tf_ned_to_enu=false.");
    }

    void FixedUpdate()
    {
        if (SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected || rb == null)
            return;

        timer += Time.fixedDeltaTime;
        if (timer < 1f / publishRateHz) return;
        timer = 0f;

        PublishImu();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CONVERSIONS UTILITAIRES (centraliser si besoin)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Convertit un vecteur exprimé en repère Unity world → vecteur NED world.
    /// Mapping position/direction : x_ned = z_unity, y_ned = x_unity, z_ned = -y_unity.
    /// </summary>
    static Vector3 WorldUnityToNED(Vector3 v)
    {
        return new Vector3(
             v.z,
             v.x,
            -v.y
        );
    }

    /// <summary>
    /// Convertit une rotation Unity (body->world) en quaternion NED (body->world NED).
    /// Méthode robuste : reconstruit les axes du corps puis compose la matrice NED.
    /// </summary>
    static Quaternion UnityRotToNEDQuat(Quaternion qUnity)
    {
        Vector3 fwd = qUnity * Vector3.forward;
        Vector3 rght = qUnity * Vector3.right;
        Vector3 down = qUnity * -Vector3.up;

        Vector3 c0 = WorldUnityToNED(fwd);
        Vector3 c1 = WorldUnityToNED(rght);
        Vector3 c2 = WorldUnityToNED(down);

        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, new Vector4(c0.x, c0.y, c0.z, 0f));
        m.SetColumn(1, new Vector4(c1.x, c1.y, c1.z, 0f));
        m.SetColumn(2, new Vector4(c2.x, c2.y, c2.z, 0f));

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

        // Orientation : appliquer la rotation de montage puis convertir en quaternion NED
        Quaternion qUnity = transform.rotation;
        Quaternion qImuUnity = qUnity * qMount;
        Quaternion qNED = UnityRotToNEDQuat(qImuUnity);

        // Vitesse angulaire : rb.angularVelocity (monde) -> body Unity -> body IMU
        Vector3 omegaBody = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 omegaImu = qMount * omegaBody;

        // Mappage cohérent Unity/IMU -> VectorNav (NED body)
        float p = omegaImu.z;   // roll  (x_vn = forward)
        float q = omegaImu.x;   // pitch (y_vn = right)
        float r = -omegaImu.y;  // yaw   (z_vn = down)  <-- unique inversion nécessaire

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

        // Mapper Unity/IMU axes -> VectorNav body (forward/right/down)
        float ax = accImu.z;
        float ay = accImu.x;
        float az = -accImu.y;

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
         "\"topic\":\"/vectornav/IMU\"," +
         "\"msg\":{" +
           "\"header\":{" +
             "\"seq\":" + seq++ + "," +
             "\"stamp\":{\"secs\":" + secs + ",\"nsecs\":" + nsecs + "}," +
             "\"frame_id\":\"vectornav\"" +
           "}," +
           "\"orientation\":{" +
             "\"x\":" + qNED.x.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"y\":" + qNED.y.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"z\":" + qNED.z.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"w\":" + qNED.w.ToString("F6", CultureInfo.InvariantCulture) +
           "}," +
           "\"orientation_covariance\":" + orientation_cov + "," +
           "\"angular_velocity\":{" +
             "\"x\":" + p.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"y\":" + q.ToString("F6", CultureInfo.InvariantCulture) + "," +
             "\"z\":" + r.ToString("F6", CultureInfo.InvariantCulture) +
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