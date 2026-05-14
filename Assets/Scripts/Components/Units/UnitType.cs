using UnityEngine;

// Classe utilitaire pour gérer les capacités de déplacement des unités
// Façade acceptant un GameObject — récupère UnitStatsData via UnitStatsComponent en interne
// Interface principale pour toute la logique de déplacement/terrain
public static class UnitTypeHelper
{
    // Récupère le UnitStatsData depuis un GameObject via UnitStatsComponent
    private static UnitStatsData GetStats(GameObject unit)
    {
        if (unit == null) return null;
        UnitStatsComponent statsComponent = unit.GetComponent<UnitStatsComponent>();
        if (statsComponent == null) return null;
        return statsComponent.GetBaseStats();
    }

    // Détermine si une unité peut voler au-dessus des obstacles
    public static bool CanFlyOverObstacles(GameObject unit)
    {
        UnitStatsData stats = GetStats(unit);
        if (stats == null)
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"CanFlyOverObstacles: UnitStatsData introuvable sur {unit?.name}", null);
            return false;
        }
        return stats.movementType == UnitMovementType.Flying;
    }

    // Détermine si une unité peut se déplacer sur un type de terrain
    // Utilisé par GridManager pour vérifier compatibilité terrain/unité
    public static bool CanWalkOnGround(GameObject unit, GroundType groundType)
    {
        UnitStatsData stats = GetStats(unit);
        if (stats == null)
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"CanWalkOnGround: UnitStatsData introuvable sur {unit?.name}", null);
            return false;
        }

        switch (stats.movementType)
        {
            case UnitMovementType.Ground:
                switch (groundType)
                {
                    case GroundType.Grass:
                    case GroundType.Dirt:
                    case GroundType.Path:
                    case GroundType.Rocky:
                    case GroundType.Sand:
                    case GroundType.Scorched:
                    case GroundType.Brambles:
                    case GroundType.Bush:
                    case GroundType.WatersapSource:
                        return true;
                    default:
                        return false;
                }

            case UnitMovementType.Swimming:
                switch (groundType)
                {
                    case GroundType.Grass:
                    case GroundType.Dirt:
                    case GroundType.Path:
                    case GroundType.Rocky:
                    case GroundType.Sand:
                    case GroundType.Scorched:
                    case GroundType.Water:
                    case GroundType.Swamp:
                    case GroundType.WatersapSource:
                        return true;
                    default:
                        return false;
                }

            case UnitMovementType.Flying:
                return true;

            default:
                GameLogger.Warn(GameLogger.Category.Movement, $"UnitMovementType inconnu : {stats.movementType}", null);
                return false;
        }
    }

    // Vérifie si un GameObject est une unité valide (a un UnitStatsComponent)
    public static bool IsValidUnit(GameObject unit)
    {
        if (unit == null) return false;
        return unit.GetComponent<UnitStatsComponent>() != null;
    }

    // Retourne une description lisible du type de déplacement
    public static string GetDisplayName(GameObject unit)
    {
        UnitStatsData stats = GetStats(unit);
        if (stats == null) return $"Unité inconnue ({unit?.name})";

        switch (stats.movementType)
        {
            case UnitMovementType.Ground:   return "Unité Terrestre";
            case UnitMovementType.Flying:   return "Unité Aérienne";
            case UnitMovementType.Swimming: return "Unité Aquatique";
            default: return $"Type Inconnu ({stats.movementType})";
        }
    }

    // Retourne une description des terrains accessibles
    public static string GetAccessibleTerrains(GameObject unit)
    {
        UnitStatsData stats = GetStats(unit);
        if (stats == null) return "Aucun terrain (stats null)";

        switch (stats.movementType)
        {
            case UnitMovementType.Ground:
                return "Prairie, Terre, Chemin, Rocailleux, Sable, Brûlé, Ronces, Buissons, Source";
            case UnitMovementType.Swimming:
                return "Prairie, Terre, Chemin, Rocailleux, Sable, Brûlé, Eau, Marécage, Source";
            case UnitMovementType.Flying:
                return "Tous les terrains (vol)";
            default:
                return "Aucun terrain (type invalide)";
        }
    }
}
