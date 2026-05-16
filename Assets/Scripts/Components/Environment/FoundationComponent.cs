using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Composant de Foundation 2x2 : détecte les Rootborn idle sur ses 4 cellules et construit un bâtiment.
public class FoundationComponent : MonoBehaviour
{
    [SerializeField] private FoundationStatsData foundationStats;
    [SerializeField] private float scanInterval = 0.2f;

    public UnityEvent OnFoundationStateChanged = new UnityEvent();

    private readonly Dictionary<Vector2Int, GameObject> rootbornsPerCell = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<Vector2Int, GameObject> idleRootbornsPerCell = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<GameObject, UnityAction<bool>> idleHandlersByRootborn = new Dictionary<GameObject, UnityAction<bool>>();
    private readonly Dictionary<GameObject, MovementController> movementByRootborn = new Dictionary<GameObject, MovementController>();
    private readonly Vector2Int[] foundationCells = new Vector2Int[4];

    private float nextScanTime;
    private bool cellsInitialized;
    private bool isBuildInProgress;

    private void Start()
    {
        InitializeCells();
        ScanRootborns();
    }

    private void Update()
    {
        if (Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + scanInterval;
        ScanRootborns();
    }

    private void OnDestroy()
    {
        foreach (GameObject rootborn in new List<GameObject>(idleHandlersByRootborn.Keys))
        {
            UnsubscribeRootborn(rootborn);
        }
    }

    public int GetIdleRootbornCount()
    {
        return idleRootbornsPerCell.Count;
    }

    public void ForceScan()
    {
        ScanRootborns();
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

    private void InitializeCells()
    {
        if (cellsInitialized || GridManager.Instance == null)
            return;

        Vector2Int bottomLeftCell = GridManager.Instance.WorldToGrid(transform.position);
        foundationCells[0] = bottomLeftCell;
        foundationCells[1] = bottomLeftCell + new Vector2Int(1, 0);
        foundationCells[2] = bottomLeftCell + new Vector2Int(0, 1);
        foundationCells[3] = bottomLeftCell + new Vector2Int(1, 1);
        cellsInitialized = true;
    }

    private void ScanRootborns()
    {
        InitializeCells();
        GameLogger.Log(GameLogger.Category.Spawn, $"️ ScanRootborns : cellsInitialized={cellsInitialized}, cells=[{string.Join(", ", foundationCells)}]", this);

        if (!cellsInitialized || EntityRegistry.Instance == null)
            return;

        bool changed = false;

        foreach (Vector2Int cell in foundationCells)
        {
            GameObject currentRootborn = GetRootbornAt(cell);
            rootbornsPerCell.TryGetValue(cell, out GameObject previousRootborn);

            if (previousRootborn != currentRootborn)
            {
                if (previousRootborn != null)
                    changed |= RemoveRootbornFromCell(cell, previousRootborn);

                if (currentRootborn != null)
                    changed |= TrackRootbornAtCell(cell, currentRootborn);
            }
            else if (currentRootborn != null)
            {
                changed |= SynchronizeIdleState(cell, currentRootborn);
            }
        }

        CleanupDestroyedRootborns(ref changed);

        if (changed)
            OnFoundationStateChanged.Invoke();
    }

    private GameObject GetRootbornAt(Vector2Int cell)
    {
        GameObject unit = EntityRegistry.Instance.GetUnitAtLogicalPosition(cell);
        GameLogger.Log(GameLogger.Category.Spawn, $"️ GetRootbornAt {cell} : unit={(unit != null ? unit.name : "null")}", this);
        GameLogger.Log(GameLogger.Category.Spawn, $"️ GetRootbornAt {cell} : eligible={(unit != null && unit.GetComponent<UnitStatsComponent>()?.IsRootborn() == true ? "oui" : "non")}", this);
        if (unit == null)
            return null;

        UnitStatsComponent stats = unit.GetComponent<UnitStatsComponent>();
        return stats != null && stats.IsRootborn() && !stats.IsDead() ? unit : null;
    }

    private bool TrackRootbornAtCell(Vector2Int cell, GameObject rootborn)
    {
        rootbornsPerCell[cell] = rootborn;
        SubscribeRootborn(rootborn);
        return SynchronizeIdleState(cell, rootborn);
    }

    private bool RemoveRootbornFromCell(Vector2Int cell, GameObject rootborn)
    {
        bool changed = false;

        if (rootbornsPerCell.TryGetValue(cell, out GameObject registeredRootborn) && registeredRootborn == rootborn)
            rootbornsPerCell.Remove(cell);

        if (idleRootbornsPerCell.TryGetValue(cell, out GameObject idleRootborn) && idleRootborn == rootborn)
        {
            idleRootbornsPerCell.Remove(cell);
            changed = true;
        }

        if (!rootbornsPerCell.ContainsValue(rootborn))
            UnsubscribeRootborn(rootborn);

        return changed;
    }

    private void SubscribeRootborn(GameObject rootborn)
    {
        if (rootborn == null || idleHandlersByRootborn.ContainsKey(rootborn))
            return;

        MovementController movement = rootborn.GetComponent<MovementController>();
        if (movement == null)
            return;

        UnityAction<bool> handler = isIdle => HandleRootbornIdleStateChanged(rootborn, isIdle);
        movement.OnIdleStateChanged.AddListener(handler);
        idleHandlersByRootborn[rootborn] = handler;
        movementByRootborn[rootborn] = movement;
    }

    private void UnsubscribeRootborn(GameObject rootborn)
    {
        if (rootborn == null || !idleHandlersByRootborn.TryGetValue(rootborn, out UnityAction<bool> handler))
            return;

        if (movementByRootborn.TryGetValue(rootborn, out MovementController movement) && movement != null)
            movement.OnIdleStateChanged.RemoveListener(handler);

        idleHandlersByRootborn.Remove(rootborn);
        movementByRootborn.Remove(rootborn);
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
                if (IsFoundationCell(cell) && rootbornsPerCell.TryGetValue(cell, out GameObject registeredRootborn) && registeredRootborn == rootborn)
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

    private void CleanupDestroyedRootborns(ref bool changed)
    {
        foreach (Vector2Int cell in new List<Vector2Int>(rootbornsPerCell.Keys))
        {
            if (rootbornsPerCell[cell] == null)
            {
                rootbornsPerCell.Remove(cell);
                idleRootbornsPerCell.Remove(cell);
                changed = true;
            }
        }
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

    [ContextMenu("Debug: Foundation State")]
    private void DebugFoundationState()
    {
        GameLogger.Log(GameLogger.Category.Spawn, $"🏗️ Foundation {gameObject.name} : {GetIdleRootbornCount()}/4 Rootborn idle, ready={IsReadyToBuild()}, team={GetTeamID()}", this);
    }
}
