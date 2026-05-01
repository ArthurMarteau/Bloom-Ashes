using UnityEngine;

// Résolveur statique de type d'entité sélectionnable
// Centralise la logique de classification : layer + composants
// Layer 9 → Unit ; BuildingStatsComponent → Building ; layer 6 ou 7 sans building → Environment
public static class SelectableResolver
{
    // Retourne le SelectableType d'un GameObject selon son layer et ses composants
    public static SelectableType ResolveType(GameObject go)
    {
        if (go == null) return SelectableType.None;

        // Layer 9 = Units (Bud, Rootborn)
        if (go.layer == 9) return SelectableType.Unit;

        // BuildingStatsComponent couvre Shroomhaven (layer 8) ET Nexus (layer 7)
        if (go.GetComponent<BuildingStatsComponent>() != null) return SelectableType.Building;

        // Layer 6 (Obstacle) ou 7 (EnvWalkable) sans BuildingStats = Foundation / arbre / etc.
        if (go.layer == 6 || go.layer == 7) return SelectableType.Environment;

        return SelectableType.None;
    }

    // Raccourci : vrai si le GameObject possède un BuildingStatsComponent
    public static bool IsBuilding(GameObject go)
    {
        return go != null && go.GetComponent<BuildingStatsComponent>() != null;
    }
}
