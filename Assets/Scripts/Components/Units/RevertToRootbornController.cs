using System.Collections;
using UnityEngine;

public class RevertToRootbornController : MonoBehaviour
{
    [Header("Revert")]
    [SerializeField] private GameObject budPrefab;
    [SerializeField] private GameObject selfPrefab;
    [SerializeField] private int revertWatersapCost = 10;
    [SerializeField] private float revertAnimDuration = 1f;

    [Header("Debug")]
    [SerializeField] private bool debugRevert = true;

    public UnitStatsData RootbornData => budPrefab?.GetComponent<BudComponent>()?.RootbornData;
    public int RevertWatersapCost => revertWatersapCost;

    public BuildingStatsComponent FindAdjacentNexus()
    {
        TeamComponent ownTeam = GetComponent<TeamComponent>();
        if (ownTeam == null)
        {
            LogWarning("FindAdjacentNexus - TeamComponent manquant");
            return null;
        }

        if (GridManager.Instance == null)
        {
            LogWarning("FindAdjacentNexus - GridManager introuvable");
            return null;
        }

        int teamID = ownTeam.GetTeamID();
        Vector2Int unitCell = GridManager.Instance.WorldToGrid(transform.position);
        Vector2Int[] offsets = RangeOffsets.GetOffsets(1);

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int adjacentCell = unitCell + offset;

            GameObject buildingObject = GridManager.Instance.GetNexusAt(adjacentCell);
            if (buildingObject == null) continue;

            BuildingStatsComponent buildingStats = buildingObject.GetComponent<BuildingStatsComponent>();
            if (buildingStats == null) continue;

            TeamComponent buildingTeam = buildingObject.GetComponent<TeamComponent>();
            if (buildingTeam == null || buildingTeam.GetTeamID() != teamID) continue;

            Log($"FindAdjacentNexus - Nexus adjacent trouve en {adjacentCell}");
            return buildingStats;
        }

        Log("FindAdjacentNexus - aucun Nexus allie adjacent");
        return null;
    }

    public bool CanRevert()
    {
        TeamComponent teamComponent = GetComponent<TeamComponent>();
        if (teamComponent == null)
        {
            LogWarning("CanRevert - TeamComponent manquant");
            return false;
        }

        if (WatersapManager.Instance == null)
        {
            LogWarning("CanRevert - WatersapManager introuvable");
            return false;
        }

        int teamID = teamComponent.GetTeamID();
        return FindAdjacentNexus() != null
            && WatersapManager.Instance.CanSpend(teamID, revertWatersapCost);
    }

    public void ExecuteRevert()
    {
        StartCoroutine(ExecuteRevertRoutine());
    }

    private IEnumerator ExecuteRevertRoutine()
    {
        TeamComponent teamComponent = GetComponent<TeamComponent>();
        if (teamComponent == null)
        {
            LogWarning("ExecuteRevert - TeamComponent manquant");
            yield break;
        }

        int teamID = teamComponent.GetTeamID();

        if (budPrefab == null)
        {
            LogError("ExecuteRevert - budPrefab non assigne");
            yield break;
        }

        if (selfPrefab == null)
        {
            LogError("ExecuteRevert - selfPrefab non assigne");
            yield break;
        }

        if (WatersapManager.Instance == null)
        {
            LogError("ExecuteRevert - WatersapManager introuvable");
            yield break;
        }

        if (!CanRevert())
        {
            Log("ExecuteRevert refuse - Nexus adjacent absent ou Watersap insuffisant");
            yield break;
        }

        WatersapManager.Instance.SpendWatersap(teamID, revertWatersapCost);

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

        GetComponent<Animator>()?.Play("Idle_Transform");

        Log($"ExecuteRevert - animation Idle_Transform, attente {revertAnimDuration}s");
        yield return new WaitForSeconds(revertAnimDuration);

        int currentHP = 0;
        UnitStatsComponent unitStatsComponent = GetComponent<UnitStatsComponent>();
        if (unitStatsComponent != null)
            currentHP = unitStatsComponent.GetCurrentHP();

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        GameObject budGO = Instantiate(budPrefab, transform.position, Quaternion.identity);
        BudComponent bud = budGO.GetComponent<BudComponent>();
        if (bud == null)
        {
            LogError("ExecuteRevert - budPrefab sans BudComponent");
            Destroy(budGO);
            yield break;
        }

        bud.InitializeRevert(selfPrefab, currentHP, teamID);

        Log($"ExecuteRevert - Bud de revert cree equipe {teamID}, cout {revertWatersapCost}, HP herites {currentHP}");

        if (EntitySpawnManager.Instance != null)
            EntitySpawnManager.Instance.NotifyEntityReplaced(gameObject, budGO);

        Destroy(gameObject);
    }

    [ContextMenu("Debug: Test Revert")]
    private void DebugTestRevert()
    {
        ExecuteRevert();
    }

    private void Log(string message)
    {
        if (debugRevert)
            Debug.Log($"RevertToRootbornController {gameObject.name} : {message}");
    }

    private void LogWarning(string message)
    {
        if (debugRevert)
            Debug.LogWarning($"RevertToRootbornController {gameObject.name} : {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"RevertToRootbornController {gameObject.name} : {message}");
    }
}
