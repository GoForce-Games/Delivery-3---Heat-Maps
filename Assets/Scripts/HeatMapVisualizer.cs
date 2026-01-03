using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class HeatMapVisualizer : MonoBehaviour
{
    [Header("Configuración General")]
    public float gridSize = 2.0f;
    public float sphereRadius = 0.5f;
    public float cubeHeight = 0.5f;
    
    [Header("Visualización")]
    public bool showHeatmap = true;
    public bool showRoute = true;
    public bool useSpheresForHeatmap = false; 
    
    [Header("Colores")]
    public Color routeColor = Color.cyan;
    public Color lowDensityColor = new Color(0, 0, 1, 0.3f);  
    public Color highDensityColor = new Color(1, 0, 0, 0.8f); 
    
    [Header("Filtros de Eventos")]
    public bool showDeaths = true;
    public bool showJumps = true;
    public bool showPositions = true;
    public bool showHits = true;
    public bool showEnemyKills = true;
    
    
    private List<GameplayEvent> loadedEvents = new List<GameplayEvent>();
    private Dictionary<Vector2Int, int> heatmapGrid = new Dictionary<Vector2Int, int>();
    private int maxEventsInCell = 1;
    
    public void LoadDataFromManager()
    {
        if (AnalyticsManager.Instance != null)
        {
            loadedEvents = AnalyticsManager.Instance.GetAllEvents();
            Debug.Log($"[HeatMapVisualizer] Cargados {loadedEvents.Count} eventos desde AnalyticsManager.");
            ProcessHeatmapGrid();
        }
        else
        {
            Debug.LogWarning("[HeatMapVisualizer] AnalyticsManager.Instance no encontrado.");
        }
    }
    
    public void LoadEvents(List<GameplayEvent> events)
    {
        loadedEvents = new List<GameplayEvent>(events);
        Debug.Log($"[HeatMapVisualizer] Cargados {loadedEvents.Count} eventos.");
        ProcessHeatmapGrid();
    }
    
    public void ClearData()
    {
        loadedEvents.Clear();
        heatmapGrid.Clear();
        maxEventsInCell = 1;
        Debug.Log("[HeatMapVisualizer] Datos limpiados.");
    }
    
    private void ProcessHeatmapGrid()
    {
        heatmapGrid.Clear();
        maxEventsInCell = 1;
        
        List<GameplayEvent> filteredEvents = GetFilteredEvents();
        
        foreach (var gameEvent in filteredEvents)
        {
            Vector2Int cellKey = GetCellKey(gameEvent.position);
            
            if (heatmapGrid.ContainsKey(cellKey))
            {
                heatmapGrid[cellKey]++;
            }
            else
            {
                heatmapGrid[cellKey] = 1;
            }
            
            
            if (heatmapGrid[cellKey] > maxEventsInCell)
            {
                maxEventsInCell = heatmapGrid[cellKey];
            }
        }
        
        Debug.Log($"[HeatMapVisualizer] Grid procesado: {heatmapGrid.Count} celdas, máximo {maxEventsInCell} eventos por celda.");
    }
    
    private Vector2Int GetCellKey(Vector3 position)
    {
        int cellX = Mathf.FloorToInt(position.x / gridSize);
        int cellZ = Mathf.FloorToInt(position.z / gridSize);
        return new Vector2Int(cellX, cellZ);
    }
    
    private Vector3 GetCellCenter(Vector2Int cellKey, float yPosition = 0f)
    {
        float x = (cellKey.x + 0.5f) * gridSize;
        float z = (cellKey.y + 0.5f) * gridSize;
        return new Vector3(x, yPosition, z);
    }
    
    private List<GameplayEvent> GetFilteredEvents()
    {
        return loadedEvents.Where(e => ShouldShowEvent(e)).ToList();
    }
    
    private bool ShouldShowEvent(GameplayEvent gameEvent)
    {
        switch (gameEvent.eventType.ToLower())
        {
            case "muerte":
                return showDeaths;
            case "salto":
                return showJumps;
            case "posicion":
                return showPositions;
            case "golpe":
                return showHits;
            case "enemigos matados":
                return showEnemyKills;
            default:
                return true; 
        }
    }
    
    private Color GetHeatmapColor(int eventCount)
    {
        float t = Mathf.Clamp01((float)eventCount / maxEventsInCell);
        return Color.Lerp(lowDensityColor, highDensityColor, t);
    }
    
    private void OnDrawGizmos()
    {
        if (loadedEvents == null || loadedEvents.Count == 0)
            return;
        
        List<GameplayEvent> filteredEvents = GetFilteredEvents();
        
        
        if (showRoute && filteredEvents.Count > 1)
        {
            DrawRoute(filteredEvents);
        }
        
        
        if (showHeatmap)
        {
            DrawHeatmap();
        }
    }
    
    private void DrawRoute(List<GameplayEvent> events)
    {
        Gizmos.color = routeColor;
        
        
        var orderedEvents = events.OrderBy(e => e.timestamp).ThenBy(e => e.sessionDuration).ToList();
        
        for (int i = 0; i < orderedEvents.Count - 1; i++)
        {
            Vector3 start = orderedEvents[i].position;
            Vector3 end = orderedEvents[i + 1].position;
            
            Gizmos.DrawLine(start, end);
            
            
            Gizmos.DrawWireSphere(start, 0.15f);
        }
        
        
        if (orderedEvents.Count > 0)
        {
            Gizmos.DrawWireSphere(orderedEvents[orderedEvents.Count - 1].position, 0.15f);
        }
    }
    
    private void DrawHeatmap()
    {
        foreach (var cell in heatmapGrid)
        {
            Color cellColor = GetHeatmapColor(cell.Value);
            Gizmos.color = cellColor;
            
            
            float avgY = CalculateAverageY(cell.Key);
            Vector3 cellCenter = GetCellCenter(cell.Key, avgY);
            
            if (useSpheresForHeatmap)
            {
                
                float scale = 1f + (float)cell.Value / maxEventsInCell;
                Gizmos.DrawSphere(cellCenter, sphereRadius * scale);
            }
            else
            {
                
                float heightScale = 1f + (float)cell.Value / maxEventsInCell * 2f;
                Vector3 cubeSize = new Vector3(gridSize * 0.9f, cubeHeight * heightScale, gridSize * 0.9f);
                Gizmos.DrawCube(cellCenter, cubeSize);
                
                
                Gizmos.color = new Color(cellColor.r, cellColor.g, cellColor.b, 1f);
                Gizmos.DrawWireCube(cellCenter, cubeSize);
            }
        }
    }
    
    private float CalculateAverageY(Vector2Int cellKey)
    {
        var eventsInCell = loadedEvents.Where(e => GetCellKey(e.position) == cellKey);
        if (eventsInCell.Any())
        {
            return eventsInCell.Average(e => e.position.y);
        }
        return 0f;
    }
    
    public void RefreshHeatmap()
    {
        ProcessHeatmapGrid();
    }
    
    public string GetStatistics()
    {
        if (loadedEvents == null || loadedEvents.Count == 0)
            return "No hay datos cargados.";
        
        var stats = loadedEvents
            .GroupBy(e => e.eventType)
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();
        
        return $"Total eventos: {loadedEvents.Count}\n" + string.Join("\n", stats);
    }
}

