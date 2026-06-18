using UnityEngine;


/// <summary>
/// Gestion simple des modes de caméra (visuel) — bascule entre regarder la cible LQR ou le sous‑marin.
/// 
/// Rôle :
/// — Fournir un toggle utilisateur pour la caméra de suivi (touche M) afin de passer rapidement
///   du suivi de la `target` (cible du LQR) au suivi du `submarine` (visualisation).
/// — Ce composant orchestre uniquement la référence `target` du composant `CameraFollow`
/// 
/// Champs publics :
/// — `camFollow`         : référence au composant `CameraFollow` attaché à la caméra (Main Camera).
/// — `targetTarget`      : Transform de la cible LQR (objet que le contrôleur cherche à atteindre).
/// — `submarineTarget`   : Transform du sous‑marin (objet physique).
///
/// Comportement :
/// — Au démarrage, la caméra utilise le mode courant (par défaut : regarder la `target`).
/// — Appuyer sur `M` inverse l'état (`lookAtSub`) et met à jour `camFollow.target`.
/// — Le changement est uniquement visuel ; il ne modifie pas l'état du simulateur ni les publishers ROS.
///
/// Intégration Unity → ROS (bonnes pratiques) :
/// — `CameraFollow` et `ModeManager` sont des composants purement visuels. Ils ne doivent pas
///   être attachés au sous‑marin si le sous‑marin publie ensuite son orientation/état vers ROS
///   (IMU, NavImuPublisher, TargetStatePublisher). Modifier la rotation d'un GameObject qui
///   sert de source de vérité pour les publishers peut générer des incohérences côté contrôleur.
/// — Séparer les responsabilités :
///     • `CameraFollow` / `ModeManager` : uniquement affichage / ergonomie dans l'éditeur / debug.
///     • Publishers (IMU, position, vitesse, target) : fournir les données envoyées à ROS.
/// — Documenter dans le README quelle Transform est publiée par chaque publisher afin d'éviter
///   d'attacher par erreur un script visuel au GameObject qui alimente ROS.
///
/// Exemple d'utilisation :
/// — Assigner `CameraFollow` (Main Camera) dans `camFollow`.
/// — Assigner `targetTarget` (objet Target) et `submarineTarget` (GameObject du sub).
/// — En Play Mode, appuyer sur `M` pour basculer la caméra entre la cible et le sous‑marin.
///
/// Notes d'extension :
/// — On peut enrichir la logique pour verrouiller certains axes de la caméra, pour animer la transition
///   ou pour exposer un événement Unity `OnModeChanged` pour que d'autres composants réagissent.
/// </summary>
public class ModeManager : MonoBehaviour
{
    [Header("Camera")]
    public CameraFollow camFollow;
    public Transform targetTarget;     // cible LQR
    public Transform submarineTarget;  // sub

    private bool lookAtSub = false; // false = target, true = sub

    void Start()
    {
        ApplyCameraMode();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            lookAtSub = !lookAtSub;
            ApplyCameraMode();
        }
    }

    void ApplyCameraMode()
    {
        if (!camFollow)
            return;

        camFollow.target = lookAtSub ? submarineTarget : targetTarget;

        Debug.Log(
            lookAtSub
                ? "[ModeManager] Camera → SUB"
                : "[ModeManager] Camera → TARGET"
        );
    }
}