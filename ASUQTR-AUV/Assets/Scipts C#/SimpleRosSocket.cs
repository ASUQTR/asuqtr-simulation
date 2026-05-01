using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;

/// <summary>
/// SimpleRosSocket
/// ----------------
/// Composant minimal pour se connecter à un rosbridge WebSocket et relayer les
/// messages bruts vers les autres composants Unity.
///
/// Responsabilités :
/// — Gérer la connexion WebSocket vers rosbridge (ws://host:port).
/// — S'abonner à un topic d'exemple (/actuator/motors) après connexion.
/// — Diffuser chaque message JSON entrant via l'événement public OnRawMessage (Action<string>).
/// — Fournir une méthode Send(string json) pour envoyer des messages vers rosbridge.
///
/// Remarques d'intégration Unity -> ROS :
/// - Le format attendu est rosbridge JSON. Exemple d'abonnement envoyé :
///   {
///     "op":"subscribe",
///     "topic":"/actuator/motors",
///     "type":"asuqtr_actuator_node/ActuatorThrottle"
///   }
/// - Les récepteurs (ex: ThrusterReceiver) doivent écouter OnRawMessage et parser
///   (préférer une désérialisation structurée avec JsonUtility / Newtonsoft plutôt que du parsing texte).
/// - Conserver une convention unique Unity→NED (positions/quaternions) partagée entre publishers/subscribers.
///
/// Événements et threading :
/// - OnMessage est déclenché par la couche WebSocket. Ce code invoque OnRawMessage(msg)
///   directement ; la callback peut provenir d'un thread de réseau selon l'implémentation.
/// - Si vos handlers manipulent l'API Unity (GameObjects, transforms...), assurez-vous de
///   dispatcher le traitement sur le thread principal (par ex. stocker le JSON dans une file et
///   traiter dans Update()) afin d'éviter des erreurs liées au thread.
///
/// WebGL / Editor :
/// - Pour WebGL, la gestion des sockets diffère (JS interop). Le code appelle DispatchMessageQueue()
///   dans Update() (sauf en WebGL build) pour traiter la file de réception sur le thread Unity.
/// - Vérifiez la compatibilité WebGL si vous ciblez le web (policy CORS, ws:// vs wss://).
///
/// Robustesse :
/// - Cette classe expose CheckConnection() pour debug et Send(json) pour l'envoi.
/// - La méthode SubscribeToMotors() est un exemple ; adaptez les abonnements au besoin.
/// - Pour les projets plus complexes, ajoutez ré-authentification / reconnexion automatique / file d'envoi.
/// </summary>
public class SimpleRosSocket : MonoBehaviour
{
    // Instance singleton accessible globalement
    public static SimpleRosSocket Instance;

    [Header("ROS Bridge")]
    [Tooltip("URL du rosbridge (ex: ws://127.0.0.1:9090)")]
    public string rosbridgeUrl = "ws://127.0.0.1:9090";

    private WebSocket ws;

    /// <summary>
    /// Evénement invoqué pour chaque message JSON brut reçu depuis rosbridge.
    /// Les abonnés doivent valider/parse le JSON avant usage.
    /// </summary>
    public Action<string> OnRawMessage;

    // Indique si le socket est open
    public bool IsConnected => ws != null && ws.State == WebSocketState.Open;

    async void Awake()
    {
        // Singleton simple : une seule instance dans la scène
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log($"[ROS] Connecting to {rosbridgeUrl}");

        try
        {
            ws = new WebSocket(rosbridgeUrl);

            // Evénements WebSocket
            ws.OnOpen += () =>
            {
                Debug.Log("[ROS] Connected successfully");
                // Exemple : s'abonner au topic des moteurs après connexion
                SubscribeToMotors();
            };

            // OnMessage reçoit un tableau d'octets ; on le convertit en chaîne UTF8 et on
            // retransmet via OnRawMessage. Les abonnés décident comment parser.
            ws.OnMessage += (bytes) =>
            {
                string msg = Encoding.UTF8.GetString(bytes);
                OnRawMessage?.Invoke(msg);
            };

            ws.OnError += (e) =>
            {
                Debug.LogError($"[ROS] Error: {e}");
            };

            ws.OnClose += (e) =>
            {
                Debug.Log($"[ROS] Closed: {e}");
            };

            await ws.Connect();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ROS] Connection failed: {e.Message}");
        }
    }

    void Update()
    {
        // Dispatcher des messages en file (nécessaire pour certaines implémentations, non pour WebGL JS glue).
        // La macro exclut l'appel dans WebGL builds (où le traitement est différent).
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    /// <summary>
    /// Exemple d'abonnement : envoie un message rosbridge "subscribe" pour /actuator/motors.
    /// Adaptez topic/type selon votre configuration ROS.
    /// </summary>
    void SubscribeToMotors()
    {
        string sub = @"{
            ""op"": ""subscribe"",
            ""topic"": ""/actuator/motors"",
            ""type"": ""asuqtr_actuator_node/ActuatorThrottle""
        }";

        ws.SendText(sub);
        Debug.Log("[ROS] Subscribed to /actuator/motors");
    }

    /// <summary>
    /// Méthode utilitaire de debug pour vérifier l'état de la connexion.
    /// </summary>
    public void CheckConnection()
    {
        Debug.Log($"[ROS] Status: {ws?.State}, Connected: {IsConnected}");
    }

    /// <summary>
    /// Envoi d'un message JSON vers rosbridge (si connecté).
    /// Les messages doivent être construits au format rosbridge JSON.
    /// </summary>
    public void Send(string json)
    {
        if (ws != null && ws.State == WebSocketState.Open)
            ws.SendText(json);
    }

    /// <summary>
    /// Fermeture propre du socket à la destruction du composant.
    /// </summary>
    async void OnDestroy()
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            await ws.Close();
        }
    }
}