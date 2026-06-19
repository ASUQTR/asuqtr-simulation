using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Construit la géométrie de la Tâche 3 RoboSub : "Recon (Bins)".
///
/// Référence (texte officiel, section 3.2.4) :
/// Un pipeline 3D en PVC, supporté au-dessus du fond du bassin. Sur ses côtés
/// se trouvent 4 bacs (bins) : 2 pour le rôle Survey & Repair (icône 🔥),
/// 2 pour le rôle Search & Rescue (icône 🩸). Près de chaque bac : une lumière
/// et un détecteur magnétique (spécifications encore en développement côté
/// RoboNation). L'AUV marque des points en laissant tomber jusqu'à 2 marqueurs
/// dans les bacs (plus de points si déposés dans les bacs correspondant à son
/// rôle), et des points additionnels pour avoir interagi avec le détecteur
/// magnétique et éteint la lumière.
///
/// Référence CAD (Task03_PipelineBins.pdf, dossier RoboSub Data Sharing) :
/// Treillis de tuyaux PVC 1" (tés + coudes) formant une structure diagonale
/// avec plusieurs montants verticaux portant chacun un panneau d'image. Cette
/// quincaillerie n'est pas répliquée à l'identique ici — seule la silhouette
/// fonctionnelle (poutre porteuse + 4 montants + bacs + panneaux d'image) est
/// modélisée, ce qui est suffisant pour la simulation.
///
/// UTILISATION :
/// 1. Créer un GameObject vide "Task3_Recon", le positionner où désiré.
/// 2. Ajouter ce script (Add Component → Task3ReconBuilder).
/// 3. Clic droit sur le composant dans l'Inspector → "Build Task 3 - Recon".
/// 4. Les 4 montants (2 Survey&Repair, 2 Search&Rescue), leurs bacs, panneaux
///    d'image, lumières et détecteurs sont créés comme enfants de ce GameObject.
/// </summary>
public class Task3ReconBuilder : MonoBehaviour
{
    [Header("Pipeline (poutre porteuse PVC)")]
    [Tooltip("Hauteur de la poutre porteuse par rapport au fond du bassin")]
    public float pipelineHeight = 0.9f;

    [Tooltip("Diamètre du tuyau PVC (exagéré pour visibilité)")]
    public float pipeDiameter = 0.05f;

    [Header("Montants (4 au total)")]
    [Tooltip("Distance entre deux montants consécutifs le long du pipeline (axe Z)")]
    public float riserSpacing = 1.0f;

    [Tooltip("Décalage latéral (X) des bacs/panneaux par rapport à l'axe du pipeline, " +
             "en alternance gauche/droite")]
    public float sideOffset = 0.6f;

    [Header("Bacs (CleverMade Milk Crate 25L, approximé)")]
    public Vector3 binSize = new Vector3(0.4f, 0.25f, 0.3f);

    [Tooltip("Hauteur du bas du bac par rapport au fond du bassin")]
    public float binBottomHeight = 0.0f;

    [Header("Panneaux d'image de rôle")]
    [Tooltip("Taille des panneaux d'image (12 in x 12 in = 0.305 m)")]
    public float roleImageSize = 0.305f;

    [Tooltip("Texture réelle Survey & Repair (🔥). Si vide, chargement automatique " +
             "depuis Assets/Textures/Task3Recon/, sinon couleur placeholder.")]
    public Texture2D fireTexture;

    [Tooltip("Texture réelle Search & Rescue (🩸). Si vide, chargement automatique " +
             "depuis Assets/Textures/Task3Recon/, sinon couleur placeholder.")]
    public Texture2D bloodTexture;

    [Header("Lumière + détecteur magnétique (specs non finalisées côté RoboNation)")]
    [Tooltip("Représente l'état initial de la lumière (allumée au début d'un run, " +
             "selon le texte officiel)")]
    public bool lightInitiallyOn = true;

    public float lightRadius = 0.04f;
    public float detectorRadius = 0.03f;

    [Tooltip("Distance (m) à laquelle le sous-marin doit s'approcher du détecteur " +
             "pour l'activer et éteindre la lumière (portée courte, type capteur reed/hall)")]
    public float magnetActivationDistance = 0.15f;

    private const string FireTexturePath = "Assets/Textures/Task3Recon/Task3_Fire.png";
    private const string BloodTexturePath = "Assets/Textures/Task3Recon/Task3_Blood.png";

