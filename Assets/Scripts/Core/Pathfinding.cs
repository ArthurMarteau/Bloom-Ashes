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

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        PriorityQueue<PathNode> openSet = new PriorityQueue<PathNode>();
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        openSet.Enqueue(new PathNode(start, 0, Heuristic(start, goal)));

        while (openSet.Count > 0)
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

