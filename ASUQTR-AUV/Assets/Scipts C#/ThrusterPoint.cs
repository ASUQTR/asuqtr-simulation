using UnityEngine;


/// <summary>
/// Composant à attacher à chaque point de poussée (moteur) présent sur le sous-marin.
/// 
/// Responsabilités :
/// - Exposer l'index matériel du thruster correspondant au message ROS (mapping indexé).
/// - Fournir la position et la direction de poussée en coordonnées MONDE (Unity).
/// 
/// Remarques d'intégration Unity → ROS :
/// - L'index `thrusterIndex` doit correspondre exactement à l'ordre attendu par le node ROS
///   qui publie/consomme les commandes moteurs (par ex. `ThrusterReceiver`). Documentez ce
///   mapping (README) et conservez-le invariant pour éviter des inversions/applications de force
///   sur le mauvais propulseur.
/// - `ThrustDirection` renvoie une direction en coordonnées MONDE ; la force calculée par
///   `ThrusterModel` (en Newton) doit être multipliée par ce vecteur (Unity world) avant
///   d'être appliquée au Rigidbody via `AddForceAtPosition`. Les unités sont : force en N,
///   position en mètres (Unity units), direction unitaire.
/// - Si le moteur est orienté différemment dans la scène, alignez l'axe local Z du GameObject
///   du thruster avec l'orientation "poussée avant" souhaitée. `ThrustDirection` utilise
///   `transform.forward` (axe Z local).
/// 
/// Bonnes pratiques :
/// - Ne changez pas `invertDirection` pour "corriger" un mapping d'index erroné côté ROS.
///   Corrigez plutôt la configuration/mapping d'indices ou la définition des ThrusterPoint
///   dans la scène.
/// - Garder les descriptions et l'ordre des thrusters dans la documentation du projet pour
///   faciliter la maintenance (cohérence Unity ↔ ROS).
/// </summary>
public class ThrusterPoint : MonoBehaviour
{
    [Tooltip("Index du moteur côté ROS / configuration (doit correspondre à l'ordre attendu par le receiver).")]
    public int thrusterIndex;

    [Tooltip("Si vrai, inverse la direction de poussée (utile si le modèle physique est monté à l'envers).")]
    public bool invertDirection = false;

    /// <summary>
    /// Direction de poussée en coordonnées MONDE (Unity).
    /// Par convention on base la direction sur l'axe local Z du GameObject (transform.forward).
    /// Si `invertDirection` est vrai, la direction est inversée.
    /// 
    /// Usage : appliquer la force comme `force = ThrustDirection * forceN`.
    /// </summary>
    public Vector3 ThrustDirection
    {
        get
        {
            // transform.forward = axe Z local en coord. monde
            Vector3 dir = transform.forward;
            return invertDirection ? -dir.normalized : dir.normalized;
        }
    }

    /// <summary>
    /// Position du point de poussée en coordonnées MONDE.
    /// Usage : passer cette position à AddForceAtPosition pour appliquer la force au bon emplacement.
    /// </summary>
    public Vector3 ThrustPosition
    {
        get { return transform.position; }
    }
}