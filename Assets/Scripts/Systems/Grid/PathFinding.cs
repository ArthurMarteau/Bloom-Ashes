using System;
using System.Collections.Generic;
using UnityEngine;

// Calculateur de chemin pur utilisant l'algorithme A*
// Responsabilité unique : recevoir deux positions + GameObject unité, retourner chemin optimal
// Utilise système de tags Unity pour identifier les capacités des unités
public static class PathFinding
{
    // Constantes de coût pour normaliser les calculs de distance
    private const int MOVEMENT_COST_STRAIGHT = 10;
    private const int MOVEMENT_COST_DIAGONAL = 14;

    // Directions partagées entre tous les calculs pour éviter une allocation par nœud exploré
    private static readonly Vector2Int[] directions = {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(1, 1),
        new Vector2Int(-1, -1),
        new Vector2Int(1, -1)
    };

    // Conteneurs réutilisés : le scheduler garantit qu'un seul calcul s'exécute à la fois
    private static readonly Stack<PathNode> nodePool = new Stack<PathNode>();
    private static readonly List<PathNode> rentedNodes = new List<PathNode>();
    private static readonly MinHeap<PathNode> openList = new MinHeap<PathNode>();
    private static readonly Dictionary<Vector2Int, PathNode> openDict = new Dictionary<Vector2Int, PathNode>();
    private static readonly HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

    // Structure de données représentant une case pendant le calcul A*
    // Contient les coûts et le lien vers le parent pour reconstitution du chemin
    // IComparable<PathNode> requis par MinHeap : compare par fCost, puis hCost en égalité
    private class PathNode : IComparable<PathNode>
    {
        public Vector2Int position;
        public int gCost; // Coût réel depuis le départ
        public int hCost; // Coût estimé vers la destination
        public int unitTieBreakSeed;
        public int fCost => gCost + hCost; // Coût total pour priorisation
        public PathNode parent; // Nœud précédent pour reconstitution

        // Réinitialise entièrement un nœud loué avant sa réutilisation
        public void Reset(Vector2Int newPosition, int seed)
        {
            position = newPosition;
            gCost = 0;
            hCost = 0;
            parent = null;
            unitTieBreakSeed = seed;
        }

        // Compare par fCost d'abord, puis hCost en cas d'égalité (même logique que GetLowestFCostNode)
        public int CompareTo(PathNode other)
        {
            int comparison = fCost.CompareTo(other.fCost);
            if (comparison == 0)
                comparison = hCost.CompareTo(other.hCost);
            if (comparison == 0)
            {
                int myTie = unchecked((position.x * 73856093) ^ (position.y * 19349663) ^ unitTieBreakSeed);
                int otherTie = unchecked((other.position.x * 73856093) ^ (other.position.y * 19349663) ^ other.unitTieBreakSeed);
                comparison = myTie.CompareTo(otherTie);
            }
            return comparison;
        }
    }

    // Point d'entrée unique pour calcul de chemin
    // Interface simple pour MovementController qui demande un trajet
    // Utilise GridManager et système de tags pour vérifier accessibilité des cases
    public static List<Vector2Int> FindPath(Vector2Int startPosition, Vector2Int endPosition, GameObject unit)
    {
        try
        {
            // Valide les paramètres avant calcul
            if (!IsValidPathRequest(startPosition, endPosition, unit))
            {
                if (GameLogger.IsEnabled(GameLogger.Category.Movement))
                    GameLogger.Warn(GameLogger.Category.Movement, $"Demande de chemin invalide : {startPosition} -> {endPosition} pour {UnitTypeHelper.GetDisplayName(unit)}", null);
                return new List<Vector2Int>();
            }

            // Lance le calcul A* et retourne le résultat
            List<Vector2Int> calculatedPath = ExecuteAStar(startPosition, endPosition, unit);

            if (GameLogger.IsEnabled(GameLogger.Category.Movement))
                GameLogger.Log(GameLogger.Category.Movement, $"Chemin calculé pour {UnitTypeHelper.GetDisplayName(unit)} : {startPosition} -> {endPosition}, {calculatedPath.Count} étapes", null);
            return calculatedPath;
        }
        finally
        {
            ReleaseAll();
        }
    }

