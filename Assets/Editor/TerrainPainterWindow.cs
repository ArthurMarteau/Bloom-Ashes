using UnityEditor;
using UnityEngine;

// Fenêtre d'édition des cartes de terrain directement dans la Scene view
public class TerrainPainterWindow : EditorWindow
{
    private enum PaintMode
    {
        Terrain,
        Markers
    }

    private const float CellSize = 1f;
    private static readonly int[] BrushSizes = { 1, 3, 5 };
    private static readonly string[] BrushSizeLabels = { "1", "3", "5" };

    [SerializeField] private TerrainMapData terrainMap;
    [SerializeField] private GroundType selectedGroundType = GroundType.Grass;
    [SerializeField] private PaintMode paintMode;
    [SerializeField] private MapMarkerType selectedMarkerType = MapMarkerType.NexusSpawn;
    [SerializeField] private int brushSize = 1;
    [SerializeField] private int resizeWidth = 80;
    [SerializeField] private int resizeHeight = 80;

    private bool isPainting;
    private int undoGroup = -1;
    private Vector2Int lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
    private readonly Vector3[] cellCorners = new Vector3[4];

    // Ouvre l'outil depuis le menu Bloom
    [MenuItem("Bloom and Ashes/Terrain Painter")]
    private static void OpenWindow()
    {
        GetWindow<TerrainPainterWindow>("Terrain Painter");
    }

