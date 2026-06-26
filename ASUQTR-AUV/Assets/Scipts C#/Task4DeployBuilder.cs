using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Construit la géométrie de la Tâche 4 RoboSub : "Deploy (Torpedoes)".
///
/// Référence (texte officiel, section 3.2.5) :
/// Un panneau vertical (2 ft x 2 ft = 0.6 m x 0.6 m, vinyl print sur fond de
/// plastique ondulé) avec 2 tailles d'ouvertures. L'AUV marque des points en
/// tirant des torpilles à travers n'importe quelle ouverture (points partiels
/// si la torpille touche le panneau sans passer à travers). Le maximum de
/// points est obtenu en tirant d'abord dans la grande ouverture (🔥/🩸) puis
/// dans la petite (🚒/🚑). Des points additionnels sont accordés pour un tir
/// plus loin du panneau : "far" (1 ft / 0.3 m) et "farther" (1.5 ft / 0.46 m).
/// Le premier pinger acoustique du parcours (3.2.9) est positionné à cette tâche.
///
/// Référence visuelle (Task4_ver1.png / Task4_ver2.png, dossier RoboSub Data
/// Sharing) : le panneau réel combine déjà les 2 rôles sur une seule plaque —
/// 2 grandes ouvertures (🔥 Survey&Repair, 🩸 Search&Rescue) en coins opposés,
/// et 2 petites ouvertures (🚒 Survey&Repair, 🚑 Search&Rescue) sur l'autre
/// diagonale. "ver2" est la même disposition tournée de 180°.
///
/// NOTE : Les diamètres exacts des ouvertures ne sont pas chiffrés dans la
/// documentation officielle ("two different size openings" seulement) — les
/// valeurs ci-dessous sont des estimations raisonnables, ajustables dans
/// l'Inspector.
///
/// UTILISATION :
/// 1. Créer un GameObject vide "Task4_Deploy", le positionner où désiré.
/// 2. Ajouter ce script (Add Component → Task4DeployBuilder).
/// 3. Clic droit sur le composant dans l'Inspector → "Build Task 4 - Deploy".
/// </summary>
public class Task4DeployBuilder : MonoBehaviour
{
    [Header("Panneau (2 ft x 2 ft = 0.6 m x 0.6 m)")]
    public float boardWidth = 0.6f;
    public float boardHeight = 0.6f;
    public float boardThickness = 0.02f;

    [Tooltip("Utiliser la disposition 'ver2' (180° par rapport à 'ver1')")]
    public bool useVersion2 = false;

    [Tooltip("Texture réelle ver1. Si vide, chargement automatique depuis " +
             "Assets/Textures/Task4Deploy/, sinon couleur placeholder.")]
    public Texture2D version1Texture;

    [Tooltip("Texture réelle ver2. Si vide, chargement automatique depuis " +
             "Assets/Textures/Task4Deploy/, sinon couleur placeholder.")]
    public Texture2D version2Texture;

    [Header("Pieds de support (estimation, ~18 in = 0.46 m d'après le CAD)")]
    public float legHeight = 0.46f;
    public float legDiameter = 0.04f;

    [Header("Ouvertures (diamètres non officiellement chiffrés — estimations)")]
    [Tooltip("Grande ouverture, associée à 🔥 (Survey&Repair) / 🩸 (Search&Rescue)")]
    public float largeOpeningDiameter = 0.3f;

    [Tooltip("Petite ouverture, associée à 🚒 (Survey&Repair) / 🚑 (Search&Rescue)")]
    public float smallOpeningDiameter = 0.15f;

    [Header("Pinger acoustique #1 (positionné à cette tâche — section 3.2.9)")]
    [Tooltip("Fréquence du pinger pour cette lane (ex. Lane A = 30 kHz d'après la carte du parcours)")]
    public float pingerFrequencyKHz = 30f;

    [Tooltip("Délai du pinger pour cette lane (ex. Lane A = 0 sec d'après la carte du parcours)")]
    public float pingerDelaySec = 0f;

    public float pingerRadius = 0.06f;

    private const string Version1TexturePath = "Assets/Textures/Task4Deploy/Task4_ver1.png";
    private const string Version2TexturePath = "Assets/Textures/Task4Deploy/Task4_ver2.png";

    private static readonly Color ColorBoardPlaceholder = new Color(0.85f, 0.85f, 0.85f);
    private static readonly Color ColorLeg = new Color(0.9f, 0.9f, 0.9f);
    private static readonly Color ColorPinger = new Color(0.1f, 0.9f, 0.3f);

