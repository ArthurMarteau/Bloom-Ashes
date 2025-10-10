using UnityEngine;

// Classe utilitaire pour gérer les capacités des unités via tags Unity
// Système simple basé 100% sur les tags sans enum intermédiaire
// Interface principale pour toute la logique de déplacement des unités
public static class UnitTypeHelper
{
    // Tags Unity pour les différents types d'unités
    // À créer dans Unity Editor → Tags & Layers
    public const string GROUND_UNIT_TAG = "GroundUnit";   // Unité terrestre standard
    public const string WATER_UNIT_TAG = "WaterUnit";     // Unité amphibie (terre + eau)
    public const string MOUNTAIN_UNIT_TAG = "MountainUnit"; // Unité alpiniste (terre + montagne)
    public const string AIR_UNIT_TAG = "AirUnit";         // Unité volante (partout)

    // Détermine si une unité peut voler au-dessus des obstacles
    // Les obstacles bloquent toutes les unités sauf les unités volantes
    // Règle simple : seule AirUnit traverse les obstacles
    public static bool CanFlyOverObstacles(GameObject unit)
    {
        if (unit == null)
        {
            Debug.LogWarning("CanFlyOverObstacles appelé avec GameObject null");
            return false;
        }

        return unit.CompareTag(AIR_UNIT_TAG);
    }

    // Détermine si une unité peut se déplacer sur un type de terrain
    // Définit les règles d'accessibilité selon les capacités de l'unité
    // Utilisé par GridManager pour vérifier compatibilité terrain/unité
    public static bool CanWalkOnGround(GameObject unit, GroundType groundType)
    {
        if (unit == null)
        {
            Debug.LogWarning("CanWalkOnGround appelé avec GameObject null");
            return false;
        }

        if (unit.CompareTag(GROUND_UNIT_TAG))
        {
            // Unité terrestre : marche seulement sur terrain normal
            return groundType == GroundType.Ground;
        }
        else if (unit.CompareTag(WATER_UNIT_TAG))
        {
            // Unité amphibie : marche sur terrain normal ET eau
            return groundType == GroundType.Ground || groundType == GroundType.Water;
        }
        else if (unit.CompareTag(MOUNTAIN_UNIT_TAG))
        {
            // Unité alpiniste : marche sur terrain normal ET montagne
            return groundType == GroundType.Ground || groundType == GroundType.Mountain;
        }
        else if (unit.CompareTag(AIR_UNIT_TAG))
        {
            // Unité volante : vole partout, tous terrains accessibles
            return true;
        }
        else
        {
            Debug.LogWarning($"Unité {unit.name} n'a pas de tag reconnu. Tag actuel: {unit.tag}");
            return false;
        }
    }

    // Retourne une description lisible du type d'unité basée sur son tag
    // Utilisé pour debug et interface utilisateur
    // Facilite l'identification dans les logs
    public static string GetDisplayName(GameObject unit)
    {
        if (unit == null)
        {
            return "Unité Null";
        }

        if (unit.CompareTag(GROUND_UNIT_TAG))
        {
            return "Unité Terrestre";
        }
        else if (unit.CompareTag(WATER_UNIT_TAG))
        {
            return "Unité Aquatique";
        }
        else if (unit.CompareTag(MOUNTAIN_UNIT_TAG))
        {
            return "Unité Montagnarde";
        }
        else if (unit.CompareTag(AIR_UNIT_TAG))
        {
            return "Unité Aérienne";
        }
        else
        {
            return $"Type Inconnu (Tag: {unit.tag})";
        }
    }

    // Vérifie si un GameObject a un tag d'unité valide
    // Utilisé pour validation et debug
    // Retourne true si le GameObject est reconnu comme unité
    public static bool IsValidUnit(GameObject unit)
    {
        if (unit == null) return false;

        return unit.CompareTag(GROUND_UNIT_TAG) ||
               unit.CompareTag(WATER_UNIT_TAG) ||
               unit.CompareTag(MOUNTAIN_UNIT_TAG) ||
               unit.CompareTag(AIR_UNIT_TAG);
    }

    // Retourne la liste de tous les tags d'unités disponibles
    // Utilisé pour configuration et validation dans l'éditeur
    // Facilite la création d'outils de développement
    public static string[] GetAllUnitTags()
    {
        return new string[]
        {
            GROUND_UNIT_TAG,
            WATER_UNIT_TAG,
            MOUNTAIN_UNIT_TAG,
            AIR_UNIT_TAG
        };
    }

    // Retourne une description des terrains accessibles pour une unité
    // Utilisé pour documentation et aide au joueur
    // Explique les capacités de déplacement de l'unité
    public static string GetAccessibleTerrains(GameObject unit)
    {
        if (unit == null)
        {
            return "Aucun terrain (unité null)";
        }

        if (unit.CompareTag(GROUND_UNIT_TAG))
        {
            return "Terrain Normal uniquement";
        }
        else if (unit.CompareTag(WATER_UNIT_TAG))
        {
            return "Terrain Normal et Eau";
        }
        else if (unit.CompareTag(MOUNTAIN_UNIT_TAG))
        {
            return "Terrain Normal et Montagne";
        }
        else if (unit.CompareTag(AIR_UNIT_TAG))
        {
            return "Tous les terrains (vol)";
        }
        else
        {
            return "Aucun terrain (tag invalide)";
        }
    }

    // Vérifie si deux unités ont le même type (même tag)
    // Utilisé pour regroupements, formations et logique de jeu
    // Retourne true si les unités partagent le même tag d'unité
    public static bool AreSameType(GameObject unitA, GameObject unitB)
    {
        if (unitA == null || unitB == null) return false;
        if (!IsValidUnit(unitA) || !IsValidUnit(unitB)) return false;

        return unitA.tag == unitB.tag;
    }
}