    // Branche la peinture et le rafraîchissement de la Scene view
    private void OnEnable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed -= HandleUndoRedo;
        Undo.undoRedoPerformed += HandleUndoRedo;
        SyncResizeFields();
    }

    // Retire tous les callbacks éditeur appartenant à la fenêtre
    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= HandleUndoRedo;
        EndPaintStroke();
    }

    // Dessine les contrôles de sélection, remplissage et redimensionnement
    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        TerrainMapData newTerrainMap = (TerrainMapData)EditorGUILayout.ObjectField(
            "Terrain Map", terrainMap, typeof(TerrainMapData), false);
        if (EditorGUI.EndChangeCheck())
        {
            terrainMap = newTerrainMap;
            SyncResizeFields();
            SceneView.RepaintAll();
        }

        if (terrainMap == null)
        {
            EditorGUILayout.HelpBox("Assignez un TerrainMapData pour peindre dans la Scene view.", MessageType.Info);
            return;
        }

        paintMode = (PaintMode)GUILayout.Toolbar((int)paintMode, new[] { "Terrain", "Markers" });
        EditorGUILayout.Space();

        if (paintMode == PaintMode.Terrain)
        {
            selectedGroundType = (GroundType)EditorGUILayout.EnumPopup("Ground Type", selectedGroundType);
            brushSize = EditorGUILayout.IntPopup("Brush Size", brushSize, BrushSizeLabels, BrushSizes);
        }
        else
        {
            selectedMarkerType = (MapMarkerType)EditorGUILayout.EnumPopup("Marker Type", selectedMarkerType);
            if (GUILayout.Button("Clear Selected Markers"))
                ClearSelectedMarkers();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Map Size", EditorStyles.boldLabel);
        resizeWidth = Mathf.Max(1, EditorGUILayout.IntField("Width", resizeWidth));
        resizeHeight = Mathf.Max(1, EditorGUILayout.IntField("Height", resizeHeight));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (paintMode == PaintMode.Terrain && GUILayout.Button("Fill All"))
            {
                FillAll();
            }

            if (GUILayout.Button("Resize"))
            {
                ResizeMap();
            }
        }

        string help = paintMode == PaintMode.Terrain
            ? "Clic gauche ou glisser dans la Scene view pour peindre."
            : "Clic gauche pour poser un marqueur ; clic sur un marqueur existant pour le retirer.";
        EditorGUILayout.HelpBox(help, MessageType.None);
    }

    // Gère l'overlay et les interactions de peinture dans la Scene view
    private void OnSceneGUI(SceneView sceneView)
    {
        if (terrainMap == null)
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.Repaint)
        {
            DrawTerrainOverlay();
            DrawMarkerOverlay();
        }

        if (currentEvent.alt || currentEvent.button != 0)
        {
            return;
        }

        if (currentEvent.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        if (currentEvent.type == EventType.MouseDown)
        {
            BeginPaintStroke();
            PaintAtMousePosition(currentEvent.mousePosition);
            if (paintMode == PaintMode.Markers)
                EndPaintStroke();
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.MouseDrag && isPainting && paintMode == PaintMode.Terrain)
        {
            PaintAtMousePosition(currentEvent.mousePosition);
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.MouseUp && isPainting)
        {
            EndPaintStroke();
            currentEvent.Use();
        }
    }

    // Démarre un groupe Undo unique pour tout le trait de pinceau
    private void BeginPaintStroke()
    {
        Undo.IncrementCurrentGroup();
        undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(paintMode == PaintMode.Terrain ? "Paint Terrain" : "Edit Map Marker");
        isPainting = true;
        lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
    }

    // Termine le trait et compacte ses modifications en une seule opération Undo
    private void EndPaintStroke()
    {
        if (undoGroup >= 0)
        {
            Undo.CollapseUndoOperations(undoGroup);
        }

        if (terrainMap != null)
        {
            AssetDatabase.SaveAssetIfDirty(terrainMap);
        }

        isPainting = false;
        undoGroup = -1;
        lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
    }

    // Convertit la souris en cellule via GridSystem puis applique le pinceau
    private void PaintAtMousePosition(Vector2 mousePosition)
    {
        Ray mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane gridPlane = new Plane(Vector3.forward, Vector3.zero);
        if (!gridPlane.Raycast(mouseRay, out float enter))
        {
            return;
        }

        Vector3 worldPosition = mouseRay.GetPoint(enter);
        var (originX, originY) = GridSystem.CalculateOrigin(terrainMap.Width, terrainMap.Height, CellSize);
        Vector2Int centerCell = GridSystem.WorldToGrid(
            worldPosition.x, worldPosition.y, CellSize, originX, originY);

        if (centerCell == lastPaintedCell)
        {
            return;
        }

        if (paintMode == PaintMode.Terrain)
            PaintBrush(centerCell);
        else
            ToggleMarker(centerCell);
        lastPaintedCell = centerCell;
    }

    // Pose le marqueur sélectionné ou retire celui déjà présent sur la cellule.
    private void ToggleMarker(Vector2Int cell)
    {
        if (cell.x < 0 || cell.x >= terrainMap.Width || cell.y < 0 || cell.y >= terrainMap.Height)
            return;

        Undo.RecordObject(terrainMap, "Edit Map Marker");
        if (!terrainMap.RemoveMarkerAt(cell))
            terrainMap.AddMarker(selectedMarkerType, cell);

        EditorUtility.SetDirty(terrainMap);
        Repaint();
        SceneView.RepaintAll();
    }

    // Peint un carré 1x1, 3x3 ou 5x5 autour de la cellule centrale
    private void PaintBrush(Vector2Int centerCell)
    {
        int radius = brushSize / 2;
        Undo.RecordObject(terrainMap, "Paint Terrain");

        for (int x = centerCell.x - radius; x <= centerCell.x + radius; x++)
        {
            for (int y = centerCell.y - radius; y <= centerCell.y + radius; y++)
            {
                terrainMap.SetCell(x, y, selectedGroundType);
            }
        }

        EditorUtility.SetDirty(terrainMap);
        SceneView.RepaintAll();
    }

    // Dessine chaque cellule avec la couleur de debug de son GroundType
    private void DrawTerrainOverlay()
    {
        var (originX, originY) = GridSystem.CalculateOrigin(terrainMap.Width, terrainMap.Height, CellSize);

        for (int x = 0; x < terrainMap.Width; x++)
        {
            for (int y = 0; y < terrainMap.Height; y++)
            {
                float left = originX + x * CellSize;
                float bottom = originY + y * CellSize;
                cellCorners[0] = new Vector3(left, bottom, 0f);
                cellCorners[1] = new Vector3(left + CellSize, bottom, 0f);
                cellCorners[2] = new Vector3(left + CellSize, bottom + CellSize, 0f);
                cellCorners[3] = new Vector3(left, bottom + CellSize, 0f);

                Color fillColor = terrainMap.GetCell(x, y).GetDebugColor();
                fillColor.a = 0.18f;
                Color outlineColor = fillColor;
                outlineColor.a = 0.35f;
                Handles.DrawSolidRectangleWithOutline(cellCorners, fillColor, outlineColor);
            }
        }
    }

    // Dessine les marqueurs au-dessus du terrain dans les deux modes d'édition.
    private void DrawMarkerOverlay()
    {
        var (originX, originY) = GridSystem.CalculateOrigin(terrainMap.Width, terrainMap.Height, CellSize);
        DrawNexusSpawnMarkers(originX, originY);
        DrawFoundationAnchorMarkers(originX, originY);
    }

    // Dessine un disque numéroté selon l'ordre stable de pose des NexusSpawn.
    private void DrawNexusSpawnMarkers(float originX, float originY)
    {
        var nexusSpawns = terrainMap.GetMarkers(MapMarkerType.NexusSpawn);
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        for (int index = 0; index < nexusSpawns.Count; index++)
        {
            Vector2Int cell = nexusSpawns[index].cell;
            Vector3 center = new Vector3(
                originX + (cell.x + 0.5f) * CellSize,
                originY + (cell.y + 0.5f) * CellSize,
                0f);
            Handles.color = new Color(0.15f, 0.65f, 1f, 0.9f);
            Handles.DrawSolidDisc(center, Vector3.forward, CellSize * 0.34f);
            Handles.Label(center, index.ToString(), labelStyle);
        }
    }

    // Dessine le contour 2x2 occupé depuis chaque ancre Foundation.
    private void DrawFoundationAnchorMarkers(float originX, float originY)
    {
        foreach (MapMarker marker in terrainMap.GetMarkers(MapMarkerType.FoundationAnchor))
        {
            float left = originX + marker.cell.x * CellSize;
            float bottom = originY + marker.cell.y * CellSize;
            cellCorners[0] = new Vector3(left, bottom, 0f);
            cellCorners[1] = new Vector3(left + CellSize * 2f, bottom, 0f);
            cellCorners[2] = new Vector3(left + CellSize * 2f, bottom + CellSize * 2f, 0f);
            cellCorners[3] = new Vector3(left, bottom + CellSize * 2f, 0f);
            Handles.DrawSolidRectangleWithOutline(
                cellCorners,
                new Color(1f, 0.55f, 0.1f, 0.12f),
                new Color(1f, 0.55f, 0.1f, 1f));
        }
    }

    // Efface avec Undo tous les marqueurs du type sélectionné.
    private void ClearSelectedMarkers()
    {
        Undo.RecordObject(terrainMap, "Clear Map Markers");
        terrainMap.ClearMarkers(selectedMarkerType);
        EditorUtility.SetDirty(terrainMap);
        AssetDatabase.SaveAssetIfDirty(terrainMap);
        Repaint();
        SceneView.RepaintAll();
    }

    // Remplit toute la carte avec le type actuellement sélectionné
    private void FillAll()
    {
        Undo.RecordObject(terrainMap, "Fill Terrain Map");

        for (int x = 0; x < terrainMap.Width; x++)
        {
            for (int y = 0; y < terrainMap.Height; y++)
            {
                terrainMap.SetCell(x, y, selectedGroundType);
            }
        }

        EditorUtility.SetDirty(terrainMap);
        AssetDatabase.SaveAssetIfDirty(terrainMap);
        SceneView.RepaintAll();
    }

    // Redimensionne la carte en préservant les cellules de l'intersection
    private void ResizeMap()
    {
        Undo.RecordObject(terrainMap, "Resize Terrain Map");
        terrainMap.Resize(resizeWidth, resizeHeight);
        EditorUtility.SetDirty(terrainMap);
        AssetDatabase.SaveAssetIfDirty(terrainMap);
        SyncResizeFields();
        SceneView.RepaintAll();
    }

    // Synchronise les champs de taille avec l'asset sélectionné
    private void SyncResizeFields()
    {
        if (terrainMap == null)
        {
            return;
        }

        resizeWidth = terrainMap.Width;
        resizeHeight = terrainMap.Height;
    }

    // Rafraîchit toutes les vues après un Undo ou Redo
    private void HandleUndoRedo()
    {
        if (terrainMap != null)
        {
            EditorUtility.SetDirty(terrainMap);
            AssetDatabase.SaveAssetIfDirty(terrainMap);
        }

        SyncResizeFields();
        Repaint();
        SceneView.RepaintAll();
    }
}
