using System.Collections;
using UnityEngine;

// Unité intermédiaire statique entre le Rootborn et l'unité de combat finale
// Spawné par TransformationController.ExecuteTransform, remplacé par l'unité finale à la fin du buildTime
// Ne se déplace pas — reste sur sa tile jusqu'à l'éclosion ou l'annulation
public class BudComponent : MonoBehaviour, ITickable
{
    private const float FallbackBuildTime = 10f;

    // Unité en cours de production — assignée via Initialize
    private UnitStatsData unitInProduction;

    private UnitStatsData respawnStatsData;
    private float productionBuildTime;

    // Équipe propriétaire — propagée à l'unité finale et au Rootborn de repli
    private int teamID;

    // Temps logique de production
    private int elapsedTicks;
    private int buildTimeTicks;
    private long simulationEntityID = -1;
    private bool isProductionActive;
    private BuildingStatsComponent buildingStatsComponent;

    // Vrai si Cancel() a été appelé — empêche la coroutine de continuer
    private bool isCancelled;

    // Retourne la progression de la production (0.0 → 1.0), clampée, pour les barres de progression UI
    public float Progress
    {
        get
        {
            if (buildTimeTicks <= 0)
                return 1f;

            return Mathf.Clamp01((float)elapsedTicks / buildTimeTicks);
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
        buildTimeTicks = SimulationClock.SecondsToTicks(productionBuildTime);
        teamID = team;
        elapsedTicks = 0;
        isCancelled = false;
        isProductionActive = true;

        PrepareBud(currentHP, maxHP);

        TeamComponent budTeam = GetComponent<TeamComponent>();
        if (budTeam != null)
            budTeam.SetTeamID(team);

        buildingStatsComponent = GetComponent<BuildingStatsComponent>();
        if (buildingStatsComponent != null)
            buildingStatsComponent.OnBuildingDestroyed.AddListener(OnBudKilled);
    }

    // Enregistre le Bud après l'attribution de son entityID par BuildingStatsComponent.Awake
    private void Start()
    {
        simulationEntityID = EntityRegistry.Instance?.GetEntityID(gameObject) ?? -1;
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Register(simulationEntityID, this);
    }

    // Retire le Bud de la boucle logique et de l'événement de destruction
    private void OnDestroy()
    {
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Unregister(simulationEntityID, this);

        if (buildingStatsComponent != null)
            buildingStatsComponent.OnBuildingDestroyed.RemoveListener(OnBudKilled);
    }

    // Avance la production sur la boucle logique fixe
    public void SimTick(long tick)
    {
        if (!isActiveAndEnabled || !isProductionActive || isCancelled)
            return;

        elapsedTicks++;
        if (elapsedTicks < buildTimeTicks)
            return;

        isProductionActive = false;

        if (unitInProduction == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : production — unitInProduction null, spawn final impossible", this);
            DespawnBud(0f, true);
            return;
        }

        SpawnFinalUnit();
    }

    // Libère la tile dans GridManager, spawne l'unité finale, puis détruit le Bud
    private void SpawnFinalUnit()
    {
        // Libération de la tile : désactive le Collider2D avant le spawn pour que
        // la tile apparaisse libre quand l'unité finale s'enregistre dans l'EntityRegistry
        Collider2D budCollider = GetComponent<Collider2D>();
        if (budCollider != null)
            budCollider.enabled = false;

        if (unitInProduction.unitPrefab == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : SpawnFinalUnit — unitPrefab null pour {unitInProduction.unitName}", this);
            DespawnBud(0f, true);
            return;
        }

        if (EntitySpawnManager.Instance == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : EntitySpawnManager introuvable, spawn final impossible", this);
            DespawnBud(0f, true);
            return;
        }

        GameObject spawnedUnit = EntitySpawnManager.Instance.ReplaceEntity(gameObject, spawnCell =>
        {
            Vector3 spawnPosition = GridManager.Instance != null ? GridManager.Instance.GridToWorld(spawnCell) : Vector3.zero;
            GameObject replacement = EntitySpawnManager.Instance.SpawnEntity(unitInProduction.unitPrefab, spawnPosition, teamID);
            if (replacement == null)
                return null;

            float freezeDuration = 0f;
            UnitStatsComponent spawnedStats = replacement.GetComponent<UnitStatsComponent>();
            if (spawnedStats != null)
            {
                UnitStatsData spawnedData = spawnedStats.GetBaseStats();
                if (spawnedData?.onSpawnVFX != null && spawnedData.onSpawnVFX.spriteDuration > 0f)
                    freezeDuration = spawnedData.onSpawnVFX.spriteDuration;
            }
            if (freezeDuration > 0f)
                UnitFreezer.FreezeForTicks(replacement, SimulationClock.SecondsToTicks(freezeDuration));

            return replacement;
        });
        if (spawnedUnit == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : SpawnFinalUnit — remplacement impossible", this);
            return;
        }

        GameLogger.Log(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : ✅ {unitInProduction.unitName} éclos (équipe {teamID})", this);

    }

    // Annule la production : stoppe la coroutine en cours, restitue un Rootborn et détruit le Bud
    public void Cancel()
    {
        isCancelled = true;
        isProductionActive = false;
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
            DespawnBud(0f, true);
            return;
        }

        if (EntitySpawnManager.Instance == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : EntitySpawnManager introuvable, restitution impossible", this);
            DespawnBud(0f, true);
            return;
        }

        GameObject respawned = EntitySpawnManager.Instance.ReplaceEntity(gameObject, respawnCell =>
        {
            Vector3 respawnPosition = GridManager.Instance != null ? GridManager.Instance.GridToWorld(respawnCell) : Vector3.zero;
            GameObject replacement = EntitySpawnManager.Instance.SpawnEntity(respawnStatsData.unitPrefab, respawnPosition, teamID);
            if (replacement == null)
                return null;

            UnitStatsComponent stats = replacement.GetComponent<UnitStatsComponent>();
            if (stats != null)
                stats.SetCurrentHP(budCurrentHP);

            float freezeDuration = 0f;
            if (respawnStatsData.onSpawnVFX != null && respawnStatsData.onSpawnVFX.spriteDuration > 0f)
                freezeDuration = respawnStatsData.onSpawnVFX.spriteDuration;
            if (freezeDuration > 0f)
                UnitFreezer.FreezeForTicks(replacement, SimulationClock.SecondsToTicks(freezeDuration));

            return replacement;
        });
        if (respawned == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : Cancel — restitution impossible", this);
            return;
        }

        GameLogger.Log(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : production annulée — {respawned.name} restitué ({budCurrentHP} HP)", this);

    }

    // Détruit le Bud via le manager central après retrait simulation immédiat.
    private void DespawnBud(float visualDelay, bool notifyDestroyed)
    {
        if (EntitySpawnManager.Instance != null)
            EntitySpawnManager.Instance.DespawnEntity(gameObject, visualDelay, notifyDestroyed);
        else
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : EntitySpawnManager introuvable, Destroy non planifié", this);
            EntityRegistry.Instance?.MarkDead(gameObject);
        }
    }

    private void OnBudKilled()
    {
        isCancelled = true;
        isProductionActive = false;
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

        float elapsedSeconds = (float)elapsedTicks / SimulationClock.TICKS_PER_SECOND;
        GameLogger.Log(GameLogger.Category.Spawn, $"🌱 BudComponent {gameObject.name} : {unitInProduction.unitName} | Équipe {teamID} | Progression {Progress * 100f:F0}% ({elapsedSeconds:F1}s / {productionBuildTime:F1}s) | Annulé : {isCancelled}", this);
    }
}
