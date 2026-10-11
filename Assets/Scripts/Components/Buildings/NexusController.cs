using UnityEngine;

public enum RootbornSpawnResult
{
    Success,
    Occupied,
    InsufficientWatersap,
    UnitCapReached,
    InvalidConfiguration,
    SpawnFailed
}

// Contrôleur du Nexus — spawn les Rootborn sur la tile du bâtiment.
// La file d'intention de profondeur 1 est rejouée exclusivement dans la simulation.
public class NexusController : MonoBehaviour, ITickable
{
    [Header("Feedback")]
    [SerializeField] private AudioClip spawnSFX;
    [SerializeField] private GameObject spawnVFX;

    [Header("Debug")]
    [SerializeField] private bool debugNexus = true;
    [SerializeField] private bool debugNexusInput = true;

    private BuildingStatsData baseStats;
    private Collider2D _collider;
    private SelectionManager selectionManager;
    private TeamComponent teamComponent;
    private long simulationEntityID = -1;
    private bool pendingSpawn;

    public bool HasPendingSpawn => pendingSpawn;

    // Récupère et valide les données au démarrage.
    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        teamComponent = GetComponent<TeamComponent>();

        BuildingStatsComponent buildingStats = GetComponent<BuildingStatsComponent>();
        if (buildingStats == null)
        {
            GameLogger.Error($"🏛️ NexusController {gameObject.name} : BuildingStatsComponent manquant !", this);
            enabled = false;
            return;
        }

        baseStats = buildingStats.GetBaseStats();
        if (baseStats == null)
        {
            GameLogger.Error($"🏛️ NexusController {gameObject.name} : BuildingStatsData non assigné !", this);
            enabled = false;
            return;
        }

        if (!baseStats.isNexus)
        {
            GameLogger.Error($"🏛️ NexusController {gameObject.name} : isNexus = false dans {baseStats.buildingName} — ce contrôleur ne doit être ajouté qu'au Nexus !", this);
            enabled = false;
            return;
        }

