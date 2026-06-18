using System;

/// <summary>
/// Définitions de messages sérialisables pour l'échange via rosbridge (JSON).
/// 
/// Usage :
/// - Ces classes servent de modèles pour la (dé)sérialisation JSON côté Unity.
/// - Exemple JSON attendu pour une commande de moteurs (rosbridge) :
///   {
///     "op":"publish",
///     "topic":"/actuator/motors",
///     "msg":{
///       "ids":[0,1,2,3,4,5,6,7],
///       "throttles":[0.0,0.8,-0.5,...]
///     }
///   }
/// 
/// Remarques d'intégration Unity → ROS :
/// - `ids` : identifiants numériques des thrusters (correspondre au mapping documenté côté scène/ROS).
/// - `throttles` : tableau de consignes normalisées en float (convention projet : [-1..1]).
/// - Ordre / longueur : `ids.Length` et `throttles.Length` doivent être cohérents ; côté récepteur,
///   vérifie les tailles et protège contre les accès hors limites.
/// - Format : Unity peut désérialiser ce JSON avec `JsonUtility.FromJson<RosbridgeWrapper>(json)`
///   si le JSON correspond exactement à la forme de ces classes. Si le JSON contient des champs
///   additionnels ou une structure différente, utilise un parseur JSON plus flexible (Newtonsoft.Json)
///   ou conserve l'extraction manuelle actuelle.
/// 
/// Bonnes pratiques :
/// - Centraliser ces modèles et documenter le mapping des indices des thrusters (README).
/// - Préférer la désérialisation structurée plutôt que la parsing texte pour réduire les erreurs.
/// </summary>
[Serializable]
public class ActuatorThrottleMsg
{
    /// <summary>
    /// Tableau d'identifiants de thrusters (par ex. [0,1,2,...]).
    /// Doit correspondre à l'ordre / mapping attendu côté scène et ROS.
    /// </summary>
    public uint[] ids;

    /// <summary>
    /// Tableau des consignes (throttles) en floats, typiquement normalisées dans [-1,1].
    /// length doit correspondre à ids.Length (ou être interprétée selon la logique du receiver).
    /// </summary>
    public float[] throttles;
}

[Serializable]
public class RosbridgeWrapper
{
    /// <summary>
    /// Opération rosbridge (ex: "publish", "subscribe", ...).
    /// </summary>
    public string op;

    /// <summary>
    /// Topic ROS (ex: "/actuator/motors").
    /// </summary>
    public string topic;

    /// <summary>
    /// Payload du message — ici on l'attend de type ActuatorThrottleMsg pour /actuator/motors.
    /// </summary>
    public ActuatorThrottleMsg msg;
}