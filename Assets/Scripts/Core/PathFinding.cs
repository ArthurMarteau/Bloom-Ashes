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

    // Structure de données représentant une case pendant le calcul A*
    // Contient les coûts et le lien vers le parent pour reconstitution du chemin
    private class PathNode
    {
        public Vector2Int position;
        public int gCost; // Coût réel depuis le départ
        public int hCost; // Coût estimé vers la destination  
        public int fCost => gCost + hCost; // Coût total pour priorisation
        public PathNode parent; // Nœud précédent pour reconstitution

        public PathNode(Vector2Int pos)
        {
            position = pos;
            gCost = 0;
            hCost = 0;
            parent = null;
        }
    }

    // Point d'entrée unique pour calcul de chemin
    // Interface simple pour MovementController qui demande un trajet
    // Utilise GridManager et système de tags pour vérifier accessibilité des cases
    public static List<Vector2Int> FindPath(Vector2Int startPosition, Vector2Int endPosition, GameObject unit)
    {
        // Valide les paramètres avant calcul
        if (!IsValidPathRequest(startPosition, endPosition, unit))
        {
            Debug.LogWarning($"Demande de chemin invalide : {startPosition} -> {endPosition} pour {UnitTypeHelper.GetDisplayName(unit)}");
            return new List<Vector2Int>();
        }

        // Lance le calcul A* et retourne le résultat
        List<Vector2Int> calculatedPath = ExecuteAStar(startPosition, endPosition, unit);

        Debug.Log($"Chemin calculé pour {UnitTypeHelper.GetDisplayName(unit)} : {startPosition} -> {endPosition}, {calculatedPath.Count} étapes");
        return calculatedPath;
    }

    // Nouvelle surcharge : calcule chemin depuis position monde réelle
    public static List<Vector2Int> FindPath(Vector3 worldStartPosition, Vector2Int endPosition, GameObject unit)
    {
        // Convertit la position monde en case grille
        Vector2Int gridStartPosition = GridManager.Instance.WorldToGrid(worldStartPosition);

        // Calcule le chemin normalement
        List<Vector2Int> path = FindPath(gridStartPosition, endPosition, unit);

        // Si l'unité n'est pas au centre de sa case, ajoute sa case actuelle au début du chemin
        Vector3 cellCenter = GridManager.Instance.GridToWorld(gridStartPosition);
        float distanceFromCenter = Vector3.Distance(worldStartPosition, cellCenter);

        if (distanceFromCenter > 0.1f && path.Count > 0)
        {
            // L'unité doit d'abord finir d'atteindre sa case actuelle
            // Mais seulement si ce n'est pas déjà la première étape
            if (path[0] != gridStartPosition)
            {
                path.Insert(0, gridStartPosition);
            }
        }

        return path;
    }

    // Implémentation pure de l'algorithme A* sans optimisations
    // Trouve le chemin optimal en explorant les possibilités de manière intelligente
    // Utilise heuristique pour guider la recherche vers la destination
    private static List<Vector2Int> ExecuteAStar(Vector2Int startPosition, Vector2Int endPosition, GameObject unit)
    {
        // Initialise les collections de travail pour le calcul
        List<PathNode> openList = new List<PathNode>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

        // Crée le nœud de départ et l'ajoute à la liste ouverte
        PathNode startNode = CreateNode(startPosition);
        startNode.hCost = CalculateHCost(startPosition, endPosition);
        openList.Add(startNode);

        // Boucle principale A* : explore jusqu'à trouver destination ou épuiser possibilités
        while (openList.Count > 0)
        {
            // Sélectionne le nœud avec le coût total le plus bas
            PathNode currentNode = GetLowestFCostNode(openList);
            openList.Remove(currentNode);
            closedSet.Add(currentNode.position);

            // Vérifie si la destination est atteinte
            if (currentNode.position == endPosition)
            {
                Debug.Log("Destination atteinte, reconstitution du chemin");
                return ReconstructPath(currentNode);
            }

            // Explore tous les voisins accessibles du nœud actuel
            List<PathNode> neighborNodes = GetNeighbors(currentNode, unit);
            foreach (PathNode neighborNode in neighborNodes)
            {
                // Ignore les nœuds déjà traités définitivement
                if (closedSet.Contains(neighborNode.position))
                    continue;

                // Calcule le nouveau coût pour atteindre ce voisin
                int tentativeGCost = currentNode.gCost + CalculateGCost(currentNode, neighborNode.position);

                // Trouve le nœud existant dans openList ou utilise le nouveau
                PathNode existingNode = openList.Find(node => node.position == neighborNode.position);
                bool isNewNode = existingNode == null;

                if (isNewNode)
                {
                    // Nouveau nœud : configure et ajoute à openList
                    neighborNode.gCost = tentativeGCost;
                    neighborNode.hCost = CalculateHCost(neighborNode.position, endPosition);
                    neighborNode.parent = currentNode;
                    openList.Add(neighborNode);
                }
                else if (tentativeGCost < existingNode.gCost)
                {
                    // Chemin plus court trouvé : met à jour le nœud existant
                    existingNode.gCost = tentativeGCost;
                    existingNode.parent = currentNode;
                }
            }
        }

        // Aucun chemin trouvé après exploration complète
        Debug.LogWarning($"Aucun chemin trouvé entre {startPosition} et {endPosition} pour {UnitTypeHelper.GetDisplayName(unit)}");
        return new List<Vector2Int>();
    }

    // Trouve tous les voisins accessibles dans les 8 directions
    // Détermine les mouvements possibles depuis la position actuelle
    // Utilise GridManager et système de tags pour vérifier l'accessibilité selon le type d'unité
    private static List<PathNode> GetNeighbors(PathNode currentNode, GameObject unit)
    {
        List<PathNode> neighbors = new List<PathNode>();
        Vector2Int currentPosition = currentNode.position;

        // Définit les 8 directions possibles (4 cardinales + 4 diagonales)
        Vector2Int[] directions = {
            new Vector2Int(-1, 0),  // Gauche
            new Vector2Int(1, 0),   // Droite  
            new Vector2Int(0, 1),   // Haut
            new Vector2Int(0, -1),  // Bas
            new Vector2Int(-1, 1),  // Haut-Gauche
            new Vector2Int(1, 1),   // Haut-Droite
            new Vector2Int(-1, -1), // Bas-Gauche
            new Vector2Int(1, -1)   // Bas-Droite
        };

        // Vérifie chaque direction pour trouver les voisins accessibles
        foreach (Vector2Int direction in directions)
        {
            Vector2Int neighborPosition = currentPosition + direction;

            // Vérifie si la position est accessible pour cette unité via son tag
            if (GridManager.Instance.IsWalkableFor(neighborPosition, unit))
            {
                PathNode neighborNode = CreateNode(neighborPosition);
                neighbors.Add(neighborNode);
            }
        }

        return neighbors;
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
    // Ajoute le coût au gCost du nœud parent pour progression
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

        Debug.Log($"Chemin reconstitué : {path.Count} étapes");
        return path;
    }

    // Crée un nouveau nœud temporaire pour ce calcul
    // Factory simple pour instancier des nœuds pendant l'exploration
    // Chaque nœud est unique à ce calcul spécifique
    private static PathNode CreateNode(Vector2Int position)
    {
        return new PathNode(position);
    }

    // Trouve le nœud avec le coût total (fCost) le plus bas dans une liste
    // Sélectionne le prochain nœud optimal à traiter selon la stratégie A*
    // En cas d'égalité fCost, privilégie le plus proche de la destination (hCost plus bas)
    private static PathNode GetLowestFCostNode(List<PathNode> nodeList)
    {
        if (nodeList.Count == 0)
            return null;

        PathNode lowestFCostNode = nodeList[0];

        for (int i = 1; i < nodeList.Count; i++)
        {
            PathNode currentNode = nodeList[i];

            // Compare d'abord fCost, puis hCost en cas d'égalité
            if (currentNode.fCost < lowestFCostNode.fCost ||
                (currentNode.fCost == lowestFCostNode.fCost && currentNode.hCost < lowestFCostNode.hCost))
            {
                lowestFCostNode = currentNode;
            }
        }

        return lowestFCostNode;
    }

    // Vérifie la validité des paramètres avant de lancer le calcul
    // Évite les calculs inutiles sur des positions invalides ou inaccessibles
    // Utilise GridManager et UnitTypeHelper pour valider les limites et l'accessibilité
    private static bool IsValidPathRequest(Vector2Int startPosition, Vector2Int endPosition, GameObject unit)
    {
        // Vérifie que l'unité existe et a un tag valide
        if (unit == null)
        {
            Debug.LogError("PathFinding appelé avec GameObject unité null");
            return false;
        }

        if (!UnitTypeHelper.IsValidUnit(unit))
        {
            Debug.LogWarning($"GameObject {unit.name} n'a pas de tag d'unité valide : {unit.tag}");
            return false;
        }

        // Vérifie que les positions sont dans les limites de la grille
        if (!GridManager.Instance.IsInBounds(startPosition) || !GridManager.Instance.IsInBounds(endPosition))
        {
            return false;
        }

        // Vérifie que la destination est accessible pour cette unité
        if (!GridManager.Instance.IsWalkableFor(endPosition, unit))
        {
            Debug.LogWarning($"Destination {endPosition} inaccessible pour {UnitTypeHelper.GetDisplayName(unit)}");
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