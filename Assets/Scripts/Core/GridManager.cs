using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;
    private float cellSize = 1f;
    private Vector3 gridOrigin;

    [Header("Layer Settings")]
    [SerializeField] private LayerMask unitLayer = 1 << 8;      // Layer pour d�tecter les unit�s
    [SerializeField] private LayerMask obstacleLayer = 1 << 6;  // Layer pour d�tecter les obstacles

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
            Debug.Log($"Grille cr��e : {gridWidth}x{gridHeight}");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Recalcule l'origine de la grille � chaque modification dans l'inspecteur (initialisation et �dition)
    private void OnValidate()
    {
        CalculateGridOrigin();

        // R�initialise la grille de terrain si les dimensions changent
        if (groundTypeGrid == null || groundTypeGrid.GetLength(0) != gridWidth || groundTypeGrid.GetLength(1) != gridHeight)
        {
            InitializeGroundTypeGrid();
        }
    }

    // Initialise la grille de terrain avec des valeurs par d�faut
    // Cr�e un tableau 2D avec du terrain normal partout au d�part
    // Modifiable via l'inspecteur ou m�thodes publiques
    private void InitializeGroundTypeGrid()
    {
        groundTypeGrid = new GroundType[gridWidth, gridHeight];

        // Remplit avec du terrain normal par d�faut
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                groundTypeGrid[x, y] = GroundType.Ground;
            }
        }

        Debug.Log("Grille de terrain initialis�e avec terrain normal");
    }

    // Recalcule l'origine de la grille pour centrer la grille
    private void CalculateGridOrigin()
    {
        float originX = -gridWidth * cellSize * 0.5f;
        float originY = -gridHeight * cellSize * 0.5f;
        gridOrigin = new Vector3(originX, originY, 0);
    }

    // Convertit une position Unity en coordonn�es de grille
    // Utilis� pour savoir dans quelle case se trouve un objet (unit�, clic souris, b�timent)
    // Permet de passer du syst�me de coordonn�es Unity au syst�me de cases du jeu
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        Vector3 localPosition = worldPosition - gridOrigin;
        int x = Mathf.FloorToInt(localPosition.x / cellSize);
        int y = Mathf.FloorToInt(localPosition.y / cellSize);
        return new Vector2Int(x, y);
    }

    // Convertit des coordonn�es de grille en position Unity centr�e sur la case
    // Utilis� pour placer visuellement les objets au centre exact d'une case
    // Permet de passer du syst�me de cases du jeu au syst�me de coordonn�es Unity
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        float x = gridPosition.x * cellSize + cellSize * 0.5f;
        float y = gridPosition.y * cellSize + cellSize * 0.5f;
        return gridOrigin + new Vector3(x, y, 0);
    }

    // V�rifie si les coordonn�es sont dans les limites de la grille
    // �vite les crashes et comportements bizarres en validant chaque position avant utilisation
    // (mouvements d'unit�s, clics joueur, calculs de pathfinding, placement d'objets)
    public bool IsInBounds(Vector2Int coordinates)
    {
        return coordinates.x >= 0 && coordinates.x < gridWidth &&
               coordinates.y >= 0 && coordinates.y < gridHeight;
    }

    // M�thode principale pour PathFinding - d�termine si une case est accessible pour une unit�
    // Combine les r�gles d'occupation (unit�s), obstacles et terrain selon les capacit�s de l'unit�
    // Interface unique pour toute la logique d'accessibilit� via syst�me de tags
    public bool IsWalkableFor(Vector2Int gridPosition, GameObject unit)
    {
        // V�rifie d'abord les limites de la grille
        if (!IsInBounds(gridPosition))
        {
            return false;
        }

        // R�gle absolue : aucune unit� ne peut occuper la m�me case qu'une autre
        // EXCEPTION : ignore l'unit� qui demande le mouvement (�vite auto-blocage)
        GameObject unitAtPosition = GetUnitAt(gridPosition);
        if (unitAtPosition != null && unitAtPosition != unit)
        {
            return false; // Case occup�e par une AUTRE unit�
        }

        // R�gle obstacles : bloquent selon les capacit�s de vol de l'unit�
        if (HasObstacleAt(gridPosition))
        {
            if (!UnitTypeHelper.CanFlyOverObstacles(unit))
            {
                return false;
            }
        }

        // R�gle terrain : accessible selon les capacit�s de d�placement de l'unit�
        GroundType terrainType = GetGroundTypeAt(gridPosition);
        if (!UnitTypeHelper.CanWalkOnGround(unit, terrainType))
        {
            return false;
        }

        // Case libre et accessible
        return true;
    }

    // Retourne le type de terrain d'une position de grille
    // Consulte le tableau statique configur� au d�marrage
    // Utilis� par IsWalkableFor pour valider compatibilit� terrain/unit�
    public GroundType GetGroundTypeAt(Vector2Int gridPosition)
    {
        if (!IsInBounds(gridPosition))
        {
            return GroundType.Ground; // Valeur par d�faut pour positions invalides
        }

        return groundTypeGrid[gridPosition.x, gridPosition.y];
    }

    // D�finit le type de terrain d'une position de grille
    // Permet de configurer la carte pendant le d�veloppement
    // Utilis� pour cr�er des zones sp�cialis�es (lacs, montagnes)
    public void SetGroundTypeAt(Vector2Int gridPosition, GroundType groundType)
    {
        if (!IsInBounds(gridPosition))
        {
            Debug.LogWarning($"Tentative de d�finir terrain hors limites : {gridPosition}");
            return;
        }

        groundTypeGrid[gridPosition.x, gridPosition.y] = groundType;
        Debug.Log($"Terrain {gridPosition} d�fini comme {groundType.GetDisplayName()}");
    }

    // D�tecte la pr�sence d'une unit� sur une position de grille
    // Convertit position grille en monde puis effectue raycast sur layer Units
    // Retourne true si une unit� occupe cette case
    public bool HasUnitAt(Vector2Int gridPosition)
    {
        Vector3 worldPosition = GridToWorld(gridPosition);
        Collider2D unitCollider = Physics2D.OverlapPoint(worldPosition, unitLayer);
        return unitCollider != null;
    }

    // D�tecte la pr�sence d'un obstacle sur une position de grille
    // Convertit position grille en monde puis effectue raycast sur layer Obstacles
    // Retourne true si un obstacle occupe cette case
    public bool HasObstacleAt(Vector2Int gridPosition)
    {
        Vector3 worldPosition = GridToWorld(gridPosition);
        Collider2D obstacleCollider = Physics2D.OverlapPoint(worldPosition, obstacleLayer);
        return obstacleCollider != null;
    }

    // Retourne l'unit� pr�sente sur une position de grille (null si aucune)
    // Utilis� pour identifier quelle unit� occupe une case sp�cifique
    // Utile pour interactions, s�lection et gestion de collisions
    public GameObject GetUnitAt(Vector2Int gridPosition)
    {
        Vector3 worldPosition = GridToWorld(gridPosition);
        Collider2D unitCollider = Physics2D.OverlapPoint(worldPosition, unitLayer);
        return unitCollider?.gameObject;
    }

    // Retourne l'obstacle pr�sent sur une position de grille (null si aucun)
    // Utilis� pour identifier quel obstacle occupe une case sp�cifique
    // Utile pour interactions, destruction et gestion d'effets
    public GameObject GetObstacleAt(Vector2Int gridPosition)
    {
        Vector3 worldPosition = GridToWorld(gridPosition);
        Collider2D obstacleCollider = Physics2D.OverlapPoint(worldPosition, obstacleLayer);
        return obstacleCollider?.gameObject;
    }

    // Getter pour la taille de cellule
    // Permet aux autres scripts de conna�tre la taille d'une case pour leurs calculs
    // (port�es d'attaque, formation d'unit�s, distances, interface utilisateur)
    public float GetCellSize()
    {
        return cellSize;
    }

    // Getter pour les dimensions de la grille
    // Permet aux autres scripts de conna�tre les limites de la carte
    // (g�n�ration d'objets, contraintes de cam�ra, logique d'IA, affichage interface)
    public Vector2Int GetGridSize()
    {
        return new Vector2Int(gridWidth, gridHeight);
    }

    // Affiche la grille dans l'�diteur et en jeu avec coloration selon le terrain
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

        // Dessine le contour de la grille en plus �pais
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

    // M�thode de test pour v�rifier les conversions
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

    // M�thode de test pour la logique de PathFinding avec syst�me de tags
    [ContextMenu("Test PathFinding Logic")]
    private void TestPathFindingLogic()
    {
        Vector2Int testPosition = new Vector2Int(2, 2);

        Debug.Log($"=== Test PathFinding Logic sur {testPosition} ===");
        Debug.Log($"Position dans limites : {IsInBounds(testPosition)}");
        Debug.Log($"Unit� pr�sente : {HasUnitAt(testPosition)}");
        Debug.Log($"Obstacle pr�sent : {HasObstacleAt(testPosition)}");
        Debug.Log($"Type terrain : {GetGroundTypeAt(testPosition).GetDisplayName()}");

        // Test avec des GameObjects fictifs (n�cessite vraies unit�s dans la sc�ne)
        GameObject unitAtPosition = GetUnitAt(testPosition);
        if (unitAtPosition != null)
        {
            Debug.Log($"Unit� d�tect�e : {unitAtPosition.name} ({UnitTypeHelper.GetDisplayName(unitAtPosition)})");
            Debug.Log($"Terrains accessibles : {UnitTypeHelper.GetAccessibleTerrains(unitAtPosition)}");
        }
        else
        {
            Debug.Log("Aucune unit� d�tect�e sur cette position");
        }
    }

    // M�thode utilitaire pour cr�er des zones de terrain rapidement
    [ContextMenu("Create Test Terrain")]
    private void CreateTestTerrain()
    {
        if (groundTypeGrid == null)
        {
            InitializeGroundTypeGrid();
        }

        // Cr�e une zone d'eau (coins bas-gauche)
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                SetGroundTypeAt(new Vector2Int(x, y), GroundType.Water);
            }
        }

        // Cr�e une zone de montagne (coins haut-droite)
        for (int x = gridWidth - 3; x < gridWidth; x++)
        {
            for (int y = gridHeight - 3; y < gridHeight; y++)
            {
                SetGroundTypeAt(new Vector2Int(x, y), GroundType.Mountain);
            }
        }

        Debug.Log("Terrain de test cr�� : Eau en bas-gauche, Montagne en haut-droite");
    }
}