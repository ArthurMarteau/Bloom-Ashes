using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    public int width = 128;
    public int height = 128;
    private float cellSize = 1f;
    private Vector2 gridOffset;
    private Dictionary<Vector2Int, GridObstacle> blockedCells = new Dictionary<Vector2Int, GridObstacle>();
    private Dictionary<Vector2Int, GameObject> occupiedCells = new Dictionary<Vector2Int, GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Calculer le décalage pour centrer la grille
        gridOffset = new Vector2(
            -width * cellSize / 2f,
            -height * cellSize / 2f
        );
    }

    // Convertit une position de grille en position monde
    public Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(
            (x + 0.5f) * cellSize + gridOffset.x,
            (y + 0.5f) * cellSize + gridOffset.y,
            0
        );
    }

    // Convertit une position monde en position de grille
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - gridOffset.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.y - gridOffset.y) / cellSize);
        return new Vector2Int(x, y);
    }

    // Enregistre la case comme bloquée par un obstacle
    public void RegisterBlockedCell(Vector2Int gridPos, GridObstacle obstacle)
    {
        blockedCells[gridPos] = obstacle;
    }

    // Désenregistre une case bloquée
    public void UnregisterBlockedCell(Vector2Int gridPos)
    {
        if (blockedCells.ContainsKey(gridPos))
            blockedCells.Remove(gridPos);
    }
    
    // Vérifie si la case est bloquée par un obstacle du terrain
    public bool IsCellBlocked(Vector2Int pos, GridObstacle.ObstacleType? type = null)
    {
        if (!blockedCells.ContainsKey(pos))
            return false;

        if (type == null)
            return true;

        return blockedCells[pos].obstacleType == type;
    }

    // Enregistre la case comme occupée par une unité
    public void RegisterOccupiedCell(Vector2Int pos, GameObject unit)
    {
        occupiedCells[pos] = unit;
    }

    // Désenregistre une case occupée
    public void UnregisterOccupiedCell(Vector2Int pos)
    {
        if (occupiedCells.ContainsKey(pos))
            occupiedCells.Remove(pos);
    }
    
    // Récupère l'occupant d'une case
    public GameObject GetOccupant(Vector2Int pos)
    {
        if (occupiedCells.ContainsKey(pos))
            return occupiedCells[pos];
        return null;
    }

    // Vérifie si une case est occupée par une unité
    public bool IsCellOccupied(Vector2Int pos, GameObject self = null)
    {
        return occupiedCells.ContainsKey(pos) && occupiedCells[pos] != self;
    }

    // Vérifie si une case est indisponible pour déplacement (obstacle ou unité)
    public bool IsCellUnavailable(Vector2Int pos, GameObject self = null)
    {
        return IsCellBlocked(pos) || IsCellOccupied(pos, self);
    }
    
    // Vérifie si une position est dans les limites de la grille
    public bool IsWithinBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    // NOUVELLE MÉTHODE : Récupère toutes les unités dans un rayon donné
    public List<GameObject> GetUnitsInRadius(Vector2Int center, int radius)
    {
        List<GameObject> unitsInRadius = new List<GameObject>();
        
        // Parcourir toutes les cases dans le rayon
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                
                // Vérifier que la position est valide et contient une unité
                if (IsWithinBounds(checkPos) && occupiedCells.ContainsKey(checkPos))
                {
                    unitsInRadius.Add(occupiedCells[checkPos]);
                }
            }
        }
        
        return unitsInRadius;
    }
    
    // NOUVELLE MÉTHODE : Récupère toutes les unités dans un rayon avec filtre
    public List<GameObject> GetUnitsInRadius(Vector2Int center, int radius, System.Func<GameObject, bool> filter)
    {
        List<GameObject> units = GetUnitsInRadius(center, radius);
        return units.Where(filter).ToList();
    }
    
    // NOUVELLE MÉTHODE : Récupère toutes les cases libres dans un rayon
    public List<Vector2Int> GetFreeCellsInRadius(Vector2Int center, int radius)
    {
        List<Vector2Int> freeCells = new List<Vector2Int>();
        
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                
                // Vérifier que la position est valide et libre
                if (IsWithinBounds(checkPos) && !IsCellUnavailable(checkPos))
                {
                    freeCells.Add(checkPos);
                }
            }
        }
        
        return freeCells;
    }
    
    // NOUVELLE MÉTHODE : Calcule la densité d'unités dans une zone
    public float GetUnitDensity(Vector2Int center, int radius)
    {
        int totalCells = (radius * 2 + 1) * (radius * 2 + 1);
        int occupiedCount = GetUnitsInRadius(center, radius).Count;
        return (float)occupiedCount / totalCells;
    }
    
    // NOUVELLE MÉTHODE : Trouve la case libre la plus proche dans un rayon
    public Vector2Int? GetNearestFreeCell(Vector2Int center, int maxRadius = 10)
    {
        // Chercher en spirale depuis le centre
        for (int radius = 1; radius <= maxRadius; radius++)
        {
            List<Vector2Int> freeCells = GetFreeCellsInRadius(center, radius);
            if (freeCells.Count > 0)
            {
                // Retourner la case libre la plus proche du centre
                return freeCells.OrderBy(cell => Vector2Int.Distance(center, cell)).First();
            }
        }
        
        return null;
    }

    // Méthode de visualisation en mode éditeur
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.grey;

        // Décalage du centre
        float xOffset = -width * cellSize / 2f;
        float yOffset = -height * cellSize / 2f;

        for (int x = 0; x <= width; x++)
        {
            Vector3 from = new Vector3(x * cellSize + xOffset, yOffset, 0);
            Vector3 to = new Vector3(x * cellSize + xOffset, height * cellSize + yOffset, 0);
            Gizmos.DrawLine(from, to);
        }

        for (int y = 0; y <= height; y++)
        {
            Vector3 from = new Vector3(xOffset, y * cellSize + yOffset, 0);
            Vector3 to = new Vector3(width * cellSize + xOffset, y * cellSize + yOffset, 0);
            Gizmos.DrawLine(from, to);
        }
    }
}

