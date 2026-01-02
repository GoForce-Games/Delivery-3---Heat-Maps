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
}

public class AnalyticsManager : MonoBehaviour
{
    [Header("Position Tracking")]
    public bool isPositionTracking = true;
    public Transform positionTracker;
    public float sampleRateSeconds = 1f;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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
        sessionStartTime = Time.time;
    }
    
    public void RecordEvent(string type, Vector3 position, bool uploadToServer = false)
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

        StartCoroutine(Upload(data, "GameplayEvent.php"));
    }
    
    public List<GameplayEvent> GetAllEvents()
    {
        return new List<GameplayEvent>(localEventsList);
    }
    
    public void UploadAllEvents()
    {
        foreach (var gameEvent in localEventsList)
        {
            UploadEvent(gameEvent);
        }
    }
    
    #endregion
}
