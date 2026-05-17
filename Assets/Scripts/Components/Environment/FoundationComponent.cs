using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Composant de Foundation 2x2 : détecte les Rootborn idle sur ses 4 cellules et construit un bâtiment.
public class FoundationComponent : MonoBehaviour, ICellListener, IMultiTile, ISelectable
{
    [SerializeField] private FoundationStatsData foundationStats;

    public UnityEvent OnFoundationStateChanged = new UnityEvent();

    private readonly Dictionary<Vector2Int, GameObject> idleRootbornsPerCell = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<GameObject, HashSet<Vector2Int>> rootbornCellsByRootborn = new Dictionary<GameObject, HashSet<Vector2Int>>();
    private readonly Dictionary<GameObject, TrackedRootbornIdleHandler> trackedRootborns = new Dictionary<GameObject, TrackedRootbornIdleHandler>();
    private Vector2Int[] foundationCells;

    private bool cellsInitialized;
    private bool isBuildInProgress;

    private class TrackedRootbornIdleHandler
    {
        public MovementController Movement;
        public UnityAction<bool> Handler;
    }

    private void Start()
    {
        InitializeCells();
        RegisterCellListeners();
        SynchronizeInitialRootborns();
    }

    private void OnDestroy()
    {
        EntityRegistry.Instance?.UnregisterAllCellListeners(this);

        foreach (GameObject rootborn in new List<GameObject>(trackedRootborns.Keys))
        {
            UnsubscribeIdleEvents(rootborn);
        }
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

            TeamComponent team = rootborn.GetComponent<TeamComponent>();
            if (team == null)
                return false;

            int rootbornTeamID = team.GetTeamID();
            if (teamID == -1)
                teamID = rootbornTeamID;
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

        if (!IsReadyToBuild() || GetTeamID() != teamID)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🏗️ Foundation {gameObject.name} : construction refusée, Rootborn incomplets ou équipes différentes", this);
            return;
        }

        isBuildInProgress = true;

        Vector3 buildPosition = transform.position;
        List<GameObject> rootbornsToDestroy = GetIdleRootborns();

        foreach (GameObject rootborn in rootbornsToDestroy)
        {
            if (rootborn == null)
                continue;

            EntitySpawnManager.Instance?.NotifyEntityDestroyed(rootborn);
            Destroy(rootborn);
        }

        GameObject buildingObject = Instantiate(buildingData.buildingPrefab, buildPosition, Quaternion.identity);
        TeamComponent buildingTeam = buildingObject.GetComponent<TeamComponent>();
        if (buildingTeam != null)
            buildingTeam.SetTeamID(teamID);

        BuildingStatsComponent buildingStats = buildingObject.GetComponent<BuildingStatsComponent>();
        if (buildingStats != null)
            buildingStats.SetOriginFoundation(foundationStats);

        EntitySpawnManager.Instance?.NotifyEntitySpawned(gameObject, buildingObject);
        EntitySpawnManager.Instance?.NotifyEntityDestroyed(gameObject);

        GameLogger.Log(GameLogger.Category.Spawn, $"🏗️ Foundation {gameObject.name} : construction de {buildingData.buildingName} équipe {teamID}", this);
        Destroy(gameObject);
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
