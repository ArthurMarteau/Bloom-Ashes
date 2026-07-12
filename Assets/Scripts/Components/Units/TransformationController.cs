using System.Collections.Generic;
using UnityEngine;

// Détecte le bâtiment compatible adjacent et exécute la transformation via Bud sur ticks.
// Rootborn -> bâtiment de faction non-Nexus. Autre unité -> Nexus allié.
public class TransformationController : MonoBehaviour, ITickable
{
    [SerializeField] private GameObject budPrefab;
    [SerializeField] private bool debugTransform = true;

    private long simulationEntityID = -1;
    private long transformAtTick = -1;
    private UnitStatsData pendingUnitData;
    private UnitStatsData pendingSelfData;
    private int pendingTeamID;
    private int pendingCurrentHP;
    private int pendingMaxHP;

    // Enregistre le contrôleur après l'attribution de l'entityID par UnitStatsComponent.Awake.
    private void Start()
    {
        simulationEntityID = EntityRegistry.Instance?.GetEntityID(gameObject) ?? -1;
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Register(simulationEntityID, this);
    }

    // Retire le contrôleur de la boucle logique.
    private void OnDestroy()
    {
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Unregister(simulationEntityID, this);
    }

    // Avance la transformation planifiée sur la boucle logique.
    public void SimTick(long tick)
    {
        if (transformAtTick < 0 || tick < transformAtTick)
            return;

        CompleteTransformAtTick();
    }

    // Trouve le bâtiment compatible adjacent depuis la position logique de l'unité.
    public BuildingStatsComponent FindAdjacentCompatibleBuilding()
    {
        TeamComponent ownTeam = GetComponent<TeamComponent>();
        if (ownTeam == null)
        {
            LogWarning("FindAdjacentCompatibleBuilding - TeamComponent manquant");
            return null;
        }

        MovementController movementController = GetComponent<MovementController>();
        if (movementController == null)
        {
            LogWarning("FindAdjacentCompatibleBuilding - MovementController manquant");
            return null;
        }

        if (EntityRegistry.Instance == null)
        {
            LogWarning("FindAdjacentCompatibleBuilding - EntityRegistry introuvable");
            return null;
        }

        int teamID = ownTeam.GetTeamID();
        bool isRootborn = GetComponent<RootbornStockComponent>() != null;
        Vector2Int unitCell = movementController.GetCurrentGridPosition();
        Vector2Int[] offsets = RangeOffsets.GetOffsets(1);

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int adjacentCell = unitCell + offset;
            GameObject buildingObject = EntityRegistry.Instance.GetBuildingAtLogicalPosition(adjacentCell);
            if (buildingObject == null) continue;

            BuildingStatsComponent buildingStats = buildingObject.GetComponent<BuildingStatsComponent>();
            if (buildingStats == null || buildingStats.IsDead()) continue;

            BuildingStatsData baseStats = buildingStats.GetBaseStats();
            if (baseStats == null) continue;
            if (isRootborn && baseStats.isNexus) continue;
            if (!isRootborn && !baseStats.isNexus) continue;

            TeamComponent buildingTeam = buildingObject.GetComponent<TeamComponent>();
            // Égalité stricte : une transformation utilise uniquement le bâtiment du même joueur.
            if (buildingTeam == null || buildingTeam.GetTeamID() != teamID) continue;

            Log($"FindAdjacentCompatibleBuilding - batiment compatible trouve en {adjacentCell}");
            return buildingStats;
        }

