using UnityEngine;


/// <summary>
/// Ajoute la poussée d'Archimède et les moments de rappel résultant d'un
/// décalage entre le centre de gravité (CG) et le centre de poussée (CB).
///
/// Objectifs :
/// - Soutenir le poids du véhicule dans l'eau
/// - Créer un comportement réaliste de rappel en roulis/tangage
/// - Permettre un réglage fin de la flottabilité (positive / neutre / négative)
///
/// Utilisation recommandée :
/// - La masse et les inerties du Rigidbody doivent être réalistes
/// - `Hydrodynamics.cs` gère les forces de traînée
/// - Les propulseurs gèrent la propulsion
///
/// Remarques importantes (intégration Unity -> ROS et conventions) :
/// - Unity utilise Y-up (Y vers le haut).
/// - Ce script suppose que l'axe avant du sous-marin est +Z,
///   l'axe droite est +X et l'axe haut est +Y (repère local Unity).
/// - `localCenterOfBuoyancy` et `localCenterOfGravity` sont exprimés en
///   coordonnées locales du sous-marin (repère corps).
/// - Ce composant n'émet PAS directement vers ROS. Cependant, pour conserver
///   la cohérence dans tout le projet, les publishers (IMU, position, orientation)
///   doivent utiliser la même convention d'axes / conversion Unity→NED que celle
///   définie centralement (ex : helper UnityPositionToNED / UnityRotToNEDQuat).
///   Modifier la convention d'axes ici sans mettre à jour les publishers provoquera
///   des incohérences difficiles à diagnostiquer côté contrôleur (LQR).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Buoyancy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Geometry")]
    [Tooltip("Approximate displaced volume in m^3.")]
    [SerializeField] private float displacedVolume = 0.0239f;

    [Tooltip("Water density in kg/m^3. Fresh water ~1000, pool water ~997-1000.")]
    [SerializeField] private float waterDensity = 1000f;

    [Tooltip("Center of gravity in local coordinates (m). Usually same as Rigidbody COM.")]
    [SerializeField] private Vector3 localCenterOfGravity = Vector3.zero;

    [Tooltip("Center of buoyancy in local coordinates (m). Usually slightly above CG for passive stability.")]
    [SerializeField] private Vector3 localCenterOfBuoyancy = new Vector3(0f, 0.02f, 0f);

    [Header("Buoyancy Tuning")]
    [Tooltip("Automatically choose buoyancyScale from Rigidbody mass and displacedVolume at startup.")]
    [SerializeField] private bool autoNeutralBuoyancy = true;

    [Tooltip("1 = neutral buoyancy. Below 1 sinks slowly, above 1 floats slowly.")]
    [SerializeField] private float targetBuoyancyRatio = 1f;

    [Tooltip("Extra multiplier on buoyancy force. 1 = physical nominal value.")]
    [SerializeField] private float buoyancyScale = 1f;

    [Tooltip("Vertical damping applied at CB to calm oscillations near equilibrium.")]
    [SerializeField] private float verticalDamping = 15f;

    [Tooltip("Angular damping helper only for roll/pitch stabilization.")]
    [SerializeField] private float restoringAngularDamping = 2.5f;

    [Header("Optional Water Surface")]
    [Tooltip("If enabled, buoyancy is only applied when CB is below waterLevelY.")]
    [SerializeField] private bool useWaterSurface = false;

    [Tooltip("World Y coordinate of water surface.")]
    [SerializeField] private float waterLevelY = 0f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private const float g = 9.81f;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // Aligne le centre de masse du Rigidbody avec la valeur spécifiée ici,
        // sauf si vous gérez le centre de masse ailleurs volontairement.
        rb.centerOfMass = localCenterOfGravity;

        if (autoNeutralBuoyancy)
            SetNeutralBuoyancyScale();
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        // Position du centre de poussée (CB) et du centre de gravité (CG) en coordonnées monde
        Vector3 worldCB = transform.TransformPoint(localCenterOfBuoyancy);
        Vector3 worldCG = transform.TransformPoint(localCenterOfGravity);

        // Logique de surface optionnelle (si activée, la poussée n'est appliquée que
        // quand le CB est sous le niveau d'eau défini)
        float immersionFactor = 1f;
        if (useWaterSurface)
        {
            if (worldCB.y >= waterLevelY)
                immersionFactor = 0f;
        }

        if (immersionFactor <= 0f)
            return;

        // Poussée d'Archimède (force verticale vers le haut)
        float buoyantForceMagnitude = waterDensity * displacedVolume * g * buoyancyScale * immersionFactor;
        Vector3 buoyantForce = Vector3.up * buoyantForceMagnitude;

        // Damping vertical appliqué au CB pour amortir les oscillations (projette la vitesse locale sur Y)
        Vector3 pointVelocity = rb.GetPointVelocity(worldCB);
        Vector3 verticalDampingForce = -Vector3.Project(pointVelocity, Vector3.up) * verticalDamping;

        // Appliquer la poussée et le damping au point CB (Force + couple si CB != CG)
        rb.AddForceAtPosition(buoyantForce + verticalDampingForce, worldCB, ForceMode.Force);

        // Aide au damping angulaire pour stabiliser roulis/tangage
        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);

        // Pour éviter les erreurs de convention d'axes, on calcule les axes de roulis/tangage
        // directement en monde à partir des axes locaux du transform.
        Vector3 worldRollAxis = transform.forward; // axe de roulis : axe avant du sub
        Vector3 worldPitchAxis = transform.right;  // axe de tangage : axe droite du sub

        // Projette la vitesse angulaire du Rigidbody sur ces axes pour obtenir les taux
        float rollRate = Vector3.Dot(rb.angularVelocity, worldRollAxis);
        float pitchRate = Vector3.Dot(rb.angularVelocity, worldPitchAxis);

        // Torque d'amortissement angulaire simple (linéaire) pour calmer roulis/tangage
        Vector3 angularDampingTorque =
            (-rollRate * restoringAngularDamping * worldRollAxis) +
            (-pitchRate * restoringAngularDamping * worldPitchAxis);

        rb.AddTorque(angularDampingTorque, ForceMode.Force);

        // Remarque : le moment de rappel statique résultant du décalage CB != CG est déjà
        // produit naturellement par AddForceAtPosition; worldCG est gardé pour débogage/gizmos.
        _ = worldCG; // conservé pour lisibilité / gizmos
    }

    /// <summary>
    /// Calcul du multiplicateur de flottabilité nécessaire pour atteindre une flottabilité neutre,
    /// compte tenu de la masse actuelle, de la densité d'eau et du volume déplacé.
    /// scale = poids / poussée_nominale
    /// </summary>
    [ContextMenu("Compute Neutral Buoyancy Scale In Console")]
    private void ComputeNeutralScale()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        float weight = rb.mass * g;
        float nominalBuoyancy = waterDensity * displacedVolume * g;

        if (nominalBuoyancy <= 1e-6f)
        {
            Debug.LogWarning("[Buoyancy] Nominal buoyancy is too small.");
            return;
        }

        float neutralScale = weight / nominalBuoyancy;
        Debug.Log($"[Buoyancy] Neutral buoyancy scale ≈ {neutralScale:F4}");
    }

    [ContextMenu("Apply Neutral Buoyancy Scale")]
    private void SetNeutralBuoyancyScale()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        float nominalBuoyancy = waterDensity * displacedVolume * g;
        if (nominalBuoyancy <= 1e-6f)
        {
            Debug.LogWarning("[Buoyancy] Cannot auto tune buoyancy: displaced volume or water density is too small.");
            return;
        }

        buoyancyScale = (rb.mass * g / nominalBuoyancy) * targetBuoyancyRatio;
        Debug.Log($"[Buoyancy] Auto buoyancy scale = {buoyancyScale:F4} for mass={rb.mass:F2}kg, volume={displacedVolume:F4}m^3, ratio={targetBuoyancyRatio:F3}");
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Gizmos.matrix = transform.localToWorldMatrix;

        // CG = rouge
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(localCenterOfGravity, 0.015f);

        // CB = cyan
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(localCenterOfBuoyancy, 0.015f);

        // Lien CG-CB
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(localCenterOfGravity, localCenterOfBuoyancy);
    }
}
