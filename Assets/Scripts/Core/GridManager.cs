using System.Collections.Generic;
using UnityEngine;

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

        // Calculer le d�calage pour centrer la grille
        gridOffset = new Vector2(
        -width * cellSize / 2f,
        -height * cellSize / 2f
        );
    }

    public Vector3 GridToWorld(int x, int y)
    {
        return new Vector3(
            (x + 0.5f) * cellSize + gridOffset.x,
            (y + 0.5f) * cellSize + gridOffset.y,
            0
        );
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - gridOffset.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.y - gridOffset.y) / cellSize);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Enregistre la case comme bloqu�e par un obstacle.
    /// </summary>
    public void RegisterBlockedCell(Vector2Int gridPos, GridObstacle obstacle)
    {
        blockedCells[gridPos] = obstacle;
    }

    public void UnregisterBlockedCell(Vector2Int gridPos)
    {
        if (blockedCells.ContainsKey(gridPos))
            blockedCells.Remove(gridPos);
    }
    // V�rifie si la case est bloqu�e par un obstacle du terrain
    public bool IsCellBlocked(Vector2Int pos, GridObstacle.ObstacleType? type = null)
    {
        if (!blockedCells.ContainsKey(pos))
            return false;

        if (type == null)
            return true;

        return blockedCells[pos].obstacleType == type;
    }

    /// <summary>
    /// Enregistre la case comme occup�e par une unit�.
    /// </summary>
    public void RegisterOccupiedCell(Vector2Int pos, GameObject unit)
    {
        occupiedCells[pos] = unit;
    }

    public void UnregisterOccupiedCell(Vector2Int pos)
    {
        if (occupiedCells.ContainsKey(pos))
            occupiedCells.Remove(pos);
    }
    public GameObject GetOccupant(Vector2Int pos)
    {
        if (occupiedCells.ContainsKey(pos))
            return occupiedCells[pos];
        return null;
    }

    public bool IsCellOccupied(Vector2Int pos, GameObject self = null)
    {
        return occupiedCells.ContainsKey(pos) && occupiedCells[pos] != self;
    }

    // M�thode globale : indisponible pour d�placement (obstacle ou unit�)
    public bool IsCellUnavailable(Vector2Int pos, GameObject self = null)
    {
        return IsCellBlocked(pos) || IsCellOccupied(pos, self);
    }
    public bool IsWithinBounds(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.grey;

        // D�calage du centre
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

