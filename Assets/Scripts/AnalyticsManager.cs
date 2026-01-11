using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;


public enum EntityType
{
    Player,
    Enemy,
    Other
}

[System.Serializable]
    public class GameplayEvent {
    public int sessionID;
    public string eventType; 
    public Vector3 position;
    public string timestamp;
    public float sessionDuration;
    public int enemyID; // ID of the enemy involved in the event
    public bool isSentToServer;
}

public class AnalyticsManager : MonoBehaviour
{
    // Server URL
    private const string SERVER_URL = "https://citmalumnes.upc.es/~edgarmd1/";
    
    // Singleton Instance
    private static AnalyticsManager _instance;
    public static AnalyticsManager Instance
    {
        get
        {
            if (!_instance) _instance = FindFirstObjectByType<AnalyticsManager>();
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Position Tracking")]
    public bool isPositionTracking = true;
    public Transform positionTracker;
    public float sampleRateSeconds = 1f;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        // Auto-find player if positionTracker not set
        if (positionTracker == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                positionTracker = player.transform;
                Debug.Log("[Analytics] Player encontrado automáticamente: " + player.name);
            }
            else
            {
                Debug.LogWarning("[Analytics] No se encontró el Player. Asigna manualmente positionTracker.");
            }
        }
        
        // Initialize session with server
        StartCoroutine(InitializeSessionCoroutine());
    }
    
    IEnumerator InitializeSessionCoroutine()
    {
        Debug.Log("[Analytics] Solicitando nuevo sessionID al servidor...");
        
        // Create form with device ID
        WWWForm form = new WWWForm();
        form.AddField("deviceID", SystemInfo.deviceUniqueIdentifier);
        
        using UnityWebRequest www = UnityWebRequest.Post(SERVER_URL + "StartSession.php", form);
        yield return www.SendWebRequest();
        
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[Analytics] Error al obtener sessionID: " + www.error);
            // Fallback: usar un ID local basado en tiempo
            currentSessionID = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
            Debug.LogWarning("[Analytics] Usando sessionID local de fallback: " + currentSessionID);
        }
        else
        {
            string response = www.downloadHandler.text.Trim();
            if (int.TryParse(response, out int serverSessionID))
            {
                currentSessionID = serverSessionID;
                Debug.Log("[Analytics] SessionID obtenido del servidor: " + currentSessionID);
            }
            else
            {
                Debug.LogError("[Analytics] Respuesta inválida del servidor: " + response);
                currentSessionID = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
            }
        }
        
        sessionStartTime = Time.time;
        
