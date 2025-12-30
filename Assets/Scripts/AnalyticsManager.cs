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
}
