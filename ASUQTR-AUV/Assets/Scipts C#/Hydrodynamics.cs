using UnityEngine;


/// ============================================================================
/// Hydrodynamique (Hydrodynamics)
/// ============================================================================
/// BUT :
/// Ce script applique un modèle hydrodynamique simple et stable au sous-marin
/// pour la simulation (non exhaustif, mais pratique pour le tuning de contrôle).
///
/// Fonctionnalités :
/// - Drag linéaire en translation (surge/forward, sway/right, heave/up)
/// - Drag quadratique en translation
/// - Damping angulaire (roll, pitch, yaw)
/// - Damping angulaire quadratique optionnel
///
/// Remarques d'usage :
/// - Ce script DOIT être attaché à l'objet principal du sous-marin (contenant le Rigidbody).
/// - Nécessite un Rigidbody (composant requis via l'attribut RequireComponent).
/// - Les coefficients doivent être calibrés empiriquement pour votre modèle.
/// - Ne modifiez pas les conventions d'axes sans propager la même convention dans
///   tous les publishers (IMU, nav, target, etc.). Voir la section "Intégration Unity->ROS"
///   ci-dessous pour les recommandations de cohérence.
///
/// Philosophie :
/// - But pragmatique : arrêter le sous-marin de glisser indéfiniment, calmer les rotations,
///   et fournir une dynamique réaliste suffisante pour le tuning du contrôleur (LQR/PID).
///
/// INTÉGRATION UNITY → ROS (CONSEILS)
/// - Unity utilise un repère "Y up" (X droite, Y haut, Z avant).
/// - Dans ce projet, ROS attend la convention NED (North-East-Down). Pour garder la cohérence :
///     UnityPositionToNED : x_ned = Unity.z, y_ned = Unity.x, z_ned = -Unity.y
///     UnityRotToNEDQuat   : convertir le quaternion Unity → quaternion NED via une utilité centrale
/// - IMPORTANT : gardez une seule source de vérité pour les conversions Unity→NED
///   (ex : helper centralisé). Si vous changez la convention ici, mettez à jour tous les
///   publishers et le code de traitement du contrôleur.
///
/// REPÈRES (repère local du sous-marin) :
/// - x local = droite (sway)  (Unity X)
/// - y local = haut   (heave) (Unity Y)
/// - z local = avant  (surge) (Unity Z)
/// - rotation autour de X = pitch
/// - rotation autour de Y = yaw
/// - rotation autour de Z = roll
///
/// Les forces de damping appliquées sont opposées aux composantes de vitesse correspondantes.
/// ============================================================================

