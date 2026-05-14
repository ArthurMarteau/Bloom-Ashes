using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Detecte le batiment compatible adjacent et execute la transformation via Bud.
// Rootborn -> batiment de faction non-Nexus. Autre unite -> Nexus allie.
public class TransformationController : MonoBehaviour
{
    [SerializeField] private GameObject budPrefab;
    [SerializeField] private float transformAnimDuration = 1f;
    [SerializeField] private bool debugTransform = true;

    public BuildingStatsComponent FindAdjacentCompatibleBuilding()
    {
        TeamComponent ownTeam = GetComponent<TeamComponent>();
        if (ownTeam == null)
        {
            LogWarning("FindAdjacentCompatibleBuilding - TeamComponent manquant");
            return null;
        }

        if (GridManager.Instance == null)
        {
            LogWarning("FindAdjacentCompatibleBuilding - GridManager introuvable");
            return null;
        }

        int teamID = ownTeam.GetTeamID();
        bool isRootborn = GetComponent<RootbornStockComponent>() != null;
        Vector2Int unitCell = GridManager.Instance.WorldToGrid(transform.position);
        Vector2Int[] offsets = RangeOffsets.GetOffsets(1);

        foreach (Vector2Int offset in offsets)
        {
            Vector2Int adjacentCell = unitCell + offset;
            GameObject buildingObject = isRootborn
                ? GridManager.Instance.GetBuildingAt(adjacentCell)
                : GridManager.Instance.GetNexusAt(adjacentCell);

            if (buildingObject == null) continue;

            BuildingStatsComponent buildingStats = buildingObject.GetComponent<BuildingStatsComponent>();
            if (buildingStats == null) continue;

            BuildingStatsData baseStats = buildingStats.GetBaseStats();
            if (baseStats == null) continue;
            if (isRootborn && baseStats.isNexus) continue;

            TeamComponent buildingTeam = buildingObject.GetComponent<TeamComponent>();
            if (buildingTeam == null || buildingTeam.GetTeamID() != teamID) continue;

            Log($"FindAdjacentCompatibleBuilding - batiment compatible trouve en {adjacentCell}");
            return buildingStats;
        }

        Log("FindAdjacentCompatibleBuilding - aucun batiment compatible adjacent");
        return null;
    }

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

        return FindAdjacentCompatibleBuilding() != null
            && WatersapManager.Instance.CanSpend(teamComponent.GetTeamID(), unitData.watersapCost);
    }

    public void ExecuteTransform(UnitStatsData unitData)
    {
        StartCoroutine(ExecuteTransformRoutine(unitData));
    }

    private IEnumerator ExecuteTransformRoutine(UnitStatsData unitData)
    {
        if (unitData == null)
        {
            LogWarning("ExecuteTransform - unitData null");
            yield break;
        }

        TeamComponent teamComponent = GetComponent<TeamComponent>();
        if (teamComponent == null)
        {
            LogWarning("ExecuteTransform - TeamComponent manquant");
            yield break;
        }

        int teamID = teamComponent.GetTeamID();

        if (budPrefab == null)
        {
            LogError("ExecuteTransform - budPrefab non assigne");
            yield break;
        }

        UnitStatsData selfData = GetComponent<UnitStatsComponent>()?.GetBaseStats();
        if (selfData == null)
        {
            LogError("ExecuteTransform - UnitStatsData introuvable sur cette unite");
            yield break;
        }

        if (WatersapManager.Instance == null)
        {
            LogError("ExecuteTransform - WatersapManager introuvable");
            yield break;
        }

        if (!CanTransform(unitData))
        {
            Log($"ExecuteTransform refuse - batiment compatible absent ou Watersap insuffisant pour {unitData.unitName}");
            yield break;
        }

        WatersapManager.Instance.SpendWatersap(teamID, unitData.watersapCost);

        UnitFreezer.Freeze(gameObject, disableComponents: true, freezeAnimator: false);

        GetComponent<Animator>()?.Play("Idle_Transform");

        bool isRootborn = GetComponent<RootbornStockComponent>() != null;
        if (!isRootborn && VFXManager.Instance != null)
            VFXManager.Instance.PlayUnitRevertStartVFX(gameObject);

        Log($"ExecuteTransform - animation Idle_Transform, attente {transformAnimDuration}s");
        yield return new WaitForSeconds(transformAnimDuration);

        UnitStatsComponent unitStatsComponent = GetComponent<UnitStatsComponent>();
        int currentHP = unitStatsComponent?.GetCurrentHP() ?? 0;
        int maxHP = unitStatsComponent?.GetMaxHP() ?? 0;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        GameObject budObject = Instantiate(budPrefab, transform.position, Quaternion.identity);
        BudComponent budComponent = budObject.GetComponent<BudComponent>();
        if (budComponent == null)
        {
            LogError("ExecuteTransform - budPrefab sans BudComponent");
            Destroy(budObject);
            yield break;
        }

        budComponent.Initialize(unitData, selfData, currentHP, maxHP, teamID);

        Log($"ExecuteTransform - Bud cree vers {unitData.unitName} (equipe {teamID}, cout {unitData.watersapCost}, HP herites {currentHP}/{maxHP})");

        if (EntitySpawnManager.Instance != null)
            EntitySpawnManager.Instance.NotifyEntityReplaced(gameObject, budObject);

        Destroy(gameObject);
    }

    [ContextMenu("Debug: Test Transform")]
    private void DebugTestTransform()
    {
        BuildingStatsComponent building = FindAdjacentCompatibleBuilding();
        List<UnitStatsData> units = building?.GetBaseStats()?.producibleUnits;

        if (units == null || units.Count == 0)
        {
            Log("DebugTestTransform - aucune unite transformable");
            return;
        }

        ExecuteTransform(units[0]);
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
