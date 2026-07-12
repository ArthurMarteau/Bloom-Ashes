using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Composant de Foundation 2x2 : détecte les Rootborn idle sur ses 4 cellules et construit un bâtiment.
public class FoundationComponent : MonoBehaviour, ICellListener, IMultiTile, ISelectable, ITickable
{
    [SerializeField] private FoundationStatsData foundationStats;

    public UnityEvent OnFoundationStateChanged = new UnityEvent();

    private readonly Dictionary<Vector2Int, GameObject> idleRootbornsPerCell = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<GameObject, HashSet<Vector2Int>> rootbornCellsByRootborn = new Dictionary<GameObject, HashSet<Vector2Int>>();
    private readonly Dictionary<GameObject, TrackedRootbornIdleHandler> trackedRootborns = new Dictionary<GameObject, TrackedRootbornIdleHandler>();
    private Vector2Int[] foundationCells;

    private bool cellsInitialized;
    private bool isBuildInProgress;
    private bool initialSyncPending;
    private long simulationEntityID = -1;
    private long buildAtTick = -1;
    private int pendingTeamID;
    private BuildingStatsData pendingBuildingData;
    private List<GameObject> pendingRootborns;

    private class TrackedRootbornIdleHandler
    {
        public MovementController Movement;
        public UnityAction<bool> Handler;
    }

    private void Start()
    {
        InitializeCells();
        RegisterFoundationCells();
        RegisterCellListeners();
        simulationEntityID = EntityRegistry.Instance?.GetEntityID(gameObject) ?? -1;
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Register(simulationEntityID, this);
        initialSyncPending = true;
    }

    private void OnDestroy()
    {
        EntityRegistry.Instance?.UnregisterAllCellListeners(this);
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Unregister(simulationEntityID, this);
        ReleaseFoundationFromSimulation();

        foreach (GameObject rootborn in new List<GameObject>(trackedRootborns.Keys))
        {
            UnsubscribeIdleEvents(rootborn);
        }
    }

    // Libère l'index Foundation avant le démontage visuel ou OnDestroy Unity.
    public void ReleaseFoundationFromSimulation()
    {
        if (EntityRegistry.Instance == null)
            return;

        if (cellsInitialized && foundationCells != null)
        {
            foreach (Vector2Int cell in foundationCells)
                EntityRegistry.Instance.UnregisterFoundation(cell);
        }

        EntityRegistry.Instance.UnregisterFoundation(gameObject);
    }

    public int GetIdleRootbornCount()
    {
        return idleRootbornsPerCell.Count;
    }

    public bool IsReadyToBuild()
    {
        if (idleRootbornsPerCell.Count != 4)
            return false;

        int teamID = -1;
        foreach (GameObject rootborn in idleRootbornsPerCell.Values)
        {
            if (rootborn == null)
                return false;

            UnitStatsComponent stats = rootborn.GetComponent<UnitStatsComponent>();
            if (stats == null || stats.IsDead() || EntityRegistry.Instance == null || !EntityRegistry.Instance.IsEntityAlive(rootborn))
                return false;

            TeamComponent team = rootborn.GetComponent<TeamComponent>();
            if (team == null)
                return false;

            int rootbornTeamID = team.GetTeamID();
            if (teamID == -1)
                teamID = rootbornTeamID;
            // Égalité stricte : les quatre Rootborn doivent appartenir au même joueur, pas seulement à des alliés.
            else if (teamID != rootbornTeamID)
                return false;
        }

        return teamID != -1;
    }

    public List<GameObject> GetIdleRootborns()
    {
        return new List<GameObject>(idleRootbornsPerCell.Values);
    }

    public FoundationStatsData GetFoundationStats()
    {
        return foundationStats;
    }

    public Sprite GetPortrait()
    {
        return foundationStats?.portrait;
    }

    public string GetDisplayName()
    {
        return foundationStats?.entityName ?? gameObject.name;
    }

    // Retourne les cellules occupées par cette Foundation.
    public List<Vector2Int> GetOccupiedTiles()
    {
        if (!cellsInitialized)
            InitializeCells();

        return new List<Vector2Int>(foundationCells);
    }

    public int GetTeamID()
    {
        foreach (GameObject rootborn in idleRootbornsPerCell.Values)
        {
            TeamComponent team = rootborn != null ? rootborn.GetComponent<TeamComponent>() : null;
            if (team != null)
                return team.GetTeamID();
        }

        return -1;
    }

