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
    public float linearDragX = 6.22f;

    [Tooltip("Drag linéaire selon l'axe local Y (heave)")]
    public float linearDragY = 5.15f;

    [Tooltip("Drag linéaire selon l'axe local Z (surge)")]
    public float linearDragZ = 4.03f;

    [Header("Coefficients de drag quadratique (repère corps local)")]
    [Tooltip("Drag quadratique selon l'axe local X (sway)")]
    public float quadraticDragX = 21.66f;

    [Tooltip("Drag quadratique selon l'axe local Y (heave)")]
    public float quadraticDragY = 36.99f;

    [Tooltip("Drag quadratique selon l'axe local Z (surge)")]
    public float quadraticDragZ = 18.18f;

    [Header("Damping angulaire (repère corps local)")]
    [Tooltip("Damping angulaire linéaire autour de l'axe local X (roll)")]
    public float angularLinearDragX = 0.07f;

    [Tooltip("Damping angulaire linéaire autour de l'axe local Y (pitch)")]
    public float angularLinearDragY = 0.07f;

    [Tooltip("Damping angulaire linéaire autour de l'axe local Z (yaw)")]
    public float angularLinearDragZ = 0.07f;

    [Header("Damping angulaire quadratique (optionnel)")]
    [Tooltip("Damping angulaire quadratique autour de l'axe local X (roll)")]
    public float angularQuadraticDragX = 1.55f;

    [Tooltip("Damping angulaire quadratique autour de l'axe local Y (pitch)")]
    public float angularQuadraticDragY = 1.55f;

    [Tooltip("Damping angulaire quadratique autour de l'axe local Z (yaw)")]
    public float angularQuadraticDragZ = 1.55f;

    [Header("Options")]
    [Tooltip("Activer/désactiver le damping angulaire quadratique")]
    public bool useQuadraticAngularDamping = true;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        // Applique les forces/transferts hydrodynamiques à chaque étape physique
        ApplyLinearHydrodynamicForces();
        ApplyAngularHydrodynamicTorques();
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
    /// Les calculs sont effectués dans le repère local du sous-marin puis transformés en monde.
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
