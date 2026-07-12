using UnityEngine;

// Singleton gérant l'exécution de toutes les abilities
// Point central pour appeler une ability depuis l'UI ou tout autre système
public class AbilityManager : MonoBehaviour
{
    public static AbilityManager Instance { get; private set; }

    public const int ENTITY_ABILITY_INDEX = 0;
    public const int SACRIFICE_ABILITY_INDEX = 1;
    [SerializeField] private SacrificeAbility sacrificeAbilityData;

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

    // Tente d'exécuter l'ability d'une entité (Nexus, Unité, etc.)
    public void TryExecuteAbility(GameObject caster, AbilityData abilityData)
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

        IAbility ability = abilityData.CreateExecutor();
        if (!ability.CanExecute(caster)) return;

        if (ability.Execute(caster) && abilityData.vfxOnExecute != null)
            VFXApplier.Apply(abilityData.vfxOnExecute, caster);
    }

    // Résout puis exécute une ability sérialisée par son index de commande.
    public void TryExecuteAbility(GameObject caster, int abilityIndex)
    {
        AbilityData abilityData = ResolveAbilityData(caster, abilityIndex);
        TryExecuteAbility(caster, abilityData);
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

        IAbility ability = abilityData.CreateExecutor();
        return ability.CanExecute(caster);
    }
}
