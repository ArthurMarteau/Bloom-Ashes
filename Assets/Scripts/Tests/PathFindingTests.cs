using System.Collections.Generic;
using UnityEngine;

// Script de test pour valider le fonctionnement du système PathFinding avec Tags
// Permet de tester différents scénarios depuis l'inspecteur Unity
// Fournit des méthodes de débogage visuelles pour voir les chemins calculés
public class PathFindingTester : MonoBehaviour
{
    [Header("Test Parameters")]
    [SerializeField] private Vector2Int startPosition = new Vector2Int(0, 0);
    [SerializeField] private Vector2Int endPosition = new Vector2Int(5, 5);

    [Header("Test Units - Assign GameObjects with Unit Tags")]
    [SerializeField] private GameObject testGroundUnit;    // GameObject avec tag "GroundUnit"
    [SerializeField] private GameObject testWaterUnit;     // GameObject avec tag "WaterUnit"
    [SerializeField] private GameObject testMountainUnit;  // GameObject avec tag "MountainUnit"
    [SerializeField] private GameObject testAirUnit;       // GameObject avec tag "AirUnit"

    [Header("Current Test Unit")]
    [SerializeField] private GameObject currentTestUnit;   // Unité actuellement testée

    [Header("Visual Debug")]
    [SerializeField] private bool showCalculatedPath = true;
    [SerializeField] private Color pathColor = Color.yellow;
    [SerializeField] private Color startPointColor = Color.green;
    [SerializeField] private Color endPointColor = Color.red;

    // Stocke le dernier chemin calculé pour l'affichage visuel
    private List<Vector2Int> lastCalculatedPath = new List<Vector2Int>();

    // Test simple de calcul de chemin depuis l'inspecteur
    // Valide le fonctionnement de base de PathFinding.FindPath() avec l'unité actuelle
    // Affiche les résultats dans la console Unity
    [ContextMenu("Test Simple Path")]
    public void TestSimplePath()
    {
        if (currentTestUnit == null)
        {
            Debug.LogError("❌ Aucune unité de test assignée ! Assigne currentTestUnit dans l'inspecteur.");
            return;
        }

        Debug.Log($"=== Test PathFinding Simple ===");
        Debug.Log($"Départ: {startPosition}, Arrivée: {endPosition}");
        Debug.Log($"Unité: {currentTestUnit.name} ({UnitTypeHelper.GetDisplayName(currentTestUnit)})");

        // Calcule le chemin avec PathFinding
        List<Vector2Int> calculatedPath = PathFinding.FindPath(startPosition, endPosition, currentTestUnit);

        // Stocke pour affichage visuel
        lastCalculatedPath = calculatedPath;

        // Affiche les résultats
        if (calculatedPath.Count > 0)
        {
            Debug.Log($"✅ Chemin trouvé ! {calculatedPath.Count} étapes:");
            for (int i = 0; i < calculatedPath.Count; i++)
            {
                Vector2Int position = calculatedPath[i];
                GroundType terrain = GridManager.Instance.GetGroundTypeAt(position);
                Debug.Log($"  Étape {i}: {position} (Terrain: {terrain.GetDisplayName()})");
            }
        }
        else
        {
            Debug.LogWarning("❌ Aucun chemin trouvé !");
        }
    }

