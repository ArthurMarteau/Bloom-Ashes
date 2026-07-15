using UnityEngine;

// Logique d'exécution de l'ability Spawn Rootborn du Nexus
// Délègue à NexusController.TrySpawnRootborn(), qui débite l'unique coût défini par l'ability.
public class SpawnRootbornExecutor : AbilityExecutor
{
    protected override bool SpendWatersapCost => false;

    public SpawnRootbornExecutor(
        AbilityData data,
        long lastUsedTick,
        System.Action<long> recordLastUsedTick)
        : base(data, lastUsedTick, recordLastUsedTick) { }

    protected override bool ExecuteAbility(GameObject caster)
    {
        NexusController nexus = caster.GetComponent<NexusController>();
        if (nexus == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"⚠️ SpawnRootbornExecutor : NexusController manquant sur {caster.name}", null);
            return false;
        }

        // Le Nexus débite le coût porté par son AbilityData afin de servir aussi la commande directe.
        bool success = nexus.TrySpawnRootborn();

        if (success)
            GameLogger.Log(GameLogger.Category.Spawn, $"✅ {abilityData.abilityName} : Rootborn spawné avec succès", null);
        return success;
    }
}
