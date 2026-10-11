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

    // Ignore strictement toute commande supplémentaire tant que le Nexus porte déjà une intention.
    public override bool CanExecute(GameObject caster)
    {
        NexusController nexus = caster != null ? caster.GetComponent<NexusController>() : null;
        return nexus != null && !nexus.HasPendingSpawn && base.CanExecute(caster);
    }

    // Affiche le refus Watersap uniquement pour le joueur propriétaire local du Nexus.
    protected override void HandleInsufficientWatersap(int teamID)
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.IsLocalPlayer(teamID))
            ToastUI.Instance?.Show("Watersap insuffisant", ToastUI.InsufficientWatersapColor);
    }

    protected override bool ExecuteAbility(GameObject caster)
    {
        NexusController nexus = caster.GetComponent<NexusController>();
        if (nexus == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"⚠️ SpawnRootbornExecutor : NexusController manquant sur {caster.name}", null);
            return false;
        }

        // Le Nexus débite le coût porté par son AbilityData afin de servir aussi la commande directe.
        bool success = nexus.ExecuteSpawnCommand();

        if (success)
            GameLogger.Log(GameLogger.Category.Spawn, $"✅ {abilityData.abilityName} : Rootborn spawné avec succès", null);
        return success;
    }
}
