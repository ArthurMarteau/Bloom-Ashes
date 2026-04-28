using UnityEngine;

// Données de l'ability Spawn Rootborn du Nexus
// Asset à créer dans Unity : Create > Bloom and Ashes > Abilities > Spawn Rootborn
// Note : watersapCost doit rester à 0 — le coût est géré en interne par NexusController.TrySpawnRootborn()
[CreateAssetMenu(fileName = "SpawnRootborn_Ability", menuName = "Bloom and Ashes/Abilities/Spawn Rootborn")]
public class SpawnRootbornAbility : AbilityData
{
    public override IAbility CreateExecutor()
    {
        return new SpawnRootbornExecutor(this);
    }
}
