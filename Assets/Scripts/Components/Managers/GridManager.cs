using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;
    private float cellSize = 1f;
    private Vector3 gridOrigin;

    [Header("Layer Settings")]
    [SerializeField] private LayerMask unitLayer = 1 << 9;      // Layer pour détecter les unités
    [SerializeField] private LayerMask obstacleLayer = 1 << 6;  // Layer pour détecter les obstacles
    [SerializeField] private LayerMask buildingLayer = 1 << 8;           // Layer "Buildings" — bâtiments de faction qui bloquent le passage
                                                                // Le Nexus reste sur Environment_Walkable et N'EST PAS dans ce mask

    [Header("Ground Type Configuration")]
    [SerializeField] private GroundType[,] groundTypeGrid;      // Configuration terrain statique

    // Instance singleton
    public static GridManager Instance { get; private set; }

    private void Awake()
    {
        // Initialise l'origine de la grille
        CalculateGridOrigin();

        // Configure le singleton
        if (Instance == null)
        {
            Instance = this;
            InitializeGroundTypeGrid();
            Debug.Log($"Grille créée : {gridWidth}x{gridHeight}");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Nettoie la référence singleton pour éviter les références fantômes entre sessions Play Mode
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Recalcule l'origine de la grille à chaque modification dans l'inspecteur (initialisation et édition)
    private void OnValidate()
    {
        CalculateGridOrigin();

        // Réinitialise la grille de terrain si les dimensions changent
        if (groundTypeGrid == null || groundTypeGrid.GetLength(0) != gridWidth || groundTypeGrid.GetLength(1) != gridHeight)
        {
            InitializeGroundTypeGrid();
        }
    }

    // Initialise la grille de terrain avec des valeurs par défaut
    // Crée un tableau 2D avec du terrain normal partout au départ
    // Modifiable via l'inspecteur ou méthodes publiques
    private void InitializeGroundTypeGrid()
    {
        groundTypeGrid = new GroundType[gridWidth, gridHeight];

        // Remplit avec du terrain normal par défaut
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                groundTypeGrid[x, y] = GroundType.Grass;
            }
        }

        Debug.Log("Grille de terrain initialisée avec terrain normal");
    }

    // Recalcule l'origine de la grille pour centrer la grille sur le monde
    // Délègue le calcul à GridSystem — GridManager ne stocke que le résultat en Vector3
    private void CalculateGridOrigin()
    {
        var (originX, originY) = GridSystem.CalculateOrigin(gridWidth, gridHeight, cellSize);
        gridOrigin = new Vector3(originX, originY, 0);
    }

    // Convertit une position Unity en coordonnées de grille
    // Pont Unity → GridSystem : extrait les floats de Vector3, délègue le calcul pur
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        return GridSystem.WorldToGrid(worldPosition.x, worldPosition.y, cellSize, gridOrigin.x, gridOrigin.y);
    }

    // Convertit des coordonnées de grille en position Unity centrée sur la case
    // Pont GridSystem → Unity : reçoit le tuple (float, float) et construit le Vector3
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        var (x, y) = GridSystem.GridToWorld(gridPosition.x, gridPosition.y, cellSize, gridOrigin.x, gridOrigin.y);
        return new Vector3(x, y, 0);
    }

    // Vérifie si les coordonnées sont dans les limites de la grille
    // Pont Vector2Int → GridSystem : délègue la vérification entière pure
    public bool IsInBounds(Vector2Int coordinates)
    {
        return GridSystem.IsInBounds(coordinates.x, coordinates.y, gridWidth, gridHeight);
    }

    // Méthode principale pour PathFinding - détermine si une case est accessible pour une unité
    // Combine les règles d'occupation (unités), obstacles et terrain selon les capacités de l'unité
    // Interface unique pour toute la logique d'accessibilité via système de tags
    // Utilise la position logique des unités pour détection fiable pendant Lerp
    public bool IsWalkableFor(Vector2Int gridPosition, GameObject unit)
    {
        // Vérifie d'abord les limites de la grille
        if (!IsInBounds(gridPosition))
        {
            return false;
        }

        // Règle absolue : aucune unité ne peut occuper la même case qu'une autre
        // EXCEPTION : ignore l'unité qui demande le mouvement (évite auto-blocage)
        // IMPORTANT : Utilise GetUnitAtLogical au lieu de GetUnitAt pour détection fiable pendant Lerp
        GameObject unitAtPosition = GetUnitAt(gridPosition);
        if (unitAtPosition != null && unitAtPosition != unit)
        {
            return false; // Case occupée par une AUTRE unité
        }

        // Règle obstacles : bloquent selon les capacités de vol de l'unité
        if (GetObstacleAt(gridPosition))
        {
            if (!UnitTypeHelper.CanFlyOverObstacles(unit))
            {
                return false;
            }
        }

        // Règle bâtiments de faction : bloquent le passage sauf pour les unités volantes
        // Le Nexus (layer Environment_Walkable) n'est PAS détecté ici → les unités passent dessus
        if (GetBuildingAt(gridPosition))
        {
            if (!UnitTypeHelper.CanFlyOverObstacles(unit))
            {
                return false;
            }
        }

        // Règle terrain : accessible selon les capacités de déplacement de l'unité
        GroundType terrainType = GetGroundTypeAt(gridPosition);
        if (!UnitTypeHelper.CanWalkOnGround(unit, terrainType))
        {
            return false;
        }

        // Case libre et accessible
        return true;
    }

    // Retourne le type de terrain d'une position de grille
    // Consulte le tableau statique configuré au démarrage
    // Utilisé par IsWalkableFor pour valider compatibilité terrain/unité
    public GroundType GetGroundTypeAt(Vector2Int gridPosition)
    {
        if (!IsInBounds(gridPosition))
        {
            return GroundType.Grass; // Valeur par défaut pour positions invalides
        }

        return groundTypeGrid[gridPosition.x, gridPosition.y];
    }

    // Définit le type de terrain d'une position de grille
    // Permet de configurer la carte pendant le développement
    // Utilisé pour créer des zones spécialisées (lacs, montagnes)
    public void SetGroundTypeAt(Vector2Int gridPosition, GroundType groundType)
    {
        if (!IsInBounds(gridPosition))
        {
            Debug.LogWarning($"Tentative de définir terrain hors limites : {gridPosition}");
            return;
        }

        groundTypeGrid[gridPosition.x, gridPosition.y] = groundType;
        Debug.Log($"Terrain {gridPosition} défini comme {groundType.GetDisplayName()}");
    }

    // Retourne l'unité présente sur une position de grille via Collider2D (null si aucune)
    // ATTENTION : Ne détecte que les unités dont transform.position est au centre de la case
    // Peut échouer si l'unité est en plein Lerp entre deux cases
    // Pour PathFinding, utiliser GetUnitAtLogical() à la place
    public GameObject GetUnitAt(Vector2Int gridPosition)
    {
        Vector3 worldPosition = GridToWorld(gridPosition);
        Collider2D unitCollider = Physics2D.OverlapPoint(worldPosition, unitLayer);
        return unitCollider?.gameObject;
    }

    // Retourne l'obstacle présent sur une position de grille (null si aucun)
    // Utilisé pour identifier quel obstacle occupe une case spécifique
    // Utile pour interactions, destruction et gestion d'effets
    public GameObject GetObstacleAt(Vector2Int gridPosition)
    {
        Vector3 worldPosition = GridToWorld(gridPosition);
        Collider2D obstacleCollider = Physics2D.OverlapPoint(worldPosition, obstacleLayer);
        return obstacleCollider?.gameObject;
    }

    // Retourne le bâtiment de faction présent sur une position de grille (null si aucun)
    // Utilise le layer Buildings — le Nexus (Environment_Walkable) n'est PAS détecté ici
    // Appelé par IsWalkableFor() pour bloquer le passage à travers les bâtiments de faction
    public GameObject GetBuildingAt(Vector2Int gridPosition)
    {
        // Aucun layer configuré → pas de bâtiments à détecter
        if (buildingLayer.value == 0)
        {
            return null;
        }

        Vector3 worldPosition = GridToWorld(gridPosition);
        Collider2D buildingCollider = Physics2D.OverlapPoint(worldPosition, buildingLayer);
        return buildingCollider?.gameObject;
    }

    // Getter pour les dimensions de la grille
    // Permet aux autres scripts de connaître les limites de la carte
    // (génération d'objets, contraintes de caméra, logique d'IA, affichage interface)
    public Vector2Int GetGridSize()
    {
        return new Vector2Int(gridWidth, gridHeight);
    }

    public float GetCellSize()
    {
        return cellSize;
    }

    // Affiche la grille dans l'éditeur et en jeu avec coloration selon le terrain
    private void OnDrawGizmos()
    {
        // Dessine les lignes de grille par-dessus
        Gizmos.color = Color.gray;

        // Dessine les lignes verticales
        for (int x = 0; x <= gridWidth; x++)
        {
            Vector3 start = gridOrigin + new Vector3(x * cellSize, 0, 0);
            Vector3 end = gridOrigin + new Vector3(x * cellSize, gridHeight * cellSize, 0);
            Gizmos.DrawLine(start, end);
        }

        // Dessine les lignes horizontales
        for (int y = 0; y <= gridHeight; y++)
        {
            Vector3 start = gridOrigin + new Vector3(0, y * cellSize, 0);
            Vector3 end = gridOrigin + new Vector3(gridWidth * cellSize, y * cellSize, 0);
            Gizmos.DrawLine(start, end);
        }

        // Dessine le contour de la grille en plus épais
        Gizmos.color = Color.white;
        Vector3 bottomLeft = gridOrigin;
        Vector3 bottomRight = gridOrigin + new Vector3(gridWidth * cellSize, 0, 0);
        Vector3 topRight = gridOrigin + new Vector3(gridWidth * cellSize, gridHeight * cellSize, 0);
        Vector3 topLeft = gridOrigin + new Vector3(0, gridHeight * cellSize, 0);

        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }

    // Méthode de test pour vérifier les conversions
    [ContextMenu("Test Grid Conversions")]
    private void TestGridConversions()
    {
        // Test quelques conversions
        Vector2Int gridPos = new Vector2Int(2, 3);
        Vector3 worldPos = GridToWorld(gridPos);
        Vector2Int backToGrid = WorldToGrid(worldPos);

        Debug.Log($"Test conversion :");
        Debug.Log($"Grille {gridPos} -> Monde {worldPos} -> Grille {backToGrid}");

        // Test les limites
        Debug.Log($"(0,0) dans les limites : {IsInBounds(new Vector2Int(0, 0))}");
        Debug.Log($"({gridWidth - 1},{gridHeight - 1}) dans les limites : {IsInBounds(new Vector2Int(gridWidth - 1, gridHeight - 1))}");
        Debug.Log($"({gridWidth},{gridHeight}) dans les limites : {IsInBounds(new Vector2Int(gridWidth, gridHeight))}");
    }

    // Méthode de test pour la logique de PathFinding avec système de tags
    [ContextMenu("Test PathFinding Logic")]
    private void TestPathFindingLogic()
    {
        Vector2Int testPosition = new Vector2Int(2, 2);

        Debug.Log($"=== Test PathFinding Logic sur {testPosition} ===");
        Debug.Log($"Position dans limites : {IsInBounds(testPosition)}");
        Debug.Log($"Unité présente (Collider) : {GetUnitAt(testPosition)}");
        Debug.Log($"Obstacle présent : {GetObstacleAt(testPosition)}");
        Debug.Log($"Type terrain : {GetGroundTypeAt(testPosition).GetDisplayName()}");

        // Test avec des GameObjects fictifs (nécessite vraies unités dans la scène)
        GameObject unitAtPosition = GetUnitAt(testPosition);
        if (unitAtPosition != null)
        {
            Debug.Log($"Unité détectée : {unitAtPosition.name} ({UnitTypeHelper.GetDisplayName(unitAtPosition)})");
            Debug.Log($"Terrains accessibles : {UnitTypeHelper.GetAccessibleTerrains(unitAtPosition)}");
        }
        else
        {
            Debug.Log("Aucune unité détectée sur cette position");
        }
    }

    // Méthode utilitaire pour créer des zones de terrain rapidement
    [ContextMenu("Create Test Terrain")]
    private void CreateTestTerrain()
    {
        if (groundTypeGrid == null)
        {
            InitializeGroundTypeGrid();
        }

        // Crée une zone d'eau (coins bas-gauche)
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                SetGroundTypeAt(new Vector2Int(x, y), GroundType.Water);
            }
        }

        // Crée une zone de montagne (coins haut-droite)
        for (int x = gridWidth - 3; x < gridWidth; x++)
        {
            for (int y = gridHeight - 3; y < gridHeight; y++)
            {
                SetGroundTypeAt(new Vector2Int(x, y), GroundType.Mountain);
            }
        }

        // Crée une zone de ronces (centre gauche)
        for (int x = 1; x < 4; x++)
        {
            SetGroundTypeAt(new Vector2Int(x, gridHeight / 2), GroundType.Brambles);
        }

        // Crée un chemin diagonal (centre)
        for (int i = 0; i < 4; i++)
        {
            SetGroundTypeAt(new Vector2Int(gridWidth / 2 + i, gridHeight / 2 + i), GroundType.Path);
        }

        Debug.Log("🗺️ Terrain de test créé : Eau bas-gauche, Montagne haut-droite, Ronces centre, Chemin diagonal");
    }
}
