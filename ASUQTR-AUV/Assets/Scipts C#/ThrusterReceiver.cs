using System;
using System.Globalization;
using UnityEngine;


/// <summary>
/// Réception des commandes moteurs publiées sur le topic ROS "/thruster_cmd".
///
/// Rôle :
///— S'abonner aux messages bruts reçus par SimpleRosSocket (OnRawMessage),
///— Extraire le tableau "throttles" depuis le JSON reçu,
///— Mettre à jour le tableau public `motorThrottles` utilisé par ThrusterApplier.
///
/// Convention et format attendu (exemple rosbridge JSON) :
/// {
///   "op":"publish",
///   "topic":"/thruster_cmd",
///   "msg":{
///     "efforts":[0.0, 8.0, -5.0, ...]
///   }
/// }
///
/// Remarques importantes d'intégration Unity → ROS :
/// - Les valeurs de `efforts` peuvent être en Newtons; elles sont normalisées avec maxThrusterForceN.
/// - L'ancien topic `/actuator/motors` reste accepté pour compatibilité locale.
/// - L'ordre et le mapping des indices doivent être documentés et cohérents entre :
///     • la configuration Unity (ThrusterPoint / ordre du tableau),
///     • le code ROS qui publie les consignes,
///     • le contrôleur qui génère ces consignes.
/// - Ce script réalise une extraction textuelle (recherche de la clé "throttles")
///   — méthode simple mais fragile. Pour plus de robustesse, préférez :
///     `JsonUtility.FromJson<RosbridgeWrapper>(json)` ou Newtonsoft.Json pour
///     désérialiser le message en `RosbridgeWrapper`/`ActuatorThrottleMsg`.
///   Si tu veux, je peux remplacer la parsing manuelle par une désérialisation sûre.
///
/// Sécurité et robustesse :
/// - On protège contre les erreurs de format JSON et les out-of-bounds lors du remplissage
///   du tableau `motorThrottles`.
/// - CultureInfo.InvariantCulture est utilisé pour garantir l'interprétation du séparateur décimal.
///
/// Note sur threading :
/// - SimpleRosSocket invoque OnRawMessage lorsqu'il reçoit des messages WebSocket.
///   Selon l'implémentation du socket, le callback peut provenir d'un thread de réseau.
///   Si vous observez des problèmes de thread/Unity, routez la mise à jour vers le thread
///   principal (ex : en stockant le JSON et en le traitant dans Update()).
/// </summary>
public class ThrusterReceiver : MonoBehaviour
{
    [Header("Thrusters")]
    [Tooltip("Tableau contenant les consignes normalisées des moteurs ([-1..1]).")]
    public float[] motorThrottles = new float[8];

    [Tooltip("Derniers efforts moteurs reçus/calculés en Newtons.")]
    public float[] motorEffortsN = new float[8];

    [Tooltip("Force ROS (N) correspondant à une commande Unity normalisée de 1.0.")]
    public float maxThrusterForceN = 40f;

    private float lastDebugLogTime = -10f;
    private bool receiverRegistered = false;
    private bool loggedMissingSocket = false;
    private bool hasEffortsInNewtons = false;

    public bool HasEffortsInNewtons => hasEffortsInNewtons;

    /// <summary>
    /// Abonnement à l'événement global de réception de messages bruts.
    /// SimpleRosSocket invoque OnRawMessage pour chaque message WebSocket reçu.
    /// </summary>
    void Start()
    {
        TryRegisterReceiver();
    }

    void Update()
    {
        if (!receiverRegistered)
            TryRegisterReceiver();
    }

    void TryRegisterReceiver()
    {
        if (receiverRegistered)
            return;

        if (SimpleRosSocket.Instance != null)
        {
            SimpleRosSocket.Instance.OnRawMessage += HandleRosMessage;
            receiverRegistered = true;
            Debug.Log("[ThrusterReceiver] Registered to SimpleRosSocket.OnRawMessage");
        }
        else if (!loggedMissingSocket)
        {
            loggedMissingSocket = true;
            Debug.LogWarning("[ThrusterReceiver] SimpleRosSocket.Instance is NULL. Will retry registration in Update().");
        }
    }

