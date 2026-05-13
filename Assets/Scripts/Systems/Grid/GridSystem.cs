using System;
using UnityEngine;

// Logique de grille pure — POCO, aucun MonoBehaviour, aucun GameObject, aucun Vector3
// 100% testable sans Unity : seules dépendances = Vector2Int (type valeur) et System.Math
// Source unique de vérité pour toutes les conversions et calculs de grille
// GridManager l'utilise comme moteur de calcul, en gardant le cycle de vie Unity pour lui
public static class GridSystem
{
    // Convertit une position monde en coordonnées de grille
    // Paramètres explicites : permet les tests unitaires sans GridManager
    // Équivalent à : Mathf.FloorToInt((worldX - originX) / cellSize)
    public static Vector2Int WorldToGrid(float worldX, float worldY, float cellSize, float originX, float originY)
    {
        int x = (int)Math.Floor((worldX - originX) / cellSize);
        int y = (int)Math.Floor((worldY - originY) / cellSize);
        return new Vector2Int(x, y);
    }

    // Convertit des coordonnées de grille en position monde centrée sur la case
    // Retourne un tuple (float, float) pour éviter toute dépendance à Vector3
    // GridManager encapsule le résultat dans un Vector3 pour l'usage Unity
    public static (float x, float y) GridToWorld(int gridX, int gridY, float cellSize, float originX, float originY)
    {
        float x = gridX * cellSize + cellSize * 0.5f + originX;
        float y = gridY * cellSize + cellSize * 0.5f + originY;
        return (x, y);
    }

    // Vérifie si des coordonnées entières sont dans les limites de la grille
    // Signature entière pour éviter la dépendance à Vector2Int (facilite les tests unitaires)
    public static bool IsInBounds(int x, int y, int width, int height)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // Calcule l'origine de la grille (coin bas-gauche) pour centrer la grille sur le monde
    // Retourne un tuple pour éviter la dépendance à Vector3
    public static (float originX, float originY) CalculateOrigin(int width, int height, float cellSize)
    {
        return (-width * cellSize * 0.5f, -height * cellSize * 0.5f);
    }

    // Vérifie si une case est praticable pour une unité terrestre de base (GroundUnit)
    // Contrôle : limites de grille + terrains inaccessibles aux unités terrestres standards
    // ATTENTION : ne tient pas compte des unités présentes ni des obstacles Physics2D
    // Pour la vérification complète incluant les occupants et capacités d'unité, utiliser GridManager.IsWalkableFor()
    public static bool IsWalkable(int x, int y, int width, int height, GroundType[,] groundGrid)
    {
        if (!IsInBounds(x, y, width, height)) return false;
        if (groundGrid == null) return true;

        GroundType terrain = groundGrid[x, y];

        // Les terrains spéciaux et impassables bloquent les unités terrestres de base (le vol est géré par l'appelant)
        // Water → aquatiques seulement, Mountain → montagnardes seulement, Swamp → aquatiques seulement
        // Lava et Ravine sont impassables pour toutes les unités terrestres
        return terrain != GroundType.Water &&
               terrain != GroundType.Mountain &&
               terrain != GroundType.Swamp &&
               terrain != GroundType.Lava &&
               terrain != GroundType.Ravine;
    }
}
