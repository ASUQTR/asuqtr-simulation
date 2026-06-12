using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// SimpleRosSocket
/// ----------------
/// Composant minimal pour se connecter à un rosbridge WebSocket et relayer les
/// messages bruts vers les autres composants Unity.
///
/// Responsabilités :
/// — Gérer la connexion WebSocket vers rosbridge (ws://host:port).
/// — S'abonner au topic de commande des thrusters après connexion.
/// — Diffuser chaque message JSON entrant via l'événement public OnRawMessage (Action<string>).
/// — Fournir une méthode Send(string json) pour envoyer des messages vers rosbridge.
///
/// Remarques d'intégration Unity -> ROS :
/// - Le format attendu est rosbridge JSON. Exemple d'abonnement envoyé :
///   {
///     "op":"subscribe",
///     "topic":"/thruster_cmd"
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
/// - La méthode SubscribeToThrusters() centralise l'abonnement aux commandes moteur.
/// - Pour les projets plus complexes, ajoutez ré-authentification / reconnexion automatique / file d'envoi.
/// </summary>
public class SimpleRosSocket : MonoBehaviour
{
    // Instance singleton accessible globalement
    public static SimpleRosSocket Instance;

    [Header("ROS Bridge")]
    [Tooltip("URL du rosbridge (ex: ws://127.0.0.1:9090)")]
    public string rosbridgeUrl = "ws://127.0.0.1:9090";

    [Header("Debug")]
    [Tooltip("Log every incoming /thruster_cmd message. Leave disabled for laptop simulation performance.")]
    public bool logThrusterMessages = false;

    [Tooltip("Log topic advertisements. Useful while debugging rosbridge, noisy otherwise.")]
    public bool logAdvertisements = false;

    private WebSocket ws;
    private readonly HashSet<string> advertisedTopics = new HashSet<string>();
    private readonly List<(string topic, string type)> pendingAdvertisements = new List<(string topic, string type)>();
    private float nextThrusterSubscribeRetryTime = 0f;
    private int thrusterSubscribeAttempts = 0;
    private bool thrusterMessageReceived = false;

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
                SubscribeToThrusters();
                FlushPendingAdvertisements();
            };

            // OnMessage reçoit un tableau d'octets ; on le convertit en chaîne UTF8 et on
            // retransmet via OnRawMessage. Les abonnés décident comment parser.
            ws.OnMessage += (bytes) =>
            {
                string msg = Encoding.UTF8.GetString(bytes);
                if ((msg.Contains("\"op\"") && msg.Contains("\"status\"")) || msg.Contains("\"level\":\"error\""))
                    Debug.Log("[ROS] Status: " + msg);
                else if (msg.Contains("/thruster_cmd"))
                {
                    thrusterMessageReceived = true;
                    if (logThrusterMessages)
                        Debug.Log("[ROS] Received raw /thruster_cmd from rosbridge");
                }
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

        if (IsConnected && !thrusterMessageReceived && thrusterSubscribeAttempts < 5 && Time.time >= nextThrusterSubscribeRetryTime)
        {
            SubscribeToThrusters();
        }
    }

    /// <summary>
    /// Envoie un message rosbridge "subscribe" pour /thruster_cmd.
    /// </summary>
    async void SubscribeToThrusters()
    {
        string sub = @"{
            ""op"": ""subscribe"",
            ""topic"": ""/thruster_cmd"",
            ""type"": ""sub_interfaces/msg/ThrusterCommand"",
            ""queue_length"": 1
        }";

        if (ws == null || ws.State != WebSocketState.Open)
            return;

        try
        {
            nextThrusterSubscribeRetryTime = Time.time + 2f;
            await ws.SendText(sub);
            thrusterSubscribeAttempts++;
            Debug.Log($"[ROS] Sent subscribe request {thrusterSubscribeAttempts}/5 to /thruster_cmd as sub_interfaces/msg/ThrusterCommand");

            if (thrusterSubscribeAttempts == 5)
                Debug.LogWarning("[ROS] No /thruster_cmd message received after 5 subscribe requests. Check /client_count, /connected_clients, and whether /thruster_cmd is actively publishing.");
        }
        catch (Exception e)
        {
            nextThrusterSubscribeRetryTime = Time.time + 2f;
            Debug.LogWarning("[ROS] Failed to subscribe to /thruster_cmd: " + e.Message);
        }
    }

    /// <summary>
    /// Advertise a ROS topic to rosbridge so ROS2 can infer the topic type before publishing.
    /// </summary>
    public void AdvertiseTopic(string topic, string type)
    {
        if (advertisedTopics.Contains(topic))
            return;

        if (ws != null && ws.State == WebSocketState.Open)
        {
            string adv = "{\"op\":\"advertise\",\"topic\":\"" + topic + "\",\"type\":\"" + type + "\"}";
            ws.SendText(adv);
            advertisedTopics.Add(topic);
            if (logAdvertisements)
                Debug.Log($"[ROS] Advertised {topic} as {type}");
            return;
        }

        if (!pendingAdvertisements.Exists(x => x.topic == topic))
        {
            pendingAdvertisements.Add((topic, type));
        }
    }

    void FlushPendingAdvertisements()
    {
        foreach (var adv in pendingAdvertisements)
        {
            if (!advertisedTopics.Contains(adv.topic))
            {
                string msg = "{\"op\":\"advertise\",\"topic\":\"" + adv.topic + "\",\"type\":\"" + adv.type + "\"}";
                ws.SendText(msg);
                advertisedTopics.Add(adv.topic);
                if (logAdvertisements)
                    Debug.Log($"[ROS] Advertised {adv.topic} as {adv.type}");
            }
        }

        pendingAdvertisements.Clear();
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
