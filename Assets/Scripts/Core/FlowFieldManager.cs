using System.Collections.Generic;
using UnityEngine;

public class FlowFieldManager : MonoBehaviour
{
    public static FlowFieldManager Instance;
    
    // Cache des flow fields calculés pour éviter les recalculs
    private Dictionary<Vector2Int, FlowField> cachedFlowFields = new Dictionary<Vector2Int, FlowField>();
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    // Génère ou récupère un flow field vers une destination
    public FlowField GetFlowField(Vector2Int destination)
    {
        // Vérifier si on a déjà calculé ce flow field
        if (cachedFlowFields.TryGetValue(destination, out FlowField existingField))
        {
            return existingField;
        }
        
        // Calculer un nouveau flow field
        FlowField newField = GenerateFlowField(destination);
        cachedFlowFields[destination] = newField;
        return newField;
    }
    
    // Génère un flow field complet vers une destination
    private FlowField GenerateFlowField(Vector2Int destination)
    {
        int width = GridManager.Instance.width;
        int height = GridManager.Instance.height;
        
        // Créer le flow field vide
        FlowField flowField = new FlowField(width, height, destination);
        
        // Étape 1 : Calculer les coûts (distance depuis la destination)
        CalculateCostField(flowField, destination);
        
        // Étape 2 : Calculer les directions optimales
        CalculateFlowDirections(flowField);
        
        return flowField;
    }
    
    // Calcule le coût (distance) depuis la destination vers toutes les cases
    private void CalculateCostField(FlowField flowField, Vector2Int destination)
    {
        Queue<Vector2Int> openSet = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        
        // Commencer par la destination (coût = 0)
        flowField.SetCost(destination, 0);
        openSet.Enqueue(destination);
        visited.Add(destination);
        
        // Propagation BFS depuis la destination
        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();
            int currentCost = flowField.GetCost(current);
            
            // Explorer tous les voisins (8 directions)
            foreach (Vector2Int neighbor in GetNeighbors(current))
            {
                // Ignorer si déjà visité ou hors limites
                if (visited.Contains(neighbor) || !GridManager.Instance.IsWithinBounds(neighbor))
                    continue;
                
                // Ignorer si la case est bloquée par un obstacle
                if (GridManager.Instance.IsCellBlocked(neighbor))
                {
                    flowField.SetCost(neighbor, int.MaxValue); // Coût infini pour obstacle
                    continue;
                }
                
                // Calculer le coût pour atteindre ce voisin
                int movementCost = CalculateIntelligentCost(current, neighbor, destination);
                int newCost = currentCost + movementCost;
                
                flowField.SetCost(neighbor, newCost);
                openSet.Enqueue(neighbor);
                visited.Add(neighbor);
            }
        }
    }
    
    // Calcule la direction optimale pour chaque case
    private void CalculateFlowDirections(FlowField flowField)
    {
        for (int x = 0; x < flowField.Width; x++)
        {
            for (int y = 0; y < flowField.Height; y++)
            {
                Vector2Int currentPos = new Vector2Int(x, y);
                
                // Si c'est un obstacle, pas de direction
                if (flowField.GetCost(currentPos) == int.MaxValue)
                {
                    flowField.SetDirection(currentPos, Vector2Int.zero);
                    continue;
                }
                
                // Trouver le voisin avec le coût le plus faible
                Vector2Int bestDirection = Vector2Int.zero;
                int bestCost = int.MaxValue;
                
                foreach (Vector2Int neighbor in GetNeighbors(currentPos))
                {
                    if (!GridManager.Instance.IsWithinBounds(neighbor))
                        continue;
                    
                    int neighborCost = flowField.GetCost(neighbor);
                    if (neighborCost < bestCost)
                    {
                        bestCost = neighborCost;
                        bestDirection = neighbor - currentPos; // Direction vers ce voisin
                    }
                }
                
                flowField.SetDirection(currentPos, bestDirection);
            }
        }
    }
    
    // Retourne les 8 voisins d'une position
    private IEnumerable<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                yield return new Vector2Int(pos.x + dx, pos.y + dy);
            }
        }
    }
    
    // Vérifie si le mouvement est diagonal
    private bool IsDirectionDiagonal(Vector2Int from, Vector2Int to)
    {
        Vector2Int direction = to - from;
        return direction.x != 0 && direction.y != 0;
    }
    
    // Nettoie le cache (à appeler quand la carte change)
    public void ClearCache()
    {
        cachedFlowFields.Clear();
    }
    
    // Supprime un flow field spécifique du cache
    public void InvalidateFlowField(Vector2Int destination)
    {
        cachedFlowFields.Remove(destination);
    }
    
    // Trouve la cellule libre la plus proche d'une cible donnée (pour la poursuite)
    public Vector2Int FindClosestFreeCell(Vector2Int start, Vector2Int target, int radiusMax = 10)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        // Commencer l'exploration depuis le point de départ
        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int bestCell = start;
        float bestDist = (start - target).sqrMagnitude;

        // Exploration BFS pour trouver la cellule libre la plus proche
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            float dist = (current - target).sqrMagnitude;
            
            // Si cette cellule est plus proche ET libre
            if (dist < bestDist && !GridManager.Instance.IsCellUnavailable(current))
            {
                bestDist = dist;
                bestCell = current;
            }

            // Explorer tous les voisins
            foreach (Vector2Int neighbor in GetNeighbors(current))
            {
                // Ignorer si déjà visité ou hors limites
                if (visited.Contains(neighbor) || !GridManager.Instance.IsWithinBounds(neighbor))
                    continue;

                // Ignorer si hors du rayon maximum (distance de Chebyshev)
                int chebyshevDistance = Mathf.Max(Mathf.Abs(neighbor.x - target.x), Mathf.Abs(neighbor.y - target.y));
                if (chebyshevDistance > radiusMax)
                    continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return bestCell;
    }
}

// Classe qui stocke un flow field complet
public class FlowField
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public Vector2Int Destination { get; private set; }
    
    // Grille des coûts (distance depuis la destination)
    private int[,] costField;
    
    // Grille des directions optimales
    private Vector2Int[,] directionField;
    
    public FlowField(int width, int height, Vector2Int destination)
    {
        Width = width;
        Height = height;
        Destination = destination;
        costField = new int[width, height];
        directionField = new Vector2Int[width, height];
        
        // Initialiser tous les coûts à l'infini
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                costField[x, y] = int.MaxValue;
            }
        }
    }
    
    // Définit le coût d'une case
    public void SetCost(Vector2Int pos, int cost)
    {
        if (IsValidPosition(pos))
            costField[pos.x, pos.y] = cost;
    }
    
    // Récupère le coût d'une case
    public int GetCost(Vector2Int pos)
    {
        if (IsValidPosition(pos))
            return costField[pos.x, pos.y];
        return int.MaxValue;
    }
    
    // Définit la direction optimale d'une case
    public void SetDirection(Vector2Int pos, Vector2Int direction)
    {
        if (IsValidPosition(pos))
            directionField[pos.x, pos.y] = direction;
    }
    
    // Récupère la direction optimale depuis une case
    public Vector2Int GetDirection(Vector2Int pos)
    {
        if (IsValidPosition(pos))
            return directionField[pos.x, pos.y];
        return Vector2Int.zero;
    }
    
    // Vérifie si une position est valide
    private bool IsValidPosition(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < Width && pos.y >= 0 && pos.y < Height;
    }
    
}