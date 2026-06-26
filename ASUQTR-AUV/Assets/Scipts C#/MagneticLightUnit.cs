using UnityEngine;

/// <summary>
/// Comportement runtime de l'unité "Lumière + Détecteur magnétique" de la Tâche 3
/// (Recon/Bins). Simule l'interaction décrite dans le texte officiel (section 3.2.4) :
/// l'AUV obtient des points additionnels en s'approchant suffisamment du détecteur
/// magnétique pour éteindre la lumière.
///
/// Basé sur le vrai prototype matériel de l'équipe (dossier RoboSub Data Sharing /
/// Task 03 / "Underwater Magnetically Activated Light (Pipeline Light Test Unit)") :
/// un ESP32-C3 lit un capteur (reed/hall) monté sur une plaque acrylique et pilote
/// une LED. Ici, la détection magnétique réelle est approximée par une simple
/// distance entre ce détecteur et le sous-marin (pas de simulation magnétique fine).
///
/// UTILISATION :
/// Ajouté automatiquement par Task3ReconBuilder.BuildRecon() sur chaque montant
/// (un par "Riser"), avec des références déjà câblées vers les enfants Light et
/// MagneticDetector. Peut aussi être ajouté/configuré manuellement.
/// </summary>
public class MagneticLightUnit : MonoBehaviour
{
    [Header("Références (assignées automatiquement par Task3ReconBuilder)")]
    public Transform lightTransform;
    public Transform detectorTransform;

    [Header("Détection")]
    [Tooltip("Si vide, recherche automatique au Start() du premier composant Buoyancy " +
             "trouvé dans la scène (présent uniquement sur le sous-marin).")]
    public Transform magnetSource;

    [Tooltip("Distance (m) à laquelle le détecteur considère l'aimant 'présent'. " +
             "Le vrai capteur reed/hall a une portée de quelques cm seulement.")]
    public float activationDistance = 0.15f;

    [Header("État")]
    public bool startLightOn = true;
    public bool IsLightOn { get; private set; }
    public bool HasBeenDetected { get; private set; }

    private static readonly Color ColorLightOn = Color.yellow;
    private static readonly Color ColorLightOff = new Color(0.2f, 0.2f, 0.2f);
    private static readonly Color ColorDetectorIdle = new Color(0.3f, 0.3f, 0.35f);
    private static readonly Color ColorDetectorTriggered = new Color(0.2f, 0.9f, 0.4f);

    private Renderer lightRenderer;
    private Renderer detectorRenderer;

    private void Start()
    {
        IsLightOn = startLightOn;

        if (lightTransform != null) lightRenderer = lightTransform.GetComponent<Renderer>();
        if (detectorTransform != null) detectorRenderer = detectorTransform.GetComponent<Renderer>();

        if (magnetSource == null)
        {
            Buoyancy auv = FindAnyObjectByType<Buoyancy>();
            if (auv != null) magnetSource = auv.transform;
        }

        ApplyLightVisual();
    }

    private void Update()
    {
        if (magnetSource == null || detectorTransform == null || HasBeenDetected) return;

        float distance = Vector3.Distance(magnetSource.position, detectorTransform.position);
        if (distance <= activationDistance)
        {
            OnMagnetDetected();
        }
    }

    private void OnMagnetDetected()
    {
        HasBeenDetected = true;
        IsLightOn = false;
        ApplyLightVisual();

        if (detectorRenderer != null)
            detectorRenderer.sharedMaterial.SetColor(detectorRenderer.sharedMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", ColorDetectorTriggered);

        Debug.Log("[MagneticLightUnit] Détecteur magnétique déclenché sur " + name +
                  " — lumière éteinte (bonus de points Recon).");
    }

    private void ApplyLightVisual()
    {
        if (lightRenderer == null) return;
        Color c = IsLightOn ? ColorLightOn : ColorLightOff;
        string prop = lightRenderer.sharedMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
        lightRenderer.sharedMaterial.SetColor(prop, c);
    }

    /// <summary>Permet de réinitialiser l'unité (utile entre deux runs en mode Play).</summary>
    public void ResetUnit()
    {
        HasBeenDetected = false;
        IsLightOn = startLightOn;
        ApplyLightVisual();
        if (detectorRenderer != null)
            detectorRenderer.sharedMaterial.SetColor(detectorRenderer.sharedMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", ColorDetectorIdle);
    }
}
