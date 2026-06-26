using UnityEngine;


/// <summary>
/// Contrôleur de cible pilotable au clavier (usage éditeur / tests).
///
/// Description :
/// - Permet de déplacer un GameObject "target" dans la scène en se basant sur l'orientation
///   de la caméra (déplacement horizontal relatif à la caméra + mouvement vertical).
/// - Conçu pour positionner une cible que le contrôleur (ex. LQR) doit suivre.
/// - NE PAS confondre ce script avec un composant de contrôle du sous-marin : il déplace
///   uniquement l'objet cible (ex. Target) que l'on publie ensuite vers ROS via TargetStatePublisher.
///
/// Commandes clavier par défaut :
/// - W / S : avancer / reculer (selon l'axe avant de la caméra)
/// - A / D : gauche / droite (selon l'axe droit de la caméra)
/// - Flèche Haut / Bas : monter / descendre (axe Y monde)
///
/// Intégration Unity → ROS (bonnes pratiques) :
/// - Si vous publiez la position cible vers ROS (TargetStatePublisher), assurez‑vous que ce
///   GameObject est bien celui référencé par le publisher pour éviter des incohérences.
/// - Pour garder la cohérence des conventions d'axes Unity→NED, ne modifiez pas la logique
///   de déplacement ici pour "corriger" des signes — gérez la conversion centralement dans les publishers.
/// - Ce script est utile localement en simulation pour positionner une cible visuelle et tester
///   la boucle de contrôle ROS sans interface externe.
///
/// Pérennité :
/// - Les commentaires ci‑dessous expliquent le rôle de chaque champ public afin que
///   les futurs contributeurs comprennent rapidement l'usage et l'intégration.
/// </summary>
public class TargetController : MonoBehaviour
{
    [Header("Mouvement")]
    [Tooltip("Vitesse de déplacement horizontal (m/s) pour la cible.")]
    public float moveSpeed = 5f;

    [Tooltip("Vitesse de déplacement vertical (m/s) pour la cible.")]
    public float verticalSpeed = 3f;

    [Header("Références")]
    [Tooltip("Transform de la caméra servant de référence pour les directions avant/droite. " +
             "Si null, la direction monde sera utilisée (moins ergonomique).")]
    public Transform cameraTransform;

    void Update()
    {
        // Si aucune caméra de référence, on ne bouge pas (évite comportements imprévus)
        if (cameraTransform == null) return;

        // Entrées clavier brutes (simple contrôle 100% clavier)
        float h = 0f;
        float v = 0f;

        // Avancer / Reculer (W / S)
        if (Input.GetKey(KeyCode.W)) v = 1f;
        if (Input.GetKey(KeyCode.S)) v = -1f;

        // Gauche / Droite (A / D)
        if (Input.GetKey(KeyCode.A)) h = -1f;
        if (Input.GetKey(KeyCode.D)) h = 1f;

        // Calcul des vecteurs directionnels basés sur la caméra (ignorant la composante Y)
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // On annule la composante verticale pour se déplacer seulement sur le plan horizontal
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        // Direction de déplacement combinée et normalisée pour éviter d'accélérer en diagonale
        Vector3 moveDir = (forward * v + right * h).normalized;

        // Application du mouvement horizontal (espace monde)
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        // Mouvements verticaux indépendants (flèches haut/bas) — appliqués en espace monde (Y up)
        if (Input.GetKey(KeyCode.UpArrow))
            transform.position += Vector3.up * verticalSpeed * Time.deltaTime;

        if (Input.GetKey(KeyCode.DownArrow))
            transform.position += Vector3.down * verticalSpeed * Time.deltaTime;
    }
}