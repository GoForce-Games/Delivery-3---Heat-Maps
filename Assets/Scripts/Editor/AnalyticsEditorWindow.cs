using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

public class AnalyticsEditorWindow : EditorWindow {
    // Must match types in PostGameplayEvents.php: Salto, Golpe, Muerte, Enemigo, Caminar, Correr, Daño
    string[] eventTypes = { "Caminar", "Salto", "Golpe", "Daño", "Muerte", "Enemigo" };
    int selectedEventIndex = 0;

    string[] visTypes = { "Heatmap", "Líneas de Ruta", "Marcadores de Evento" };
    int selectedVisIndex = 0;

    List<string> sessionIds = new List<string> { "Ver Todo" };
    int selectedSessionIndex = 0;

    float gridSize = 1.0f;
    Color heatmapColor = Color.red;
    
    HeatMapVisualizer visualizer;
    bool isFetchingSessions = false;
    bool isLoadingFromDB = false;

    [MenuItem("Tools/Analytics Viewer")]
    public static void ShowWindow() {
        GetWindow<AnalyticsEditorWindow>("Analytics Viewer");
    }
    
    void OnEnable() {
        visualizer = FindFirstObjectByType<HeatMapVisualizer>();
        FetchAvailableSessions();
    }

    void OnGUI() {
        GUILayout.Label("Configuración de Visualización", EditorStyles.boldLabel);
        
        selectedEventIndex = EditorGUILayout.Popup("Tipo de Evento", selectedEventIndex, eventTypes);
        selectedVisIndex = EditorGUILayout.Popup("Visualización", selectedVisIndex, visTypes);
        
        // Session selection with refresh button
        EditorGUILayout.BeginHorizontal();
        selectedSessionIndex = EditorGUILayout.Popup("Sesión ID", selectedSessionIndex, sessionIds.ToArray());
        EditorGUI.BeginDisabledGroup(isFetchingSessions);
        if (GUILayout.Button(isFetchingSessions ? "..." : "↻", GUILayout.Width(30))) {
            FetchAvailableSessions();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        // Control de cuadrícula
        gridSize = EditorGUILayout.Slider("Tamaño Cuadrícula", gridSize, 1.0f, 10.0f);
        heatmapColor = EditorGUILayout.ColorField("Color Base", heatmapColor);
        
        // Visualizer reference
        visualizer = (HeatMapVisualizer)EditorGUILayout.ObjectField(
            "Visualizer",
            visualizer,
            typeof(HeatMapVisualizer),
            true
        );

        EditorGUI.BeginDisabledGroup(isLoadingFromDB || visualizer == null);
        if (GUILayout.Button(isLoadingFromDB ? "Cargando..." : "Cargar Datos desde Base de Datos"))
        {
            LoadFromDatabase(
                eventTypes[selectedEventIndex],
                sessionIds[selectedSessionIndex]
            );
        }
        EditorGUI.EndDisabledGroup();
        
        if (visualizer == null)
        {
            EditorGUILayout.HelpBox("Asigna un HeatMapVisualizer de la escena.", MessageType.Warning);
        }
    }
    
    #region Database Loading
    
    private const string SERVER_URL = "https://citmalumnes.upc.es/~edgarmd1/";
    
    [Serializable]
    private class SessionsWrapper {
        public List<string> sessions;
    }
    
    [Serializable]
    private class EventWrapper {
        public List<GameplayEvent> events;
    }
    
    private void FetchAvailableSessions() {
        if (AnalyticsManager.Instance == null) {
            Debug.LogWarning("[AnalyticsEditorWindow] AnalyticsManager no encontrado. Inicia Play Mode primero.");
            return;
        }
        
        isFetchingSessions = true;
        AnalyticsManager.Instance.StartCoroutine(FetchSessionsCoroutine());
    }
    
    private IEnumerator FetchSessionsCoroutine() {
        using UnityWebRequest www = UnityWebRequest.Get(SERVER_URL + "GetSessions.php");
        yield return www.SendWebRequest();
        
        isFetchingSessions = false;
        
        if (www.result != UnityWebRequest.Result.Success) {
            Debug.LogError("[AnalyticsEditorWindow] Error al obtener sesiones: " + www.error);
            yield break;
        }
        
        try {
            SessionsWrapper wrapper = JsonUtility.FromJson<SessionsWrapper>(www.downloadHandler.text);
            sessionIds = new List<string> { "Ver Todo" };
            if (wrapper.sessions != null) {
                sessionIds.AddRange(wrapper.sessions);
            }
            Debug.Log($"[AnalyticsEditorWindow] Obtenidas {sessionIds.Count - 1} sesiones del servidor.");
            Repaint();
        }
        catch (Exception e) {
            Debug.LogError("[AnalyticsEditorWindow] Error al parsear sesiones: " + e.Message);
        }
    }
    
    private void LoadFromDatabase(string eventType, string sessionID) {
        if (AnalyticsManager.Instance == null) {
            Debug.LogError("[AnalyticsEditorWindow] AnalyticsManager no encontrado.");
            return;
        }
        
        isLoadingFromDB = true;
        AnalyticsManager.Instance.StartCoroutine(LoadDatabaseCoroutine(eventType, sessionID));
    }
    
    private IEnumerator LoadDatabaseCoroutine(string eventType, string sessionID) {
        WWWForm form = new WWWForm();
        form.AddField("eventType", eventType);
        form.AddField("sessionID", sessionID);

        using UnityWebRequest www = UnityWebRequest.Post(SERVER_URL + "GetGameplayEvents.php", form);
        yield return www.SendWebRequest();
        
        isLoadingFromDB = false;

        if (www.result != UnityWebRequest.Result.Success) {
            Debug.LogError("[AnalyticsEditorWindow] Error al cargar desde DB: " + www.error);
            yield break;
        }

        try {
            List<GameplayEvent> events = JsonUtility
                .FromJson<EventWrapper>(www.downloadHandler.text)
                .events;

            if (visualizer != null) {
                visualizer.LoadEvents(events);
                SceneView.RepaintAll();
            }
            
            Debug.Log($"[AnalyticsEditorWindow] Cargados {events.Count} eventos.");
            Repaint();
        }
        catch (Exception e) {
            Debug.LogError("[AnalyticsEditorWindow] Error al parsear eventos: " + e.Message);
        }
    }
    
    #endregion
}
