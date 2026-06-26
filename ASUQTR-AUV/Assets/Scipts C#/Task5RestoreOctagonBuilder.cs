using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Construit la géométrie de la Tâche 5 RoboSub : "Restore (Octagon)"
/// (nommée officiellement "Resupply", section 3.2.6).
///
/// Référence (texte officiel) :
/// Un octogone flottant (~9 ft / 2.7 m de diamètre) à la surface, avec 4 images
/// suspendues à l'intérieur, faisant face vers le centre (🧭 Survey, 🔧 Repair,
/// 🆘 Search, 🛟 Rescue). Sous l'octogone, une table de "resupply" présente des
/// objets spécifiques à chaque rôle (Survey&Repair : prise électrique + écrou-
/// boulon ; Search&Rescue : pilule + pansement) et 2 paniers le long de bords
/// opposés, marqués respectivement avertissement (⚠) et casque-croix-rouge (➕).
/// Le second (et dernier) pinger acoustique du parcours (3.2.9) est positionné
/// près du centre de l'octogone.
///
/// Référence CAD (Task05_Octagon.pdf, Task05_Restore.pdf, dossier RoboSub Data
/// Sharing) : anneau octogonal en PVC (segments de 45 in + coudes 45°), bras
/// radiaux (riser 12 in + bras 120 in + crochet) portant les images suspendues ;
/// table en PVC 24 in x 24 in (coins coupés), 2 rails latéraux portant des
/// paniers coulissants. Quincaillerie PVC non répliquée à l'identique — seule
/// la silhouette fonctionnelle est modélisée, comme pour les tâches précédentes.
///
/// CONVENTION DE HAUTEUR (spécifique à cette tâche) :
/// Le GameObject parent est positionné par PracticeSceneBuilder au fond du
/// bassin (Y local = 0 = fond), comme les autres tâches. Ici, l'anneau
/// octogonal flotte à la SURFACE, donc ses enfants sont positionnés à
/// Y local = +poolDepth, tandis que la table de resupply reste près du fond
/// (Y local ≈ 0).
///
/// UTILISATION :
/// 1. Créer un GameObject vide "Task5_Restore", le positionner où désiré.
/// 2. Ajouter ce script (Add Component → Task5RestoreOctagonBuilder).
/// 3. Clic droit sur le composant dans l'Inspector → "Build Task 5 - Restore".
/// </summary>
public class Task5RestoreOctagonBuilder : MonoBehaviour
{
    [Header("Bassin")]
    [Tooltip("Profondeur du bassin — distance entre le fond (Y local = 0 de ce " +
             "GameObject, posé au fond par PracticeSceneBuilder) et la surface, " +
             "où flotte l'octogone.")]
    public float poolDepth = 2.13f;

    [Header("Octogone flottant (~9 ft / 2.7 m de diamètre)")]
    public float octagonDiameter = 2.74f;
    public float ringTubeDiameter = 0.05f;

    [Header("Bras suspendant les 4 images (vers le centre)")]
    public float armRiserHeight = 0.3f;
    [Tooltip("Longueur du bras horizontal partant de l'anneau vers le centre")]
    public float armLength = 1.0f;
    public float hangingImageSize = 0.3f;
    [Tooltip("Hauteur des images suspendues sous la surface")]
    public float hangingImageDepth = 0.25f;

    [Tooltip("Textures réelles (🧭🔧🆘🛟). Si vide, chargement automatique depuis " +
             "Assets/Textures/Task5Restore/, sinon couleur placeholder.")]
    public Texture2D surveyTexture;
    public Texture2D repairTexture;
    public Texture2D searchTexture;
    public Texture2D rescueTexture;

    [Header("Table de Resupply (près du fond du bassin)")]
    public Vector3 tableSize = new Vector3(0.61f, 0.05f, 0.61f);
    public float tableLegHeight = 0.8f;

    [Tooltip("Textures des objets de la table (rôle Survey&Repair)")]
    public Texture2D electricTexture;
    public Texture2D nutBoltTexture;

