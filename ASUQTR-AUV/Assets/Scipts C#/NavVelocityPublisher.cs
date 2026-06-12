using System.Globalization;
using UnityEngine;


/// <summary>
/// Publie optionnellement la vitesse linéaire du sous-marin pour debug Unity.
///
/// But :
/// - Fournir un topic de debug si on veut inspecter la vitesse calculée côté Unity.
/// - La boucle de contrôle principale n'utilise plus ce publisher : la vitesse utile au LQR est déjà
///   incluse dans /odometry/filtered via NavPositionPublisher.
///
/// Conventions d'axes (important, centraliser si possible) :
/// - Repère Unity (body) : X = droite (right), Y = haut (up), Z = avant (forward)
/// - Repère VectorNav / NED body (marine) utilisé côté ROS :
///     x_vn = forward (north)  = Unity Z
///     y_vn = right   (east)   = Unity X
///     z_vn = down    (down)   = -Unity Y
///
/// Mapping implémenté ici (body Unity → body VectorNav) :
///   u = vBodyUnity.z   // composante avant (forward)
///   v = vBodyUnity.x   // composante droite (right)
///   w = -vBodyUnity.y  // composante bas (down = -up)
///
/// Remarques d'intégration Unity → ROS :
/// - Cette conversion doit être cohérente avec NavImuPublisher et NavPositionPublisher.
///   Idéalement, centraliser la logique de conversion Unity→NED dans un utilitaire partagé
///   pour éviter des inversions/signes incohérents qui cassent la boucle de contrôle.
/// - Le message publié ici contient un header et un champ `point` (x,y,z)
///   correspondant à (u,v,w) en m/s ; `frame_id` = "base_link" (repère corps du sous-marin).
/// - Fréquence de publication configurable via `publishRateHz`.
/// - Ce publisher n'envoie que la vitesse linéaire ; les vitesses angulaires sont fournies
///   par l'IMU (/vectornav/imu) si nécessaire.
///
/// Unités : m/s pour la vitesse linéaire.
/// </summary>
public class NavVelocityPublisher : MonoBehaviour
{
    [Tooltip("Laisser false en simulation de contrôle normale. /odometry/filtered contient déjà twist.")]
    public bool publishToRos = false;

    [Tooltip("Topic de debug optionnel pour la vitesse Unity.")]
    public string topic = "/debug/unity_velocity";

    [Tooltip("Fréquence de publication en Hz")]
    public float publishRateHz = 30f;

    private Rigidbody rb;
    private float timer = 0f;
    private uint seq = 0;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
            Debug.LogError("[NavVelocityPublisher] Rigidbody manquant !");
    }

    void FixedUpdate()
    {
        if (!publishToRos || SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected || rb == null)
            return;

        timer += Time.fixedDeltaTime;
        if (timer < 1f / publishRateHz) return;
        timer = 0f;

        PublishVelocity();
    }

    void PublishVelocity()
    {
        // Vitesse linéaire : rb.linearVelocity (monde) -> exprimée dans le repère corps Unity
        Vector3 vBodyUnity = transform.InverseTransformDirection(rb.linearVelocity);

        // Body Unity -> body VectorNav (NED marine body) : permutation + signe
        float u = vBodyUnity.z;    // avant / forward
        float v = vBodyUnity.x;    // droite / right
        float w = -vBodyUnity.y;   // bas / down

        double now = Time.realtimeSinceStartupAsDouble;
        uint secs = (uint)now;
        uint nsecs = (uint)((now - secs) * 1e9);

        // Message rosbridge JSON (équivalent minimal à un twist.linear ou geometry_msgs/Point)
        string msg =
            "{\"op\":\"publish\"," +
             "\"topic\":\"" + topic + "\"," +
             "\"msg\":{" +
               "\"header\":{" +
                 "\"seq\":" + seq++ + "," +
                 "\"stamp\":{\"sec\":" + secs + ",\"nanosec\":" + nsecs + "}," +
                 "\"frame_id\":\"base_link\"" +   // repère corps du sous-marin
               "}," +
               "\"point\":{" +
                 "\"x\":" + u.ToString("F6", CultureInfo.InvariantCulture) + "," +
                 "\"y\":" + v.ToString("F6", CultureInfo.InvariantCulture) + "," +
                 "\"z\":" + w.ToString("F6", CultureInfo.InvariantCulture) +
               "}" +
             "}}";

        SimpleRosSocket.Instance.Send(msg);
    }
}
