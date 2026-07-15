using UnityEngine;

// ScriptableObject de données pour l'ability de transfert manuel de Watersap du Rootborn
// Coût = 0, Cooldown = 0 — la logique de disponibilité est gérée par RootbornStockComponent
[CreateAssetMenu(fileName = "WatersapTransfer_Ability", menuName = "Bloom and Ashes/Abilities/Watersap Transfer")]
public class WatersapTransferAbility : AbilityData
{
    // Crée l'executor associé à cette ability
    public override IAbility CreateExecutor(long lastUsedTick, System.Action<long> recordLastUsedTick)
    {
        return new WatersapTransferExecutor(this, lastUsedTick, recordLastUsedTick);
    }
}
