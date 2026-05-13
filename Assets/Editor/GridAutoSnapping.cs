using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public static class GridAutoSnapping
{
    static GridAutoSnapping()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static bool snappingEnabled = true;

    [MenuItem("Tools/Enable Grid Auto Snapping %g")] // Ctrl+G
    private static void ToggleSnapping()
    {
        snappingEnabled = !snappingEnabled;
        Debug.Log("Grid Auto Snapping " + (snappingEnabled ? "Enabled" : "Disabled"));
    }

    private static bool wasDragging = false;

    private static void OnSceneGUI(SceneView sceneView)
    {
        GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
        if (!snappingEnabled || gridManager == null)
            return;

        Event e = Event.current;

        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            EditorApplication.delayCall += () =>
            {
                if (!snappingEnabled)
                    return;

                GridManager delayedGridManager = Object.FindFirstObjectByType<GridManager>();
                if (delayedGridManager == null)
                    return;

                foreach (Transform t in Selection.transforms)
                {
                    SnapTransform(t, delayedGridManager, "Snap Dropped Prefab");
                }
            };
        }

        if (Selection.transforms.Length == 0 ||
           (Tools.current != Tool.Move && Tools.current != Tool.Rect))
            return;

        // Détecte si on est en train de drag (move)
        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            wasDragging = true;
        }

        // Snap à la fin du drag (mouse up)
        if (wasDragging && e.type == EventType.MouseUp && e.button == 0)
        {
            foreach (Transform t in Selection.transforms)
            {
                SnapTransform(t, gridManager);
            }
            wasDragging = false;
        }
    }

    private static void SnapTransform(Transform t, GridManager gridManager)
    {
        SnapTransform(t, gridManager, "Snap To Grid");
    }

    private static void SnapTransform(Transform t, GridManager gridManager, string undoName)
    {
        Vector2Int gridPos = gridManager.WorldToGrid(t.position);
        Vector3 snappedPos = gridManager.GridToWorld(gridPos);

        if ((t.position - snappedPos).sqrMagnitude > 0.0001f)
        {
            Undo.RecordObject(t, undoName);
            t.position = snappedPos;
        }
    }
}
