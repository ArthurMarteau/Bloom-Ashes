using System.Collections.Generic;
using UnityEngine;


public class Pathfinding : MonoBehaviour
{
    public static Pathfinding Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Trouve le chemin optimal d'une cellule à une autre (A*)
    /// </summary>
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        var closedSet = new HashSet<Vector2Int>();
        var openSet = new PriorityQueue<PathNode>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        openSet.Enqueue(new PathNode(start, 0, Heuristic(start, goal)));

        while (!openSet.IsEmpty())
        {
            PathNode current = openSet.Dequeue();
            if (current.Position == goal)
                return ReconstructPath(cameFrom, current.Position);

            closedSet.Add(current.Position);

            foreach (Vector2Int neighbor in GetNeighbors(current.Position))
            {
                if (closedSet.Contains(neighbor) || GridManager.Instance.IsCellUnavailable(neighbor))
                    continue;

                int tentativeG = current.G + 1;
                PathNode neighborNode = new PathNode(neighbor, tentativeG, Heuristic(neighbor, goal));

                if (!openSet.ContainsBetter(neighborNode))
                {
                    cameFrom[neighbor] = current.Position;
                    openSet.Enqueue(neighborNode);
                }
            }
        }

        return null; // Aucun chemin trouvé
    }

    /// <summary>
    /// Trouve la cellule la plus proche atteignable autour de la cible.
    /// Utile si la cellule cible est occupée.
    /// </summary>
    public Vector2Int FindClosestFreeCell(Vector2Int start, Vector2Int target, int radiusMax = 10)
    {
        var queue = new Queue<Vector2Int>();
        var visited = new HashSet<Vector2Int>();

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int bestCell = start;
        float bestDist = (start - target).sqrMagnitude;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            float dist = (current - target).sqrMagnitude;
            if (dist < bestDist && !GridManager.Instance.IsCellUnavailable(current))
            {
                bestDist = dist;
                bestCell = current;
            }

            foreach (Vector2Int neighbor in GetNeighbors(current))
            {
                if (visited.Contains(neighbor) || !GridManager.Instance.IsWithinBounds(neighbor))
                    continue;

                if (Mathf.Abs(neighbor.x - target.x) > radiusMax || Mathf.Abs(neighbor.y - target.y) > radiusMax)
                    continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return bestCell;
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    private int Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // distance de Manhattan
    }

    private IEnumerable<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                yield return new Vector2Int(pos.x + dx, pos.y + dy);
            }
    }
}

