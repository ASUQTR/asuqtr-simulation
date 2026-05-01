using UnityEngine;

// ---------------------------------------------------------------------------
// CameraFollow
// ---------------------------------------------------------------------------
// But :
// - Composant minimal qui fait pivoter une caméra pour regarder une cible (Transform).
// - Utiliser la caméra principale pour suivre visuellement un objet de la scène.
//
// Règles d'utilisation / pérennité :
// - Ce script DOIT être attaché à une Camera (ex : Main Camera). Il ne doit PAS être
//   attaché à l'objet suivi (ex : le sous‑marin) — sinon la caméra forcera la rotation
//   de l'objet suivi et créera des comportements indésirables.
// - Assigner la Transform `target` dans l'Inspector (objet à regarder).
// - La rotation s'effectue dans LateUpdate() afin de s'exécuter après la physique et
//   éviter les micro-sauts lorsque l'objet suivi bouge en FixedUpdate().
//
// Intégration Unity → ROS (remarques importantes) :
// - Ce composant n'envoie rien vers ROS et n'affecte pas directement les publishers/subscribers.
// - Si vous publiez l'orientation via un publisher (IMU, TargetStatePublisher, ...), gardez
//   en tête que les modifications de rotation d'un GameObject par un script de suivi peuvent
//   interférer avec les lectures/contrôles si le script est attaché au mauvais objet.
//   Exemple de mauvaise pratique : attacher CameraFollow au sous-marin (le sub se fera
//   réorienter par la caméra). Pour la cohérence du système, séparez les responsabilités :
//     • CameraFollow -> uniquement visuel
//     • NavImuPublisher / TargetStatePublisher -> publication ROS
//
// Conseils de maintenance :
// - Préférer LateUpdate() pour le comportement de caméra (déjà utilisé ici).
// - Si vous avez besoin de verrouiller certains axes (ex : empêcher le roll), effectuez
//   un filtrage explicite sur les euler/local axes avant d'appliquer la rotation.
// - Documenter dans le README l'usage de ce composant et lier le mapping camera ↔ publishers
//   dans la documentation du projet pour éviter les erreurs futures.
//
// ---------------------------------------------------------------------------
// Composant de suivi de cible pour une caméra.

public class CameraFollow : MonoBehaviour
{
    [Header("Références")]
    public Transform target;          // Transform de la cible à regarder (à assigner dans l'Inspector)
    public float rotationSpeed = 2.0f; // Vitesse de rotation (smoothing)

    void LateUpdate()
    {
        if (target == null) return;

        // Calcul de la direction vers la cible (vecteur monde)
        Vector3 direction = target.position - transform.position;

        // Construire la rotation qui regarde vers la cible en conservant 'up' = Y monde
        Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);

        // Interpolation douce vers la rotation cible (évite les à-coups)
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
    }
}