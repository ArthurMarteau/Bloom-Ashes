using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Permet au Rootborn de détecter les bâtiments de faction adjacents et d'en lire la liste de production
// Chaque Rootborn adjacent à un bâtiment allié non-Nexus peut initier la production d'unités
public class RootbornProductionController : MonoBehaviour
{
    [Header("Production")]
    [Tooltip("Prefab du Bud instancié quand le Rootborn se transforme en unité de combat")]
    [SerializeField] private GameObject budPrefab;

    [Tooltip("Durée de l'animation Idle_Transform avant instanciation du Bud")]
    [SerializeField] private float transformAnimDuration = 1f;

    [Header("Debug")]
    [SerializeField] private bool debugProduction = true;

    // Parcourt les 8 cases adjacentes et retourne le premier bâtiment de faction allié trouvé
    // Un bâtiment valide : non-Nexus, appartient à la même équipe que ce Rootborn
    public BuildingStatsComponent FindAdjacentFactionBuilding()
    {
        TeamComponent ownTeam = GetComponent<TeamComponent>();
        if (ownTeam == null)
        {
            Debug.LogWarning($"🌿 RootbornProductionController {gameObject.name} : TeamComponent manquant");
            return null;
        }
        int teamID = ownTeam.GetTeamID();

        Vector2Int rootbornCell = GridManager.Instance.WorldToGrid(transform.position);
        Vector2Int[] offsets = RangeOffsets.GetOffsets(1);

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int adjacentCell = rootbornCell + offset;

            GameObject buildingObject = GridManager.Instance.GetBuildingAt(adjacentCell);
            if (buildingObject == null) continue;

            BuildingStatsComponent buildingStats = buildingObject.GetComponent<BuildingStatsComponent>();
            if (buildingStats == null) continue;

            // Ignore le Nexus — seuls les bâtiments de faction sont des producteurs d'unités
            if (buildingStats.GetBaseStats().isNexus) continue;

            // Vérifie l'appartenance d'équipe
            TeamComponent buildingTeam = buildingObject.GetComponent<TeamComponent>();
            if (buildingTeam == null || buildingTeam.GetTeamID() != teamID) continue;

            if (debugProduction)
                Debug.Log($"🌿 RootbornProductionController {gameObject.name} : bâtiment adjacent trouvé → {buildingStats.GetBuildingName()} en {adjacentCell}");

            return buildingStats;
        }

        if (debugProduction)
            Debug.Log($"🌿 RootbornProductionController {gameObject.name} : aucun bâtiment de faction adjacent");

