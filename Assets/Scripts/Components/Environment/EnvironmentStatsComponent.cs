using System.Collections.Generic;
using UnityEngine;

// Composant simple exposant les données de base d'une entité d'environnement.
public class EnvironmentStatsComponent : MonoBehaviour, IMultiTile
{
    [SerializeField] private EnvironmentStatsData baseStats;

    // Retourne le ScriptableObject de stats de base.
    public EnvironmentStatsData GetBaseStats() => baseStats;

    // Retourne toutes les cellules occupées par cette entité d'environnement.
    public List<Vector2Int> GetOccupiedTiles()
    {
        List<Vector2Int> tiles = new List<Vector2Int>();
        if (baseStats == null || GridManager.Instance == null)
            return tiles;

        Vector2Int anchor = GridManager.Instance.WorldToGrid(transform.position);
        for (int x = 0; x < baseStats.sizeX; x++)
        {
            for (int y = 0; y < baseStats.sizeY; y++)
            {
                tiles.Add(new Vector2Int(anchor.x + x, anchor.y + y));
            }
        }

        return tiles;
    }
}