#if UNITY_EDITOR
public class HeatmapEditorWindow : EditorWindow
{
    private HeatMapVisualizer visualizer;
    private Vector2 scrollPosition;
    private string jsonPath = "";
    
    [MenuItem("Window/Analytics/Heatmap Visualizer")]
    public static void ShowWindow()
    {
        GetWindow<HeatmapEditorWindow>("Heatmap Visualizer");
    }
    
    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.LabelField("🔥 Heatmap Visualizer", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        
        DrawVisualizerSection();
        
        if (visualizer == null)
        {
            EditorGUILayout.HelpBox("Selecciona un HeatMapVisualizer en la escena o crea uno nuevo.", MessageType.Info);
            
            if (GUILayout.Button("Crear HeatMapVisualizer en Escena"))
            {
                CreateVisualizer();
            }
            
            EditorGUILayout.EndScrollView();
            return;
        }
        
        EditorGUILayout.Space(10);
        
        
        DrawDataLoadingSection();
        
        EditorGUILayout.Space(10);
        
        
        DrawFiltersSection();
        
        EditorGUILayout.Space(10);
        
        
        DrawVisualizationSection();
        
        EditorGUILayout.Space(10);
        
        
        DrawStatisticsSection();
        
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawVisualizerSection()
    {
        EditorGUILayout.LabelField("Visualizador", EditorStyles.boldLabel);
        
        visualizer = (HeatMapVisualizer)EditorGUILayout.ObjectField(
            "HeatMap Visualizer",
            visualizer,
            typeof(HeatMapVisualizer),
            true
        );
        
        
        if (visualizer == null)
        {
            visualizer = FindFirstObjectByType<HeatMapVisualizer>();
        }
    }
    
    private void DrawDataLoadingSection()
    {
        EditorGUILayout.LabelField("📂 Carga de Datos", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Cargar Datos", GUILayout.Height(30)))
        {
            visualizer.LoadDataFromManager();
            SceneView.RepaintAll();
        }
        
        if (GUILayout.Button("Limpiar Datos", GUILayout.Height(30)))
        {
            visualizer.ClearData();
            SceneView.RepaintAll();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        
        EditorGUILayout.BeginHorizontal();
        jsonPath = EditorGUILayout.TextField("Ruta JSON:", jsonPath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFilePanel("Seleccionar archivo JSON", Application.dataPath, "json");
            if (!string.IsNullOrEmpty(path))
            {
                jsonPath = path;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        if (!string.IsNullOrEmpty(jsonPath) && GUILayout.Button("Cargar desde JSON"))
        {
            LoadFromJson(jsonPath);
        }
    }
    
    private void DrawFiltersSection()
    {
        EditorGUILayout.LabelField("🔍 Filtros de Eventos", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        
        EditorGUILayout.BeginHorizontal();
        visualizer.showDeaths = EditorGUILayout.ToggleLeft("💀 Muertes", visualizer.showDeaths, GUILayout.Width(100));
        visualizer.showJumps = EditorGUILayout.ToggleLeft("🦘 Saltos", visualizer.showJumps, GUILayout.Width(100));
        visualizer.showPositions = EditorGUILayout.ToggleLeft("📍 Posiciones", visualizer.showPositions, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        visualizer.showHits = EditorGUILayout.ToggleLeft("⚔️ Golpes", visualizer.showHits, GUILayout.Width(100));
        visualizer.showEnemyKills = EditorGUILayout.ToggleLeft("👹 Enemigos", visualizer.showEnemyKills, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        
        if (EditorGUI.EndChangeCheck())
        {
            visualizer.RefreshHeatmap();
            SceneView.RepaintAll();
            EditorUtility.SetDirty(visualizer);
        }
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Mostrar Todo"))
        {
            visualizer.showDeaths = true;
            visualizer.showJumps = true;
            visualizer.showPositions = true;
            visualizer.showHits = true;
            visualizer.showEnemyKills = true;
            visualizer.RefreshHeatmap();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Ocultar Todo"))
        {
            visualizer.showDeaths = false;
            visualizer.showJumps = false;
            visualizer.showPositions = false;
            visualizer.showHits = false;
            visualizer.showEnemyKills = false;
            visualizer.RefreshHeatmap();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        
        EditorGUILayout.LabelField("Filtros Rápidos:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Solo Muertes"))
        {
            SetAllFilters(false);
            visualizer.showDeaths = true;
            visualizer.RefreshHeatmap();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Solo Saltos"))
        {
            SetAllFilters(false);
            visualizer.showJumps = true;
            visualizer.RefreshHeatmap();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Solo Posiciones"))
        {
            SetAllFilters(false);
            visualizer.showPositions = true;
            visualizer.RefreshHeatmap();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawVisualizationSection()
    {
        EditorGUILayout.LabelField("🎨 Configuración Visual", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        
        visualizer.showHeatmap = EditorGUILayout.Toggle("Mostrar Heatmap", visualizer.showHeatmap);
        visualizer.showRoute = EditorGUILayout.Toggle("Mostrar Ruta", visualizer.showRoute);
        visualizer.useSpheresForHeatmap = EditorGUILayout.Toggle("Usar Esferas", visualizer.useSpheresForHeatmap);
        
        EditorGUILayout.Space(5);
        
        visualizer.gridSize = EditorGUILayout.Slider("Tamaño de Celda", visualizer.gridSize, 0.5f, 10f);
        visualizer.sphereRadius = EditorGUILayout.Slider("Radio Esfera", visualizer.sphereRadius, 0.1f, 2f);
        visualizer.cubeHeight = EditorGUILayout.Slider("Altura Cubo", visualizer.cubeHeight, 0.1f, 3f);
        
        EditorGUILayout.Space(5);
        
        visualizer.routeColor = EditorGUILayout.ColorField("Color Ruta", visualizer.routeColor);
        visualizer.lowDensityColor = EditorGUILayout.ColorField("Color Baja Densidad", visualizer.lowDensityColor);
        visualizer.highDensityColor = EditorGUILayout.ColorField("Color Alta Densidad", visualizer.highDensityColor);
        
        if (EditorGUI.EndChangeCheck())
        {
            visualizer.RefreshHeatmap();
            SceneView.RepaintAll();
            EditorUtility.SetDirty(visualizer);
        }
    }
    
    private void DrawStatisticsSection()
    {
        EditorGUILayout.LabelField("📊 Estadísticas", EditorStyles.boldLabel);
        
        string stats = visualizer.GetStatistics();
        EditorGUILayout.HelpBox(stats, MessageType.None);
        
        if (GUILayout.Button("Refrescar Heatmap"))
        {
            visualizer.RefreshHeatmap();
            SceneView.RepaintAll();
        }
    }
    
    private void SetAllFilters(bool value)
    {
        visualizer.showDeaths = value;
        visualizer.showJumps = value;
        visualizer.showPositions = value;
        visualizer.showHits = value;
        visualizer.showEnemyKills = value;
    }
    
    private void CreateVisualizer()
    {
        GameObject visualizerGO = new GameObject("HeatMapVisualizer");
        visualizer = visualizerGO.AddComponent<HeatMapVisualizer>();
        Selection.activeGameObject = visualizerGO;
        Debug.Log("[HeatmapEditorWindow] HeatMapVisualizer creado en la escena.");
    }
    
    private void LoadFromJson(string path)
    {
        try
        {
            string json = System.IO.File.ReadAllText(path);
            
            
            Debug.Log($"[HeatmapEditorWindow] Cargando desde: {path}");
            EditorUtility.DisplayDialog("Cargar JSON", "Función de carga desde JSON pendiente de implementar según formato del servidor.", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HeatmapEditorWindow] Error al cargar JSON: {e.Message}");
        }
    }
    
    private void OnEnable()
    {
        
        visualizer = FindFirstObjectByType<HeatMapVisualizer>();
    }
}
#endif