        return null;
    }

    // Retourne la liste des unités produisibles depuis le bâtiment adjacent
    // Retourne une liste vide si aucun bâtiment valide n'est adjacent
    public List<UnitStatsData> GetProducibleUnits()
    {
        BuildingStatsComponent building = FindAdjacentFactionBuilding();
        if (building == null)
            return new List<UnitStatsData>();

        return building.GetUnlockedUnits();
    }

    // Retourne le statut de disponibilité d'une unité selon le Watersap disponible
    // Le filtre de tier est géré visuellement par ProductionPanelUI
    public UnitProductionStatus GetUnitStatus(UnitStatsData unitData, int teamID, int buildingCurrentTier)
    {
        // Vérifie le Watersap disponible
        if (WatersapManager.Instance.GetWatersap(teamID) < unitData.watersapCost)
            return UnitProductionStatus.NotEnoughWatersap;

        return UnitProductionStatus.Available;
    }

    // Lance la coroutine de transformation — point d'entrée public
    public void ExecuteTransform(UnitStatsData unitData)
    {
        StartCoroutine(ExecuteTransformRoutine(unitData));
    }

    // Coroutine de transformation : validations, animation, instanciation du Bud, destruction
    private IEnumerator ExecuteTransformRoutine(UnitStatsData unitData)
    {
        // Étape 1 : validations
        if (unitData == null)
        {
            Debug.LogWarning($"🌿 RootbornProductionController {gameObject.name} : ExecuteTransform — unitData null");
            yield break;
        }

        TeamComponent teamComponent = GetComponent<TeamComponent>();
        if (teamComponent == null)
        {
            Debug.LogWarning($"🌿 RootbornProductionController {gameObject.name} : ExecuteTransform — TeamComponent manquant");
            yield break;
        }
        int teamID = teamComponent.GetTeamID();

        if (WatersapManager.Instance == null)
        {
            Debug.LogError($"🌿 RootbornProductionController {gameObject.name} : ExecuteTransform — WatersapManager introuvable");
            yield break;
        }

        if (!WatersapManager.Instance.CanSpend(teamID, unitData.watersapCost))
        {
            Debug.Log($"🌿 RootbornProductionController {gameObject.name} : ExecuteTransform refusé — Watersap insuffisant pour {unitData.unitName} (coût : {unitData.watersapCost})");
            yield break;
        }

        if (budPrefab == null)
        {
            Debug.LogError($"🌿 RootbornProductionController {gameObject.name} : ExecuteTransform — budPrefab non assigné !");
            yield break;
        }

        // Étape 2 : dépense le Watersap
        WatersapManager.Instance.SpendWatersap(teamID, unitData.watersapCost);

        // Étape 3 : désactivation des controllers
        MovementController movementController = GetComponent<MovementController>();
        if (movementController != null)
        {
            movementController.StopMovement();
            movementController.enabled = false;
        }

        AttackController attackController = GetComponent<AttackController>();
        if (attackController != null)
            attackController.enabled = false;

        ChaseController chaseController = GetComponent<ChaseController>();
        if (chaseController != null)
            chaseController.enabled = false;

        // Étape 4 : animation de transformation
        GetComponent<Animator>()?.Play("Idle_Transform");

        if (debugProduction)
            Debug.Log($"🌿 RootbornProductionController {gameObject.name} : animation Idle_Transform — attente {transformAnimDuration}s");

        // Étape 5 : attente de l'animation
        yield return new WaitForSeconds(transformAnimDuration);

        // Étape 6 : sauvegarde position et HP courants
        Vector3 spawnPosition = transform.position;

        int rootbornCurrentHP = 0;
        UnitStatsComponent unitStatsComponent = GetComponent<UnitStatsComponent>();
        if (unitStatsComponent != null)
            rootbornCurrentHP = unitStatsComponent.GetCurrentHP();

        if (debugProduction)
            Debug.Log($"🌿 RootbornProductionController {gameObject.name} : transformation → {unitData.unitName} (équipe {teamID}, coût {unitData.watersapCost}, HP hérités {rootbornCurrentHP})");

        // Étape 7 : instanciation du Bud et initialisation
        GameObject budObject = Instantiate(budPrefab, spawnPosition, Quaternion.identity);
        BudComponent budComponent = budObject.GetComponent<BudComponent>();

        if (budComponent == null)
        {
            Debug.LogError($"🌿 RootbornProductionController {gameObject.name} : ExecuteTransform — BudComponent introuvable sur budPrefab !");
            Destroy(budObject);
            yield break;
        }

        budComponent.Initialize(unitData, rootbornCurrentHP, teamID);

        // Notifie le système de remplacement AVANT destruction
        if (EntitySpawnManager.Instance != null)
        {
            EntitySpawnManager.Instance.NotifyEntityReplaced(gameObject, budObject);
        }

        // Étape 8 : destruction du Rootborn
        Destroy(gameObject);
    }

    // Teste ExecuteTransform avec la première unité produisible du bâtiment adjacent
    [ContextMenu("Debug: Test ExecuteTransform (première unité)")]
    private void DebugTestExecuteTransform()
    {
        List<UnitStatsData> producibleUnits = GetProducibleUnits();

        if (producibleUnits.Count == 0)
        {
            Debug.Log($"🌿 RootbornProductionController {gameObject.name} : aucune unité produisible");
            return;
        }

        ExecuteTransform(producibleUnits[0]);
    }

    // Affiche le bâtiment de faction adjacent depuis l'éditeur
    [ContextMenu("Debug: Find Adjacent Building")]
    private void DebugFindAdjacentBuilding()
    {
        BuildingStatsComponent building = FindAdjacentFactionBuilding();

        if (building == null)
        {
            Debug.Log($"🌿 RootbornProductionController {gameObject.name} : aucun bâtiment de faction adjacent trouvé");
            return;
        }

        BuildingStatsData stats = building.GetBaseStats();
        Debug.Log($"🌿 RootbornProductionController {gameObject.name} : {stats.buildingName} | Tier max : {stats.maxTier} | Unités : {stats.producibleUnits?.Count ?? 0}");
    }
}
