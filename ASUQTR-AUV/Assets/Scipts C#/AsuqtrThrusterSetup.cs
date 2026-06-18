using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Utilitaire de configuration automatique des 8 ThrusterPoint ASUQTR.
///
/// BUT :
/// Crée ou met à jour les 8 GameObjects ThrusterPoint sous le sous-marin,
/// aux positions et orientations réelles issues du fichier :
/// "Parametre_Sous_Marin_12_Juin_2026.txt"
///
/// POSITIONS RÉELLES (repère corps Blender → repère corps Unity) :
/// Blender : X=droite, Y=avant, Z=haut
/// Unity   : X=droite, Y=haut,  Z=avant
/// Conversion : BlenderX→UnityX, BlenderY→UnityZ, BlenderZ→UnityY
///
/// MAPPING DES INDICES :
///   Index 0 → T1 : avant-droite   HORIZONTAL  dir=(-0.707, 0, +0.707)
///   Index 1 → T2 : arrière-droite HORIZONTAL  dir=(-0.707, 0, -0.707)
///   Index 2 → T3 : avant-gauche   VERTICAL    dir=(0, -1, 0)
///   Index 3 → T4 : arrière-gauche VERTICAL    dir=(0, -1, 0)
///   Index 4 → T5 : avant-droite   VERTICAL    dir=(0, -1, 0)
///   Index 5 → T6 : arrière-droite VERTICAL    dir=(0, -1, 0)
///   Index 6 → T7 : avant-gauche   HORIZONTAL  dir=(+0.707, 0, +0.707)
///   Index 7 → T8 : arrière-gauche HORIZONTAL  dir=(+0.707, 0, -0.707)
///
/// UTILISATION :
/// 1. Attacher ce script au GameObject racine du sous-marin.
/// 2. Dans l'Inspector, cliquer "Setup Thrusters ASUQTR" (ContextMenu).
/// 3. Les 8 GameObjects ThrusterPoint sont créés en tant qu'enfants.
/// 4. Vérifier que ThrusterApplier.thrusters[] référence ces objets dans l'ordre.
///
/// REMARQUE :
/// ThrustDirection = transform.forward du GameObject (axe Z local).
/// Ce script oriente chaque GameObject pour que son Z local pointe dans la
/// direction de poussée souhaitée.
/// </summary>
public class AsuqtrThrusterSetup : MonoBehaviour
{
    // =========================================================
    // DONNÉES RÉELLES (Parametre_Sous_Marin_12_Juin_2026.txt)
    // =========================================================

