using UnityEngine;

// Logique d'exécution de l'ability de transfert de Watersap du Rootborn (auto et manuel)
// Délègue à RootbornStockComponent.TryTransfer()
public class WatersapTransferExecutor : AbilityExecutor
{
    public WatersapTransferExecutor(AbilityData data) : base(data) { }

    protected override bool ExecuteAbility(GameObject caster)
    {
        RootbornStockComponent stockComponent = caster.GetComponent<RootbornStockComponent>();
        if (stockComponent == null)
        {
            Debug.LogWarning($"⚠️ WatersapTransferExecutor : RootbornStockComponent manquant sur {caster.name}");
            return false;
        }

        bool success = stockComponent.TryTransfer();

        if (success)
            Debug.Log($"✅ {abilityData.abilityName} : Watersap transféré");
        return success;
    }
}
