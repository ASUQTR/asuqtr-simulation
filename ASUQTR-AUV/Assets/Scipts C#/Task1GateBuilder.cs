using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Construit la géométrie de la Tâche 1 RoboSub : "Begin Assessment (Gate)".
///
/// Référence :
/// https://robonation.gitbook.io/robosub-resources/section-3-autonomy-challenge/3.2-task-descriptions
/// (section 3.2.2 — Task 1 : Begin Assessment / Gate)
///
/// DESCRIPTION DE LA TÂCHE :
/// - Portique (gate) en tube PVC, flottant juste sous la surface, ancré au fond.
/// - Dimensions hors-tout : 120 in x 60 in (3 m x 1.5 m).
/// - Autour des montants verticaux : panneaux de 3 po de large (plastique ondulé)
///   formant une boîte autour du tube.
///     • Côté DROIT  : ROUGE en haut, NOIR en bas (Red, Right, Above).
///     • Côté GAUCHE : NOIR en haut, ROUGE en bas.
/// - Diviseur central : plaque plate ROUGE de 2 po de large, 24 po de haut (0.61 m),
///   séparant le portique en deux passages.
/// - Une image de rôle de chaque côté du diviseur (12 in x 12 in) :
///     • Survey & Repair  : 🧭 / ⚒️
///     • Search & Rescue  : 🛟 / 🆘
///   Les vrais visuels officiels (vinyl print RoboNation) sont utilisés s'ils sont
///   présents dans Assets/Textures/Task1Gate/, sinon des plans de couleur servent
///   de placeholder.
///
/// UTILISATION :
/// 1. Créer un GameObject vide "Task1_Gate" dans la scène, le positionner où désiré.
/// 2. Ajouter ce script (Add Component → Task1GateBuilder).
/// 3. (Optionnel) Glisser les textures réelles dans les champs Survey/Search Texture
///    de l'Inspector — sinon le script les charge automatiquement depuis
///    Assets/Textures/Task1Gate/Task1_SurveyRepair.png et Task1_SearchRescue.png.
/// 4. Clic droit sur le composant dans l'Inspector → "Build Task 1 - Gate".
/// 5. La géométrie complète est créée comme enfants de ce GameObject.
/// 6. Relancer le ContextMenu pour reconstruire (les anciens enfants sont supprimés).
///
/// NOTE :
/// Toutes les pièces sont des primitives Unity (Cylinder/Cube/Quad). Les panneaux et
/// montants utilisent des matériaux de couleur unie générés à l'exécution ; les images
/// de rôle utilisent les vrais visuels officiels quand disponibles.
/// </summary>
public class Task1GateBuilder : MonoBehaviour
{
    [Header("Dimensions hors-tout (mètres)")]
    [Tooltip("Largeur du portique, centre à centre des montants (120 in = 3.048 m)")]
    public float gateWidth = 3.048f;

    [Tooltip("Hauteur du portique, du fond jusqu'au tube supérieur (60 in = 1.524 m)")]
    public float gateHeight = 1.524f;

    [Header("Montants / tube PVC")]
    [Tooltip("Diamètre des tubes PVC (¾ in ≈ 0.022 m, exagéré pour visibilité)")]
    public float pipeDiameter = 0.05f;

    [Header("Panneaux (boîte autour des montants)")]
    [Tooltip("Largeur des panneaux de plastique ondulé (3 in = 0.0762 m)")]
    public float panelSize = 0.0762f;

    [Header("Diviseur central")]
    [Tooltip("Largeur de la plaque diviseur (2 in = 0.0508 m)")]
    public float dividerWidth = 0.0508f;

    [Tooltip("Hauteur de la plaque diviseur (24 in = 0.61 m)")]
    public float dividerHeight = 0.61f;

    [Header("Images de rôle")]
    [Tooltip("Taille des images de rôle (12 in x 12 in = 0.305 m)")]
    public float roleImageSize = 0.305f;

    [Tooltip("Texture réelle Survey & Repair (vinyl print officiel). Si vide, chargement " +
             "automatique depuis Assets/Textures/Task1Gate/, sinon couleur placeholder.")]
    public Texture2D surveyRepairTexture;

    [Tooltip("Texture réelle Search & Rescue (vinyl print officiel). Si vide, chargement " +
             "automatique depuis Assets/Textures/Task1Gate/, sinon couleur placeholder.")]
    public Texture2D searchRescueTexture;

    private const string SurveyRepairTexturePath = "Assets/Textures/Task1Gate/Task1_SurveyRepair.png";
    private const string SearchRescueTexturePath = "Assets/Textures/Task1Gate/Task1_SearchRescue.png";

    // Couleurs (utilisées seulement si aucune texture réelle n'est disponible)
    private static readonly Color ColorWhitePVC = Color.white;
    private static readonly Color ColorRed = new Color(0.8f, 0.05f, 0.05f);
    private static readonly Color ColorBlack = new Color(0.05f, 0.05f, 0.05f);
    private static readonly Color ColorRoleRepair = new Color(0.1f, 0.5f, 0.9f);  // bleu = Survey & Repair (🧭/⚒️)
    private static readonly Color ColorRoleRescue = new Color(1f, 0.55f, 0f);     // orange = Search & Rescue (🛟/🆘)