[RequireComponent(typeof(Rigidbody))]
public class Hydrodynamics : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;

    [Header("Coefficients de drag linéaire (repère corps local)")]
    [Tooltip("Drag linéaire selon l'axe local X (sway)")]
    public float linearDragX = 43.6523f;

    [Tooltip("Drag linéaire selon l'axe local Y (heave)")]
    public float linearDragY = 52.9362f;

    [Tooltip("Drag linéaire selon l'axe local Z (surge)")]
    public float linearDragZ = 23.9201f;

    [Header("Coefficients de drag quadratique (repère corps local)")]
    [Tooltip("Drag quadratique selon l'axe local X (sway)")]
    public float quadraticDragX = 80.1106f;

    [Tooltip("Drag quadratique selon l'axe local Y (heave)")]
    public float quadraticDragY = 117.8097f;

    [Tooltip("Drag quadratique selon l'axe local Z (surge)")]
    public float quadraticDragZ = 26.7035f;

    [Header("Damping angulaire (repère corps local)")]
    [Tooltip("Damping angulaire linéaire autour de l'axe local X (pitch)")]
    public float angularLinearDragX = 1.4659f;

    [Tooltip("Damping angulaire linéaire autour de l'axe local Y (yaw)")]
    public float angularLinearDragY = 1.3090f;

    [Tooltip("Damping angulaire linéaire autour de l'axe local Z (roll)")]
    public float angularLinearDragZ = 1.0752f;

    [Header("Damping angulaire quadratique (optionnel)")]
    [Tooltip("Damping angulaire quadratique autour de l'axe local X (pitch)")]
    public float angularQuadraticDragX = 5.0470f;

    [Tooltip("Damping angulaire quadratique autour de l'axe local Y (yaw)")]
    public float angularQuadraticDragY = 2.9207f;

    [Tooltip("Damping angulaire quadratique autour de l'axe local Z (roll)")]
    public float angularQuadraticDragZ = 3.1250f;

    [Header("Options")]
    [Tooltip("Activer/désactiver le damping angulaire quadratique")]
    public bool useQuadraticAngularDamping = true;

    [Header("Masse ajoutée (repère corps local)")]
    [Tooltip("Active l'approximation diagonale de masse et d'inertie ajoutées.")]
    public bool useAddedMass = true;

    [Tooltip("Masse ajoutée sur l'axe local X (sway), en kg.")]
    public float addedMassX = 13.3f;

    [Tooltip("Masse ajoutée sur l'axe local Y (heave), en kg.")]
    public float addedMassY = 13.3f;

    [Tooltip("Masse ajoutée sur l'axe local Z (surge), en kg.")]
    public float addedMassZ = 9.58f;

    [Tooltip("Inertie ajoutée autour de l'axe local X (pitch), en kg·m².")]
    public float addedInertiaX = 0.0203f;

    [Tooltip("Inertie ajoutée autour de l'axe local Y (yaw), en kg·m².")]
    public float addedInertiaY = 0.0203f;

    [Tooltip("Inertie ajoutée autour de l'axe local Z (roll), en kg·m².")]
    public float addedInertiaZ = 0f;

    [Header("Debug angulaire")]
    [Tooltip("Affiche omegaLocal et dragTorqueLocal à intervalle limité.")]
    public bool logAngularDamping = false;

    [Min(0.1f)]
    [Tooltip("Intervalle minimum en secondes entre deux logs de damping angulaire.")]
    public float angularDebugInterval = 1f;

    private float nextAngularDebugTime;
    private Vector3 previousLinearVelocityWorld;
    private Vector3 previousAngularVelocityWorld;
    private Vector3 lastAddedForceWorld;
    private Vector3 lastAddedTorqueWorld;
    private bool addedMassStateInitialized;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        ResetAddedMassState();
    }

    void OnEnable()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        ResetAddedMassState();
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        // Applique les forces/transferts hydrodynamiques à chaque étape physique
        if (useAddedMass)
            ApplyAddedMassForces();
        else if (addedMassStateInitialized)
            ResetAddedMassState();

        ApplyLinearHydrodynamicForces();
        ApplyAngularHydrodynamicTorques();
    }

    void ResetAddedMassState()
    {
        if (rb == null)
            return;

        previousLinearVelocityWorld = rb.linearVelocity;
        previousAngularVelocityWorld = rb.angularVelocity;
        lastAddedForceWorld = Vector3.zero;
        lastAddedTorqueWorld = Vector3.zero;
        addedMassStateInitialized = false;
    }

    /// <summary>
    /// Approxime une matrice de masse ajoutée diagonale dans le repère corps.
    /// La force externe du pas précédent est reconstruite à partir de l'accélération
    /// mesurée, en retirant la correction de masse ajoutée déjà appliquée. Cela
    /// évite de réinjecter directement -Ma*a et de créer une oscillation numérique.
    /// </summary>
    void ApplyAddedMassForces()
    {
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f)
            return;

        if (!addedMassStateInitialized)
        {
            previousLinearVelocityWorld = rb.linearVelocity;
            previousAngularVelocityWorld = rb.angularVelocity;
            lastAddedForceWorld = Vector3.zero;
            lastAddedTorqueWorld = Vector3.zero;
            addedMassStateInitialized = true;
            return;
        }

        Vector3 linearAccelerationWorld =
            (rb.linearVelocity - previousLinearVelocityWorld) / dt;
        Vector3 angularAccelerationWorld =
            (rb.angularVelocity - previousAngularVelocityWorld) / dt;

        previousLinearVelocityWorld = rb.linearVelocity;
        previousAngularVelocityWorld = rb.angularVelocity;

        Vector3 linearAccelerationLocal =
            transform.InverseTransformDirection(linearAccelerationWorld);
        Vector3 angularAccelerationLocal =
            transform.InverseTransformDirection(angularAccelerationWorld);
        Vector3 omegaLocal = transform.InverseTransformDirection(rb.angularVelocity);

        Vector3 previousAddedForceLocal =
            transform.InverseTransformDirection(lastAddedForceWorld);
        Vector3 previousAddedTorqueLocal =
            transform.InverseTransformDirection(lastAddedTorqueWorld);

        Vector3 externalForceLocal =
            rb.mass * linearAccelerationLocal - previousAddedForceLocal;

        Vector3 localInertia = GetLocalAxisInertia();
        Vector3 angularMomentumLocal = Vector3.Scale(localInertia, omegaLocal);
        Vector3 gyroscopicTorqueLocal = Vector3.Cross(omegaLocal, angularMomentumLocal);
        Vector3 externalTorqueLocal =
            Vector3.Scale(localInertia, angularAccelerationLocal) +
            gyroscopicTorqueLocal -
            previousAddedTorqueLocal;

        Vector3 addedForceLocal = new Vector3(
            ComputeAddedMassReaction(externalForceLocal.x, rb.mass, addedMassX),
            ComputeAddedMassReaction(externalForceLocal.y, rb.mass, addedMassY),
            ComputeAddedMassReaction(externalForceLocal.z, rb.mass, addedMassZ)
        );

        Vector3 addedTorqueLocal = new Vector3(
            ComputeAddedMassReaction(externalTorqueLocal.x, localInertia.x, addedInertiaX),
            ComputeAddedMassReaction(externalTorqueLocal.y, localInertia.y, addedInertiaY),
            ComputeAddedMassReaction(externalTorqueLocal.z, localInertia.z, addedInertiaZ)
        );

        lastAddedForceWorld = transform.TransformDirection(addedForceLocal);
        lastAddedTorqueWorld = transform.TransformDirection(addedTorqueLocal);

        rb.AddForce(lastAddedForceWorld, ForceMode.Force);
        rb.AddTorque(lastAddedTorqueWorld, ForceMode.Force);
    }

    Vector3 GetLocalAxisInertia()
    {
        Quaternion localToPrincipal = Quaternion.Inverse(rb.inertiaTensorRotation);
        Vector3 principalX = localToPrincipal * Vector3.right;
        Vector3 principalY = localToPrincipal * Vector3.up;
        Vector3 principalZ = localToPrincipal * Vector3.forward;
        Vector3 inertia = rb.inertiaTensor;

        return new Vector3(
            Vector3.Dot(Vector3.Scale(principalX, principalX), inertia),
            Vector3.Dot(Vector3.Scale(principalY, principalY), inertia),
            Vector3.Dot(Vector3.Scale(principalZ, principalZ), inertia)
        );
    }

    float ComputeAddedMassReaction(float externalLoad, float rigidMass, float addedMass)
    {
        float clampedAddedMass = Mathf.Max(0f, addedMass);
        float totalMass = Mathf.Max(1e-6f, rigidMass + clampedAddedMass);
        return -(clampedAddedMass / totalMass) * externalLoad;
    }

    /// <summary>
    /// Applique le drag translatoire (linéaire + quadratique) exprimé dans le repère local,
    /// puis converti en monde avant d'être appliqué au Rigidbody.
    /// </summary>
    void ApplyLinearHydrodynamicForces()
    {
        // Vitesse du Rigidbody exprimée dans le repère local du sous-marin (body frame)
        Vector3 vLocal = transform.InverseTransformDirection(rb.linearVelocity);

        // Calcul du drag en repère local pour chaque axe (force opposée à la vitesse)
        Vector3 dragForceLocal = new Vector3(
            ComputeAxisDrag(vLocal.x, linearDragX, quadraticDragX),
            ComputeAxisDrag(vLocal.y, linearDragY, quadraticDragY),
            ComputeAxisDrag(vLocal.z, linearDragZ, quadraticDragZ)
        );

        // Conversion vers le repère monde (Unity world) et application
        Vector3 dragForceWorld = transform.TransformDirection(dragForceLocal);
        rb.AddForce(dragForceWorld, ForceMode.Force);
    }

    /// <summary>
    /// Applique un torque d'amortissement angulaire (linéaire + option quadratique).
    /// X freine le pitch, Y freine le yaw et Z freine le roll. Les calculs sont
    /// effectués dans le repère local du sous-marin puis transformés en monde.
    /// </summary>
    void ApplyAngularHydrodynamicTorques()
    {
        // Vitesse angulaire du Rigidbody exprimée dans le repère local du sous-marin
        Vector3 omegaLocal = transform.InverseTransformDirection(rb.angularVelocity);

        // Calcul du couple d'amortissement pour chaque axe local (opposé au taux angulaire)
        Vector3 dragTorqueLocal = new Vector3(
            ComputeAxisAngularDrag(omegaLocal.x, angularLinearDragX, angularQuadraticDragX, useQuadraticAngularDamping),
            ComputeAxisAngularDrag(omegaLocal.y, angularLinearDragY, angularQuadraticDragY, useQuadraticAngularDamping),
            ComputeAxisAngularDrag(omegaLocal.z, angularLinearDragZ, angularQuadraticDragZ, useQuadraticAngularDamping)
        );

        // Conversion vers le repère monde et application du couple
        Vector3 dragTorqueWorld = transform.TransformDirection(dragTorqueLocal);
        rb.AddTorque(dragTorqueWorld, ForceMode.Force);

        if (logAngularDamping && Time.time >= nextAngularDebugTime)
        {
            nextAngularDebugTime = Time.time + Mathf.Max(0.1f, angularDebugInterval);
            Debug.Log(
                $"[Hydrodynamics] omegaLocal pitch/yaw/roll={omegaLocal:F3}, " +
                $"dragTorqueLocal pitch/yaw/roll={dragTorqueLocal:F3}",
                this
            );
        }
    }

    /// <summary>
    /// Drag 1D opposé à la vitesse : F = -(a*v + b*|v|*v)
    /// - a : coefficient linéaire
    /// - b : coefficient quadratique
    /// </summary>
    float ComputeAxisDrag(float velocity, float linearCoeff, float quadraticCoeff)
    {
        return -(linearCoeff * velocity + quadraticCoeff * Mathf.Abs(velocity) * velocity);
    }

    /// <summary>
    /// Damping rotationnel 1D opposé à la vitesse angulaire
    /// - Si useQuadratic est vrai, ajoute la composante quadratique en plus de la composante linéaire.
    /// </summary>
    float ComputeAxisAngularDrag(float omega, float linearCoeff, float quadraticCoeff, bool useQuadratic)
    {
        float torque = -(linearCoeff * omega);

        if (useQuadratic)
            torque += -(quadraticCoeff * Mathf.Abs(omega) * omega);

        return torque;
    }
}
