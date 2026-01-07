using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class AnalyticsDBLoader 
{
    public static void LoadFromDatabase(
        string eventType,
        string sessionID,
        Action<List<GameplayEvent>> callback)
    {
        AnalyticsManager.Instance.StartCoroutine(
            LoadCoroutine(eventType, sessionID, callback)
        );
    }

    private static IEnumerator LoadCoroutine(
        string eventType,
        string sessionID,
        Action<List<GameplayEvent>> callback)
    {
        WWWForm form = new WWWForm();
        form.AddField("eventType", eventType);
        form.AddField("sessionID", sessionID);

        using UnityWebRequest www = UnityWebRequest.Post(
            "https://citmalumnes.upc.es/~edgarmd1/",
            form
        );

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[AnalyticsDBLoader] " + www.error);
            yield break;
        }

        List<GameplayEvent> events = JsonUtility
            .FromJson<EventWrapper>(www.downloadHandler.text)
            .events;

        callback?.Invoke(events);
    }

    [Serializable]
    private class EventWrapper
    {
        public List<GameplayEvent> events;
    }
}