    private static readonly Color ColorPvcWhite = Color.white;
    private static readonly Color ColorBin = new Color(0.15f, 0.35f, 0.2f);
    private static readonly Color ColorFireRole = new Color(1f, 0.4f, 0.1f);   // placeholder si pas de texture
    private static readonly Color ColorBloodRole = new Color(0.6f, 0.05f, 0.1f); // placeholder si pas de texture
    private static readonly Color ColorLightOn = Color.yellow;
    private static readonly Color ColorLightOff = new Color(0.2f, 0.2f, 0.2f);
    private static readonly Color ColorDetector = new Color(0.3f, 0.3f, 0.35f);

    // Ordre des rôles le long des 4 montants : 2 Survey&Repair (Fire), 2 Search&Rescue (Blood)
    private static readonly bool[] RiserIsFireRole = { true, false, true, false };

    [ContextMenu("Build Task 3 - Recon")]
    public void BuildRecon()
    {
        ClearChildren();

        Texture2D fireTex = ResolveTexture(fireTexture, FireTexturePath);
        Texture2D bloodTex = ResolveTexture(bloodTexture, BloodTexturePath);

        int riserCount = RiserIsFireRole.Length;
        float totalLength = riserSpacing * (riserCount - 1);
        float startZ = -totalLength * 0.5f;

        // ── Poutre porteuse horizontale (tuyau PVC) ─────────────────────
        CreatePrimitive("Pipeline_Beam", PrimitiveType.Cylinder,
            new Vector3(0f, pipelineHeight, 0f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(pipeDiameter, totalLength * 0.5f, pipeDiameter),
            ColorPvcWhite);

        for (int i = 0; i < riserCount; i++)
        {
            float z = startZ + i * riserSpacing;
            bool isFireRole = RiserIsFireRole[i];
            float side = (i % 2 == 0) ? sideOffset : -sideOffset;
            string riserName = "Riser" + (i + 1) + (isFireRole ? "_SurveyRepair" : "_SearchRescue");

            BuildRiser(riserName, z, side, isFireRole, isFireRole ? fireTex : bloodTex);
        }

        Debug.Log("[Task3ReconBuilder] Recon (Bins) construit — 4 montants (2 Survey&Repair / " +
                  "2 Search&Rescue). Rappel : jusqu'à 2 marqueurs à déposer, bonus pour bacs du " +
                  "bon rôle + interaction détecteur magnétique / lumière.");

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
    }

    // =====================================================================
    //  CONSTRUCTION D'UN MONTANT (pipe vertical + bac + panneau + lumière + détecteur)
    // =====================================================================

    private void BuildRiser(string name, float z, float sideX, bool isFireRole, Texture2D roleTexture)
    {
        GameObject riser = new GameObject(name);
        riser.transform.SetParent(transform, worldPositionStays: false);
        riser.transform.localPosition = new Vector3(0f, 0f, z);

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(riser, "Create " + name);
#endif

        // Montant vertical (tuyau PVC), du fond jusqu'à la poutre porteuse.
        CreatePrimitiveUnder(riser.transform, "Riser_Pipe", PrimitiveType.Cylinder,
            new Vector3(0f, pipelineHeight * 0.5f, 0f),
            Quaternion.identity,
            new Vector3(pipeDiameter, pipelineHeight * 0.5f, pipeDiameter),
            ColorPvcWhite);

        // Bac (bin) ouvert sur le dessus, posé sur le fond, décalé latéralement.
        // L'image de rôle est collée au fond intérieur du bac (plutôt que sur un
        // panneau séparé sur le montant) : suite au retour d'Elliot, "mettre les
        // images dans les carrés [bacs] et faire qu'une de ses faces soit ouverte".
        // L'ancien panneau séparé utilisait en plus une rotation (LookRotation)
        // qui faisait pointer la face visible du Quad à l'opposé du bac — d'où le
        // signalement "objets mal liés" (image invisible / mal orientée).
        Color placeholderColor = isFireRole ? ColorFireRole : ColorBloodRole;
        CreateOpenTopBin(riser.transform, "Bin", new Vector3(sideX, 0f, 0f),
            binSize, ColorBin, roleTexture, placeholderColor);

        // Lumière (placeholder sphère) — état initial allumé selon le texte officiel.
        GameObject light = CreatePrimitiveUnder(riser.transform, "Light", PrimitiveType.Sphere,
            new Vector3(sideX * 0.5f, pipelineHeight * 0.9f, 0f),
            Quaternion.identity,
            new Vector3(lightRadius, lightRadius, lightRadius),
            lightInitiallyOn ? ColorLightOn : ColorLightOff);

        // Détecteur magnétique. Basé sur le prototype matériel réel de l'équipe
        // (ESP32-C3 + capteur reed/hall, dossier "Underwater Magnetically Activated
        // Light") : portée de détection courte (quelques cm), simulée ici par
        // MagneticLightUnit à distance plutôt que par un vrai champ magnétique.
        GameObject detector = CreatePrimitiveUnder(riser.transform, "MagneticDetector", PrimitiveType.Sphere,
            new Vector3(sideX * 0.5f, pipelineHeight * 0.55f, 0f),
            Quaternion.identity,
            new Vector3(detectorRadius, detectorRadius, detectorRadius),
            ColorDetector);
        Collider detectorCol = detector.GetComponent<Collider>();
        if (detectorCol != null) detectorCol.enabled = false; // détection par distance, pas par collision

        MagneticLightUnit lightUnit = riser.AddComponent<MagneticLightUnit>();
        lightUnit.lightTransform = light.transform;
        lightUnit.detectorTransform = detector.transform;
        lightUnit.startLightOn = lightInitiallyOn;
        lightUnit.activationDistance = magnetActivationDistance;
    }

    // =====================================================================
    //  BAC OUVERT (5 faces : fond + 4 parois, dessus ouvert) AVEC IMAGE DE
    //  RÔLE COLLÉE AU FOND INTÉRIEUR, VISIBLE À TRAVERS L'OUVERTURE DU HAUT.
    // =====================================================================

    private GameObject CreateOpenTopBin(Transform parent, string name, Vector3 localPos,
        Vector3 size, Color binColor, Texture2D roleTexture, Color roleColor)
    {
        GameObject bin = new GameObject(name);
        bin.transform.SetParent(parent, worldPositionStays: false);
        bin.transform.localPosition = new Vector3(localPos.x, binBottomHeight, localPos.z);

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(bin, "Create " + name);
#endif

        const float wallThickness = 0.015f;
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;

        // Fond du bac.
        CreatePrimitiveUnder(bin.transform, "Bin_Bottom", PrimitiveType.Cube,
            new Vector3(0f, wallThickness * 0.5f, 0f), Quaternion.identity,
            new Vector3(size.x, wallThickness, size.z), binColor);

        // 4 parois latérales — pas de face du dessus : le bac reste ouvert.
        CreatePrimitiveUnder(bin.transform, "Bin_Wall_Left", PrimitiveType.Cube,
            new Vector3(-halfX, size.y * 0.5f, 0f), Quaternion.identity,
            new Vector3(wallThickness, size.y, size.z), binColor);
        CreatePrimitiveUnder(bin.transform, "Bin_Wall_Right", PrimitiveType.Cube,
            new Vector3(halfX, size.y * 0.5f, 0f), Quaternion.identity,
            new Vector3(wallThickness, size.y, size.z), binColor);
        CreatePrimitiveUnder(bin.transform, "Bin_Wall_Front", PrimitiveType.Cube,
            new Vector3(0f, size.y * 0.5f, -halfZ), Quaternion.identity,
            new Vector3(size.x, size.y, wallThickness), binColor);
        CreatePrimitiveUnder(bin.transform, "Bin_Wall_Back", PrimitiveType.Cube,
            new Vector3(0f, size.y * 0.5f, halfZ), Quaternion.identity,
            new Vector3(size.x, size.y, wallThickness), binColor);

        // Image de rôle collée au fond intérieur, face vers le haut (+Y) — visible
        // par une caméra/un capteur regardant à travers l'ouverture du dessus.
        // Le Quad par défaut a sa normale visible vers -Z local ; Euler(90,0,0)
        // amène cette face -Z vers le monde +Y (face vers le haut).
        float imageSize = Mathf.Min(roleImageSize, Mathf.Min(size.x, size.z) * 0.85f);
        GameObject image = CreatePrimitiveUnder(bin.transform, "RoleImage", PrimitiveType.Quad,
            new Vector3(0f, wallThickness + 0.002f, 0f),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(imageSize, imageSize, 1f),
            roleColor, roleTexture);
        Collider imageCol = image.GetComponent<Collider>();
        if (imageCol != null) imageCol.enabled = false;

        return bin;
    }

    // =====================================================================
    //  HELPERS
    // =====================================================================

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }

    private Texture2D ResolveTexture(Texture2D assigned, string assetPath)
    {
        if (assigned != null) return assigned;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
#else
        return null;
#endif
    }

    private GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 localPos,
        Quaternion localRot, Vector3 localScale, Color color, Texture2D texture = null)
    {
        return CreatePrimitiveUnder(transform, name, type, localPos, localRot, localScale, color, texture);
    }

    private GameObject CreatePrimitiveUnder(Transform parent, string name, PrimitiveType type,
        Vector3 localPos, Quaternion localRot, Vector3 localScale, Color color, Texture2D texture = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
#endif

        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = texture != null ? CreateTexturedMaterial(texture) : CreateColorMaterial(color);
            rend.sharedMaterial = mat;
        }

        return go;
    }

    private Material CreateColorMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }

    private Material CreateTexturedMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        return mat;
    }
}