    [Tooltip("Textures des objets de la table (rôle Search&Rescue)")]
    public Texture2D pillTexture;
    public Texture2D bandAidTexture;

    [Header("Paniers (le long de 2 bords opposés de la table)")]
    public Vector3 basketSize = new Vector3(0.27f, 0.08f, 0.18f);
    public Texture2D warningTexture;
    public Texture2D redCrossTexture;

    [Header("Pinger acoustique #2 (dernier pinger du parcours — section 3.2.9)")]
    [Tooltip("Même signature que le pinger #1 de cette lane (Lane A = 30 kHz d'après " +
             "la carte du parcours) — les 2 pingers d'une lane partagent la même " +
             "fréquence pour confirmer à l'AUV qu'il est dans la bonne lane.")]
    public float pingerFrequencyKHz = 30f;
    public float pingerDelaySec = 0f;
    public float pingerRadius = 0.06f;

    private const string TexturesRoot = "Assets/Textures/Task5Restore/";

    private static readonly Color ColorPvcWhite = Color.white;
    private static readonly Color ColorTablePlaceholder = new Color(0.8f, 0.8f, 0.75f);
    private static readonly Color ColorBasketPlaceholder = new Color(0.5f, 0.5f, 0.55f);
    private static readonly Color ColorPinger = new Color(0.1f, 0.9f, 0.3f);

    // Les 4 images suspendues, réparties sur 4 des 8 côtés de l'octogone (alternés).
    private struct HangingImage
    {
        public string Name;
        public string TexturePath;
    }

    [ContextMenu("Build Task 5 - Restore")]
    public void BuildRestore()
    {
        ClearChildren();

        BuildOctagonRing();
        BuildHangingImages();
        BuildResupplyTable();
        BuildPinger();

        Debug.Log("[Task5RestoreOctagonBuilder] Restore (Octagon) construit — anneau flottant " +
                  octagonDiameter + " m, 4 images suspendues, table de resupply + 2 paniers, " +
                  "pinger #2 : " + pingerFrequencyKHz + " kHz / " + pingerDelaySec + " s.");

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
    }

    // =====================================================================
    //  ANNEAU OCTOGONAL (flotte à la surface, Y local = +poolDepth)
    // =====================================================================

