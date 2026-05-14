using UnityEngine;

// Logique d'exécution de l'ability de transfert de Watersap du Rootborn (auto et manuel)
// Délègue à RootbornStockComponent.TryTransfer()
public class WatersapTransferExecutor : AbilityExecutor
{
    public WatersapTransferExecutor(AbilityData data) : base(data) { }

    public override bool CanExecute(GameObject caster)
    {
        if (!base.CanExecute(caster))
            return false;

        MovementController movementController = caster.GetComponent<MovementController>();
        if (movementController == null)
            return false;

        MovementController.MovementState state = movementController.GetCurrentState();
        if (state != MovementController.MovementState.Idle)
        {
            GameLogger.Log(GameLogger.Category.Watersap, $"⏳ WatersapTransfer bloqué : Rootborn non idle ({state})", null);
            return false;
        }

        return true;
    }

    protected override bool ExecuteAbility(GameObject caster)
    {
        RootbornStockComponent stockComponent = caster.GetComponent<RootbornStockComponent>();
        if (stockComponent == null)
        {
            GameLogger.Warn(GameLogger.Category.Watersap, $"⚠️ WatersapTransferExecutor : RootbornStockComponent manquant sur {caster.name}", null);
            return false;
        }

        bool success = stockComponent.TryTransfer();

        if (success)
            GameLogger.Log(GameLogger.Category.Watersap, $"✅ {abilityData.abilityName} : Watersap transféré", null);
        return success;
    }
}