    [ContextMenu("Build Task 1 - Gate")]
    public void BuildGate()
    {
        ClearChildren();

        float halfWidth = gateWidth * 0.5f;

        // ── Montants verticaux (PVC) ────────────────────────────────────
        CreateLeg("Leg_Left", new Vector3(-halfWidth, gateHeight * 0.5f, 0f));
        CreateLeg("Leg_Right", new Vector3(halfWidth, gateHeight * 0.5f, 0f));

        // ── Tube horizontal supérieur (PVC) ─────────────────────────────
        GameObject topPipe = CreatePrimitive("Top_Pipe", PrimitiveType.Cylinder,
            new Vector3(0f, gateHeight, 0f),
            Quaternion.Euler(0f, 0f, 90f),
            new Vector3(pipeDiameter, gateWidth * 0.5f, pipeDiameter),
            ColorWhitePVC);

        // ── Panneaux autour des montants (boîte rouge/noir) ─────────────
        // Côté DROIT : rouge en haut, noir en bas (Red, Right, Above)
        CreatePanelBox("Panel_Right_Top", halfWidth, gateHeight * 0.75f, gateHeight * 0.5f, ColorRed);
        CreatePanelBox("Panel_Right_Bottom", halfWidth, gateHeight * 0.25f, gateHeight * 0.5f, ColorBlack);

        // Côté GAUCHE : noir en haut, rouge en bas
        CreatePanelBox("Panel_Left_Top", -halfWidth, gateHeight * 0.75f, gateHeight * 0.5f, ColorBlack);
        CreatePanelBox("Panel_Left_Bottom", -halfWidth, gateHeight * 0.25f, gateHeight * 0.5f, ColorRed);

        // ── Diviseur central (plaque rouge 2 in) ────────────────────────
        // Suspendu depuis le tube supérieur, descend de dividerHeight.
        float dividerCenterY = gateHeight - dividerHeight * 0.5f;
        CreatePrimitive("Gate_Divider", PrimitiveType.Cube,
            new Vector3(0f, dividerCenterY, 0f),
            Quaternion.identity,
            new Vector3(dividerWidth, dividerHeight, panelSize * 0.5f),
            ColorRed);

        // ── Images de rôle (une de chaque côté du diviseur) ─────────────
        // Placées au centre de chaque passage, face à l'AUV qui approche (-Z par convention).
        float imageY = gateHeight * 0.5f;
        float imageX = halfWidth * 0.5f;

        Texture2D repairTex = ResolveTexture(surveyRepairTexture, SurveyRepairTexturePath);
        Texture2D rescueTex = ResolveTexture(searchRescueTexture, SearchRescueTexturePath);

        CreateRoleImage("Role_SurveyRepair", new Vector3(-imageX, imageY, 0.01f), ColorRoleRepair, repairTex);
        CreateRoleImage("Role_SearchRescue", new Vector3(imageX, imageY, 0.01f), ColorRoleRescue, rescueTex);

        Debug.Log("[Task1GateBuilder] Gate Task 1 construit — largeur=" + gateWidth +
                  "m, hauteur=" + gateHeight + "m. Côté droit=ROUGE haut/NOIR bas, " +
                  "côté gauche=NOIR haut/ROUGE bas. Rôles : gauche=Survey&Repair, droite=Search&Rescue (à ajuster).");

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

    private void CreateLeg(string name, Vector3 localPos)
    {
        CreatePrimitive(name, PrimitiveType.Cylinder,
            localPos,
            Quaternion.identity,
            new Vector3(pipeDiameter, gateHeight * 0.5f, pipeDiameter),
            ColorWhitePVC);
    }

    private void CreatePanelBox(string name, float x, float y, float height, Color color)
    {
        CreatePrimitive(name, PrimitiveType.Cube,
            new Vector3(x, y, 0f),
            Quaternion.identity,
            new Vector3(panelSize, height, panelSize),
            color);
    }

    private void CreateRoleImage(string name, Vector3 localPos, Color color, Texture2D texture)
    {
        GameObject quad = CreatePrimitive(name, PrimitiveType.Quad,
            localPos,
            // Le Quad par défaut d'Unity a sa normale visible vers -Z en local.
            // Sans rotation, cette face -Z pointe donc déjà vers l'AUV qui approche
            // (convention -Z = sens d'approche). L'ancienne rotation de 180° sur Y
            // retournait l'image à l'envers (visible seulement APRÈS le passage du
            // portique) — c'était le bug signalé par Elliot.
            Quaternion.identity,
            new Vector3(roleImageSize, roleImageSize, 1f),
            color,
            texture);

        // Désactiver la collision sur les images (purement visuel)
        Collider col = quad.GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }

    /// <summary>
    /// Retourne la texture assignée dans l'Inspector, ou tente de la charger automatiquement
    /// depuis le chemin d'asset par défaut (Assets/Textures/Task1Gate/...) en mode éditeur.
    /// Retourne null si rien n'est trouvé (CreateRoleImage utilisera alors la couleur placeholder).
    /// </summary>
    private Texture2D ResolveTexture(Texture2D assigned, string assetPath)
    {
        if (assigned != null) return assigned;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
#else
        return null;
#endif
    }

    /// <summary>
    /// Crée une primitive Unity comme enfant de ce GameObject, avec une couleur unie.
    /// localScale ici représente directement transform.localScale (pas une taille en mètres
    /// pour les cylindres : Unity's default cylinder height=2, radius=0.5 → scale.y=hauteur/2...
    /// simplifié : on utilise localScale=(diam, hauteur/2, diam) pour les cylindres, ce qui donne
    /// hauteur réelle = scale.y * 2 (taille par défaut du cylindre Unity).
    /// </summary>
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

    /// <summary>
    /// Crée un matériau blanc (non teinté) avec la texture réelle (vinyl print) appliquée
    /// comme map principale. Couvre URP (_BaseMap) et le pipeline standard (_MainTex).
    /// </summary>
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
