using UnityEngine;

// Logique d'exécution de l'ability Spawn Rootborn du Nexus
// Délègue à NexusController.TrySpawnRootborn() qui gère déjà le coût Watersap en interne
// Le watersapCost dans SpawnRootbornAbility doit donc rester à 0
public class SpawnRootbornExecutor : AbilityExecutor
{
    public SpawnRootbornExecutor(AbilityData data) : base(data) { }

    protected override bool ExecuteAbility(GameObject caster)
    {
        NexusController nexus = caster.GetComponent<NexusController>();
        if (nexus == null)
        {
            GameLogger.Warn(GameLogger.Category.Spawn, $"⚠️ SpawnRootbornExecutor : NexusController manquant sur {caster.name}", null);
            return false;
        }

        // TrySpawnRootborn gère la vérification et la dépense du Watersap en interne
        bool success = nexus.TrySpawnRootborn();

        if (success)
            GameLogger.Log(GameLogger.Category.Spawn, $"✅ {abilityData.abilityName} : Rootborn spawné avec succès", null);
        return success;
    }
}