    public void ExecuteBuild(BuildingStatsData buildingData, int teamID)
    {
        if (isBuildInProgress)
            return;

        if (buildingData == null || buildingData.buildingPrefab == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🏗️ Foundation {gameObject.name} : bâtiment ou prefab manquant", this);
            return;
        }

        // Égalité stricte : seul le propriétaire des Rootborn peut lancer leur construction.
        bool isReadyToBuild = IsReadyToBuild();
        int foundationTeamID = GetTeamID();
        if (!isReadyToBuild || foundationTeamID != teamID)
        {
            if (GameLogger.IsEnabled(GameLogger.Category.Spawn))
            {
                List<Vector2Int> orderedCells = new List<Vector2Int>(idleRootbornsPerCell.Keys);
                orderedCells.Sort((first, second) =>
                {
                    int xComparison = first.x.CompareTo(second.x);
                    return xComparison != 0 ? xComparison : first.y.CompareTo(second.y);
                });

                List<string> rootbornDetails = new List<string>(orderedCells.Count);
                foreach (Vector2Int cell in orderedCells)
                {
                    GameObject rootborn = idleRootbornsPerCell[cell];
                    TeamComponent rootbornTeam = rootborn != null ? rootborn.GetComponent<TeamComponent>() : null;
                    MovementController movement = rootborn != null ? rootborn.GetComponent<MovementController>() : null;
                    string rootbornName = rootborn != null ? rootborn.name : "null";
                    string rootbornTeamID = rootbornTeam != null ? rootbornTeam.GetTeamID().ToString() : "absent";
                    string movementState = movement != null ? movement.GetCurrentState().ToString() : "absent";
                    bool isMoving = rootborn != null && EntityRegistry.Instance != null
                        && EntityRegistry.Instance.IsMoving(rootborn);
                    rootbornDetails.Add($"{cell} → {rootbornName}, team={rootbornTeamID}, state={movementState}, registryMoving={isMoving}");
                }

                GameLogger.Warn(GameLogger.Category.Spawn,
                    $"🏗️ Foundation {gameObject.name} : construction refusée — idle={idleRootbornsPerCell.Count}/4, ready={isReadyToBuild}, foundationTeam={foundationTeamID}, commandTeam={teamID}, Rootborn [{string.Join(" ; ", rootbornDetails)}]", this);
            }
            return;
        }

        StartBuild(buildingData, teamID);
    }

    // Exécute une commande BuildBuilding depuis l'index de bâtiment envoyé en lockstep.
    public void ExecuteBuild(int buildingIndex, int teamID)
    {
        List<BuildingStatsData> buildings = foundationStats?.constructibleBuildings;
        if (buildings == null || buildingIndex < 0 || buildingIndex >= buildings.Count)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🏗️ Foundation {gameObject.name} : index bâtiment invalide {buildingIndex}", this);
            return;
        }

