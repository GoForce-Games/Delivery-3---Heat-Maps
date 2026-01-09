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
    public string sessionID;
    public string eventType; 
    public Vector3 position;
    public string timestamp;
    public float sessionDuration;
    public bool isSentToServer;
}

public class AnalyticsManager : MonoBehaviour
{
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
        
        // Start position tracking coroutine
        if (isPositionTracking && positionTracker != null)
        {
            StartCoroutine(PositionTrackingCoroutine());
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
                RecordEvent("Posicion", positionTracker.position);
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

        using var www = UnityWebRequest.Post("https://citmalumnes.upc.es/~edgarmd1/" + endpoint, form);
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
    private string currentSessionID = System.Guid.NewGuid().ToString();
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
        GameplayEvent newEvent = new GameplayEvent
        {
            sessionID = currentSessionID,
            eventType = type,
            position = position,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            sessionDuration = Time.time - sessionStartTime
        };

        localEventsList.Add(newEvent);
        Debug.Log($"[Analytics] Evento registrado: {type} en {position}. Total eventos: {localEventsList.Count}");

        if (uploadToServer)
        {
            UploadEvent(newEvent);
        }
    }
    
    private void UploadEvent(GameplayEvent gameEvent)
    {
        Dictionary<string, string> data = new Dictionary<string, string>
        {
            ["sessionID"] = gameEvent.sessionID,
            ["eventType"] = gameEvent.eventType,
            ["positionX"] = gameEvent.position.x.ToString(),
            ["positionY"] = gameEvent.position.y.ToString(),
            ["positionZ"] = gameEvent.position.z.ToString(),
            ["timestamp"] = gameEvent.timestamp,
            ["sessionDuration"] = gameEvent.sessionDuration.ToString()
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
        public string sessionID;
        public string eventType;
        public float posX, posY, posZ;
        public string timestamp;
        public float sessionDuration;
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
                posX = e.position.x,
                posY = e.position.y,
                posZ = e.position.z,
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
            EventsWrapper wrapper = JsonUtility.FromJson<EventsWrapper>(json);
            
            foreach (var se in wrapper.events)
            {
                importedEvents.Add(new GameplayEvent
                {
                    sessionID = se.sessionID,
                    eventType = se.eventType,
                    position = new Vector3(se.posX, se.posY, se.posZ),
                    timestamp = se.timestamp,
                    sessionDuration = se.sessionDuration
                });
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
