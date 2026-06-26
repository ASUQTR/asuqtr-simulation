using System.Globalization;
using UnityEngine;


/// <summary>
/// Publie la cible sous forme de geometry_msgs/PoseStamped sur le topic écouté par
/// control_node en mode lqr_tuning.
///
/// Notes d'intégration Unity → ROS (convention NED pour ce projet) :
/// - Ce composant réalise la conversion des coordonnées Unity (Unity: X=droite, Y=haut, Z=avant)
///   vers la convention NED attendue côté ROS :
///     NED.x (north) = Unity.z (forward)
///     NED.y (east)  = Unity.x (right)
///     NED.z (down)  = -Unity.y (up)
///
/// - IMPORTANT : cette conversion doit être unique et cohérente pour tout le projet
///   (IMU, nav, target, etc.). Si la convention change, modifiez uniquement la fonction
///   UnityPositionToNED() afin d'éviter des incohérences difficiles à déboguer.
///
/// - IMPORTANT : côté control_node, pose.orientation.x/y/z sont interprétés comme
///   Roll/Pitch/Yaw en degrés, pas comme un quaternion ROS standard.
/// </summary>
public class TargetStatePublisher : MonoBehaviour
{
    [Header("Target Object")]
    public Transform targetObject;

    [Header("ROS Settings")]
    public string positionTopic = "/debug/target_pose";
    public string frameId = "odom";
    public float publishRateHz = 10f;

    [Header("Origin (optional)")]
    [Tooltip("Si vrai, publie la cible relative à la position initiale du sous-marin.")]
    public bool useRelativeOrigin = true;
    private Vector3 originNED;
    private bool originCaptured = false;

    [Header("Orientation")]
    [Tooltip("Si vrai, publie un yaw pour que le sous-marin puisse orienter une caméra vers la cible")]
    public bool pointCameraAtTarget = true;

    [Tooltip("Transform du sous-marin utilisé pour calculer la direction de pointage. Si null, recherche le tag 'Player'.")]
    public Transform submarineTransform;

    private float timer = 0f;

    void Start()
    {
        if (targetObject == null)
            Debug.LogWarning("[TargetStatePublisher] Aucun targetObject assigné !");

        // Si aucune référence au sous-marin n'est fournie, tenter de la localiser par tag
        if (submarineTransform == null)
            submarineTransform = GameObject.FindWithTag("Player")?.transform;

        // Migration douce pour les scenes qui avaient encore les anciens topics.
        if (positionTopic.StartsWith("/control/"))
            positionTopic = "/debug/target_pose";

        if (useRelativeOrigin)
            CaptureOrigin();
    }

    /// <summary>
    /// Conversion centralisée Unity position -> NED position.
    /// Toutes les publications doivent utiliser cette fonction pour rester cohérentes.
    /// Mapping actuel : x_ned = Unity.z, y_ned = Unity.x, z_ned = -Unity.y.
    /// </summary>
    static Vector3 UnityPositionToNED(Vector3 unityPos)
    {
        return new Vector3(unityPos.z, unityPos.x, -unityPos.y);
    }

    void CaptureOrigin()
    {
        if (targetObject == null) return;

        // Utiliser la même origine que l'odométrie du sous-marin.
        // Ainsi, target_state - current_state reste dans un repère cohérent côté control_node.
        Transform originTransform = submarineTransform != null ? submarineTransform : targetObject;
        originNED = UnityPositionToNED(originTransform.position);
        originCaptured = true;
        Debug.Log($"[TargetStatePublisher] Origin (NED) captured: {originNED}");
    }

    public void ResetOrigin()
    {
        CaptureOrigin();
    }

    void Update()
    {
        // Vérifications rapides : socket ROS et cible valide
        if (SimpleRosSocket.Instance == null || !SimpleRosSocket.Instance.IsConnected)
            return;
        if (targetObject == null)
            return;

        // Contrôle de la fréquence de publication
        timer += Time.deltaTime;
        if (timer < 1f / publishRateHz)
            return;

        timer = 0f;
        PublishTargetPose();
    }

    void PublishTargetPose()
    {
        // Conversion Unity -> NED via l'utilitaire central
        Vector3 ned = UnityPositionToNED(targetObject.position);
        float x_ned = ned.x;
        float y_ned = ned.y;
        float z_ned = ned.z;

        // Si demandé, publier la position relative à l'origine capturée
        float dx = x_ned;
        float dy = y_ned;
        float dz = z_ned;

        if (useRelativeOrigin && originCaptured)
        {
            dx = x_ned - originNED.x;
            dy = y_ned - originNED.y;
            dz = z_ned - originNED.z;
        }

        float yawDeg = 0f;
        if (pointCameraAtTarget && submarineTransform != null)
            yawDeg = ComputeTargetYawDeg();

        // control_node/debug_target_callback lit orientation.x/y/z comme Euler NED en degres.
        string msg =
            "{\"op\":\"publish\"," +
	         "\"topic\":\"" + positionTopic + "\"," +
	         "\"msg\":{" +
               "\"header\":{\"frame_id\":\"" + frameId + "\"}," +
               "\"pose\":{" +
                 "\"position\":{" +
	               "\"x\":" + dx.ToString("F6", CultureInfo.InvariantCulture) + "," +
	               "\"y\":" + dy.ToString("F6", CultureInfo.InvariantCulture) + "," +
	               "\"z\":" + dz.ToString("F6", CultureInfo.InvariantCulture) +
                 "}," +
                 "\"orientation\":{" +
                   "\"x\":0.0," +
                   "\"y\":0.0," +
                   "\"z\":" + yawDeg.ToString("F6", CultureInfo.InvariantCulture) + "," +
                   "\"w\":1.0" +
                 "}" +
               "}" +
	         "}}";

        SimpleRosSocket.Instance.Send(msg);
    }

    float ComputeTargetYawDeg()
    {
        // Vecteur Unity du sous-marin vers la cible
        Vector3 directionUnity = targetObject.position - submarineTransform.position;

        // Conversion du vecteur direction en NED (on traite le vecteur comme une position relative)
        Vector3 directionNED = UnityPositionToNED(directionUnity);

        // Calcul du yaw en NED : atan2(east, north) -> atan2(y_ned, x_ned).
        float yaw = Mathf.Atan2(directionNED.y, directionNED.x);
        return yaw * Mathf.Rad2Deg;
    }
}
