using System.Collections.Generic;
using UnityEngine;

// Singleton gérant l'exécution de toutes les abilities
// Point central pour appeler une ability depuis l'UI ou tout autre système
public class AbilityManager : MonoBehaviour
{
    public static AbilityManager Instance { get; private set; }

    public const int ENTITY_ABILITY_INDEX = 0;
    public const int SACRIFICE_ABILITY_INDEX = 1;
    [SerializeField] private SacrificeAbility sacrificeAbilityData;
    private readonly Dictionary<(long entityID, int abilityIndex), long> lastUsedTicks =
        new Dictionary<(long entityID, int abilityIndex), long>();
    private readonly Dictionary<GameObject, long> cooldownEntityIDs =
        new Dictionary<GameObject, long>();

    // Initialise le singleton
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // S'abonne au départ des entités afin de libérer leur état de cooldown.
    private void Start()
    {
        if (EntitySpawnManager.Instance != null)
            EntitySpawnManager.Instance.OnEntityDespawned.AddListener(HandleEntityDespawned);
    }

    // Tente d'exécuter l'ability d'une entité (Nexus, Unité, etc.)
    public void TryExecuteAbility(GameObject caster, AbilityData abilityData)
    {
        TryExecuteAbility(caster, abilityData, ResolveAbilityIndex(caster, abilityData));
    }

    // Exécute une ability identifiée par la même clé que la commande lockstep.
    private void TryExecuteAbility(GameObject caster, AbilityData abilityData, int abilityIndex)
    {
        if (caster == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, "⚠️ AbilityManager : Caster null", this);
            return;
        }

        if (abilityData == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, $"⚠️ AbilityManager : AbilityData null sur {caster.name}", this);
            return;
        }

        if (!TryCreateExecutor(caster, abilityData, abilityIndex, out IAbility ability))
            return;

        if (ability.Execute(caster) && abilityData.vfxOnExecute != null)
            VFXApplier.Apply(abilityData.vfxOnExecute, caster);
    }

    // Résout puis exécute une ability sérialisée par son index de commande.
    public void TryExecuteAbility(GameObject caster, int abilityIndex)
    {
        AbilityData abilityData = ResolveAbilityData(caster, abilityIndex);
        TryExecuteAbility(caster, abilityData, abilityIndex);
    }

    // Enregistre l'asset de sacrifice fourni par l'UI de la scène.
    public void RegisterSacrificeAbility(SacrificeAbility abilityData)
    {
        if (abilityData != null)
            sacrificeAbilityData = abilityData;
    }

    // Retourne l'index de commande d'une ability portée par l'entité si son type correspond.
    public bool TryGetEntityAbilityIndex<TAbility>(GameObject caster, out int abilityIndex) where TAbility : AbilityData
    {
        abilityIndex = -1;
        AbilityData abilityData = ResolveAbilityData(caster, ENTITY_ABILITY_INDEX);
        if (abilityData is TAbility)
        {
            abilityIndex = ENTITY_ABILITY_INDEX;
            return true;
        }

        return false;
    }

    // Retourne l'ability correspondant au contrat d'index des commandes.
    private AbilityData ResolveAbilityData(GameObject caster, int abilityIndex)
    {
        if (caster == null)
            return null;
        if (abilityIndex == SACRIFICE_ABILITY_INDEX)
            return sacrificeAbilityData;
        if (abilityIndex != ENTITY_ABILITY_INDEX)
            return null;

        BuildingStatsComponent buildingStats = caster.GetComponent<BuildingStatsComponent>();
        if (buildingStats != null)
            return buildingStats.GetBaseStats()?.abilityData;

        UnitStatsComponent unitStats = caster.GetComponent<UnitStatsComponent>();
        return unitStats?.GetBaseStats()?.abilityData;
    }

    // Vérifie si une ability peut être exécutée (pour griser un bouton UI)
    public bool CanExecuteAbility(GameObject caster, AbilityData abilityData)
    {
        if (caster == null || abilityData == null) return false;

        int abilityIndex = ResolveAbilityIndex(caster, abilityData);
        return TryCreateExecutor(caster, abilityData, abilityIndex, out IAbility ability)
            && ability.CanExecute(caster);
    }

    // Crée un executor éphémère branché sur l'état durable du couple entité/ability.
    private bool TryCreateExecutor(
        GameObject caster,
        AbilityData abilityData,
        int abilityIndex,
        out IAbility ability)
    {
        ability = null;
        if (caster == null || abilityData == null || abilityIndex < 0 || EntityRegistry.Instance == null)
            return false;

        long entityID = EntityRegistry.Instance.GetEntityID(caster);
        if (entityID < 0)
            return false;

        cooldownEntityIDs[caster] = entityID;
        (long entityID, int abilityIndex) key = (entityID, abilityIndex);
        long lastUsedTick = lastUsedTicks.TryGetValue(key, out long storedTick) ? storedTick : -1;
        ability = abilityData.CreateExecutor(lastUsedTick, tick => lastUsedTicks[key] = tick);
        return ability != null;
    }

    // Résout l'index canonique lorsqu'un appel interne fournit directement le SO.
    private int ResolveAbilityIndex(GameObject caster, AbilityData abilityData)
    {
        if (abilityData == null)
            return -1;
        if (abilityData == sacrificeAbilityData)
            return SACRIFICE_ABILITY_INDEX;
        if (ResolveAbilityData(caster, ENTITY_ABILITY_INDEX) == abilityData)
            return ENTITY_ABILITY_INDEX;

        return -1;
    }

    // Supprime tous les cooldowns associés à l'entité qui quitte la simulation.
    private void HandleEntityDespawned(GameObject entity)
    {
        if (entity == null || !cooldownEntityIDs.TryGetValue(entity, out long entityID))
            return;

        cooldownEntityIDs.Remove(entity);

        List<(long entityID, int abilityIndex)> keysToRemove =
            new List<(long entityID, int abilityIndex)>();
        foreach (var key in lastUsedTicks.Keys)
        {
            if (key.entityID == entityID)
                keysToRemove.Add(key);
        }

        foreach (var key in keysToRemove)
            lastUsedTicks.Remove(key);
    }

    // Désabonne le manager et nettoie son état durable.
    private void OnDestroy()
    {
        if (EntitySpawnManager.Instance != null)
            EntitySpawnManager.Instance.OnEntityDespawned.RemoveListener(HandleEntityDespawned);

        lastUsedTicks.Clear();
        cooldownEntityIDs.Clear();
        if (Instance == this)
            Instance = null;
    }
}