    // Test de validation des positions avant calcul
    // Vérifie que PathFinding gère correctement les cas invalides
    // Teste les limites de grille et positions inaccessibles
    [ContextMenu("Test Invalid Positions")]
    public void TestInvalidPositions()
    {
        if (currentTestUnit == null)
        {
            Debug.LogError("❌ Aucune unité de test assignée ! Assigne currentTestUnit dans l'inspecteur.");
            return;
        }

        Debug.Log($"=== Test Positions Invalides ===");
        Debug.Log($"Unité testée: {UnitTypeHelper.GetDisplayName(currentTestUnit)}");

        // Test position hors limites
        Vector2Int outOfBounds = new Vector2Int(-1, -1);
        List<Vector2Int> pathOutOfBounds = PathFinding.FindPath(startPosition, outOfBounds, currentTestUnit);
        Debug.Log($"Chemin vers position hors limites {outOfBounds}: {pathOutOfBounds.Count} étapes");

        // Test départ = arrivée
        List<Vector2Int> pathSamePosition = PathFinding.FindPath(startPosition, startPosition, currentTestUnit);
        Debug.Log($"Chemin départ = arrivée {startPosition}: {pathSamePosition.Count} étapes");

        // Test position potentiellement bloquée
        Vector2Int potentiallyBlocked = new Vector2Int(2, 2);
        bool isWalkable = GridManager.Instance.IsWalkableFor(potentiallyBlocked, currentTestUnit);
        Debug.Log($"Position {potentiallyBlocked} accessible: {isWalkable}");

        if (!isWalkable)
        {
            GroundType terrainType = GridManager.Instance.GetGroundTypeAt(potentiallyBlocked);
            bool hasObstacle = GridManager.Instance.HasObstacleAt(potentiallyBlocked);
            bool hasUnit = GridManager.Instance.HasUnitAt(potentiallyBlocked);

            Debug.Log($"  Raison blocage - Terrain: {terrainType.GetDisplayName()}, Obstacle: {hasObstacle}, Unité: {hasUnit}");

            List<Vector2Int> pathToBlocked = PathFinding.FindPath(startPosition, potentiallyBlocked, currentTestUnit);
            Debug.Log($"Chemin vers position bloquée: {pathToBlocked.Count} étapes");
        }
    }

    // Test comparatif entre tous les types d'unités disponibles
    // Valide que les unités ont des chemins différents selon leurs capacités
    // Utile pour vérifier la logique des tags et terrains
    [ContextMenu("Test All Unit Types")]
    public void TestAllUnitTypes()
    {
        Debug.Log($"=== Test Tous Types d'Unités ===");
        Debug.Log($"Trajet: {startPosition} -> {endPosition}");

        GameObject[] testUnits = { testGroundUnit, testWaterUnit, testMountainUnit, testAirUnit };
        string[] unitNames = { "Ground", "Water", "Mountain", "Air" };

        for (int i = 0; i < testUnits.Length; i++)
        {
            GameObject testUnit = testUnits[i];
            string unitName = unitNames[i];

            if (testUnit == null)
            {
                Debug.LogWarning($"⚠️ {unitName}Unit non assignée dans l'inspecteur");
                continue;
            }

            if (!UnitTypeHelper.IsValidUnit(testUnit))
            {
                Debug.LogError($"❌ {testUnit.name} n'a pas un tag d'unité valide: {testUnit.tag}");
                continue;
            }

            List<Vector2Int> calculatedPath = PathFinding.FindPath(startPosition, endPosition, testUnit);
            Debug.Log($"{UnitTypeHelper.GetDisplayName(testUnit)}: {calculatedPath.Count} étapes");

            if (calculatedPath.Count > 0)
            {
                string pathString = "";
                for (int j = 0; j < Mathf.Min(calculatedPath.Count, 5); j++) // Limite à 5 étapes pour lisibilité
                {
                    pathString += $"{calculatedPath[j]} ";
                }
                if (calculatedPath.Count > 5) pathString += "...";
                Debug.Log($"  Chemin: {pathString}");
            }
        }
    }

    // Test de performance avec calculs multiples
    // Mesure le temps d'exécution pour détecter des problèmes de performance
    // Simule des calculs intensifs comme dans un vrai RTS
    [ContextMenu("Test Performance")]
    public void TestPerformance()
    {
        if (currentTestUnit == null)
        {
            Debug.LogError("❌ Aucune unité de test assignée ! Assigne currentTestUnit dans l'inspecteur.");
            return;
        }

        Debug.Log($"=== Test Performance PathFinding ===");
        Debug.Log($"Unité testée: {UnitTypeHelper.GetDisplayName(currentTestUnit)}");

        int numberOfTests = 100;
        float startTime = Time.realtimeSinceStartup;

        for (int i = 0; i < numberOfTests; i++)
        {
            // Génère des positions aléatoires pour tests variés
            Vector2Int randomStart = new Vector2Int(
                Random.Range(0, GridManager.Instance.GetGridSize().x),
                Random.Range(0, GridManager.Instance.GetGridSize().y)
            );
            Vector2Int randomEnd = new Vector2Int(
                Random.Range(0, GridManager.Instance.GetGridSize().x),
                Random.Range(0, GridManager.Instance.GetGridSize().y)
            );

            // Calcule le chemin (ignore le résultat, mesure juste le temps)
            PathFinding.FindPath(randomStart, randomEnd, currentTestUnit);
        }

        float endTime = Time.realtimeSinceStartup;
        float totalTime = endTime - startTime;
        float averageTime = totalTime / numberOfTests;

        Debug.Log($"Performance: {numberOfTests} calculs en {totalTime:F3}s");
        Debug.Log($"Temps moyen par calcul: {averageTime:F5}s ({averageTime * 1000:F2}ms)");
    }

