using UnityEngine;

/// <summary>
/// Contrôleur clavier 6-DOF pour tests locaux du sous-marin ASUQTR.
///
/// RÔLE :
/// Remplace ThrusterTestInput lors des tests sans ROS.
/// Calcule les 8 commandes normalisées [-1..1] à partir des entrées clavier,
/// puis les écrit dans ThrusterReceiver.motorThrottles (même interface que ROS).
///
/// PRINCIPE (allocation X-frame) :
/// Les 4 thrusters horizontaux (T0,T1,T6,T7) forment un cadre en X.
/// Les 4 thrusters verticaux (T2,T3,T4,T5) contrôlent heave/pitch/roulis.
///
/// Mapping des indices (doit correspondre à l'ordre des ThrusterPoint dans ThrusterApplier) :
///   0 = T1  avant-droite  HORIZONTAL  dir=(-0.707, 0, +0.707)
///   1 = T2  arrière-droite HORIZONTAL dir=(-0.707, 0, -0.707)
///   2 = T3  avant-gauche  VERTICAL    dir=(0, -1, 0)
///   3 = T4  arrière-gauche VERTICAL   dir=(0, -1, 0)
///   4 = T5  avant-droite  VERTICAL    dir=(0, -1, 0)
///   5 = T6  arrière-droite VERTICAL   dir=(0, -1, 0)
///   6 = T7  avant-gauche  HORIZONTAL  dir=(+0.707, 0, +0.707)
///   7 = T8  arrière-gauche HORIZONTAL dir=(+0.707, 0, -0.707)
///
/// CONTRÔLES CLAVIER :
///   W / S          → Surge   (avant / arrière)
///   A / D          → Lacet   (gauche / droite)
///   Q / E          → Sway    (dérive gauche / droite)
///   Espace / Ctrl  → Heave   (montée / descente)
///   R / F          → Tangage (nez haut / nez bas)
///   Z / X          → Roulis  (droite / gauche)
///
/// INTÉGRATION :
/// - Attacher ce script sur le même GameObject que ThrusterReceiver.
/// - Désactiver ce composant quand la connexion ROS est active (ModeManager).
/// - Utilise la même interface motorThrottles que ThrusterReceiver → aucune
///   modification de ThrusterApplier requise.
/// </summary>
public class ThrusterKeyboardController : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Référence au ThrusterReceiver (même interface que les commandes ROS).")]
    public ThrusterReceiver receiver;

    [Header("Limites")]
    [Tooltip("Commande maximale envoyée aux thrusters (0.0 à 1.0). Limiter pour protéger les moteurs.")]
    [Range(0f, 1f)]
    public float maxCommand = 0.8f;

    [Tooltip("Désactiver ce contrôleur si une connexion ROS active est détectée.")]
    public bool disableWhenRosConnected = true;

    [Header("Lissage")]
    [Tooltip("Facteur de lissage (0 = instantané, 1 = jamais). Réduit les à-coups.")]
    [Range(0f, 0.95f)]
    public float smoothing = 0.15f;

    // Commandes lissées (interne)
    private float[] _smoothed = new float[8];

    void Reset()
    {
        receiver = GetComponent<ThrusterReceiver>();
    }

    void Awake()
    {
        if (receiver == null)
            receiver = GetComponent<ThrusterReceiver>();
    }

    void Update()
    {
        if (receiver == null) return;

        // Désactivation si ROS connecté
        if (disableWhenRosConnected && SimpleRosSocket.Instance != null
            && SimpleRosSocket.Instance.IsConnected)
            return;

        // ──────────────────────────────────────────────
        // 1. Lecture des axes de commande normalisés [-1, +1]
        // ──────────────────────────────────────────────
        float surge = Input.GetAxis("Vertical");                                   // W/S
        float yaw   = Input.GetAxis("Horizontal");                                 // A/D

        float sway  = (Input.GetKey(KeyCode.E) ? 1f : 0f)
                    + (Input.GetKey(KeyCode.Q) ? -1f : 0f);

        float heave = (Input.GetKey(KeyCode.Space)       ? 1f : 0f)
                    + (Input.GetKey(KeyCode.LeftControl) ? -1f : 0f);

        float pitch = (Input.GetKey(KeyCode.R) ? 1f : 0f)
                    + (Input.GetKey(KeyCode.F) ? -1f : 0f);

        float roll  = (Input.GetKey(KeyCode.Z) ? 1f : 0f)
                    + (Input.GetKey(KeyCode.X) ? -1f : 0f);

        // ──────────────────────────────────────────────
        // 2. Matrice d'allocation (TAM analytique)
        // ──────────────────────────────────────────────
        // Dérivée de la TAM 6×8 calculée à partir des positions/directions réelles ASUQTR :
        //
        // Horizontal X-frame (indices 0,1,6,7) :
        //   surge (+Fz) : [0]=+, [1]=-, [6]=+, [7]=-
        //   sway  (+Fx) : [0]=-, [1]=-, [6]=+, [7]=+
        //   yaw   (+My) : [0]=-, [1]=+, [6]=+, [7]=-
        //
        // Vertical (indices 2,3,4,5) — thruster dir=(0,-1,0) :
        //   heave haut (+Fy) : tous négatifs (thrusters poussent vers le bas)
        //   tangage nez-bas  : avant=[2,4]=+, arrière=[3,5]=-
        //   roulis droit (+Tz): gauche=[2,3]=+, droite=[4,5]=-

        float[] raw = new float[8];

        // Horizontaux
        raw[0] = ( surge - sway - yaw);   // T1 avant-droite
        raw[1] = (-surge - sway + yaw);   // T2 arrière-droite
        raw[6] = ( surge + sway + yaw);   // T7 avant-gauche
        raw[7] = (-surge + sway - yaw);   // T8 arrière-gauche

        // Verticaux
        raw[2] = (-heave + pitch + roll);  // T3 avant-gauche
        raw[3] = (-heave + pitch - roll);  // T4 arrière-gauche
        raw[4] = (-heave - pitch + roll);  // T5 avant-droite
        raw[5] = (-heave - pitch - roll);  // T6 arrière-droite

        // ──────────────────────────────────────────────
        // 3. Normalisation (évite la saturation)
        // ──────────────────────────────────────────────
        float maxRaw = 0f;
        foreach (float v in raw)
            maxRaw = Mathf.Max(maxRaw, Mathf.Abs(v));

        float scale = (maxRaw > 1f) ? 1f / maxRaw : 1f;
        scale *= maxCommand;

        // ──────────────────────────────────────────────
        // 4. Lissage + écriture dans motorThrottles
        // ──────────────────────────────────────────────
        for (int i = 0; i < 8; i++)
        {
            float target = raw[i] * scale;
            _smoothed[i] = Mathf.Lerp(target, _smoothed[i], smoothing);
            receiver.motorThrottles[i] = _smoothed[i];
        }
    }

    void OnGUI()
    {
        if (receiver == null) return;
        if (disableWhenRosConnected && SimpleRosSocket.Instance != null
            && SimpleRosSocket.Instance.IsConnected) return;

        // Affichage minimal HUD
        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        s.normal.textColor = new Color(0f, 1f, 0.4f);

        float x = 10f, y = 10f;
        GUI.Label(new Rect(x, y, 400f, 20f), "═ ASUQTR Keyboard Controller ═", s); y += 20f;
        GUI.Label(new Rect(x, y, 400f, 18f), "W/S=Surge  A/D=Lacet  Q/E=Sway", s); y += 18f;
        GUI.Label(new Rect(x, y, 400f, 18f), "Space/Ctrl=Heave  R/F=Tangage  Z/X=Roulis", s); y += 22f;

        string[] names = { "T1 H-fR", "T2 H-rR", "T3 V-fL", "T4 V-rL",
                           "T5 V-fR", "T6 V-rR", "T7 H-fL", "T8 H-rL" };

        for (int i = 0; i < 8; i++)
        {
            float cmd = receiver.motorThrottles[i];
            s.normal.textColor = Mathf.Abs(cmd) > 0.05f
                ? new Color(0f, 1f, 0.4f)
                : new Color(0.5f, 0.5f, 0.5f);
            GUI.Label(new Rect(x, y, 350f, 18f),
                $"[{i}] {names[i]}  {cmd:+0.00;-0.00;+0.00}", s);
            y += 18f;
        }
    }
}