        // Start position tracking after session is initialized
        if (isPositionTracking && positionTracker != null)
        {
            StartCoroutine(PositionTrackingCoroutine());
        }
    }
    
    void OnApplicationQuit()
    {
        // End the session when the game closes (synchronous call because coroutines don't complete on quit)
        if (currentSessionID > 0)
        {
            EndSessionSync();
        }
    }
    
    void EndSessionSync()
    {
        Debug.Log("[Analytics] Cerrando sesión...");
        
        try
        {
            // Use synchronous web request for OnApplicationQuit
            using var client = new System.Net.WebClient();
            var data = new System.Collections.Specialized.NameValueCollection();
            data["sessionID"] = currentSessionID.ToString();
            
            byte[] response = client.UploadValues(SERVER_URL + "EndSession.php", "POST", data);
            string result = System.Text.Encoding.UTF8.GetString(response);
            
            Debug.Log("[Analytics] Sesión cerrada: " + result);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Analytics] Error al cerrar sesión: " + e.Message);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    IEnumerator PositionTrackingCoroutine()
    {
        Debug.Log("[Analytics] Iniciando tracking de posición cada " + sampleRateSeconds + " segundos.");
        
        while (true)
        {
            yield return new WaitForSeconds(sampleRateSeconds);
            
            if (positionTracker != null)
            {
                RecordEvent("Caminar", positionTracker.position);
            }
        }
    }
    
    public void RecordEvent(string type)
    {
        if (positionTracker != null)
        {
            RecordEvent(type, positionTracker.position);
        }
        else
        {
            RecordEvent(type, Vector3.zero);
            Debug.LogWarning("[Analytics] RecordEvent llamado sin positionTracker configurado.");
        }
    }
    
    IEnumerator Upload(Dictionary<string, string> data, string endpoint, Action<uint> callback = null)
    {
        WWWForm form = new WWWForm();
        foreach (var kvp in data)
        {
            form.AddField(kvp.Key, kvp.Value);
        }

        using var www = UnityWebRequest.Post(SERVER_URL + endpoint, form);
        www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.Log(www.error);
        }
        else
        {
            Debug.Log("Form upload complete!" + www.downloadHandler.text);
            if (uint.TryParse(www.downloadHandler.text, out var returnValue)){
                Debug.Log("Callback invoked with value: " + returnValue);
                callback?.Invoke(returnValue);
            }
            else
                Debug.Log("Error retrieving data: " + www.downloadHandler.text);
        }
    }

    #region Data collection delegates

    public void OnPlayerDeath(Transform t)
    {
        Dictionary<string, string> data = new Dictionary<string, string>();
        
        data["time"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        data["position"] = t.position.ToString();
        data["cause"] = "Unknown"; // TODO modify damageable script to include cause of damage/death

        StartCoroutine(Upload(data, "PlayerDeath"));
    }

    

    #endregion

    #region Event Recording
    
    private List<GameplayEvent> localEventsList = new List<GameplayEvent>();
    private int currentSessionID = 1;
    private float sessionStartTime;
    
    void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        sessionStartTime = Time.time;
    }
    
    public void RecordEvent(string type, Vector3 position, bool uploadToServer = true)
    {
        RecordEvent(type, position, 0, uploadToServer);
    }
    
    public void RecordEvent(string type, Vector3 position, int enemyID, bool uploadToServer = true)
    {
        GameplayEvent newEvent = new GameplayEvent
        {
            sessionID = currentSessionID,
            eventType = type,
            position = position,
            // Use Spanish timezone via safe helper
            timestamp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTargetTimeZone()).ToString("yyyy-MM-dd HH:mm:ss"),
            sessionDuration = Time.time - sessionStartTime,
            enemyID = enemyID
        };

        localEventsList.Add(newEvent);
        Debug.Log($"[Analytics] Evento registrado: {type} en {position} (enemyID: {enemyID}). Total eventos: {localEventsList.Count}");

        if (uploadToServer)
        {
            UploadEvent(newEvent);
        }
    }
    private TimeZoneInfo GetTargetTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Fallback for Mac/Linux
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
            }
            catch
            {
                // Ultimate fallback
                return TimeZoneInfo.Local;
            }
        }
    }
    
    private void UploadEvent(GameplayEvent gameEvent)
    {
        // Use InvariantCulture to ensure decimal point (.) instead of comma (,)
        Dictionary<string, string> data = new Dictionary<string, string>
        {
            ["sessionID"] = gameEvent.sessionID.ToString(),
            ["eventType"] = gameEvent.eventType,
            ["positionX"] = gameEvent.position.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            ["positionY"] = gameEvent.position.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            ["positionZ"] = gameEvent.position.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            ["timestamp"] = gameEvent.timestamp,
            ["sessionDuration"] = gameEvent.sessionDuration.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            ["enemyID"] = gameEvent.enemyID.ToString()
        };

        StartCoroutine(Upload(data, "PostGameplayEvents.php"));
    }
    
    public List<GameplayEvent> GetAllEvents()
    {
        return new List<GameplayEvent>(localEventsList);
    }
    
    public void UploadAllEvents()
    {
        foreach (var gameEvent in localEventsList)
        {
            if (!gameEvent.isSentToServer)
            {
                UploadEvent(gameEvent);
                gameEvent.isSentToServer = true;
            }
        }
    }
    
    #endregion
    
    #region JSON Export/Import
    
    [System.Serializable]
    private class SerializableEvent
    {
        public int sessionID;
        public string eventType;
        public float positionX, positionY, positionZ;
        public string timestamp;
        public float sessionDuration;
    }
    
    // Helper classes for PHPMyAdmin raw export
    [System.Serializable]
    private class PhpMyAdminEvent
    {
        public string event_id;
        public string session_id;
        public string pos_x;
        public string pos_y;
        public string pos_z;
        public string timestampo; // Matches the typo in the DB export
    }

    [System.Serializable]
    private class PhpMyAdminWrapper
    {
        public List<PhpMyAdminEvent> items;
    }
    
    [System.Serializable]
    private class EventsWrapper
    {
        public List<SerializableEvent> events = new List<SerializableEvent>();
    }
    
    public void ExportToJson(string path)
    {
        EventsWrapper wrapper = new EventsWrapper();
        
        foreach (var e in localEventsList)
        {
            wrapper.events.Add(new SerializableEvent
            {
                sessionID = e.sessionID,
                eventType = e.eventType,
                positionX = e.position.x,
                positionY = e.position.y,
                positionZ = e.position.z,
                timestamp = e.timestamp,
                sessionDuration = e.sessionDuration
            });
        }
        
        string json = JsonUtility.ToJson(wrapper, true);
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"[Analytics] Exportados {localEventsList.Count} eventos a: {path}");
    }
    
    public List<GameplayEvent> ImportFromJson(string path)
    {
        List<GameplayEvent> importedEvents = new List<GameplayEvent>();
        
        try
        {
            string json = System.IO.File.ReadAllText(path);
            
            // Check if it is a PHPMyAdmin export (starts with [ array)
            if (json.TrimStart().StartsWith("["))
            {
                Debug.Log("[Analytics] Detectado formato PHPMyAdmin.");
                
                // Extract the "data" array manually since JsonUtility can't handle the top-level array structure
                int dataIndex = json.IndexOf("\"data\":");
                if (dataIndex != -1)
                {
                    // Attempt to extract table name from header to determine event type
                    // Format: "name":"walk_event" or similar inside the table definition object
                    string eventType = "Position"; // Default
                    
                    if (json.Contains("\"walk_event\"")) eventType = "posicion";
                    else if (json.Contains("\"jump_event\"")) eventType = "salto";
                    else if (json.Contains("\"death_event\"")) eventType = "muerte";
                    else if (json.Contains("\"hit_event\"")) eventType = "golpe";
                    else if (json.Contains("\"damage_event\"")) eventType = "golpe";
                    else if (json.Contains("\"kill_event\"")) eventType = "enemigos matados";
                    
                    Debug.Log($"[Analytics] Detectado tipo de evento por tabla: {eventType}");

                    int start = json.IndexOf("[", dataIndex);
                    
                    // The file ends with `]}]` or `] } ]`
                    // So `json.LastIndexOf(']')` is the very last one.
                    // `json.LastIndexOf(']', json.LastIndexOf(']') - 1)` should be the data array closer if it's the last element.
                    
                    int lastBracket = json.LastIndexOf(']');
                    int secondLastBracket = json.LastIndexOf(']', lastBracket - 1);
                    
                    // Construct a wrapper that JsonUtility accepts
                    string wrappedJson = "{\"items\":" + json.Substring(start, secondLastBracket - start + 1) + "}";
                    
                    PhpMyAdminWrapper phpWrapper = JsonUtility.FromJson<PhpMyAdminWrapper>(wrappedJson);
                    
                    if (phpWrapper != null && phpWrapper.items != null)
                    {
                        foreach (var item in phpWrapper.items)
                        {
                            if (float.TryParse(item.pos_x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                                float.TryParse(item.pos_y, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y) &&
                                float.TryParse(item.pos_z, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
                            {
                                importedEvents.Add(new GameplayEvent
                                {
                                    sessionID = int.Parse(item.session_id),
                                    eventType = eventType,
                                    position = new Vector3(x, y, z),
                                    timestamp = item.timestampo,
                                    sessionDuration = 0 // Not in this export
                                });
                            }
                        }
                    }
                }
            }
            else
            {
                // Standard Unity format
                EventsWrapper wrapper = JsonUtility.FromJson<EventsWrapper>(json);
                if (wrapper != null)
                {
                    foreach (var se in wrapper.events)
                    {
                        importedEvents.Add(new GameplayEvent
                        {
                            sessionID = se.sessionID,
                            eventType = se.eventType,
                            position = new Vector3(se.positionX, se.positionY, se.positionZ),
                            timestamp = se.timestamp,
                            sessionDuration = se.sessionDuration
                        });
                    }
                }
            }
            
            Debug.Log($"[Analytics] Importados {importedEvents.Count} eventos desde: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Analytics] Error al importar JSON: {e.Message}");
        }
        
        return importedEvents;
    }
    
    #endregion
}
