using UnityEngine;


/// ============================================================================
/// ThrusterModel
/// ============================================================================
/// BUT :
/// Ce script fournit un modèle mathématique du thruster (T200) permettant de
/// convertir une commande moteur normalisée en une force de poussée réaliste
/// exprimée en Newton (pour Unity).
///
/// RÔLE ET USAGE :
/// - Il s'agit d'une classe utilitaire STATIQUE (calcul uniquement).
/// - NE PAS attacher cette classe à un GameObject (ce n'est pas un MonoBehaviour).
/// - D'autres composants (ex : `ThrusterApplier`, `ThrusterReceiver`) l'utilisent
///   pour convertir des consignes en forces physiques appliquées au Rigidbody.
///
/// PRINCIPES DE FONCTIONNEMENT :
/// 1) La commande `cmd` est normalisée dans [-1, 1] (1 = pleine puissance avant,
///    -1 = pleine puissance arrière). Respectez cette convention dans le reste du projet.
/// 2) Une zone morte est appliquée autour du neutre pour annuler le bruit/biais au repos.
/// 3) Deux modèles polynomiaux (pour 16 V et 18 V) décrivent la poussée expérimentale
///    en kgf en fonction de la commande normalisée.
/// 4) La poussée finale est interpolée selon la tension d'alimentation réelle (`supplyVoltage`).
/// 5) On convertit la poussée de kgf → Newton (1 kgf = 9.80665 N) pour l'utiliser dans Unity.
///
/// INTÉGRATION UNITY → ROS / COHÉRENCE D'ÉCOSYSTÈME :
/// - Ce composant n'envoie rien à ROS directement, mais ses conventions impactent la simulation :
///   * Les thrusts sont convertis en forces Newton appliquées dans le repère Unity (X=right, Y=up, Z=forward).
///   * Pour la cohérence entre capteurs/publishers (IMU, position, target) et le contrôleur ROS (LQR),
///     assurez-vous que la convention Unity→NED utilisée par les publishers est documentée et unique
///     (par ex. helper central UnityPositionToNED / UnityRotToNEDQuat).
/// - Unités : `CommandToForceN` retourne une force en Newton. Les messages ROS qui attendent des efforts
///   doivent connaître cette convention (par ex. si vous publiez forces ou torques côté ROS).
///
/// PARAMÈTRES IMPORTANTS :
/// - `supplyVoltage` : tension d'alimentation utilisée pour interpoler entre modèles 16V/18V.
/// - `commandFullScale` : amplitude de la commande correspondant à la pleine échelle (généralement 1.0).
///
/// REMARQUES DE MAINTENANCE :
/// - Les polynômes sont issus d'ajustements expérimentaux ; conservez les coefficients avec leur source
///   et documentez tout nouveau calibre.
/// - Si vous changez la manière dont les consignes sont représentées (plage, deadband), mettez à jour
///   tous les composants consommateurs (`ThrusterReceiver`, UI, tests automatisés).
/// ============================================================================

public static class ThrusterModel
{
    // Tension d’alimentation réelle du système (ex: 16.8 V pour batteries LiPo chargées)
    public static float supplyVoltage = 16.8f;

    // Valeur de commande correspondant à la pleine échelle (généralement 1.0)
    public static float commandFullScale = 1.0f;

    /// <summary>
    /// Convertit une commande moteur normalisée (cmd ∈ [-1,1]) en force de poussée (Newton).
    /// - applique deadband
    /// - calcule poussée en kgf via polynômes (16V & 18V)
    /// - interpole selon la tension d'alim
    /// - convertit kgf -> N
    /// </summary>
    public static float CommandToForceN(float cmd)
    {
        // Normalisation de la commande (sécurité)
        float s = Mathf.Clamp(cmd / commandFullScale, -1f, 1f);

        // Zone morte (aucune poussée réelle près du neutre pour éviter le bruit)
        if (Mathf.Abs(s) <= 0.08f)
            return 0f;

        // Calcul de la poussée (kgf) pour 16V et 18V selon modèle polynomial empirique
        float f16 = Force16Kgf(s);
        float f18 = Force18Kgf(s);

        // Interpolation linéaire selon la tension d'alimentation entre 16V et 18V
        float t = Mathf.InverseLerp(16f, 18f, supplyVoltage);
        float forceKgf = Mathf.Lerp(f16, f18, t);

        // Protection numérique près du neutre : annule les très petites valeurs
        if (Mathf.Abs(forceKgf) < 0.02f)
            forceKgf = 0f;

        // Conversion kgf -> Newton (1 kgf = 9.80665 N)
        return forceKgf * 9.80665f;
    }

    /// <summary>
    /// Modèle polynomial empirique de poussée (kgf) pour 16 V.
    /// Le polynôme est défini différemment pour marche avant (forward) et arrière (reverse).
    /// Les coefficients proviennent d'ajustements expérimentaux.
    /// </summary>
    private static float Force16Kgf(float s)
    {
        // Forward
        if (s > 0.08f)
        {
            return
                -3.6871f * s * s * s +
                 10.4681f * s * s +
                -1.8068f * s +
                -0.0030f;
        }
        // Reverse
        else
        {
            return
                -5.0925f * s * s * s +
                -11.5232f * s * s +
                -3.0118f * s +
                -0.1548f;
        }
    }

    /// <summary>
    /// Modèle polynomial empirique de poussée (kgf) pour 18 V.
    /// Même principe que pour 16 V : polynômes séparés forward/reverse.
    /// </summary>
    private static float Force18Kgf(float s)
    {
        // Forward
        if (s > 0.08f)
        {
            return
                -5.6732f * s * s * s +
                 14.2911f * s * s +
                -3.2676f * s +
                 0.1445f;
        }
        // Reverse
        else
        {
            return
                -4.7853f * s * s * s +
                -11.6349f * s * s +
                -2.8050f * s +
                -0.1228f;
        }
    }
}