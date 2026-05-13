using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Organise automatiquement les nouveaux GameObjects racines selon leur composant de stats.
[InitializeOnLoad]
public static class HierarchyAutoOrganizer
{
    private const string UnitsParentName = "Units";
    private const string BuildingsParentName = "Buildings";
    private const string EnvironmentParentName = "Environment";
    private const string UndoName = "Auto-organize hierarchy";

    // Abonne l'organizer aux changements d'objets publiés par l'éditeur.
    static HierarchyAutoOrganizer()
    {
        ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
    }

    // Parcourt les créations de hiérarchies et organise les nouveaux GameObjects racines.
    private static void OnObjectChangesPublished(ref ObjectChangeEventStream stream)
    {
        for (int i = 0; i < stream.length; i++)
        {
            if (stream.GetEventType(i) != ObjectChangeKind.CreateGameObjectHierarchy)
            {
                continue;
            }

            if (!TryGetObjectFromChangeEvent(ref stream, i, out GameObject go))
            {
                continue;
            }

            TryOrganizeGameObject(go);
        }
    }

    // Récupère le GameObject créé depuis l'event ObjectChangeEvents.
    private static bool TryGetObjectFromChangeEvent(ref ObjectChangeEventStream stream, int eventIndex, out GameObject go)
    {
        stream.GetCreateGameObjectHierarchyEvent(eventIndex, out var createEvent);
        go = EditorUtility.InstanceIDToObject(createEvent.instanceId) as GameObject;
        return go != null;
    }

    // Déplace un GameObject racine vers le parent cible s'il correspond à une catégorie connue.
    private static void TryOrganizeGameObject(GameObject go)
    {
        if (go == null || go.transform.parent != null || go.name.StartsWith("---"))
        {
            return;
        }

        if (!go.scene.IsValid() || !go.scene.isLoaded)
        {
            return;
        }

        string parentName = GetTargetParentName(go);
        if (string.IsNullOrEmpty(parentName))
        {
            return;
        }

        GameObject parent = GameObject.Find(parentName);
        if (parent == null || parent == go || go.transform.parent == parent.transform)
        {
            return;
        }

        Undo.SetTransformParent(go.transform, parent.transform, UndoName);
    }

    // Détermine le parent cible selon les composants de stats présents dans la hiérarchie.
    private static string GetTargetParentName(GameObject go)
    {
        if (go.GetComponentInChildren<UnitStatsComponent>(true) != null)
        {
            return UnitsParentName;
        }

        if (go.GetComponentInChildren<BuildingStatsComponent>(true) != null)
        {
            return BuildingsParentName;
        }

        if (go.GetComponentInChildren<EnvironmentStatsComponent>(true) != null)
        {
            return EnvironmentParentName;
        }

        return null;
    }
}
