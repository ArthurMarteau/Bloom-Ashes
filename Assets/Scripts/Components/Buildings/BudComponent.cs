using System.Collections;
using UnityEngine;

// Unité intermédiaire statique entre le Rootborn et l'unité de combat finale
// Spawné par TransformationController.ExecuteTransform, remplacé par l'unité finale à la fin du buildTime
// Ne se déplace pas — reste sur sa tile jusqu'à l'éclosion ou l'annulation
public class BudComponent : MonoBehaviour
{
    private const float FallbackBuildTime = 10f;

    // Unité en cours de production — assignée via Initialize
    private UnitStatsData unitInProduction;

    private UnitStatsData respawnStatsData;
    private float productionBuildTime;

    // Équipe propriétaire — propagée à l'unité finale et au Rootborn de repli
    private int teamID;

    // Temps écoulé depuis le début de la production
    private float elapsedTime;

    // Vrai si Cancel() a été appelé — empêche la coroutine de continuer
    private bool isCancelled;

    // Retourne la progression de la production (0.0 → 1.0), clampée, pour les barres de progression UI
    public float Progress
    {
        get
        {
            if (productionBuildTime <= 0f)
                return 1f;

            return Mathf.Clamp01(elapsedTime / productionBuildTime);
        }
    }

    // Retourne le ScriptableObject de l'unité en production, pour l'affichage du portrait UI
    public UnitStatsData UnitInProduction => unitInProduction;

    // Initialise le Bud avec l'unité finale à produire, les stats à restituer si annulé, les HP hérités et l'équipe propriétaire
    public void Initialize(UnitStatsData unitData, UnitStatsData respawnStatsData, int currentHP, int maxHP, int team)
    {
        unitInProduction = unitData;
        this.respawnStatsData = respawnStatsData;
        productionBuildTime = GetBuildTime(unitInProduction);
        teamID = team;
        elapsedTime = 0f;
        isCancelled = false;

        PrepareBud(currentHP, maxHP);

        TeamComponent budTeam = GetComponent<TeamComponent>();
        if (budTeam != null)
            budTeam.SetTeamID(team);

        StartCoroutine(ProductionRoutine());

        BuildingStatsComponent buildingStats = GetComponent<BuildingStatsComponent>();
        if (buildingStats != null)
            buildingStats.OnBuildingDestroyed.AddListener(OnBudKilled);
    }

