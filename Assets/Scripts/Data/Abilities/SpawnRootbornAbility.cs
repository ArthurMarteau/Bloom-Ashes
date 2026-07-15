using UnityEngine;

// Données de l'ability Spawn Rootborn du Nexus
// Asset à créer dans Unity : Create > Bloom and Ashes > Abilities > Spawn Rootborn
// watersapCost est la source unique du coût de spawn, débité par NexusController.TrySpawnRootborn().
[CreateAssetMenu(fileName = "SpawnRootborn_Ability", menuName = "Bloom and Ashes/Abilities/Spawn Rootborn")]
public class SpawnRootbornAbility : AbilityData
{
    public override IAbility CreateExecutor(long lastUsedTick, System.Action<long> recordLastUsedTick)
    {
        return new SpawnRootbornExecutor(this, lastUsedTick, recordLastUsedTick);
    }
}
