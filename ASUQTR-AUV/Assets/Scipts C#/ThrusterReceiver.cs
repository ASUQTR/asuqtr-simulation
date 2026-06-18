using System;
using System.Globalization;
using UnityEngine;


/// <summary>
/// Réception des commandes moteurs publiées sur le topic ROS "/actuator/motors".
///
/// Rôle :
///— S'abonner aux messages bruts reçus par SimpleRosSocket (OnRawMessage),
///— Extraire le tableau "throttles" depuis le JSON reçu,
///— Mettre à jour le tableau public `motorThrottles` utilisé par ThrusterApplier.
///
/// Convention et format attendu (exemple rosbridge JSON) :
/// {
///   "op":"publish",
///   "topic":"/actuator/motors",
///   "msg":{
///     "ids":[0,1,2,3,4,5,6,7],
///     "throttles":[0.0, 0.8, -0.5, ...]
///   }
/// }
///
/// Remarques importantes d'intégration Unity → ROS :
/// - Les valeurs de `throttles` sont attendues normalisées dans l'intervalle [-1, 1].
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

    /// <summary>
    /// Abonnement à l'événement global de réception de messages bruts.
    /// SimpleRosSocket invoque OnRawMessage pour chaque message WebSocket reçu.
    /// </summary>
    void Start()
    {
        if (SimpleRosSocket.Instance != null)
        {
            SimpleRosSocket.Instance.OnRawMessage += HandleRosMessage;
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
    /// Comportement actuel : filtrage textuel rapide puis extraction du tableau "throttles".
    /// </summary>
    void HandleRosMessage(string json)
    {
        Debug.Log("[ThrusterReceiver] Message reçu");

        // Filtrage rapide : n'intéresse que le topic /actuator/motors
        if (!json.Contains("\"/actuator/motors\""))
            return;

        try
        {
            // Recherche textuelle de la clé "data" (std_msgs/Float32MultiArray)
            int idx = json.IndexOf("\"data\"");
            if (idx < 0) return;

            // Repérer les crochets du tableau et extraire le contenu brut
            int start = json.IndexOf('[', idx);
            int end = json.IndexOf(']', start);
            if (start < 0 || end < 0) return;

            string array = json.Substring(start + 1, end - start - 1);

            // Séparer par virgule (les valeurs doivent être au format invariant, ex: "0.8")
            string[] values = array.Split(',');

            // Conversion string -> float en respectant InvariantCulture et protection bounds
            for (int i = 0; i < values.Length && i < motorThrottles.Length; i++)
            {
                // Trim pour éviter les espaces indésirables
                string token = values[i].Trim();

                // Conversion sécurisée ; si échoue, l'exception est captée ci-dessous
                motorThrottles[i] = float.Parse(token, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception e)
        {
            // Gestion d'erreur : JSON mal formé ou conversion échouée
            Debug.LogWarning("[ThrusterReceiver] Parse error: " + e.Message);
        }
    }
}