    [ContextMenu("Build Task 4 - Deploy")]
    public void BuildDeploy()
    {
        ClearChildren();

        float boardCenterY = legHeight + boardHeight * 0.5f;

        // ── Pieds de support ─────────────────────────────────────────
        float legX = boardWidth * 0.35f;
        CreatePrimitive("Leg_Left", PrimitiveType.Cylinder,
            new Vector3(-legX, legHeight * 0.5f, 0f), Quaternion.identity,
            new Vector3(legDiameter, legHeight * 0.5f, legDiameter), ColorLeg);
        CreatePrimitive("Leg_Right", PrimitiveType.Cylinder,
            new Vector3(legX, legHeight * 0.5f, 0f), Quaternion.identity,
            new Vector3(legDiameter, legHeight * 0.5f, legDiameter), ColorLeg);

        // ── Panneau (vinyl print, face -Z vers l'AUV qui approche) ─────
        Texture2D tex = useVersion2
            ? ResolveTexture(version2Texture, Version2TexturePath)
            : ResolveTexture(version1Texture, Version1TexturePath);

        GameObject board = CreatePrimitive("Board", PrimitiveType.Cube,
            new Vector3(0f, boardCenterY, 0f), Quaternion.identity,
            new Vector3(boardWidth, boardHeight, boardThickness),
            ColorBoardPlaceholder);

        // Face avant texturée (Quad séparé, plus simple à texturer qu'une face de Cube).
        // Le Quad par défaut a sa normale visible vers -Z local ; sans rotation,
        // cette face -Z pointe déjà vers l'AUV qui approche (convention -Z).
        // L'ancienne rotation de 180° sur Y retournait l'image vers +Z (à l'intérieur
        // du panneau), donc seule la pancarte (Board) était visible, jamais l'image —
        // exactement le bug signalé : "on voyait la pancarte mais pas les images dessus".
        GameObject face = CreatePrimitive("Board_Front", PrimitiveType.Quad,
            new Vector3(0f, boardCenterY, -boardThickness * 0.5f - 0.001f),
            Quaternion.identity,
            new Vector3(boardWidth, boardHeight, 1f),
            Color.white, tex);
        Collider faceCol = face.GetComponent<Collider>();
        if (faceCol != null) faceCol.enabled = false;

        // ── Ouvertures (zones de détection pour les torpilles) ──────────
        // ver1 : grandes ouvertures en haut-gauche (🔥) / bas-droite (🩸)
        //        petites ouvertures en haut-droite (🚒) / bas-gauche (🚑)
        // ver2 : disposition inverse (180°)
        float qx = boardWidth * 0.25f;
        float qy = boardHeight * 0.25f;

        bool largeOnTopLeftDiagonal = !useVersion2;

        CreateOpening("Opening_TopLeft", new Vector3(-qx, boardCenterY + qy, 0f),
            largeOnTopLeftDiagonal ? largeOpeningDiameter : smallOpeningDiameter);
        CreateOpening("Opening_TopRight", new Vector3(qx, boardCenterY + qy, 0f),
            largeOnTopLeftDiagonal ? smallOpeningDiameter : largeOpeningDiameter);
        CreateOpening("Opening_BottomLeft", new Vector3(-qx, boardCenterY - qy, 0f),
            largeOnTopLeftDiagonal ? smallOpeningDiameter : largeOpeningDiameter);
        CreateOpening("Opening_BottomRight", new Vector3(qx, boardCenterY - qy, 0f),
            largeOnTopLeftDiagonal ? largeOpeningDiameter : smallOpeningDiameter);

        // ── Pinger acoustique (3.2.9 — premier pinger du parcours) ──────
        GameObject pinger = CreatePrimitive("Pinger", PrimitiveType.Sphere,
            new Vector3(boardWidth * 0.8f, legHeight * 0.5f, 0.3f), Quaternion.identity,
            new Vector3(pingerRadius, pingerRadius, pingerRadius), ColorPinger);
        pinger.name = $"Pinger_{pingerFrequencyKHz:F0}kHz_{pingerDelaySec:F1}s";

        Debug.Log("[Task4DeployBuilder] Deploy (Torpedoes) construit — panneau " +
                  (useVersion2 ? "ver2" : "ver1") + ", 2 grandes + 2 petites ouvertures. " +
                  "Pinger #1 : " + pingerFrequencyKHz + " kHz / " + pingerDelaySec + " s.");

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
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

    private void CreateOpening(string name, Vector3 localPos, float diameter)
    {
        GameObject opening = new GameObject(name);
        opening.transform.SetParent(transform, worldPositionStays: false);
        opening.transform.localPosition = localPos;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(opening, "Create " + name);
#endif

        SphereCollider col = opening.AddComponent<SphereCollider>();
        col.radius = diameter * 0.5f;
        col.isTrigger = true;
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
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
#endif

        go.transform.SetParent(transform, worldPositionStays: false);
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