    /// <summary>
    /// Désabonnement à la destruction pour éviter les références pendantes.
    /// </summary>
    void OnDestroy()
    {
        if (receiverRegistered && SimpleRosSocket.Instance != null)
            SimpleRosSocket.Instance.OnRawMessage -= HandleRosMessage;
    }

    /// <summary>
    /// Callback appelé pour chaque message ROS brut (JSON).
    /// Comportement actuel : filtrage textuel rapide puis extraction du tableau "throttles".
    /// </summary>
    void HandleRosMessage(string json)
    {
        if (!json.Contains("/thruster_cmd") && !json.Contains("/actuator/motors"))
            return;

        try
        {
            // Essayer la désérialisation structurelle en premier lieu.
            RosbridgeWrapper wrapper = JsonUtility.FromJson<RosbridgeWrapper>(json);

            float[] source = null;
            bool sourceIsEfforts = false;
            if (wrapper?.msg != null)
            {
                if (wrapper.msg.efforts != null && wrapper.msg.efforts.Length > 0)
                {
                    source = wrapper.msg.efforts;
                    sourceIsEfforts = true;
                }
                else if (wrapper.msg.throttles != null && wrapper.msg.throttles.Length > 0)
                {
                    source = wrapper.msg.throttles;
                }
            }

            if (source == null)
            {
                // Fallback sur parsing texte si le JSON a une structure différente.
                source = ExtractArrayFromJson(json, "efforts");
                sourceIsEfforts = source != null;
                if (source == null)
                {
                    source = ExtractArrayFromJson(json, "throttles");
                    sourceIsEfforts = false;
                }
            }

            if (source == null)
            {
                Debug.LogWarning("[ThrusterReceiver] /thruster_cmd reçu, mais aucun tableau efforts/throttles trouvé. json=" + json);
                return;
            }

            for (int i = 0; i < source.Length && i < motorThrottles.Length; i++)
            {
                if (sourceIsEfforts)
                {
                    motorEffortsN[i] = source[i];
                    motorThrottles[i] = NormalizeThrusterCommand(source[i]);
                }
                else
                {
                    motorThrottles[i] = Mathf.Clamp(source[i], -1f, 1f);
                    motorEffortsN[i] = ThrusterModel.CommandToForceN(motorThrottles[i]);
                }
            }

            hasEffortsInNewtons = sourceIsEfforts;

            if (Time.time - lastDebugLogTime > 1f)
            {
                lastDebugLogTime = Time.time;
                Debug.Log(
                    "[ThrusterReceiver] /thruster_cmd reçu -> efforts_N=[" +
                    string.Join(", ", motorEffortsN) +
                    "] throttles=[" +
                    string.Join(", ", motorThrottles) +
                    "]"
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ThrusterReceiver] Parse error: " + e.Message + " | json=" + json);
        }
    }

    float[] ExtractArrayFromJson(string json, string key)
    {
        int idx = json.IndexOf('"' + key + '"', StringComparison.Ordinal);
        if (idx < 0) return null;

        int start = json.IndexOf('[', idx);
        int end = json.IndexOf(']', start);
        if (start < 0 || end < 0) return null;

        string array = json.Substring(start + 1, end - start - 1);
        string[] values = array.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
        float[] result = new float[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            if (float.TryParse(values[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                result[i] = value;
            else
                return null;
        }

        return result;
    }

    float NormalizeThrusterCommand(float value)
    {
        if (Mathf.Abs(value) <= 1f)
            return Mathf.Clamp(value, -1f, 1f);

        float scale = Mathf.Max(0.001f, maxThrusterForceN);
        return Mathf.Clamp(value / scale, -1f, 1f);
    }
}