        Log("FindAdjacentCompatibleBuilding - aucun batiment compatible adjacent");
        return null;
    }

    // Vérifie la disponibilité actuelle d'une transformation avant affichage UI ou exécution.
    public bool CanTransform(UnitStatsData unitData)
    {
        if (unitData == null)
        {
            LogWarning("CanTransform - unitData null");
            return false;
        }

        TeamComponent teamComponent = GetComponent<TeamComponent>();
        if (teamComponent == null)
        {
            LogWarning("CanTransform - TeamComponent manquant");
            return false;
        }

        if (WatersapManager.Instance == null)
        {
            LogWarning("CanTransform - WatersapManager introuvable");
            return false;
        }

        BuildingStatsComponent building = FindAdjacentCompatibleBuilding();
        return building != null
            && unitData.tierRequired <= building.GetCurrentTier()
            && WatersapManager.Instance.CanSpend(teamComponent.GetTeamID(), unitData.watersapCost);
    }

    // Trouve une Foundation sous le Rootborn via l'index logique, sans Physics2D.
    public FoundationComponent FindFoundationUnderRootborn()
    {
        MovementController movementController = GetComponent<MovementController>();
        if (movementController == null)
        {
            LogWarning("FindFoundationUnderRootborn - MovementController manquant");
            return null;
        }

        Vector2Int myCell = movementController.GetCurrentGridPosition();
        GameObject foundationObject = EntityRegistry.Instance?.GetFoundationAt(myCell);
        FoundationComponent foundation = foundationObject != null ? foundationObject.GetComponent<FoundationComponent>() : null;

        if (foundation != null)
            Log($"FindFoundationUnderRootborn - Foundation trouvee sous {myCell}");

        return foundation;
    }

    // Exécute une commande TransformUnit depuis l'index d'unité envoyé en lockstep.
    public void ExecuteTransform(int unitIndex)
    {
        BuildingStatsComponent building = FindAdjacentCompatibleBuilding();
        List<UnitStatsData> units = building?.GetBaseStats()?.producibleUnits;
        if (units == null || unitIndex < 0 || unitIndex >= units.Count)
        {
            LogWarning($"ExecuteTransform - index unité invalide {unitIndex}");
            return;
        }

        StartTransform(units[unitIndex]);
    }

    // Compatibilité debug : résout l'index puis passe par le même chemin que la commande.
    public void ExecuteTransform(UnitStatsData unitData)
    {
        BuildingStatsComponent building = FindAdjacentCompatibleBuilding();
        List<UnitStatsData> units = building?.GetBaseStats()?.producibleUnits;
        int unitIndex = units != null ? units.IndexOf(unitData) : -1;
        ExecuteTransform(unitIndex);
    }

    // Lance la phase visuelle de transformation et programme le spawn Bud en ticks.
    private void StartTransform(UnitStatsData unitData)
    {
        if (transformAtTick >= 0)
            return;

        TeamComponent teamComponent = GetComponent<TeamComponent>();
        if (teamComponent == null)
        {
            LogWarning("ExecuteTransform - TeamComponent manquant");
            return;
        }

        int teamID = teamComponent.GetTeamID();
        if (budPrefab == null)
        {
            LogError("ExecuteTransform - budPrefab non assigne");
            return;
        }

        UnitStatsComponent unitStatsComponent = GetComponent<UnitStatsComponent>();
        UnitStatsData selfData = unitStatsComponent?.GetBaseStats();
        if (selfData == null)
        {
            LogError("ExecuteTransform - UnitStatsData introuvable sur cette unite");
            return;
        }

        if (WatersapManager.Instance == null)
        {
            LogError("ExecuteTransform - WatersapManager introuvable");
            return;
        }

        if (!CanTransform(unitData))
        {
            Log($"ExecuteTransform refuse - batiment compatible absent ou Watersap insuffisant pour {unitData.unitName}");
            return;
        }

        if (!WatersapManager.Instance.SpendWatersap(teamID, unitData.watersapCost))
            return;

        UnitFreezer.Freeze(gameObject, disableComponents: true, freezeAnimator: false);

        float transformDelay = selfData.transformDelay;
        if (selfData.onTransformVFX != null && selfData.onTransformVFX.spriteDuration > transformDelay)
            transformDelay = selfData.onTransformVFX.spriteDuration;

        if (selfData.onTransformVFX != null)
            VFXApplier.Apply(selfData.onTransformVFX, gameObject);

        pendingUnitData = unitData;
        pendingSelfData = selfData;
        pendingTeamID = teamID;
        pendingCurrentHP = unitStatsComponent?.GetCurrentHP() ?? 0;
        pendingMaxHP = unitStatsComponent?.GetMaxHP() ?? 0;

        long currentTick = SimulationClockManager.Instance != null ? SimulationClockManager.Instance.CurrentTick : 0;
        transformAtTick = currentTick + SimulationClock.SecondsToTicks(transformDelay);
        Log($"ExecuteTransform - animation Idle_Transform, échéance tick {transformAtTick}");
    }

    // Revalide le bâtiment adjacent puis remplace l'unité par un Bud.
    private void CompleteTransformAtTick()
    {
        if (pendingUnitData == null || FindAdjacentCompatibleBuilding() == null)
        {
            AbortTransform();
            return;
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        Vector2Int budCell = GetCurrentLogicalCell();
        Vector3 budPosition = GridManager.Instance != null ? GridManager.Instance.GridToWorld(budCell) : Vector3.zero;
        GameObject budObject = EntitySpawnManager.Instance != null
            ? EntitySpawnManager.Instance.SpawnEntity(budPrefab, budPosition, pendingTeamID)
            : null;
        if (budObject == null)
        {
            AbortTransform();
            return;
        }

        BudComponent budComponent = budObject.GetComponent<BudComponent>();
        if (budComponent == null)
        {
            LogError("ExecuteTransform - budPrefab sans BudComponent");
            EntitySpawnManager.Instance?.DespawnEntity(budObject, 0f);
            AbortTransform();
            return;
        }

        budComponent.Initialize(pendingUnitData, pendingSelfData, pendingCurrentHP, pendingMaxHP, pendingTeamID);
        if (EntityRegistry.Instance != null)
            EntityRegistry.Instance.UpdateLogicalPosition(budObject, budCell);

        EntitySpawnManager.Instance?.NotifyEntityReplaced(gameObject, budObject);
        EntitySpawnManager.Instance?.DespawnEntity(gameObject, 0f, false);

        Log($"ExecuteTransform - Bud cree vers {pendingUnitData.unitName} (equipe {pendingTeamID}, cout {pendingUnitData.watersapCost}, HP herites {pendingCurrentHP}/{pendingMaxHP})");
        ClearTransformState();
    }

    // Annule la transformation si la revalidation échoue.
    private void AbortTransform()
    {
        UnitFreezer.Unfreeze(gameObject, enableComponents: true, unfreezeAnimator: false);
        LogWarning("ExecuteTransform - transformation annulée à l'échéance");
        ClearTransformState();
    }

    // Nettoie l'état de transformation planifié.
    private void ClearTransformState()
    {
        transformAtTick = -1;
        pendingUnitData = null;
        pendingSelfData = null;
        pendingTeamID = 0;
        pendingCurrentHP = 0;
        pendingMaxHP = 0;
    }

    // Retourne la cellule logique de l'unité sans lire sa position visuelle.
    private Vector2Int GetCurrentLogicalCell()
    {
        MovementController movementController = GetComponent<MovementController>();
        if (movementController != null)
            return movementController.GetCurrentGridPosition();

        if (EntityRegistry.Instance != null && EntityRegistry.Instance.TryGetLogicalPosition(gameObject, out Vector2Int cell))
            return cell;

        return Vector2Int.zero;
    }

    [ContextMenu("Debug: Test Transform")]
    private void DebugTestTransform()
    {
        ExecuteTransform(0);
    }

    private void Log(string message)
    {
        if (debugTransform)
            GameLogger.Log(GameLogger.Category.Combat, $"TransformationController {gameObject.name} : {message}", this);
    }

    private void LogWarning(string message)
    {
        if (debugTransform)
            GameLogger.Warn(GameLogger.Category.Combat, $"TransformationController {gameObject.name} : {message}", this);
    }

    private void LogError(string message)
    {
        GameLogger.Error($"TransformationController {gameObject.name} : {message}", this);
    }
}
