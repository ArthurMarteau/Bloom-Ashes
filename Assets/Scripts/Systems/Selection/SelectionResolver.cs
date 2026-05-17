using UnityEngine;

// Résout le type métier d'un hit sélectionnable à partir de ses composants
public static class SelectionResolver
{
    // Classifie un GameObject touché en unité, bâtiment ou obstacle
    public static SelectionType Classify(GameObject hit, out GameObject entity)
    {
        entity = hit;

        if (hit == null)
        {
            entity = null;
            return SelectionType.None;
        }

        bool hasUnitStats = hit.GetComponent<UnitStatsComponent>() != null;
        bool hasBuildingStats = hit.GetComponent<BuildingStatsComponent>() != null;
        bool hasEnvironmentStats = hit.GetComponent<EnvironmentStatsComponent>() != null
                                || hit.GetComponent<FoundationComponent>() != null;

        if (hasUnitStats && !hasBuildingStats)
        {
            return SelectionType.Units;
        }

        if (hasBuildingStats)
        {
            return SelectionType.Building;
        }

        if (hasEnvironmentStats)
        {
            return SelectionType.Environment;
        }

        return SelectionType.Obstacle;
    }

    // Retourne le meilleur collider selon la priorité Unit > Building > Obstacle
    public static GameObject ResolveBestHit(Collider2D[] colliders)
    {
        if (colliders == null || colliders.Length == 0)
        {
            return null;
        }

        GameObject bestEntity = null;
        int bestPriority = -1;

        foreach (Collider2D hitCollider in colliders)
        {
            if (hitCollider == null)
            {
                continue;
            }

            GameObject candidate = hitCollider.gameObject;
            SelectionType type = Classify(candidate, out GameObject entity);
            int priority = GetPriority(type);

            if (priority > bestPriority)
            {
                bestEntity = entity;
                bestPriority = priority;
            }
        }

        return bestEntity;
    }

    // Indique si le GameObject est une vraie unité sélectionnable en multi-sélection
    public static bool IsSelectableUnit(GameObject entity)
    {
        if (entity == null)
        {
            return false;
        }

        return entity.GetComponent<UnitStatsComponent>() != null && entity.GetComponent<BuildingStatsComponent>() == null;
    }

    // Convertit un type de sélection en priorité de clic
    private static int GetPriority(SelectionType type)
    {
        switch (type)
        {
            case SelectionType.Units:
                return 3;
            case SelectionType.Building:
                return 2;
            case SelectionType.Environment:
            case SelectionType.Obstacle:
                return 1;
            default:
                return 0;
        }
    }
}
