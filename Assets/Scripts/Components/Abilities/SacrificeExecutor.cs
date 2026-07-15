using UnityEngine;

public class SacrificeExecutor : AbilityExecutor
{
    private readonly SacrificeAbility sacrificeData;

    public SacrificeExecutor(
        SacrificeAbility data,
        long lastUsedTick,
        System.Action<long> recordLastUsedTick)
        : base(data, lastUsedTick, recordLastUsedTick)
    {
        sacrificeData = data;
    }

    public override bool CanExecute(GameObject caster)
    {
        if (!base.CanExecute(caster))
            return false;

        UnitStatsComponent stats = caster.GetComponent<UnitStatsComponent>();
        return stats != null && !stats.IsDead();
    }

    protected override bool ExecuteAbility(GameObject caster)
    {
        UnitFreezer.Freeze(caster, disableComponents: false, freezeAnimator: true);

        TeamComponent teamComponent = caster.GetComponent<TeamComponent>();
        if (teamComponent == null || WatersapManager.Instance == null)
            return false;

        WatersapManager.Instance.AddWatersap(teamComponent.GetTeamID(), sacrificeData.watersapGranted);
        GameLogger.Log(GameLogger.Category.Watersap, $" Sacrifice {caster.name} : +{sacrificeData.watersapGranted} Watersap", null);

        EntitySpawnManager.Instance?.DespawnEntity(caster, 1f);
        return true;
    }
}
