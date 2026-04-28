using UnityEngine;

// Logique d'exécution de l'ability de transfert manuel de Watersap du Rootborn
// Délègue à RootbornStockComponent.TryManualTransfer() qui valide les conditions du GDD
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

        bool success = stockComponent.TryManualTransfer();

        if (success)
            Debug.Log($"✅ {abilityData.abilityName} : Watersap transféré manuellement");
        return success;
    }
}