    // Coroutine principale : incrémente le timer frame par frame puis déclenche l'éclosion
    private IEnumerator ProductionRoutine()
    {
        // Attente active — incrémentation manuelle pour exposer Progress à l'UI
        while (elapsedTime < productionBuildTime)
        {
            if (isCancelled) yield break;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (isCancelled) yield break;

        if (isCancelled) yield break;

        if (unitInProduction == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : ProductionRoutine — unitInProduction null, spawn final impossible", this);
            Destroy(gameObject);
            yield break;
        }

        SpawnFinalUnit();
    }

    // Libère la tile dans GridManager, instancie l'unité finale, puis détruit le Bud
    private void SpawnFinalUnit()
    {
        // Libération de la tile : désactive le Collider2D avant l'Instantiate pour que
        // la tile apparaisse libre quand l'unité finale s'enregistre dans l'EntityRegistry
        Collider2D budCollider = GetComponent<Collider2D>();
        if (budCollider != null)
            budCollider.enabled = false;

        if (unitInProduction.unitPrefab == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : SpawnFinalUnit — unitPrefab null pour {unitInProduction.unitName}", this);
            Destroy(gameObject);
            return;
        }

        GameObject spawnedUnit = Instantiate(unitInProduction.unitPrefab, transform.position, Quaternion.identity);

        TeamComponent spawnedTeam = spawnedUnit.GetComponent<TeamComponent>();
        if (spawnedTeam != null)
            spawnedTeam.SetTeamID(teamID);

        spawnedUnit.GetComponent<UnitStatsComponent>()?.NotifySpawned();

        float freezeDuration = 0f;
        UnitStatsComponent spawnedStats = spawnedUnit.GetComponent<UnitStatsComponent>();
        if (spawnedStats != null)
        {
            UnitStatsData spawnedData = spawnedStats.GetBaseStats();
            if (spawnedData?.onSpawnVFX != null && spawnedData.onSpawnVFX.spriteDuration > 0f)
                freezeDuration = spawnedData.onSpawnVFX.spriteDuration;
        }
        if (freezeDuration > 0f)
            StartCoroutine(FreezeFor(spawnedUnit, freezeDuration));

        GameLogger.Log(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : ✅ {unitInProduction.unitName} éclos (équipe {teamID})", this);

        if (EntityRegistry.Instance != null && GridManager.Instance != null)
        {
            Vector2Int spawnedCell = GridManager.Instance.WorldToGrid(spawnedUnit.transform.position);
            EntityRegistry.Instance.UpdateLogicalPosition(spawnedUnit, spawnedCell);
        }

        // Notifie le système de remplacement AVANT destruction
        if (EntitySpawnManager.Instance != null)
        {
            EntitySpawnManager.Instance.NotifyEntityReplaced(gameObject, spawnedUnit);
        }

        Destroy(gameObject);
    }

    // Annule la production : stoppe la coroutine en cours, restitue un Rootborn et détruit le Bud
    public void Cancel()
    {
        isCancelled = true;
        StopAllCoroutines();

        // Étape 1 : Récupère les HP actuels du Bud
        int budCurrentHP = 0;
        BuildingStatsComponent budStats = GetComponent<BuildingStatsComponent>();
        if (budStats != null)
            budCurrentHP = budStats.GetCurrentHP();

        // Étape 2 : Libération de la tile
        Collider2D budCollider = GetComponent<Collider2D>();
        if (budCollider != null)
            budCollider.enabled = false;

        if (respawnStatsData?.unitPrefab == null)
        {
            GameLogger.Error($"🌱 BudComponent {gameObject.name} : Cancel — respawnStatsData ou unitPrefab non assigné", this);
            Destroy(gameObject);
            return;
        }

        GameObject respawned = Instantiate(respawnStatsData.unitPrefab, transform.position, Quaternion.identity);

        TeamComponent t = respawned.GetComponent<TeamComponent>();
        if (t != null)
            t.SetTeamID(teamID);

        UnitStatsComponent stats = respawned.GetComponent<UnitStatsComponent>();
        if (stats != null)
            stats.SetCurrentHP(budCurrentHP);

        stats?.NotifySpawned();

        float freezeDuration = 0f;
        if (respawnStatsData?.onSpawnVFX != null && respawnStatsData.onSpawnVFX.spriteDuration > 0f)
            freezeDuration = respawnStatsData.onSpawnVFX.spriteDuration;
        if (freezeDuration > 0f)
            StartCoroutine(FreezeFor(respawned, freezeDuration));

        GameLogger.Log(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : production annulée — {respawned.name} restitué ({budCurrentHP} HP)", this);

        if (EntityRegistry.Instance != null && GridManager.Instance != null)
        {
            Vector2Int respawnCell = GridManager.Instance.WorldToGrid(respawned.transform.position);
            EntityRegistry.Instance.UpdateLogicalPosition(respawned, respawnCell);
        }

        // Étape 7 : Notifie le système de remplacement
        if (EntitySpawnManager.Instance != null)
            EntitySpawnManager.Instance.NotifyEntityReplaced(gameObject, respawned);

        // Étape 8 : Destruction Bud
        Destroy(gameObject);
    }

    private IEnumerator FreezeFor(GameObject unit, float duration)
    {
        yield return null; // attendre Start() de MovementController
        if (unit == null) yield break;
        UnitFreezer.Freeze(unit, disableComponents: true, freezeAnimator: false);
        yield return new WaitForSeconds(duration);
        if (unit == null) yield break;
        UnitFreezer.Unfreeze(unit, enableComponents: true, unfreezeAnimator: false);
    }

    private void OnBudKilled()
    {
        isCancelled = true;
        StopAllCoroutines();
    }

    private void PrepareBud(int currentHP, int maxHP)
    {
        // Propagation des HP au BuildingStatsComponent du Bud si présent
        BuildingStatsComponent buildingStats = GetComponent<BuildingStatsComponent>();
        if (buildingStats != null)
        {
            buildingStats.SetMaxHP(maxHP);
            buildingStats.SetCurrentHP(currentHP);
        }

        Collider2D budCollider = GetComponent<Collider2D>();
        if (budCollider != null)
            budCollider.enabled = true;

        Animator animator = GetComponent<Animator>();
        if (animator != null)
            animator.Play("Idle");
    }

    private float GetBuildTime(UnitStatsData unitData)
    {
        if (unitData == null || unitData.buildTime <= 0f)
            return FallbackBuildTime;

        return unitData.buildTime;
    }

    // Affiche l'état courant de la production depuis l'éditeur
    [ContextMenu("Debug: Production State")]
    private void DebugProductionState()
    {
        if (unitInProduction == null)
        {
            GameLogger.Log(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : non initialisé", this);
            return;
        }

        GameLogger.Log(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : {unitInProduction.unitName} | Équipe {teamID} | Progression {Progress * 100f:F0}% ({elapsedTime:F1}s / {productionBuildTime:F1}s) | Annulé : {isCancelled}", this);
    }
}