    // Test d'accessibilité terrain pour l'unité actuelle
    // Analyse quels terrains sont accessibles selon le type d'unité
    // Affiche un rapport détaillé de compatibilité terrain/unité
    [ContextMenu("Test Terrain Accessibility")]
    public void TestTerrainAccessibility()
    {
        if (currentTestUnit == null)
        {
            Debug.LogError("❌ Aucune unité de test assignée ! Assigne currentTestUnit dans l'inspecteur.");
            return;
        }

        Debug.Log($"=== Test Accessibilité Terrain ===");
        Debug.Log($"Unité: {UnitTypeHelper.GetDisplayName(currentTestUnit)} (Tag: {currentTestUnit.tag})");
        Debug.Log($"Capacités: {UnitTypeHelper.GetAccessibleTerrains(currentTestUnit)}");

        // Test chaque type de terrain
        GroundType[] terrainTypes = { GroundType.Ground, GroundType.Water, GroundType.Mountain };

        foreach (GroundType terrainType in terrainTypes)
        {
            bool canWalk = UnitTypeHelper.CanWalkOnGround(currentTestUnit, terrainType);
            string result = canWalk ? "✅ Accessible" : "❌ Bloqué";
            Debug.Log($"  {terrainType.GetDisplayName()}: {result}");
        }

        // Test obstacles
        bool canFly = UnitTypeHelper.CanFlyOverObstacles(currentTestUnit);
        string obstacleResult = canFly ? "✅ Traverse" : "❌ Bloqué";
        Debug.Log($"  Obstacles: {obstacleResult}");
    }