    // Implémentation A* avec MinHeap pour extraction O(log n) du nœud optimal
    // openList (MinHeap) : extraction du meilleur nœud en O(log n) au lieu de O(n)
    // openDict (Dictionary) : lookup d'un nœud existant par position en O(1) au lieu de O(n)
    private static List<Vector2Int> ExecuteAStar(Vector2Int startPosition, Vector2Int endPosition, GameObject unit)
    {
        int tieBreakSeed = unit != null ? unit.GetInstanceID() : 0;

        // Crée le nœud de départ et l'ajoute aux deux structures
        PathNode startNode = Rent(startPosition, tieBreakSeed);
        startNode.hCost = CalculateHCost(startPosition, endPosition);
        openList.Insert(startNode);
        openDict[startNode.position] = startNode;

        // Boucle principale A* : explore jusqu'à trouver destination ou épuiser possibilités
        while (openList.Count > 0)
        {
            // Extrait le nœud de coût minimal en O(log n) via MinHeap
            PathNode currentNode = openList.ExtractMin();
            openDict.Remove(currentNode.position);
            closedSet.Add(currentNode.position);

            // Vérifie si la destination est atteinte
            if (currentNode.position == endPosition)
            {
                if (GameLogger.IsEnabled(GameLogger.Category.Movement))
                    GameLogger.Log(GameLogger.Category.Movement, "Destination atteinte, reconstitution du chemin", null);
                return ReconstructPath(currentNode);
            }

            // Explore directement les 8 voisins sans créer de liste temporaire
            foreach (Vector2Int direction in directions)
            {
                Vector2Int neighborPosition = currentNode.position + direction;

                if (!GridManager.Instance.IsWalkableFor(neighborPosition, unit))
                    continue;

                // Ignore les nœuds déjà traités définitivement
                if (closedSet.Contains(neighborPosition))
                    continue;

                // Calcule le nouveau coût pour atteindre ce voisin
                int tentativeGCost = currentNode.gCost + CalculateGCost(currentNode, neighborPosition);

                // Cherche un nœud existant dans openDict en O(1) (remplace openList.Find O(n))
                if (!openDict.TryGetValue(neighborPosition, out PathNode existingNode))
                {
                    // Nouveau nœud : configure et ajoute aux deux structures
                    PathNode neighborNode = Rent(neighborPosition, tieBreakSeed);
                    neighborNode.gCost = tentativeGCost;
                    neighborNode.hCost = CalculateHCost(neighborPosition, endPosition);
                    neighborNode.parent = currentNode;
                    openList.Insert(neighborNode);
                    openDict[neighborPosition] = neighborNode;
                }
                else if (tentativeGCost < existingNode.gCost)
                {
                    // Chemin plus court trouvé : met à jour le nœud et réorganise le tas
                    // UpdateItem remonte le nœud dans le MinHeap car son fCost a diminué
                    existingNode.gCost = tentativeGCost;
                    existingNode.parent = currentNode;
                    openList.UpdateItem(existingNode);
                }
            }
        }

        // Aucun chemin trouvé après exploration complète
        if (GameLogger.IsEnabled(GameLogger.Category.Movement))
            GameLogger.Warn(GameLogger.Category.Movement, $"Aucun chemin trouvé entre {startPosition} et {endPosition} pour {UnitTypeHelper.GetDisplayName(unit)}", null);
        return new List<Vector2Int>();
    }

    // Calcule la distance heuristique Manhattan avec support des diagonales
    // Estime le coût restant pour guider A* vers la destination efficacement
    // Utilise les mêmes coûts que les mouvements réels pour cohérence
    private static int CalculateHCost(Vector2Int positionA, Vector2Int positionB)
    {
        int deltaX = Mathf.Abs(positionA.x - positionB.x);
        int deltaY = Mathf.Abs(positionA.y - positionB.y);

        // Calcule les mouvements diagonaux et droits optimaux
        int diagonalMoves = Mathf.Min(deltaX, deltaY);
        int straightMoves = Mathf.Abs(deltaX - deltaY);

        return diagonalMoves * MOVEMENT_COST_DIAGONAL + straightMoves * MOVEMENT_COST_STRAIGHT;
    }

