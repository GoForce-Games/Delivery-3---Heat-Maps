using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class AnalyticsEditorWindow : EditorWindow {
    string[] eventTypes = { "Ruta", "Muerte", "Enemigo", "Salto", "Golpe" };
    int selectedEventIndex = 0;

    string[] visTypes = { "Heatmap", "Líneas de Ruta", "Marcadores de Evento" };
    int selectedVisIndex = 0;

    List<string> sessionIds = new List<string> { "Ver Todo" };
    int selectedSessionIndex = 0;

    float gridSize = 1.0f;
    Color heatmapColor = Color.red;

    [MenuItem("Tools/Analytics Viewer")]
    public static void ShowWindow() {
        GetWindow<AnalyticsEditorWindow>("Analytics Viewer");
    }

    void OnGUI() {
        GUILayout.Label("Configuración de Visualización", EditorStyles.boldLabel);
        
        selectedEventIndex = EditorGUILayout.Popup("Tipo de Evento", selectedEventIndex, eventTypes);
        selectedVisIndex = EditorGUILayout.Popup("Visualización", selectedVisIndex, visTypes);
        selectedSessionIndex = EditorGUILayout.Popup("Sesión ID", selectedSessionIndex, sessionIds.ToArray());

        // Control de cuadrícula
        gridSize = EditorGUILayout.Slider("Tamaño Cuadrícula", gridSize, 1.0f, 10.0f);
        heatmapColor = EditorGUILayout.ColorField("Color Base", heatmapColor);

        if (GUILayout.Button("Cargar Datos desde Base de Datos")) {
            // TODO call script PHP
        }
    }
}
