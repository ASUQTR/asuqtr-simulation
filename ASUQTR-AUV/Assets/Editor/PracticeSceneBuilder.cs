#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Crée une nouvelle scène "Practice" contenant le couloir A (bassin) de la
/// compétition RoboSub, avec les obstacles/tâches déjà construits dedans.
///
/// IMPORTANT :
/// Ce script NE TOUCHE PAS à la scène actuelle (SampleScene, avec le sous-marin
/// dans sa boîte) — il crée une scène séparée "Practice.unity" dans
/// Assets/Scenes/, que vous pouvez ouvrir/fermer indépendamment.
///
/// DIMENSIONS DU COULOIR A (bassin Woollett Aquatics Center) :
///   Largeur (X) : 12.5 m
///   Longueur (Z) : 22.86 m
///   Profondeur (Y) : 2.13 m
///   Volume ≈ 609 m³
///
/// CONVENTION :
/// - Surface de l'eau au niveau Y = 0 (cohérent avec Buoyancy.cs : waterLevelY = 0).
/// - Fond du bassin à Y = -profondeur.
/// - Le couloir est centré sur X=0, et s'étend en Z de -longueur/2 à +longueur/2.
/// - Le sous-marin entre par l'extrémité Z négative et avance vers +Z.
///
/// UTILISATION :
/// Menu Unity → ASUQTR → Construire la scène Practice (Couloir A)
///
/// Au fur et à mesure que d'autres tâches RoboSub sont construites (Recon,
/// Deploy, Restore/Octogone), ajoutez leur placement dans la méthode
/// BuildLaneACourse() ci-dessous, à la suite du Gate et du Slalom.
/// </summary>
public static class PracticeSceneBuilder
{
    // Dimensions réelles du couloir A (mètres)
    private const float LaneWidth = 12.5f;
    private const float LaneLength = 22.86f;
    private const float LaneDepth = 2.13f;

    private const string ScenePath = "Assets/Scenes/Practice.unity";

    [MenuItem("ASUQTR/Construire la scène Practice (Couloir A)")]
    public static void BuildPracticeScene()
    {
        // Nouvelle scène vide avec Main Camera + Directional Light par défaut.
        // (Cette opération propose de sauvegarder la scène actuelle si elle a des
        // changements non sauvegardés — elle ne modifie jamais SampleScene.unity sur disque.)
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        BuildPool();
        BuildLaneACourse();

        // Sauvegarde sous Assets/Scenes/Practice.unity (écrase si déjà existant).
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);