    // Test d'obstacles dynamiques
    // Vérifie que PathFinding détecte correctement les changements d'environnement
    // Teste la détection temps réel des unités et obstacles
    [ContextMenu("Test Dynamic Obstacles")]
    public void TestDynamicObstacles()
    {
        if (currentTestUnit == null)
        {
            Debug.LogError("❌ Aucune unité de test assignée ! Assigne currentTestUnit dans l'inspecteur.");
            return;
        }

        Debug.Log($"=== Test Obstacles Dynamiques ===");
        Debug.Log($"Unité analysée: {UnitTypeHelper.GetDisplayName(currentTestUnit)}");

        // Analyse l'état actuel de la grille
        Vector2Int gridSize = GridManager.Instance.GetGridSize();
        int totalCells = gridSize.x * gridSize.y;
        int blockedCells = 0;
        int unitCells = 0;
        int obstacleCells = 0;
        int terrainBlockedCells = 0;

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Vector2Int position = new Vector2Int(x, y);

                if (GridManager.Instance.HasUnitAt(position))
                {
                    unitCells++;
                    blockedCells++;
                }
                else if (GridManager.Instance.HasObstacleAt(position))
                {
                    obstacleCells++;
                    // Les obstacles bloquent seulement les unités non-volantes
                    if (!UnitTypeHelper.CanFlyOverObstacles(currentTestUnit))
                    {
                        blockedCells++;
                    }
                }
                else
                {
                    // Vérifie accessibilité terrain
                    GroundType terrain = GridManager.Instance.GetGroundTypeAt(position);
                    if (!UnitTypeHelper.CanWalkOnGround(currentTestUnit, terrain))
                    {
                        terrainBlockedCells++;
                        blockedCells++;
                    }
                }
            }
        }

        Debug.Log($"Analyse grille {gridSize}:");
        Debug.Log($"  Total cases: {totalCells}");
        Debug.Log($"  Cases avec unités: {unitCells}");
        Debug.Log($"  Cases avec obstacles: {obstacleCells}");
        Debug.Log($"  Cases terrain inaccessible: {terrainBlockedCells}");
        Debug.Log($"  Cases bloquées pour {UnitTypeHelper.GetDisplayName(currentTestUnit)}: {blockedCells}");
        Debug.Log($"  Cases libres: {totalCells - blockedCells}");

        float occupationRate = (float)blockedCells / totalCells * 100f;
        Debug.Log($"  Taux de blocage: {occupationRate:F1}%");
    }

    // Méthode utilitaire pour assigner rapidement l'unité de test courante
    [ContextMenu("Set Current Unit - Ground")]
    public void SetCurrentUnitGround() { currentTestUnit = testGroundUnit; }

    [ContextMenu("Set Current Unit - Water")]
    public void SetCurrentUnitWater() { currentTestUnit = testWaterUnit; }

    [ContextMenu("Set Current Unit - Mountain")]
    public void SetCurrentUnitMountain() { currentTestUnit = testMountainUnit; }

    [ContextMenu("Set Current Unit - Air")]
    public void SetCurrentUnitAir() { currentTestUnit = testAirUnit; }

    // Affichage visuel des chemins calculés dans la vue Scene
    // Dessine le dernier chemin calculé avec des lignes colorées
    // Aide au débogage visuel des algorithmes de pathfinding
    private void OnDrawGizmos()
    {
        if (!showCalculatedPath || lastCalculatedPath.Count == 0 || GridManager.Instance == null)
            return;

        // Dessine le point de départ
        Gizmos.color = startPointColor;
        Vector3 startWorld = GridManager.Instance.GridToWorld(startPosition);
        Gizmos.DrawWireSphere(startWorld, 0.3f);

        // Dessine le point d'arrivée
        Gizmos.color = endPointColor;
        Vector3 endWorld = GridManager.Instance.GridToWorld(endPosition);
        Gizmos.DrawWireSphere(endWorld, 0.3f);

        // Dessine le chemin calculé
        Gizmos.color = pathColor;
        for (int i = 0; i < lastCalculatedPath.Count - 1; i++)
        {
            Vector3 currentWorld = GridManager.Instance.GridToWorld(lastCalculatedPath[i]);
            Vector3 nextWorld = GridManager.Instance.GridToWorld(lastCalculatedPath[i + 1]);

            // Ligne entre les étapes
            Gizmos.DrawLine(currentWorld, nextWorld);

            // Petit carré sur chaque étape
            Gizmos.DrawWireCube(currentWorld, Vector3.one * 0.2f);
        }

        // Carré sur la dernière position
        if (lastCalculatedPath.Count > 0)
        {
            Vector3 lastWorld = GridManager.Instance.GridToWorld(lastCalculatedPath[lastCalculatedPath.Count - 1]);
            Gizmos.DrawWireCube(lastWorld, Vector3.one * 0.2f);
        }
    }

    // Efface le dernier chemin calculé pour nettoyer l'affichage
    [ContextMenu("Clear Path Display")]
    public void ClearPathDisplay()
    {
        lastCalculatedPath.Clear();
        Debug.Log("Affichage du chemin effacé");
    }

    // Valide la configuration des unités de test
    [ContextMenu("Validate Test Setup")]
    public void ValidateTestSetup()
    {
        Debug.Log("=== Validation Configuration Test ===");

        GameObject[] testUnits = { testGroundUnit, testWaterUnit, testMountainUnit, testAirUnit };
        string[] expectedTags = { "GroundUnit", "WaterUnit", "MountainUnit", "AirUnit" };
        string[] unitNames = { "Ground", "Water", "Mountain", "Air" };

        bool allValid = true;

        for (int i = 0; i < testUnits.Length; i++)
        {
            GameObject testUnit = testUnits[i];
            string expectedTag = expectedTags[i];
            string unitName = unitNames[i];

            if (testUnit == null)
            {
                Debug.LogWarning($"⚠️ {unitName}Unit non assignée");
                allValid = false;
            }
            else if (!testUnit.CompareTag(expectedTag))
            {
                Debug.LogError($"❌ {testUnit.name} devrait avoir le tag '{expectedTag}' mais a '{testUnit.tag}'");
                allValid = false;
            }
            else
            {
                Debug.Log($"✅ {unitName}Unit correctement configurée: {testUnit.name} (Tag: {testUnit.tag})");
            }
        }

        if (allValid)
        {
            Debug.Log("🎉 Toutes les unités de test sont correctement configurées !");
        }
        else
        {
            Debug.LogWarning("⚠️ Configuration incomplète. Suivez le guide de setup.");
        }
    }
}
