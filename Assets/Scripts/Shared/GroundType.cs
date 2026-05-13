using UnityEngine;

// Types de terrain qui influencent l'accessibilité, les coûts de déplacement et la défense
// Correspondent aux tuiles visibles sur le layer Ground dans Unity
// Configuration statique définie au démarrage de la partie
public enum GroundType
{
    Grass,          // Prairie - terrain normal de base
    Dirt,           // Terre battue - terrain normal
    Path,           // Chemin - route ou sentier rapide
    Rocky,          // Rocailleux - terrain difficile accessible aux unités terrestres et montagnardes
    Swamp,          // Marécage - accessible seulement aux unités aquatiques et aériennes
    Brambles,       // Ronces - terrain difficile offrant une couverture défensive
    Sand,           // Sable - terrain normal, ralentit légèrement les unités
    Scorched,       // Brûlé - terrain dégagé sans couverture
    Lava,           // Lave - impassable pour toutes les unités non-aériennes
    Water,          // Eau - accessible seulement aux unités aquatiques et aériennes
    Mountain,       // Montagne - accessible seulement aux unités montagnardes et aériennes
    Ravine,         // Ravin - impassable pour toutes les unités non-aériennes
    Bush,           // Buissons - offre une couverture défensive
    WatersapSource  // Source de sève d'eau - terrain spécial accessible aux unités terrestres et aquatiques
}