    private static readonly ThrusterData[] THRUSTERS = new ThrusterData[]
    {
        // --- HORIZONTAUX (X-frame) ---
        new ThrusterData
        {
            Name        = "Thruster_T1_H_FrontRight",
            Index       = 0,
            // Blender pos (0.2987, 0.2130, 0) → Unity (0.2987, 0, 0.2130)
            LocalPos    = new Vector3( 0.2987f, 0f,  0.2130f),
            // Blender dir (-0.707, 0.707, 0) → Unity (-0.707, 0, 0.707)
            ThrustDir   = new Vector3(-0.7071f, 0f,  0.7071f),
            IsVertical  = false
        },
        new ThrusterData
        {
            Name        = "Thruster_T2_H_RearRight",
            Index       = 1,
            // Blender pos (0.2987, -0.2130, 0) → Unity (0.2987, 0, -0.2130)
            LocalPos    = new Vector3( 0.2987f, 0f, -0.2130f),
            // Blender dir (-0.707, -0.707, 0) → Unity (-0.707, 0, -0.707)
            ThrustDir   = new Vector3(-0.7071f, 0f, -0.7071f),
            IsVertical  = false
        },

        // --- VERTICAUX ---
        new ThrusterData
        {
            Name        = "Thruster_T3_V_FrontLeft",
            Index       = 2,
            // Blender pos (-0.1073, 0.2725, 0) → Unity (-0.1073, 0, 0.2725)
            LocalPos    = new Vector3(-0.1073f, 0f,  0.2725f),
            // Blender dir (0, 0, -1) → Unity (0, -1, 0)
            ThrustDir   = new Vector3(0f, -1f, 0f),
            IsVertical  = true
        },
        new ThrusterData
        {
            Name        = "Thruster_T4_V_RearLeft",
            Index       = 3,
            // Blender pos (-0.1073, -0.2725, 0) → Unity (-0.1073, 0, -0.2725)
            LocalPos    = new Vector3(-0.1073f, 0f, -0.2725f),
            ThrustDir   = new Vector3(0f, -1f, 0f),
            IsVertical  = true
        },
        new ThrusterData
        {
            Name        = "Thruster_T5_V_FrontRight",
            Index       = 4,
            // Blender pos (0.1073, 0.2725, 0) → Unity (0.1073, 0, 0.2725)
            LocalPos    = new Vector3( 0.1073f, 0f,  0.2725f),
            ThrustDir   = new Vector3(0f, -1f, 0f),
            IsVertical  = true
        },
        new ThrusterData
        {
            Name        = "Thruster_T6_V_RearRight",
            Index       = 5,
            // Blender pos (0.1073, -0.2725, 0) → Unity (0.1073, 0, -0.2725)
            LocalPos    = new Vector3( 0.1073f, 0f, -0.2725f),
            ThrustDir   = new Vector3(0f, -1f, 0f),
            IsVertical  = true
        },

        // --- HORIZONTAUX (suite) ---
        new ThrusterData
        {
            Name        = "Thruster_T7_H_FrontLeft",
            Index       = 6,
            // Blender pos (-0.2987, 0.2130, 0) → Unity (-0.2987, 0, 0.2130)
            LocalPos    = new Vector3(-0.2987f, 0f,  0.2130f),
            // Blender dir (0.707, 0.707, 0) → Unity (0.707, 0, 0.707)
            ThrustDir   = new Vector3( 0.7071f, 0f,  0.7071f),
            IsVertical  = false
        },
        new ThrusterData
        {
            Name        = "Thruster_T8_H_RearLeft",
            Index       = 7,
            // Blender pos (-0.2987, -0.2130, 0) → Unity (-0.2987, 0, -0.2130)
            LocalPos    = new Vector3(-0.2987f, 0f, -0.2130f),
            // Blender dir (0.707, -0.707, 0) → Unity (0.707, 0, -0.707)
            ThrustDir   = new Vector3( 0.7071f, 0f, -0.7071f),
            IsVertical  = false
        },
    };

    // =========================================================
    // SETUP
    // =========================================================

    [ContextMenu("Setup Thrusters ASUQTR")]
    public void SetupThrusters()
    {
        // Supprimer les anciens ThrusterPoint enfants
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<ThrusterPoint>() != null)
            {
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }
        }

        // Créer les nouveaux ThrusterPoint
        foreach (var td in THRUSTERS)
        {
            GameObject go = new GameObject(td.Name);

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, $"Create {td.Name}");
#endif
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = td.LocalPos;

            // Orienter l'axe Z local (forward) dans la direction de poussée souhaitée
            if (td.ThrustDir != Vector3.zero)
                go.transform.localRotation = Quaternion.LookRotation(td.ThrustDir, Vector3.up);

            // Ajouter ThrusterPoint
            ThrusterPoint tp = go.AddComponent<ThrusterPoint>();
            tp.thrusterIndex = td.Index;
            tp.invertDirection = false;

            // Gizmo de couleur pour distinguer H et V (tag optionnel)
            go.tag = td.IsVertical ? "Untagged" : "Untagged";
        }

        Debug.Log($"[AsuqtrThrusterSetup] 8 ThrusterPoints créés sous '{gameObject.name}'. " +
                  "Vérifiez l'ordre dans ThrusterApplier.thrusters[].");

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
    }

    // =========================================================
    // GIZMOS : visualisation dans l'éditeur
    // =========================================================

    void OnDrawGizmosSelected()
    {
        foreach (var td in THRUSTERS)
        {
            Vector3 worldPos = transform.TransformPoint(td.LocalPos);
            Vector3 worldDir = transform.TransformDirection(td.ThrustDir);

            Gizmos.color = td.IsVertical ? Color.yellow : Color.cyan;
            Gizmos.DrawSphere(worldPos, 0.02f);
            Gizmos.DrawRay(worldPos, worldDir * 0.1f);

#if UNITY_EDITOR
            Handles.Label(worldPos + Vector3.up * 0.03f, td.Name.Replace("Thruster_", ""));
#endif
        }
    }

    // =========================================================
    // STRUCTURE DE DONNÉES INTERNE
    // =========================================================

    private struct ThrusterData
    {
        public string  Name;
        public int     Index;
        public Vector3 LocalPos;
        public Vector3 ThrustDir;
        public bool    IsVertical;
    }
}
