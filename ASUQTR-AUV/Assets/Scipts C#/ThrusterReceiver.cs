using System;
using System.Globalization;
using UnityEngine;


/// <summary>
/// Réception des commandes moteurs publiées par la control node ROS2 sur "/thruster_cmd".
///
/// Rôle :
///— S'abonner aux messages bruts reçus par SimpleRosSocket (OnRawMessage),
///— Extraire le tableau "efforts" en Newton depuis le JSON reçu,
///— Convertir chaque effort Newton en throttle normalisé Unity [-1, 1],
///— Mettre à jour le tableau public `motorThrottles` utilisé par ThrusterApplier.
///
/// Convention et format attendu (exemple rosbridge JSON) :
/// {
///   "op":"publish",
///   "topic":"/thruster_cmd",
///   "msg":{
///     "header":{...},
///     "efforts":[0.0, 12.0, -5.0, ...]
///   }
/// }
///
/// Remarques importantes d'intégration Unity → ROS :
/// - La control node ROS2 publie des efforts en Newton.
/// - Unity applique des forces via ThrusterModel à partir d'une commande throttle [-1, 1].
/// - Ce script effectue donc l'inverse du mapping hardware approximatif :
///   Newtons ROS -> throttle Unity.
/// - L'ordre et le mapping des indices doivent être documentés et cohérents entre :
///     • la configuration Unity (ThrusterPoint / ordre du tableau),
///     • le code ROS qui publie les consignes,
///     • le contrôleur qui génère ces consignes.
/// - Ce script désérialise l'enveloppe rosbridge avec JsonUtility puis lit msg.efforts.
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
    // Same empirical T200 force table used by the ROS2 actuator node.
    // It maps physical force in Newtons to normalized ContinuousServo throttle.
    // Keeping this table here makes the Unity simulator consume the same /thruster_cmd
    // Newton efforts as the physical actuator path, without changing control_node.
    private static readonly float[] KnownForcesN =
    {
        -40.22f,
        -27.47f,
        -14.71f,
        -5.89f,
        0.0f,
        0.0f,
        0.0f,
        7.85f,
        19.62f,
        34.33f,
        51.50f
    };

    private static readonly float[] KnownThrottles =
    {
        -1.0f,
        -0.75f,
        -0.50f,
        -0.25f,
        -0.0625f,
        0.0f,
        0.0625f,
        0.25f,
        0.50f,
        0.75f,
        1.0f
    };

    [Header("Thrusters")]
    [Tooltip("Tableau contenant les consignes normalisées des moteurs ([-1..1]).")]
    public float[] motorThrottles = new float[8];

    [Header("Debug")]
    [Tooltip("Active les logs de reception /thruster_cmd et de conversion effort -> throttle.")]
    public bool debugLogs = false;

    /// <summary>
    /// Abonnement à l'événement global de réception de messages bruts.
    /// SimpleRosSocket invoque OnRawMessage pour chaque message WebSocket reçu.
    /// </summary>
    void Start()
    {
        if (SimpleRosSocket.Instance != null)
        {
            SimpleRosSocket.Instance.OnRawMessage += HandleRosMessage;
            if (debugLogs)
                Debug.Log("[ThrusterReceiver] Listening for " + SimpleRosSocket.ThrusterCommandTopic);
        }
        else
        {
            Debug.LogError("[ThrusterReceiver] SimpleRosSocket.Instance is NULL");
        }
    }

    /// <summary>
    /// Désabonnement à la destruction pour éviter les références pendantes.
    /// </summary>
    void OnDestroy()
    {
        if (SimpleRosSocket.Instance != null)
            SimpleRosSocket.Instance.OnRawMessage -= HandleRosMessage;
    }

    /// <summary>
    /// Callback appelé pour chaque message ROS brut (JSON).
    /// Comportement actuel : désérialisation de l'enveloppe rosbridge puis extraction de msg.efforts.
    /// </summary>
    void HandleRosMessage(string json)
    {
        try
        {
            ThrusterCommandEnvelope envelope = JsonUtility.FromJson<ThrusterCommandEnvelope>(json);
            if (envelope == null || envelope.topic != SimpleRosSocket.ThrusterCommandTopic)
                return;

            if (debugLogs)
                Debug.Log("[ThrusterReceiver] Received " + SimpleRosSocket.ThrusterCommandTopic);

            if (envelope.msg == null || envelope.msg.efforts == null)
            {
                Debug.LogWarning("[ThrusterReceiver] /thruster_cmd missing msg.efforts array.");
                return;
            }

            if (envelope.msg.efforts.Length != motorThrottles.Length)
            {
                Debug.LogWarning(
                    "[ThrusterReceiver] Invalid /thruster_cmd efforts length: " +
                    envelope.msg.efforts.Length + " (expected " + motorThrottles.Length + "). Commands zeroed.");
                Array.Clear(motorThrottles, 0, motorThrottles.Length);
                return;
            }

            // Conversion Newtons -> throttle [-1, 1] en respectant l'ordre des 8 thrusters.
            for (int i = 0; i < envelope.msg.efforts.Length; i++)
            {
                float effortN = (float)envelope.msg.efforts[i];
                float throttle = NewtonToThrottle(effortN);
                motorThrottles[i] = throttle;

                if (debugLogs)
                    Debug.Log("[ThrusterReceiver] effort[" + i + "]=" +
                              effortN.ToString("F3", CultureInfo.InvariantCulture) +
                              " N -> throttle=" +
                              throttle.ToString("F3", CultureInfo.InvariantCulture));
            }
        }
        catch (Exception e)
        {
            // Gestion d'erreur : JSON mal formé ou conversion échouée
            Debug.LogWarning("[ThrusterReceiver] Parse error: " + e.Message);
        }
    }

    /// <summary>
    /// Convertit un effort physique ROS2 en Newton vers un throttle normalisé Unity.
    ///
    /// ROS2 control_node -> /thruster_cmd:
    ///   efforts[i] = force demand in Newtons for T200 thruster i.
    ///
    /// Unity physics:
    ///   ThrusterApplier reads motorThrottles[i] in [-1, 1], then ThrusterModel
    ///   converts that throttle back to Newtons before applying forces.
    ///
    /// This interpolation mirrors the physical actuator node's force table so the
    /// simulated path and hardware path stay close enough for controller testing.
    /// </summary>
    static float NewtonToThrottle(float forceN)
    {
        if (Mathf.Abs(forceN) < 0.01f)
            return 0.0f;

        if (forceN <= KnownForcesN[0])
            return KnownThrottles[0];

        int last = KnownForcesN.Length - 1;
        if (forceN >= KnownForcesN[last])
            return KnownThrottles[last];

        for (int i = 0; i < last; i++)
        {
            float f0 = KnownForcesN[i];
            float f1 = KnownForcesN[i + 1];

            if (forceN < f0 || forceN > f1)
                continue;

            // The table contains a 0N deadband with repeated force values.
            if (Mathf.Approximately(f0, f1))
                return 0.0f;

            float t = Mathf.InverseLerp(f0, f1, forceN);
            return Mathf.Clamp(Mathf.Lerp(KnownThrottles[i], KnownThrottles[i + 1], t), -1.0f, 1.0f);
        }

        return 0.0f;
    }
}
