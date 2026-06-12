using UnityEngine;


/// <summary>
/// Entrée de test des propulseurs (keyboard) — uniquement pour debug local.
/// 
/// But :
///— Fournir un moyen simple de piloter un thruster depuis l'éditeur (W/S)
///— Permettre de tester la chaîne : ThrusterReceiver->ThrusterApplier / ThrusterModel->physique
///
/// Règles d'intégration Unity → ROS :
/// - Ce script NE publie PAS vers ROS. Il écrit directement dans `receiver.motorThrottles[]`
///   pour simuler des commandes reçues. Pour un usage réel, les commandes doivent provenir
///   du topic ROS "/thruster_cmd" et être traitées par `ThrusterReceiver`.
/// - `ThrusterReceiver.motorThrottles` doit contenir 8 éléments (0..7). Ici on modifie
///   l'index 0 à titre d'exemple. Adaptez le mapping aux indices de vos thrusters.
/// - Les consignes attendues sont normalisées dans l'intervalle [-1, 1] (1 = pleine poussée avant,
///   -1 = pleine poussée arrière). Respectez cette convention pour la cohérence avec le contrôleur.
/// 
/// Utilisation :
/// - Attacher ce composant à un GameObject dans la scène et lier `receiver` dans l'Inspector.
/// - Désactiver / retirer ce script en cas d'utilisation réelle via ROS pour éviter des conflits.
///
/// Remarque : ce fichier est volontairement minimal. Pour des tests plus avancés, envisagez
/// d'ajouter un mapping joystick, un lissage des commandes et un mode "override" explicite.
/// </summary>
public class ThrusterTestInput : MonoBehaviour
{
    [Tooltip("Référence au ThrusterReceiver qui contient le tableau motorThrottles.")]
    public ThrusterReceiver receiver;

    void Update()
    {
        if (receiver == null)
        {
            // Avertissement unique si la référence n'est pas assignée (pour éviter spam)
            Debug.LogWarning("[ThrusterTestInput] Référence 'receiver' non assignée. Assignez ThrusterReceiver dans l'Inspector.");
            enabled = false;
            return;
        }

        // Exemple simple : on pilote uniquement le thruster index 0 avec W/S
        // Valeurs normalisées attendues : [-1..1]
        receiver.motorThrottles[0] = 0f;

        if (Input.GetKey(KeyCode.W))
            receiver.motorThrottles[0] = 0.8f;  // poussée avant (80%)

        if (Input.GetKey(KeyCode.S))
            receiver.motorThrottles[0] = -0.8f; // poussée arrière (80%)
    }
}