// Classe utilitaire pour la gestion des types de terrain
// Fournit des méthodes helper pour affichage, debug, coûts de déplacement et modificateurs
// Utilisée par GridManager, UnitTypeHelper et outils de développement
public static class GroundTypeExtensions
{
    // Retourne une description lisible du type de terrain
    // Utilisé pour debug et interface utilisateur
    public static string GetDisplayName(this GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Grass:           return "Prairie";
            case GroundType.Dirt:            return "Terre Battue";
            case GroundType.Path:            return "Chemin";
            case GroundType.Rocky:           return "Rocailleux";
            case GroundType.Swamp:           return "Marécage";
            case GroundType.Brambles:        return "Ronces";
            case GroundType.Sand:            return "Sable";
            case GroundType.Scorched:        return "Brûlé";
            case GroundType.Lava:            return "Lave";
            case GroundType.Water:           return "Eau";
            case GroundType.Mountain:        return "Montagne";
            case GroundType.Ravine:          return "Ravin";
            case GroundType.Bush:            return "Buissons";
            case GroundType.WatersapSource:  return "Source de Sève d'Eau";
            default:                         return "Terrain Inconnu";
        }
    }

    // Retourne la couleur associée au terrain pour debug visuel dans l'éditeur
    // Permet de visualiser rapidement les zones de terrain
    public static Color GetDebugColor(this GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Grass:           return new Color(0.2f, 0.8f, 0.2f);   // Vert vif
            case GroundType.Dirt:            return new Color(0.6f, 0.4f, 0.2f);   // Brun
            case GroundType.Path:            return new Color(0.9f, 0.8f, 0.5f);   // Beige
            case GroundType.Rocky:           return new Color(0.5f, 0.5f, 0.5f);   // Gris moyen
            case GroundType.Swamp:           return new Color(0.3f, 0.5f, 0.2f);   // Vert foncé
            case GroundType.Brambles:        return new Color(0.4f, 0.3f, 0.1f);   // Brun foncé
            case GroundType.Sand:            return new Color(0.95f, 0.85f, 0.6f); // Jaune sable
            case GroundType.Scorched:        return new Color(0.3f, 0.2f, 0.1f);   // Noir brûlé
            case GroundType.Lava:            return new Color(1.0f, 0.3f, 0.0f);   // Orange vif
            case GroundType.Water:           return Color.blue;                     // Bleu
            case GroundType.Mountain:        return Color.gray;                     // Gris
            case GroundType.Ravine:          return new Color(0.2f, 0.1f, 0.1f);   // Noir profond
            case GroundType.Bush:            return new Color(0.1f, 0.5f, 0.1f);   // Vert foncé
            case GroundType.WatersapSource:  return new Color(0.4f, 0.8f, 0.9f);   // Bleu clair
            default:                         return Color.white;
        }
    }

    // Retourne une description des unités pouvant accéder à ce terrain
    // Utilisé pour documentation et aide au joueur
    public static string GetAccessibleUnits(this GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Grass:
            case GroundType.Dirt:
            case GroundType.Path:
            case GroundType.Scorched:
                return "Toutes les unités terrestres, aquatiques et aériennes";
            case GroundType.Rocky:
                return "Unités Terrestres, Montagnardes et Aériennes";
            case GroundType.Swamp:
                return "Unités Aquatiques et Aériennes";
            case GroundType.Brambles:
            case GroundType.Bush:
                return "Unités Terrestres, Montagnardes et Aériennes";
            case GroundType.Sand:
            case GroundType.WatersapSource:
                return "Unités Terrestres, Aquatiques et Aériennes";
            case GroundType.Lava:
            case GroundType.Ravine:
                return "Unités Aériennes uniquement";
            case GroundType.Water:
                return "Unités Aquatiques et Aériennes";
            case GroundType.Mountain:
                return "Unités Montagnardes et Aériennes";
            default:
                return "Aucune unité";
        }
    }

    // Retourne le coût de déplacement de base pour ce terrain
    // Utilisé par les algorithmes de pathfinding pour pondérer les chemins
    // Coût normal = 10, plus élevé = terrain plus difficile à traverser
    public static int GetPathCost(this GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Path:            return 5;   // Chemin rapide
            case GroundType.Grass:
            case GroundType.Dirt:
            case GroundType.WatersapSource:  return 10;  // Terrain normal
            case GroundType.Sand:
            case GroundType.Rocky:
            case GroundType.Bush:            return 15;  // Terrain légèrement difficile
            case GroundType.Brambles:
            case GroundType.Swamp:
            case GroundType.Scorched:
            case GroundType.Water:
            case GroundType.Mountain:        return 20;  // Terrain difficile
            case GroundType.Lava:
            case GroundType.Ravine:          return 30;  // Terrain extrême
            default:                         return 10;
        }
    }

    // Retourne le modificateur de vitesse de déplacement pour ce terrain
    // Multiplicateur appliqué à la vitesse de base de l'unité (1.0 = vitesse normale)
    public static float GetMoveSpeedModifier(this GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Path:            return 1.5f;  // Chemin rapide
            case GroundType.Grass:
            case GroundType.Dirt:
            case GroundType.WatersapSource:  return 1.0f;  // Vitesse normale
            case GroundType.Sand:
            case GroundType.Rocky:
            case GroundType.Bush:
            case GroundType.Scorched:        return 0.8f;  // Légèrement ralenti
            case GroundType.Water:
            case GroundType.Mountain:        return 0.7f;  // Terrain spécial
            case GroundType.Swamp:           return 0.6f;  // Fortement ralenti
            case GroundType.Brambles:        return 0.5f;  // Très ralenti
            case GroundType.Lava:
            case GroundType.Ravine:          return 0.3f;  // Extrêmement ralenti (Air uniquement)
            default:                         return 1.0f;
        }
    }

    // Retourne le modificateur de défense pour les unités positionnées sur ce terrain
    // Bonus ou malus ajouté à la défense de base de l'unité
    public static int GetDefenseModifier(this GroundType groundType)
    {
        switch (groundType)
        {
            case GroundType.Mountain:        return 3;   // Avantage de hauteur
            case GroundType.Bush:            return 2;   // Couverture dense
            case GroundType.Brambles:
            case GroundType.Rocky:           return 1;   // Couverture légère
            case GroundType.Path:
            case GroundType.Scorched:        return -1;  // Terrain découvert
            default:                         return 0;   // Aucun modificateur
        }
    }

    // Retourne si ce terrain est totalement impassable pour les unités non-aériennes
    // Les unités aériennes peuvent survoler ces terrains via CanFlyOverObstacles()
    public static bool IsImpassable(this GroundType groundType)
    {
        return groundType == GroundType.Lava || groundType == GroundType.Ravine;
    }
}