        if (debugNexus)
            GameLogger.Log(GameLogger.Category.Spawn, $"🏛️ NexusController {gameObject.name} : initialisé ({baseStats.buildingName})", this);
    }

    // S'abonne à l'input local et enregistre le Nexus dans la boucle logique stable.
    private void Start()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnRightClickDown.AddListener(OnRightClickHandler);

        selectionManager = Object.FindFirstObjectByType<SelectionManager>();
        simulationEntityID = EntityRegistry.Instance?.GetEntityID(gameObject) ?? -1;
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Register(simulationEntityID, this);
    }

    // Désabonne les callbacks et retire le Nexus de la boucle logique.
    private void OnDestroy()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.OnRightClickDown.RemoveListener(OnRightClickHandler);

        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Unregister(simulationEntityID, this);
    }

    // Déclenche une commande si le clic droit vise le Nexus localement sélectionné.
    private void OnRightClickHandler(Vector3 worldPosition)
    {
        if (_collider == null || !_collider.OverlapPoint(worldPosition))
            return;
        if (selectionManager == null || !IsThisNexusSelected())
            return;

        if (debugNexusInput)
            GameLogger.Log(GameLogger.Category.Spawn,
                $"️ NexusController {gameObject.name} : clic droit détecté sur Nexus sélectionné", this);

        SubmitSpawnCommand();
    }

    // Sérialise l'intention ; aucune ressource n'est débitée avant son exécution.
    private void SubmitSpawnCommand()
    {
        if (CommandExecutor.Instance == null || EntityRegistry.Instance == null || teamComponent == null)
            return;

        long nexusEntityID = EntityRegistry.Instance.GetEntityID(gameObject);
        if (nexusEntityID < 0)
            return;

        CommandExecutor.Instance.Submit(new GameCommand
        {
            playerID = teamComponent.GetTeamID(),
            type = CommandType.SpawnRootborn,
            targetEntityID = nexusEntityID
        });
    }

    // Vérifie que le bâtiment sélectionné est bien ce Nexus.
    private bool IsThisNexusSelected()
    {
        return selectionManager.GetSelectedBuilding() == gameObject;
    }

    // Exécute une commande et mémorise uniquement un refus temporaire par occupation.
    public bool ExecuteSpawnCommand()
    {
        if (pendingSpawn)
            return false;

        RootbornSpawnResult result = TrySpawnRootborn();
        if (result == RootbornSpawnResult.Occupied)
            pendingSpawn = true;

        return result == RootbornSpawnResult.Success;
    }

    // Retente l'intention dans la simulation, sans débiter tant que toutes les règles ne passent pas.
    public void SimTick(long tick)
    {
        if (!pendingSpawn || !isActiveAndEnabled)
            return;

        EntityRegistry registry = EntityRegistry.Instance;
        SpawnRootbornAbility spawnAbility = baseStats?.abilityData as SpawnRootbornAbility;
        if (registry == null || teamComponent == null || spawnAbility == null
            || !registry.TryGetLogicalPosition(gameObject, out Vector2Int nexusTile))
        {
            pendingSpawn = false;
            return;
        }

        if (registry.GetUnitAtLogicalPosition(nexusTile) != null)
            return;

        int teamID = teamComponent.GetTeamID();
        if (registry.CountUnitsForTeam(teamID) >= MatchRules.MAX_UNITS_PER_PLAYER
            || WatersapManager.Instance == null
            || !WatersapManager.Instance.CanSpend(teamID, spawnAbility.watersapCost))
        {
            return;
        }

        RootbornSpawnResult result = TrySpawnRootborn();
        if (result == RootbornSpawnResult.Success)
            pendingSpawn = false;
        else if (result != RootbornSpawnResult.Occupied
            && result != RootbornSpawnResult.InsufficientWatersap
            && result != RootbornSpawnResult.UnitCapReached)
            pendingSpawn = false;
    }

    // Tente le spawn effectif et expose précisément sa cause de refus.
    public RootbornSpawnResult TrySpawnRootborn()
    {
        if (teamComponent == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn,
                $"🏛️ NexusController {gameObject.name} : TeamComponent manquant — spawn annulé", this);
            return RootbornSpawnResult.InvalidConfiguration;
        }
        int teamID = teamComponent.GetTeamID();

        if (baseStats.producibleUnits == null || baseStats.producibleUnits.Count == 0)
        {
            GameLogger.Warn(GameLogger.Category.Spawn,
                $"🏛️ NexusController {gameObject.name} : producibleUnits vide — aucun Rootborn à spawner", this, teamID);
            return RootbornSpawnResult.InvalidConfiguration;
        }

        UnitStatsData rootbornData = baseStats.producibleUnits[0];
        if (rootbornData == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn,
                $"🏛️ NexusController {gameObject.name} : producibleUnits[0] est null", this, teamID);
            return RootbornSpawnResult.InvalidConfiguration;
        }

        if (rootbornData.unitPrefab == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn,
                $"🏛️ NexusController {gameObject.name} : unitPrefab manquant sur {rootbornData.unitName}", this, teamID);
            return RootbornSpawnResult.InvalidConfiguration;
        }

        SpawnRootbornAbility spawnAbility = baseStats.abilityData as SpawnRootbornAbility;
        if (spawnAbility == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn,
                $"🏛️ NexusController {gameObject.name} : SpawnRootbornAbility manquante — coût de spawn indéterminé", this, teamID);
            return RootbornSpawnResult.InvalidConfiguration;
        }

        if (EntitySpawnManager.Instance == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn,
                $"🏛️ NexusController {gameObject.name} : EntitySpawnManager introuvable — spawn annulé", this, teamID);
            return RootbornSpawnResult.InvalidConfiguration;
        }

        EntityRegistry registry = EntityRegistry.Instance;
        if (registry == null || !registry.TryGetLogicalPosition(gameObject, out Vector2Int nexusTile))
        {
            GameLogger.Warn(GameLogger.Category.Spawn,
                $"🏛️ NexusController {gameObject.name} : position logique introuvable — spawn annulé", this, teamID);
            return RootbornSpawnResult.InvalidConfiguration;
        }

        if (registry.GetNexusAtLogicalPosition(nexusTile) != gameObject)
        {
            GameLogger.Warn(GameLogger.Category.Spawn,
                $"🏛️ NexusController {gameObject.name} : Nexus absent de l'index logique en {nexusTile} — spawn annulé", this, teamID);
            return RootbornSpawnResult.InvalidConfiguration;
        }

        if (registry.CountUnitsForTeam(teamID) >= MatchRules.MAX_UNITS_PER_PLAYER)
        {
            GameLogger.Warn(GameLogger.Category.Match,
                $"NexusController {gameObject.name} : limite de {MatchRules.MAX_UNITS_PER_PLAYER} unités atteinte pour l'équipe {teamID} — spawn Rootborn refusé", this, teamID);
            if (PlayerManager.Instance != null && PlayerManager.Instance.IsLocalPlayer(teamID))
                ToastUI.Instance?.Show($"Limite d'unités atteinte ({MatchRules.MAX_UNITS_PER_PLAYER})", ToastUI.UnitLimitColor);
            return RootbornSpawnResult.UnitCapReached;
        }

        int spawnCost = spawnAbility.watersapCost;
        if (WatersapManager.Instance == null
            || !WatersapManager.Instance.CanSpend(teamID, spawnCost))
        {
            if (debugNexus)
                GameLogger.Warn(GameLogger.Category.Spawn,
                    $"🏛️ NexusController {gameObject.name} : Watersap insuffisant pour spawner {rootbornData.unitName} (coût : {spawnCost})", this, teamID);
            if (PlayerManager.Instance != null && PlayerManager.Instance.IsLocalPlayer(teamID))
                ToastUI.Instance?.Show("Watersap insuffisant", ToastUI.InsufficientWatersapColor);
            return RootbornSpawnResult.InsufficientWatersap;
        }

        GameObject occupant = registry.GetUnitAtLogicalPosition(nexusTile);
        if (occupant != null)
        {
            if (debugNexus)
                GameLogger.Warn(GameLogger.Category.Spawn,
                    $"️ NexusController {gameObject.name} : case {nexusTile} occupée par {occupant.name} — spawn mis en attente", this, teamID);
            return RootbornSpawnResult.Occupied;
        }

        if (!WatersapManager.Instance.SpendWatersap(teamID, spawnCost, SpendReason.Production))
            return RootbornSpawnResult.InsufficientWatersap;

        GameObject spawnedUnit = EntitySpawnManager.Instance.SpawnEntity(
            rootbornData.unitPrefab, GridManager.Instance.GridToWorld(nexusTile), teamID);
        if (spawnedUnit == null)
            return RootbornSpawnResult.SpawnFailed;

        if (debugNexus)
            GameLogger.Log(GameLogger.Category.Spawn,
                $"🏛️ NexusController {gameObject.name} : Rootborn spawné (équipe {teamID}, coût {spawnCost} Watersap)", this, teamID);

        EntitySpawnManager.Instance.NotifyEntitySpawned(gameObject, spawnedUnit);
        return RootbornSpawnResult.Success;
    }

    // Teste la commande de spawn depuis l'éditeur.
    [ContextMenu("Test: Spawn Rootborn")]
    private void TestSpawnRootborn()
    {
        ExecuteSpawnCommand();
    }
}