    private void BuildOctagonRing()
    {
        GameObject ring = new GameObject("OctagonRing");
        ring.transform.SetParent(transform, worldPositionStays: false);
        ring.transform.localPosition = new Vector3(0f, poolDepth, 0f);

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(ring, "Create OctagonRing");
#endif

        // 8 segments formant un octogone régulier (apothème = octagonDiameter / 2).
        float apothem = octagonDiameter * 0.5f;
        float sideLength = apothem * 2f * Mathf.Tan(Mathf.PI / 8f);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            float nextAngle = (i + 1) * 45f * Mathf.Deg2Rad;

            Vector3 p1 = new Vector3(apothem * Mathf.Cos(angle), 0f, apothem * Mathf.Sin(angle));
            Vector3 p2 = new Vector3(apothem * Mathf.Cos(nextAngle), 0f, apothem * Mathf.Sin(nextAngle));
            Vector3 mid = (p1 + p2) * 0.5f;
            Vector3 dir = (p2 - p1).normalized;

            GameObject segment = CreatePrimitiveUnder(ring.transform, "Side" + i, PrimitiveType.Cylinder,
                mid, Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f),
                new Vector3(ringTubeDiameter, sideLength * 0.5f, ringTubeDiameter),
                ColorPvcWhite);
        }
    }

    // =====================================================================
    //  4 IMAGES SUSPENDUES (faisant face au centre)
    // =====================================================================

    private void BuildHangingImages()
    {
        Texture2D survey = ResolveTexture(surveyTexture, TexturesRoot + "Task5_Survey.png");
        Texture2D repair = ResolveTexture(repairTexture, TexturesRoot + "Task5_Repair.png");
        Texture2D search = ResolveTexture(searchTexture, TexturesRoot + "Task5_Search.png");
        Texture2D rescue = ResolveTexture(rescueTexture, TexturesRoot + "Task5_Rescue.png");

        HangingImage[] images = new HangingImage[4];
        images[0] = new HangingImage { Name = "Hanging_Survey", TexturePath = null };
        images[1] = new HangingImage { Name = "Hanging_Repair", TexturePath = null };
        images[2] = new HangingImage { Name = "Hanging_Search", TexturePath = null };
        images[3] = new HangingImage { Name = "Hanging_Rescue", TexturePath = null };
        Texture2D[] textures = { survey, repair, search, rescue };

        float apothem = octagonDiameter * 0.5f;

        // Réparties sur 4 des 8 côtés (1 sur 2), faces tournées vers le centre.
        for (int k = 0; k < 4; k++)
        {
            int sideIndex = k * 2;
            float angle = (sideIndex + 0.5f) * 45f * Mathf.Deg2Rad; // milieu du côté
            Vector3 ringPoint = new Vector3(apothem * Mathf.Cos(angle), 0f, apothem * Mathf.Sin(angle));
            Vector3 inward = -ringPoint.normalized;

            GameObject arm = new GameObject(images[k].Name + "_Arm");
            arm.transform.SetParent(transform, worldPositionStays: false);
            arm.transform.localPosition = new Vector3(0f, poolDepth, 0f) + ringPoint;

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(arm, "Create " + images[k].Name + "_Arm");
#endif

            // Montant vertical depuis l'anneau.
            CreatePrimitiveUnder(arm.transform, "Riser", PrimitiveType.Cylinder,
                new Vector3(0f, armRiserHeight * 0.5f, 0f), Quaternion.identity,
                new Vector3(ringTubeDiameter, armRiserHeight * 0.5f, ringTubeDiameter),
                ColorPvcWhite);

            // Bras horizontal vers le centre, au sommet du montant.
            Vector3 armMid = inward * (armLength * 0.5f) + Vector3.up * armRiserHeight;
            CreatePrimitiveUnder(arm.transform, "Arm", PrimitiveType.Cylinder,
                armMid, Quaternion.LookRotation(inward) * Quaternion.Euler(90f, 0f, 0f),
                new Vector3(ringTubeDiameter, armLength * 0.5f, ringTubeDiameter),
                ColorPvcWhite);

            // Image suspendue au bout du bras, orientée vers le centre du bassin.
            Vector3 imagePos = inward * armLength + Vector3.up * (armRiserHeight - hangingImageDepth);
            GameObject panel = CreatePrimitiveUnder(arm.transform, images[k].Name, PrimitiveType.Quad,
                imagePos, Quaternion.LookRotation(inward),
                new Vector3(hangingImageSize, hangingImageSize, 1f),
                Color.white, textures[k]);
            Collider panelCol = panel.GetComponent<Collider>();
            if (panelCol != null) panelCol.enabled = false;
        }
    }

    // =====================================================================
    //  TABLE DE RESUPPLY (près du fond du bassin)
    // =====================================================================

    private void BuildResupplyTable()
    {
        GameObject table = new GameObject("ResupplyTable");
        table.transform.SetParent(transform, worldPositionStays: false);
        table.transform.localPosition = Vector3.zero;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(table, "Create ResupplyTable");
#endif

        // 4 pieds.
        float legX = tableSize.x * 0.4f;
        float legZ = tableSize.z * 0.4f;
        Vector3[] legOffsets =
        {
            new Vector3(-legX, 0f, -legZ), new Vector3(legX, 0f, -legZ),
            new Vector3(-legX, 0f, legZ), new Vector3(legX, 0f, legZ),
        };
        for (int i = 0; i < legOffsets.Length; i++)
        {
            CreatePrimitiveUnder(table.transform, "Leg" + i, PrimitiveType.Cylinder,
                legOffsets[i] + Vector3.up * tableLegHeight * 0.5f, Quaternion.identity,
                new Vector3(0.03f, tableLegHeight * 0.5f, 0.03f), ColorPvcWhite);
        }

        // Plateau.
        float topY = tableLegHeight + tableSize.y * 0.5f;
        CreatePrimitiveUnder(table.transform, "TableTop", PrimitiveType.Cube,
            new Vector3(0f, topY, 0f), Quaternion.identity, tableSize, ColorTablePlaceholder);

        // 4 objets de resupply (2 par rôle), posés sur le plateau, coins opposés.
        Texture2D electric = ResolveTexture(electricTexture, TexturesRoot + "Task5_Electric.png");
        Texture2D nutBolt = ResolveTexture(nutBoltTexture, TexturesRoot + "Task5_NutBolt.png");
        Texture2D pill = ResolveTexture(pillTexture, TexturesRoot + "Task5_Pill.png");
        Texture2D bandAid = ResolveTexture(bandAidTexture, TexturesRoot + "Task5_BandAid.png");

        float itemY = topY + tableSize.y * 0.5f + 0.02f;
        float ix = tableSize.x * 0.25f;
        float iz = tableSize.z * 0.25f;

        CreateResupplyItem(table.transform, "Item_Electric_SurveyRepair", new Vector3(-ix, itemY, -iz), electric);
        CreateResupplyItem(table.transform, "Item_NutBolt_SurveyRepair", new Vector3(-ix, itemY, iz), nutBolt);
        CreateResupplyItem(table.transform, "Item_Pill_SearchRescue", new Vector3(ix, itemY, -iz), pill);
        CreateResupplyItem(table.transform, "Item_BandAid_SearchRescue", new Vector3(ix, itemY, iz), bandAid);

        // 2 paniers le long de bords opposés (X), marqués Warning / RedCross.
        Texture2D warning = ResolveTexture(warningTexture, TexturesRoot + "Task5_Warning.png");
        Texture2D redCross = ResolveTexture(redCrossTexture, TexturesRoot + "Task5_RedCross.png");

        BuildBasket(table.transform, "Basket_SurveyRepair_Warning",
            new Vector3(-(tableSize.x * 0.5f + basketSize.x * 0.5f), topY, 0f), warning);
        BuildBasket(table.transform, "Basket_SearchRescue_RedCross",
            new Vector3(tableSize.x * 0.5f + basketSize.x * 0.5f, topY, 0f), redCross);
    }

    private void CreateResupplyItem(Transform parent, string name, Vector3 localPos, Texture2D texture)
    {
        GameObject item = CreatePrimitiveUnder(parent, name, PrimitiveType.Cylinder,
            localPos + Vector3.up * 0.025f, Quaternion.identity,
            new Vector3(0.04f, 0.025f, 0.04f), Color.white, texture);
        Collider col = item.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void BuildBasket(Transform parent, string name, Vector3 localPos, Texture2D markerTexture)
    {
        GameObject basket = new GameObject(name);
        basket.transform.SetParent(parent, worldPositionStays: false);
        basket.transform.localPosition = localPos;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(basket, "Create " + name);
#endif

        CreatePrimitiveUnder(basket.transform, "BasketBody", PrimitiveType.Cube,
            Vector3.zero, Quaternion.identity, basketSize, ColorBasketPlaceholder);

        // Étiquette verticale (icône de rôle) sur le côté extérieur du panier.
        GameObject marker = CreatePrimitiveUnder(basket.transform, "Marker", PrimitiveType.Quad,
            new Vector3(0f, basketSize.y * 0.5f + 0.08f, 0f), Quaternion.identity,
            new Vector3(0.1f, 0.1f, 1f), Color.white, markerTexture);
        Collider markerCol = marker.GetComponent<Collider>();
        if (markerCol != null) markerCol.enabled = false;
    }

    // =====================================================================
    //  PINGER ACOUSTIQUE #2
    // =====================================================================

    private void BuildPinger()
    {
        GameObject pinger = CreatePrimitive("Pinger", PrimitiveType.Sphere,
            new Vector3(0f, tableLegHeight + 0.3f, 0f), Quaternion.identity,
            new Vector3(pingerRadius, pingerRadius, pingerRadius), ColorPinger);
        pinger.name = $"Pinger_{pingerFrequencyKHz:F0}kHz_{pingerDelaySec:F1}s";
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
