using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Construit la géométrie de la Tâche 2 RoboSub : "Éviter les débris (Slalom)".
///
/// Référence (texte officiel, section 3.2.3) :
/// Naviguer dans un chenal obstrué par des débris : des tuyaux verticaux en PVC
/// ROUGE et BLANC, ancrés au sol et flottant verticalement à différentes hauteurs.
/// Disposition : BLANC à gauche, ROUGE au milieu, BLANC à droite.
/// Il y a 3 ensembles de tuyaux ; le sous-marin marque des points pour chaque
/// ensemble franchi avec succès.
///   - Bonus : rester du même côté du tuyau rouge qu'au passage du Gate
///     (si le diviseur rouge du Gate était à droite, traverser avec le rouge à droite).
///   - Bonus : rester DANS le plan des tuyaux (ni au-dessus, ni en dessous) ;
///     dès qu'une partie de l'AUV dépasse ce plan, il est considéré "dedans".
///
/// Référence CAD (Task02_Debris_Full.pdf, dossier RoboSub Data Sharing) :
/// Documente la quincaillerie PVC réelle (tés, coupleurs, bouchons, pieds en
/// chevalet) utilisée pour ancrer chaque tuyau au fond du bassin. Ces pièces de
/// fixation ne sont pas modélisées ici — seuls les tuyaux flottants (l'élément
/// que l'AUV doit éviter/traverser) sont représentés, ce qui est suffisant pour
/// la simulation.
///
/// UTILISATION :
/// 1. Créer un GameObject vide "Task2_AvoidDebris", le positionner où désiré
///    dans le couloir (après le Gate).
/// 2. Ajouter ce script (Add Component → Task2AvoidDebrisBuilder).
/// 3. Clic droit sur le composant dans l'Inspector → "Build Task 2 - Avoid Debris".
/// 4. Les 3 ensembles (9 tuyaux au total) sont créés comme enfants de ce GameObject.
/// </summary>
public class Task2AvoidDebrisBuilder : MonoBehaviour
{
    [Header("Tuyaux (PVC)")]
    [Tooltip("Diamètre des tuyaux (exagéré pour visibilité)")]
    public float poleDiameter = 0.05f;

    [Tooltip("Longueur de chaque tuyau flottant (60 in = 1.524 m, même échelle que le Gate)")]
    public float poleLength = 1.524f;

    [Tooltip("Hauteur du bas du tuyau par rapport au fond du bassin (\"flottant\", n'atteint pas le sol)")]
    public float poleBottomHeight = 0.3f;

    [Tooltip("Variation aléatoire (+/-) appliquée à poleBottomHeight pour chaque tuyau, simulant " +
             "\"ancrés à différentes hauteurs\" tel que décrit dans la spec officielle.")]
    public float poleHeightJitter = 0.15f;

    [Header("Disposition d'un ensemble (Blanc / Rouge / Blanc)")]
    [Tooltip("Distance entre deux tuyaux adjacents au sein d'un même ensemble")]
    public float poleSpacingX = 1.0f;

    [Header("Disposition des ensembles le long du couloir")]
    [Tooltip("Nombre d'ensembles (3 par la spec officielle)")]
    public int numberOfSets = 3;

    [Tooltip("Distance entre deux ensembles consécutifs (le long de l'axe Z)")]
    public float setSpacingZ = 3.0f;

    [Tooltip("Décalage latéral (X) entre ensembles consécutifs — chaque ensemble est " +
             "décalé par rapport au précédent (zigzag), comme demandé : \"ils ont tous " +
             "un offset\". Centré sur l'axe X=0 du Task2_AvoidDebris.")]
    public float setLateralOffsetX = 0.5f;

    [Tooltip("Décalage de hauteur (Y) entre ensembles consécutifs, en plus du bruit " +
             "aléatoire par tuyau (poleHeightJitter). Donne à chaque ensemble une " +
             "hauteur d'ancrage visiblement différente (pas seulement du bruit), " +
             "tel que demandé : \"ils ont tous un offset\".")]
    public float setHeightOffsetY = 0.2f;

    [Tooltip("Graine aléatoire pour poleHeightJitter, pour un résultat reproductible")]
    public int randomSeed = 2;

    private static readonly Color ColorWhitePVC = Color.white;
    private static readonly Color ColorRed = new Color(0.8f, 0.05f, 0.05f);

    [ContextMenu("Build Task 2 - Avoid Debris")]
    public void BuildAvoidDebris()
    {
        ClearChildren();

        Random.InitState(randomSeed);

        for (int setIndex = 0; setIndex < numberOfSets; setIndex++)
        {
            float z = setIndex * setSpacingZ;
            string setName = "Set" + (setIndex + 1);

            // Décalages propres à cet ensemble (zigzag centré sur 0), pour qu'aucun
            // des "numberOfSets" ensembles ne soit identique à un autre — ni en X,
            // ni en hauteur d'ancrage (Y).
            float setCenteredIndex = setIndex - (numberOfSets - 1) * 0.5f;
            float setOffsetX = setCenteredIndex * setLateralOffsetX;
            float setOffsetY = setCenteredIndex * setHeightOffsetY;

            // Vue de dessus (CAD officiel) : les 3 tuyaux d'un même ensemble sont
            // alignés sur le même Z (ligne droite Blanc/Rouge/Blanc) ; c'est l'ensemble
            // entier (toute la rangée) qui est décalé latéralement (zigzag) d'un
            // ensemble à l'autre — pas chaque tuyau individuellement.
            CreatePole(setName + "_White_Left", setOffsetX - poleSpacingX, z, setOffsetY, ColorWhitePVC);
            CreatePole(setName + "_Red_Middle", setOffsetX, z, setOffsetY, ColorRed);
            CreatePole(setName + "_White_Right", setOffsetX + poleSpacingX, z, setOffsetY, ColorWhitePVC);
        }

        Debug.Log("[Task2AvoidDebrisBuilder] Avoid Debris (Slalom) construit — " + numberOfSets +
                  " ensembles Blanc/Rouge/Blanc. Rappel : bonus de points pour traverser du même " +
                  "côté que le diviseur rouge du Gate.");

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

    private void CreatePole(string name, float x, float z, float heightOffset, Color color)
    {
        float jitter = Random.Range(-poleHeightJitter, poleHeightJitter);
        float bottomHeight = Mathf.Max(0f, poleBottomHeight + heightOffset + jitter);
        float centerY = bottomHeight + poleLength * 0.5f;

        CreatePrimitive(name, new Vector3(x, centerY, z),
            new Vector3(poleDiameter, poleLength * 0.5f, poleDiameter),
            color);
    }

    /// <summary>
    /// Crée un cylindre vertical comme enfant de ce GameObject, avec une couleur unie.
    /// localScale=(diam, longueur/2, diam) → longueur réelle = scale.y * 2
    /// (taille par défaut du cylindre Unity : hauteur=2, rayon=0.5).
    /// </summary>
    private GameObject CreatePrimitive(string name, Vector3 localPos, Vector3 localScale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
#endif

        go.transform.SetParent(transform, worldPositionStays: false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = CreateColorMaterial(color);
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
}
