using UnityEngine;



/// ============================================================================
/// ThrusterApplier
/// ============================================================================
/// BUT :
/// Ce script applique les forces de propulsion (poussée) au sous-marin dans Unity
/// en utilisant les commandes moteurs reçues (par ex. via ROS -> ThrusterReceiver).
///
/// RÔLE ET RESPONSABILITÉS :
/// - Lire les consignes normalisées de chaque moteur (array `motorThrottles` fourni par
///   `ThrusterReceiver` qui écoute le topic ROS "/thruster_cmd").
/// - Convertir chaque consigne en poussée cible (Newton) via `ThrusterModel`.
/// - Simuler la dynamique moteur (lag / constante de temps) avec un modèle du 1er ordre
///   pour rendre la réponse plus réaliste (la poussée ne change pas instantanément).
/// - Appliquer la force résultante au `Rigidbody` du sous-marin à la position et dans la
///   direction spécifiées par chaque `ThrusterPoint` (force appliquée via AddForceAtPosition).
///
/// REMARQUES D'INTÉGRATION UNITY → ROS :
/// - Conventions et unités :
///     * Les commandes reçues côté ROS doivent être normalisées dans [-1, 1].
///     * `ThrusterModel.CommandToForceN(cmd)` retourne une force en Newton (N).
///     * `ThrusterPoint.ThrustDirection` est une direction en coordonnées MONDE (Unity).
///     * `ThrusterPoint.ThrustPosition` est une position en coordonnées MONDE (Unity).
/// - Ordre / mapping des thrusters : l'index i dans `receiver.motorThrottles[i]` doit correspondre
///   à `thrusters[i].thrusterIndex` côté scène / description matériel. Documentez et conservez
///   cet ordre cohérent entre la configuration Unity et les messages ROS pour éviter des
///   inversions de moteur (e.g. poussée appliquée au mauvais propulseur).
/// - Ce composant NE publie PAS vers ROS — il applique localement la physique. Les commandes
///   doivent provenir d'un `ThrusterReceiver` qui remplit `motorThrottles` à partir du réseau.
/// - L'intégration correcte avec le contrôleur ROS suppose que : IMU, position, vitesse et
///   commandes utilisent la même convention d'axes (Unity→NED) documentée globalement.
///
/// PRINCIPE DU MODÈLE DYNAMIQUE (1er ordre) :
///   dF/dt = (F_target - F_current) / tau
/// Discrétisation utilisée (réponse exponentielle) : alpha = 1 - exp(-dt / tau)
/// current = Lerp(current, target, alpha)
/// tau typique pour T200 : ~0.05 → 0.1 s (ajuster selon besoin).
///
/// EXIGENCES EN SCÈNE :
/// - Attacher ce script au GameObject principal du sous-marin.
/// - Ce GameObject doit contenir :
///     • un Rigidbody (référence `submarine`),
///     • un composant `ThrusterReceiver` (qui reçoit les consignes ROS),
///     • un tableau `thrusters` (objets `ThrusterPoint`) décrivant position/direction des moteurs.
///
/// REMARQUES DE MAINTENANCE :
/// - Garder la logique de mapping des indices et des axes bien documentée (README).
/// - Si vous changez la façon de représenter les consignes (plage, deadband), adaptez
///   `ThrusterModel`, `ThrusterReceiver` et les tests automatisés en conséquence.
/// ============================================================================

public class ThrusterApplier : MonoBehaviour
{
    [Tooltip("Rigidbody du sous-marin sur lequel appliquer les forces (physique Unity).")]
    public Rigidbody submarine;

    [Tooltip("Liste des points de poussée (position + direction) configurés dans la scène.")]
    public ThrusterPoint[] thrusters;

    [Header("Dynamique du moteur")]
    [Tooltip("Activer/désactiver la simulation du délai moteur (1er ordre).")]
    public bool useMotorLag = true;

    [Tooltip("Constante de temps tau du moteur en secondes. Plus petit = réponse plus rapide.")]
    public float motorTimeConstant = 0.07f;

    // Référence au composant qui reçoit les commandes (populé côté ROS)
    private ThrusterReceiver receiver;

    // Force actuelle appliquée par chaque thruster (mémoire du système).
    // Taille initiale fixe : adaptée à 8 thrusters par défaut. Si votre configuration diffère,
    // vous pouvez gérer dynamiquement la taille sur Awake/Start.
    private float[] currentForcesN;

    void Awake()
    {
        receiver = GetComponent<ThrusterReceiver>();

        // Allocation initiale (8 par défaut, garder compatibilité avec le driver ROS attendu).
        currentForcesN = new float[8];
    }

    void OnDisable()
    {
        // Un ThrusterApplier désactivé est un vrai test "thrusters off".
        // Efface aussi la mémoire du lag pour éviter une reprise avec une force résiduelle.
        if (currentForcesN == null)
            return;

        for (int i = 0; i < currentForcesN.Length; i++)
            currentForcesN[i] = 0f;
    }

    void FixedUpdate()
    {
        // Sécurité : vérifie que tous les éléments requis sont présents
        if (receiver == null || submarine == null || thrusters == null)
            return;

        // On ne parcourt que le nombre minimal d'éléments disponibles pour éviter les OOB.
        // Si ROS publie sub_interfaces/ThrusterCommand.efforts, les valeurs sont déjà en Newtons.
        int receiverCount = receiver.HasEffortsInNewtons
            ? receiver.motorEffortsN.Length
            : receiver.motorThrottles.Length;
        int count = Mathf.Min(thrusters.Length, receiverCount);

        if (currentForcesN == null || currentForcesN.Length < count)
            currentForcesN = new float[count];

        for (int i = 0; i < count; i++)
        {
            float targetForceN;
            if (receiver.HasEffortsInNewtons)
            {
                // ROS control_node publie déjà les efforts en Newtons.
                targetForceN = receiver.motorEffortsN[i];
            }
            else
            {
                // Compatibilité avec les commandes normalisées locales/legacy.
                float cmd = receiver.motorThrottles[i];
                targetForceN = ThrusterModel.CommandToForceN(cmd);
            }

            float appliedForceN;

            // 3. Application du modèle dynamique (lag moteur 1er ordre)
            if (useMotorLag)
            {
                // Protection : constante de temps minimale pour éviter division par zéro / comportements instables
                float tau = Mathf.Max(0.001f, motorTimeConstant);

                // Discrétisation exponentielle équivalente au filtre du 1er ordre
                float alpha = 1f - Mathf.Exp(-Time.fixedDeltaTime / tau);

                // Mise à jour progressive de la force courante vers la cible
                currentForcesN[i] = Mathf.Lerp(currentForcesN[i], targetForceN, alpha);

                appliedForceN = currentForcesN[i];
            }
            else
            {
                // Réponse instantanée (pas de dynamique)
                currentForcesN[i] = targetForceN;
                appliedForceN = targetForceN;
            }

            // Évite d’appliquer des forces négligeables (bruit numérique)
            if (Mathf.Abs(appliedForceN) < 0.001f)
                continue;

            // Récupération du ThrusterPoint configuré (position & direction en monde)
            ThrusterPoint t = thrusters[i];

            // 4. Calcul de la force vectorielle et application au Rigidbody à la position correcte.
            //    ThrustDirection est supposée être un vecteur unitaire en coordonnées MONDE.
            Vector3 force = t.ThrustDirection * appliedForceN;

            // Applique la force à la position du thruster — génère aussi un couple si la force
            // n'est pas appliquée au centre de masse (effet réaliste).
            submarine.AddForceAtPosition(
                force,
                t.ThrustPosition,
                ForceMode.Force
            );
        }
    }
}
