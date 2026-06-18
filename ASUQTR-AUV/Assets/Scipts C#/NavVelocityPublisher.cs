using System.Globalization;
using UnityEngine;


/// <summary>
/// Publie la vitesse linéaire du sous-marin exprimée dans le repère corps VectorNav (body frame)
/// vers le topic ROS `/nav_node/velocity` (format rosbridge JSON).
///
/// But :
/// - Fournir au contrôleur ROS (LQR / nav_node) la vitesse linéaire du véhicule dans son repère corps
///   (u, v, w) en m/s, cohérente avec la convention NED utilisée pour IMU et position.
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
/// - Le message publié ici contient un header (seq, stamp) et un champ `point` (x,y,z)
///   correspondant à (u,v,w) en m/s ; `frame_id` = "base_link" (repère corps du sous-marin).
/// - Fréquence de publication configurable via `publishRateHz`.
/// - Ce publisher n'envoie que la vitesse linéaire ; les vitesses angulaires sont fournies
///   par l'IMU (/vectornav/IMU) si nécessaire.
///
/// Unités : m/s pour la vitesse linéaire.
/// </summary>
public class NavVelocityPublisher : MonoBehaviour
{
    [Tooltip("Fréquence de publication en Hz")]
    public float publishRateHz = 30f;

    private Rigidbody rb;
    private float timer = 0f;
    // seq supprimé — ROS2 Jazzy n'a plus de champ seq dans Header

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
            Debug.LogError("[NavVelocityPublisher] Rigidbody manquant !");
    }

    void FixedUpdate()
    {
        if (SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected || rb == null)
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
             "\"topic\":\"/nav_node/velocity\"," +
             "\"msg\":{" +
               "\"header\":{" +
                 "\"stamp\":{\"secs\":" + secs + ",\"nsecs\":" + nsecs + "}," +
                 "\"frame_id\":\"base_link\"" +
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