    // Calcule le coût réel de déplacement entre deux cases adjacentes
    // Détermine le coût précis selon le type de mouvement (droit ou diagonal)
    private static int CalculateGCost(PathNode fromNode, Vector2Int toPosition)
    {
        Vector2Int fromPosition = fromNode.position;
        int deltaX = Mathf.Abs(fromPosition.x - toPosition.x);
        int deltaY = Mathf.Abs(fromPosition.y - toPosition.y);

        // Détermine le type de mouvement et retourne le coût approprié
        if (deltaX == 1 && deltaY == 1)
        {
            return MOVEMENT_COST_DIAGONAL; // Mouvement diagonal
        }
        else
        {
            return MOVEMENT_COST_STRAIGHT; // Mouvement droit
        }
    }

    // Reconstitue le chemin final en remontant les parents depuis la destination
    // Convertit la structure interne A* en liste de positions utilisable
    // Inverse la liste pour obtenir l'ordre correct départ -> arrivée
    private static List<Vector2Int> ReconstructPath(PathNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        PathNode currentNode = endNode;

        // Remonte la chaîne des parents jusqu'au nœud de départ
        while (currentNode != null)
        {
            path.Add(currentNode.position);
            currentNode = currentNode.parent;
        }

        // Inverse pour obtenir l'ordre départ -> arrivée
        path.Reverse();

        // Supprime la case de départ — l'unité y est déjà, inutile de l'inclure
        if (path.Count > 0) path.RemoveAt(0);

        if (GameLogger.IsEnabled(GameLogger.Category.Movement))
            GameLogger.Log(GameLogger.Category.Movement, $"Chemin reconstitué : {path.Count} étapes", null);
        return path;
    }

    // Loue et réinitialise un nœud temporaire pour le calcul courant
    private static PathNode Rent(Vector2Int position, int seed)
    {
        PathNode node = nodePool.Count > 0 ? nodePool.Pop() : new PathNode();
        node.Reset(position, seed);
        rentedNodes.Add(node);
        return node;
    }

    // Rend tous les nœuds et vide les conteneurs réutilisables après chaque calcul
    private static void ReleaseAll()
    {
        while (openList.Count > 0)
            openList.ExtractMin();

        openDict.Clear();
        closedSet.Clear();

        foreach (PathNode node in rentedNodes)
        {
            node.parent = null;
            nodePool.Push(node);
        }

        rentedNodes.Clear();
    }

    // Vérifie la validité des paramètres avant de lancer le calcul
    // Évite les calculs inutiles sur des positions invalides ou inaccessibles
    // Utilise GridManager et UnitTypeHelper pour valider les limites et l'accessibilité
    private static bool IsValidPathRequest(Vector2Int startPosition, Vector2Int endPosition, GameObject unit)
    {
        // Vérifie que l'unité existe et a un tag valide
        if (unit == null)
        {
            GameLogger.Error("PathFinding appelé avec GameObject unité null", null);
            return false;
        }

        if (!UnitTypeHelper.IsValidUnit(unit))
        {
            if (GameLogger.IsEnabled(GameLogger.Category.Movement))
                GameLogger.Warn(GameLogger.Category.Movement, $"GameObject {unit.name} n'a pas de tag d'unité valide : {unit.tag}", null);
            return false;
        }

        // Vérifie que les positions sont dans les limites de la grille via GridSystem (calcul pur)
        Vector2Int gridSize = GridManager.Instance.GetGridSize();
        if (!GridSystem.IsInBounds(startPosition.x, startPosition.y, gridSize.x, gridSize.y) ||
            !GridSystem.IsInBounds(endPosition.x, endPosition.y, gridSize.x, gridSize.y))
        {
            return false;
        }

        // Vérifie que la destination est accessible pour cette unité
        if (!GridManager.Instance.IsWalkableFor(endPosition, unit))
        {
            if (GameLogger.IsEnabled(GameLogger.Category.Movement))
                GameLogger.Warn(GameLogger.Category.Movement, $"Destination {endPosition} inaccessible pour {UnitTypeHelper.GetDisplayName(unit)}", null);
            return false;
        }

        // Cas particulier : si départ = arrivée, considère comme valide
        if (startPosition == endPosition)
        {
            return true;
        }

        return true;
    }
}