        if (saved)
        {
            Debug.Log("[PracticeSceneBuilder] Scène 'Practice' créée et sauvegardée : " + ScenePath);
        }
        else
        {
            Debug.LogWarning("[PracticeSceneBuilder] La sauvegarde de la scène a été annulée ou a échoué.");
        }
    }

    // =====================================================================
    //  BASSIN (volume d'eau du couloir A)
    // =====================================================================

    private static void BuildPool()
    {
        GameObject pool = new GameObject("Pool_LaneA");

        // Volume d'eau visuel : un Cube mis à l'échelle des dimensions réelles du couloir.
        // Centré en X/Z, du fond (Y = -LaneDepth) à la surface (Y = 0).
        GameObject waterVolume = GameObject.CreatePrimitive(PrimitiveType.Cube);
        waterVolume.name = "WaterVolume";
        waterVolume.transform.SetParent(pool.transform, worldPositionStays: false);
        waterVolume.transform.localPosition = new Vector3(0f, -LaneDepth * 0.5f, 0f);
        waterVolume.transform.localScale = new Vector3(LaneWidth, LaneDepth, LaneLength);

        // Matériau bleu semi-transparent (URP Lit en mode Transparent).
        Renderer rend = waterVolume.GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = CreateTransparentMaterial(new Color(0.1f, 0.45f, 0.75f, 0.25f));

        // Le volume d'eau n'est pas un obstacle physique — collider en trigger seulement,
        // utile plus tard pour détecter "le sous-marin est dans l'eau / hors limites".
        Collider col = waterVolume.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        // Sol du bassin (simple plan visuel, gris clair, au fond du bassin).
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(pool.transform, worldPositionStays: false);
        floor.transform.localPosition = new Vector3(0f, -LaneDepth - 0.05f, 0f);
        floor.transform.localScale = new Vector3(LaneWidth, 0.1f, LaneLength);

        Renderer floorRend = floor.GetComponent<Renderer>();
        if (floorRend != null)
            floorRend.sharedMaterial = CreateOpaqueMaterial(new Color(0.75f, 0.75f, 0.7f));

        BuildPerimeterWalls(pool);

        Undo.RegisterCreatedObjectUndo(pool, "Create Pool_LaneA");
    }

    /// <summary>
    /// Murs invisibles (transparents) entourant le bassin sur ses 4 côtés, du fond
    /// jusqu'à un peu au-dessus de la surface. Purement physiques (collider plein,
    /// pas trigger) : empêchent le sous-marin de sortir du volume d'eau, sans gêner
    /// la vue (matériau quasi transparent). Demande explicite : "un mur transparent
    /// qui empêche le sous-marin de sortir de l'eau".
    /// </summary>
    private static void BuildPerimeterWalls(GameObject pool)
    {
        const float wallThickness = 0.1f;
        const float freeboard = 0.4f; // hauteur au-dessus de la surface (Y=0)
        float wallHeight = LaneDepth + freeboard;
        float wallCenterY = -LaneDepth * 0.5f + freeboard * 0.5f;

        Material wallMat = CreateTransparentMaterial(new Color(0.6f, 0.85f, 1f, 0.06f));

        // Murs latéraux (X = ±LaneWidth/2), le long de l'axe Z.
        CreateWall(pool.transform, "Wall_Left", new Vector3(-LaneWidth * 0.5f, wallCenterY, 0f),
            new Vector3(wallThickness, wallHeight, LaneLength), wallMat);
        CreateWall(pool.transform, "Wall_Right", new Vector3(LaneWidth * 0.5f, wallCenterY, 0f),
            new Vector3(wallThickness, wallHeight, LaneLength), wallMat);

        // Murs d'extrémité (Z = ±LaneLength/2), le long de l'axe X.
        CreateWall(pool.transform, "Wall_Near", new Vector3(0f, wallCenterY, -LaneLength * 0.5f),
            new Vector3(LaneWidth, wallHeight, wallThickness), wallMat);
        CreateWall(pool.transform, "Wall_Far", new Vector3(0f, wallCenterY, LaneLength * 0.5f),
            new Vector3(LaneWidth, wallHeight, wallThickness), wallMat);
    }

    private static void CreateWall(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, worldPositionStays: false);
        wall.transform.localPosition = localPos;
        wall.transform.localScale = localScale;

        Renderer rend = wall.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = mat;

        // Collider plein (PAS trigger) : bloque physiquement le Rigidbody du sous-marin.
        Collider col = wall.GetComponent<Collider>();
        if (col != null) col.isTrigger = false;

        Undo.RegisterCreatedObjectUndo(wall, "Create " + name);
    }

    // =====================================================================
    //  COURS DU COULOIR A (tâches RoboSub)
    // =====================================================================

    private static void BuildLaneACourse()
    {
        GameObject course = new GameObject("Course_LaneA");
        Undo.RegisterCreatedObjectUndo(course, "Create Course_LaneA");

        // ─────────────────────────────────────────────────────────────────
        // RÉORGANISATION (suite à la carte officielle du parcours) :
        // - Les tâches ne sont PAS toutes alignées sur l'axe central (X=0) :
        //   elles sont décalées latéralement en zigzag, comme sur l'image.
        // - L'espacement en Z entre tâches a été resserré pour que tout le
        //   parcours (Gate → Restore) reste DANS les limites du bassin
        //   (±LaneLength/2 = ±11.43 m) — Task5 dépassait avant ce correctif.
        // ─────────────────────────────────────────────────────────────────

        // ── Tâche 1 : Gate (Begin Assessment) ───────────────────────────
        // Centrée (X=0), proche de l'extrémité d'entrée (Z négatif).
        float gateX = 0f;
        // Marge agrandie (3.0 m au lieu de 2.0 m) suite au retour d'Elliot :
        // le Gate était trop proche du mur d'entrée du bassin.
        float gateZ = -LaneLength * 0.5f + 3.0f;

        GameObject gateGO = new GameObject("Task1_Gate");
        gateGO.transform.SetParent(course.transform, worldPositionStays: false);
        gateGO.transform.localPosition = new Vector3(gateX, -LaneDepth, gateZ);

        Task1GateBuilder gateBuilder = gateGO.AddComponent<Task1GateBuilder>();
        gateBuilder.BuildGate();

        Undo.RegisterCreatedObjectUndo(gateGO, "Create Task1_Gate");

        // ── Tâche 2 : Avoid Debris (Slalom) ──────────────────────────────
        // Décalée vers la droite (+X), espacement de sets resserré (2.0 m
        // au lieu de 3.0 m) pour laisser de la place aux tâches suivantes.
        float debrisX = 1.3f;
        float debrisZ = gateZ + 3.0f;

        GameObject debrisGO = new GameObject("Task2_AvoidDebris");
        debrisGO.transform.SetParent(course.transform, worldPositionStays: false);
        debrisGO.transform.localPosition = new Vector3(debrisX, -LaneDepth, debrisZ);

        Task2AvoidDebrisBuilder debrisBuilder = debrisGO.AddComponent<Task2AvoidDebrisBuilder>();
        debrisBuilder.setSpacingZ = 2.0f;
        debrisBuilder.BuildAvoidDebris();

        Undo.RegisterCreatedObjectUndo(debrisGO, "Create Task2_AvoidDebris");

        float debrisEndZ = debrisZ + (debrisBuilder.numberOfSets - 1) * debrisBuilder.setSpacingZ;

        // ── Tâche 3 : Recon (Bins) ───────────────────────────────────────
        // Décalée vers la gauche (-X) — alternance en zigzag par rapport au Slalom.
        float reconX = -1.3f;
        float reconZ = debrisEndZ + 3.0f;

        GameObject reconGO = new GameObject("Task3_Recon");
        reconGO.transform.SetParent(course.transform, worldPositionStays: false);
        reconGO.transform.localPosition = new Vector3(reconX, -LaneDepth, reconZ);

        Task3ReconBuilder reconBuilder = reconGO.AddComponent<Task3ReconBuilder>();
        reconBuilder.BuildRecon();

        Undo.RegisterCreatedObjectUndo(reconGO, "Create Task3_Recon");

        // ── Path markers (3.2.8) ──────────────────────────────────────
        // Purement visuels, aucun point attribué. Chaque segment relie le
        // centre d'une tâche à la suivante, orienté en diagonale comme sur
        // la carte officielle (et non plus systématiquement parallèle à Z).
        CreatePathMarker(course.transform, "Path_GateToSlalom",
            new Vector3(gateX, -LaneDepth + 0.03f, gateZ),
            new Vector3(debrisX, -LaneDepth + 0.03f, debrisZ));
        CreatePathMarker(course.transform, "Path_SlalomToRecon",
            new Vector3(debrisX, -LaneDepth + 0.03f, debrisEndZ),
            new Vector3(reconX, -LaneDepth + 0.03f, reconZ));

        // ── Tâche 4 : Deploy (Torpedoes) ─────────────────────────────────
        // Décalée vers la droite (+X), zigzag avec Recon. Porte le 1er pinger.
        float deployX = 1.3f;
        float deployZ = reconZ + 3.0f;

        GameObject deployGO = new GameObject("Task4_Deploy");
        deployGO.transform.SetParent(course.transform, worldPositionStays: false);
        deployGO.transform.localPosition = new Vector3(deployX, -LaneDepth, deployZ);

        Task4DeployBuilder deployBuilder = deployGO.AddComponent<Task4DeployBuilder>();
        deployBuilder.BuildDeploy();

        Undo.RegisterCreatedObjectUndo(deployGO, "Create Task4_Deploy");

        // ── Tâche 5 : Restore (Octogone) + Pinger #2 ─────────────────────
        // Décalée vers la gauche (-X). Espacement resserré pour rester bien
        // À L'INTÉRIEUR du bassin (anciennement hors limites : Z dépassait
        // +LaneLength/2). L'anneau flotte à la surface ; ce GameObject est
        // posé au fond et Task5RestoreOctagonBuilder remonte l'anneau
        // jusqu'à la surface (poolDepth).
        float restoreX = -1.0f;
        float restoreZ = deployZ + 4.0f;

        GameObject restoreGO = new GameObject("Task5_Restore");
        restoreGO.transform.SetParent(course.transform, worldPositionStays: false);
        restoreGO.transform.localPosition = new Vector3(restoreX, -LaneDepth, restoreZ);

        Task5RestoreOctagonBuilder restoreBuilder = restoreGO.AddComponent<Task5RestoreOctagonBuilder>();
        restoreBuilder.poolDepth = LaneDepth;
        restoreBuilder.BuildRestore();

        Undo.RegisterCreatedObjectUndo(restoreGO, "Create Task5_Restore");

        // ── Prochaines tâches (à ajouter ici une fois construites) ──────
        // TODO Task 6 - Return Home : pas de nouvelle géométrie, logique de
        // passage par le Gate (Task1_Gate) à confirmer côté scoring/contrôle.
    }

    // =====================================================================
    //  PATH MARKERS (3.2.8) — repères visuels orange, sans scoring
    // =====================================================================

    /// <summary>
    /// Crée un repère de chemin (Path marker) : segment droit orange posé sur le
    /// fond du bassin, reliant deux points (centre-à-centre des tâches voisines).
    /// Orienté en diagonale quand les tâches sont décalées latéralement (zigzag),
    /// comme sur la carte officielle du parcours — et non plus systématiquement
    /// parallèle à l'axe Z.
    /// </summary>
    private static void CreatePathMarker(Transform parent, string name, Vector3 from, Vector3 to)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = name;
        marker.transform.SetParent(parent, worldPositionStays: false);

        Vector3 mid = (from + to) * 0.5f;
        Vector3 delta = to - from;
        float length = Mathf.Max(delta.magnitude, 0.01f);

        marker.transform.localPosition = mid;
        marker.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        marker.transform.localScale = new Vector3(0.15f, 0.02f, length);

        Renderer rend = marker.GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = CreateOpaqueMaterial(new Color(1f, 0.45f, 0f));

        Collider col = marker.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        Undo.RegisterCreatedObjectUndo(marker, "Create " + name);
    }

    // =====================================================================
    //  MATÉRIAUX
    // =====================================================================

    private static Material CreateOpaqueMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        Material mat = new Material(shader);

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        // Configuration "Transparent" pour URP Lit (équivalent à choisir
        // Surface Type = Transparent dans l'Inspector du matériau).
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // 1 = Transparent
            mat.SetFloat("_Blend", 0f);   // 0 = Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
        }
        else
        {
            // Fallback pipeline standard.
            mat.SetFloat("_Mode", 3f); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        return mat;
    }
}
#endif
