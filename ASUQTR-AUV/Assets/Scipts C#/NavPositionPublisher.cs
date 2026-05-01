using System.Globalization;
using UnityEngine;

/// <summary>
/// Publie la position NED relative du sous-marin vers le topic ROS configuré (ex: /nav_node/position).
///
/// Rôle :
///— Fournir une publication périodique de la position du Rigidbody en convention NED,
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
/// - JSON équivalent à geometry_msgs/Point embarqué sous "point", header.frame_id = "map".
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
    [Tooltip("Topic rosbridge qui recevra la position (geometry_msgs/Point style).")]
    public string topic = "/nav_node/position";

    [Tooltip("Intervalle entre deux publications (s). Par exemple 0.1 = 10 Hz.")]
    public float publishInterval = 0.1f;

    // Origine NED capturée au démarrage (équivalent à initial_position / reset_odom)
    private Vector3 originNED;
    private float timer = 0f;

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

    void CaptureOrigin()
    {
        Vector3 pos = rb.position;
        originNED = UnityPositionToNED(pos);
        Debug.Log($"[NavPositionPublisher] Origine NED capturée : {originNED}");
    }

    void Update()
    {
        // Vérifications : socket ROS active et Rigidbody présent
        if (SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected || rb == null)
            return;

        // Contrôle de fréquence
        timer += Time.deltaTime;
        if (timer < publishInterval) return;
        timer = 0f;

        PublishPosition();
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

        // Construction JSON rosbridge (geometry_msgs/Point style dans "point")
        string msg =
            "{\"op\":\"publish\"," +
             "\"topic\":\"" + topic + "\"," +
             "\"msg\":{" +
               "\"header\":{" +
                 "\"frame_id\":\"map\"" +
               "}," +
               "\"point\":{" +
                 "\"x\":" + dx.ToString("F6", CultureInfo.InvariantCulture) + "," +
                 "\"y\":" + dy.ToString("F6", CultureInfo.InvariantCulture) + "," +
                 "\"z\":" + dz.ToString("F6", CultureInfo.InvariantCulture) +
               "}" +
             "}}";

        SimpleRosSocket.Instance.Send(msg);
    }
}