        ExecuteBuild(buildings[buildingIndex], teamID);
    }

    // Lance la phase visuelle de build et programme la décision finale en ticks.
    private void StartBuild(BuildingStatsData buildingData, int teamID)
    {
        isBuildInProgress = true;
        OnFoundationStateChanged.RemoveAllListeners();

        pendingBuildingData = buildingData;
        pendingTeamID = teamID;
        pendingRootborns = GetIdleRootborns();
        foreach (GameObject rootborn in pendingRootborns)
        {
            if (rootborn != null)
                UnitFreezer.Freeze(rootborn, disableComponents: true, freezeAnimator: true);
        }

        if (foundationStats.onBuildVFX != null)
            VFXApplier.Apply(foundationStats.onBuildVFX, gameObject);

        float delay = foundationStats.buildDelay;
        if (foundationStats.onBuildVFX != null && foundationStats.onBuildVFX.spriteDuration > delay)
            delay = foundationStats.onBuildVFX.spriteDuration;

        long currentTick = SimulationClockManager.Instance != null ? SimulationClockManager.Instance.CurrentTick : 0;
        buildAtTick = currentTick + SimulationClock.SecondsToTicks(delay);
    }

    // Termine la construction uniquement quand le tick déterministe est atteint.
    public void SimTick(long tick)
    {
        if (initialSyncPending)
        {
            initialSyncPending = false;
            SynchronizeInitialRootborns();
        }

        if (!isBuildInProgress || buildAtTick < 0 || tick < buildAtTick)
            return;

        CompleteBuildAtTick();
    }

    // Revalide les Rootborn puis remplace la Foundation par le bâtiment.
    private void CompleteBuildAtTick()
    {
        // Égalité stricte : la propriété doit rester inchangée jusqu'à la résolution du build.
        if (pendingBuildingData == null || pendingRootborns == null || !IsReadyToBuild() || GetTeamID() != pendingTeamID)
        {
            AbortBuild();
            return;
        }

        Vector3 buildPosition = GetBuildWorldPosition();

        foreach (GameObject rootborn in pendingRootborns)
        {
            if (rootborn != null)
                EntitySpawnManager.Instance?.DespawnEntity(rootborn, 0f);
        }

        GameObject buildingObject = EntitySpawnManager.Instance != null
            ? EntitySpawnManager.Instance.SpawnEntity(pendingBuildingData.buildingPrefab, buildPosition, pendingTeamID)
            : null;
        if (buildingObject == null)
        {
            AbortBuild();
            return;
        }

        BuildingStatsComponent buildingStats = buildingObject.GetComponent<BuildingStatsComponent>();
        if (buildingStats != null)
            buildingStats.SetOriginFoundation(foundationStats);

        EntitySpawnManager.Instance?.NotifyEntitySpawned(gameObject, buildingObject);
        GameLogger.Log(GameLogger.Category.Spawn, $"🏗️ Foundation {gameObject.name} : construction de {pendingBuildingData.buildingName} équipe {pendingTeamID}", this);
        EntitySpawnManager.Instance?.DespawnEntity(gameObject, 0f);
        ClearBuildState();
    }

    // Annule une construction si la revalidation échoue au tick final.
    private void AbortBuild()
    {
        if (pendingRootborns != null)
        {
            foreach (GameObject rootborn in pendingRootborns)
            {
                if (rootborn != null && EntityRegistry.Instance != null && EntityRegistry.Instance.IsEntityAlive(rootborn))
                    UnitFreezer.Unfreeze(rootborn, enableComponents: true, unfreezeAnimator: true);
            }
        }

        GameLogger.Warn(GameLogger.Category.Spawn, $"🏗️ Foundation {gameObject.name} : build annulé à l'échéance, état invalide", this);
        ClearBuildState();
        OnFoundationStateChanged.Invoke();
    }

    // Nettoie l'état de build planifié.
    private void ClearBuildState()
    {
        isBuildInProgress = false;
        buildAtTick = -1;
        pendingTeamID = 0;
        pendingBuildingData = null;
        pendingRootborns = null;
    }

    // Retourne la position monde de l'ancre logique de la Foundation.
    private Vector3 GetBuildWorldPosition()
    {
        if (foundationCells != null && foundationCells.Length > 0 && GridManager.Instance != null)
            return GridManager.Instance.GridToWorld(foundationCells[0]);

        return Vector3.zero;
    }

    public void OnUnitEntered(Vector2Int cell, GameObject unit)
    {
        if (!IsFoundationCell(cell)) return;
        if (!IsEligibleRootborn(unit)) return;

        if (!rootbornCellsByRootborn.TryGetValue(unit, out HashSet<Vector2Int> cells))
        {
            cells = new HashSet<Vector2Int>();
            rootbornCellsByRootborn[unit] = cells;
        }

        cells.Add(cell);
        SubscribeIdleEvents(unit);

        if (SynchronizeIdleState(cell, unit))
            OnFoundationStateChanged.Invoke();
    }

    public void OnUnitLeft(Vector2Int cell, GameObject unit)
    {
        if (!IsFoundationCell(cell)) return;

        bool changed = RemoveIdleRootborn(unit);

        if (unit != null && rootbornCellsByRootborn.TryGetValue(unit, out HashSet<Vector2Int> cells))
        {
            cells.Remove(cell);
            if (cells.Count == 0)
            {
                rootbornCellsByRootborn.Remove(unit);
                UnsubscribeIdleEvents(unit);
            }
        }

        if (changed)
            OnFoundationStateChanged.Invoke();
    }

    private void InitializeCells()
    {
        if (cellsInitialized || GridManager.Instance == null)
            return;

        int sizeX = foundationStats != null ? foundationStats.sizeX : 2;
        int sizeY = foundationStats != null ? foundationStats.sizeY : 2;
        foundationCells = new Vector2Int[sizeX * sizeY];

        int index = 0;
        Vector2Int bottomLeft = GridManager.Instance.WorldToGrid(transform.position);
        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                foundationCells[index++] = bottomLeft + new Vector2Int(x, y);
            }
        }

        cellsInitialized = true;
    }

    private void RegisterCellListeners()
    {
        if (!cellsInitialized || EntityRegistry.Instance == null)
            return;

        foreach (Vector2Int cell in foundationCells)
        {
            EntityRegistry.Instance.RegisterCellListener(cell, this);
        }
    }

    // Enregistre les cellules de Foundation pour les commandes et les recherches sans Physics2D.
    private void RegisterFoundationCells()
    {
        if (!cellsInitialized || EntityRegistry.Instance == null)
            return;

        foreach (Vector2Int cell in foundationCells)
            EntityRegistry.Instance.RegisterFoundation(cell, gameObject);
    }

    private void SynchronizeInitialRootborns()
    {
        if (!cellsInitialized || EntityRegistry.Instance == null)
            return;

        foreach (Vector2Int cell in foundationCells)
        {
            GameObject unit = EntityRegistry.Instance.GetUnitAtLogicalPosition(cell);
            if (IsEligibleRootborn(unit))
                OnUnitEntered(cell, unit);
        }
    }

    private void SubscribeIdleEvents(GameObject rootborn)
    {
        if (rootborn == null || trackedRootborns.ContainsKey(rootborn))
            return;

        MovementController movement = rootborn.GetComponent<MovementController>();
        if (movement == null)
            return;

        UnityAction<bool> handler = isIdle => HandleRootbornIdleStateChanged(rootborn, isIdle);
        movement.OnIdleStateChanged.AddListener(handler);
        trackedRootborns[rootborn] = new TrackedRootbornIdleHandler
        {
            Movement = movement,
            Handler = handler
        };
    }

    private void UnsubscribeIdleEvents(GameObject rootborn)
    {
        if (rootborn == null || !trackedRootborns.TryGetValue(rootborn, out TrackedRootbornIdleHandler tracked))
            return;

        if (tracked.Movement != null)
            tracked.Movement.OnIdleStateChanged.RemoveListener(tracked.Handler);

        trackedRootborns.Remove(rootborn);
    }

    private void HandleRootbornIdleStateChanged(GameObject rootborn, bool isIdle)
    {
        bool changed = false;

        if (!isIdle || rootborn == null)
        {
            changed = RemoveIdleRootborn(rootborn);
        }
        else
        {
            MovementController movement = rootborn.GetComponent<MovementController>();
            if (movement != null)
            {
                Vector2Int cell = movement.GetCurrentGridPosition();
                if (IsFoundationCell(cell) && rootbornCellsByRootborn.TryGetValue(rootborn, out HashSet<Vector2Int> cells) && cells.Contains(cell))
                    changed = SetIdleRootborn(cell, rootborn);
            }
        }

        if (changed)
            OnFoundationStateChanged.Invoke();
    }

    private bool SynchronizeIdleState(Vector2Int cell, GameObject rootborn)
    {
        MovementController movement = rootborn.GetComponent<MovementController>();
        bool isIdle = movement != null && movement.GetCurrentState() == MovementController.MovementState.Idle;
        return isIdle ? SetIdleRootborn(cell, rootborn) : RemoveIdleRootborn(rootborn);
    }

    private bool SetIdleRootborn(Vector2Int cell, GameObject rootborn)
    {
        if (idleRootbornsPerCell.TryGetValue(cell, out GameObject existing) && existing == rootborn)
            return false;

        idleRootbornsPerCell[cell] = rootborn;
        return true;
    }

    private bool RemoveIdleRootborn(GameObject rootborn)
    {
        bool changed = false;
        foreach (Vector2Int cell in new List<Vector2Int>(idleRootbornsPerCell.Keys))
        {
            if (idleRootbornsPerCell[cell] == rootborn)
            {
                idleRootbornsPerCell.Remove(cell);
                changed = true;
            }
        }

        return changed;
    }

    private bool IsFoundationCell(Vector2Int cell)
    {
        foreach (Vector2Int foundationCell in foundationCells)
        {
            if (foundationCell == cell)
                return true;
        }

        return false;
    }

    private bool IsEligibleRootborn(GameObject unit)
    {
        if (unit == null)
            return false;

        UnitStatsComponent stats = unit.GetComponent<UnitStatsComponent>();
        return stats != null && stats.IsRootborn() && !stats.IsDead();
    }

    [ContextMenu("Debug: Foundation State")]
    private void DebugFoundationState()
    {
        GameLogger.Log(GameLogger.Category.Spawn, $"🏗️ Foundation {gameObject.name} : {GetIdleRootbornCount()}/4 Rootborn idle, ready={IsReadyToBuild()}, team={GetTeamID()}", this);
    }
}
