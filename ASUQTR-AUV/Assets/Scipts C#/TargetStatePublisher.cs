using System.Globalization;
using UnityEngine;


/// <summary>
/// Publie la position de la cible et (optionnellement) un yaw de pointage vers elle
/// sur les topics ROS utilisés par le node de contrôle.
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
/// - Angles : les angles d'Euler sont ambigus (ordre de rotation). Ce script ne publie
///   que le yaw (cap) calculé pour pointer vers la cible. Pour publier une attitude complète,
///   préférez convertir les quaternions Unity → NED via une utilité centrale et publier
///   le quaternion côté ROS/geometry_msgs/Quaternion.
/// </summary>
public class TargetStatePublisher : MonoBehaviour
{
    [Header("Target Object")]
    public Transform targetObject;

    [Header("ROS Settings")]
    public string positionTopic = "/control/abs_ned_pos_target";
    public string angleTopic = "/control/abs_angle_target";
    public float publishRateHz = 10f;

    [Header("Origin (optional)")]
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

        // Capturer l'origine en NED en utilisant la conversion centralisée
        originNED = UnityPositionToNED(targetObject.position);
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
        PublishTargetPosition();

        if (pointCameraAtTarget && submarineTransform != null)
            PublishTargetOrientation();
    }

    void PublishTargetPosition()
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

        // Construction du message JSON pour rosbridge (équivalent geometry_msgs/Point)
        string msg =
            "{\"op\":\"publish\"," +
             "\"topic\":\"" + positionTopic + "\"," +
             "\"msg\":{" +
               "\"x\":" + dx.ToString("F6", CultureInfo.InvariantCulture) + "," +
               "\"y\":" + dy.ToString("F6", CultureInfo.InvariantCulture) + "," +
               "\"z\":" + dz.ToString("F6", CultureInfo.InvariantCulture) +
             "}}";

        SimpleRosSocket.Instance.Send(msg);
    }

    void PublishTargetOrientation()
    {
        // Vecteur Unity du sous-marin vers la cible
        Vector3 directionUnity = targetObject.position - submarineTransform.position;

        // Conversion du vecteur direction en NED (on traite le vecteur comme une position relative)
        Vector3 directionNED = UnityPositionToNED(directionUnity);

        // Calcul du yaw en NED : atan2(east, north) -> atan2(y_ned, x_ned)
        // Résultat en radians. Interprétation : angle mesuré depuis l'axe nord (X_ned).
        float yaw = Mathf.Atan2(directionNED.y, directionNED.x);

        // Nous publions NaN pour roll/pitch pour indiquer qu'ils sont non fournis / non modifiés.
        // Si le node de contrôle exige des valeurs numériques, remplacer NaN par les valeurs voulues,
        // mais la solution robuste reste de publier un quaternion d'attitude converti Unity->NED.
        string msg =
            "{\"op\":\"publish\"," +
             "\"topic\":\"" + angleTopic + "\"," +
             "\"msg\":{" +
               "\"x\":" + float.NaN.ToString(CultureInfo.InvariantCulture) + "," +
               "\"y\":" + float.NaN.ToString(CultureInfo.InvariantCulture) + "," +
               "\"z\":" + yaw.ToString("F6", CultureInfo.InvariantCulture) +
             "}}";

        SimpleRosSocket.Instance.Send(msg);
    }
}