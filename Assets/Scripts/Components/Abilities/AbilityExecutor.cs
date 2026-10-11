using UnityEngine;

// Classe abstraite de base pour toutes les logiques d'ability
// Fournit la gestion du cooldown et la vérification Watersap
// Les abilities concrètes implémentent ExecuteAbility()
public abstract class AbilityExecutor : IAbility
{
    protected AbilityData abilityData;
    private readonly long lastUsedTick;
    private readonly System.Action<long> recordLastUsedTick;
    private readonly int cooldownTicks;

    // Indique si la classe de base doit débiter le coût avant la logique spécifique.
    protected virtual bool SpendWatersapCost => true;

    protected AbilityExecutor(
        AbilityData data,
        long lastUsedTick,
        System.Action<long> recordLastUsedTick)
    {
        abilityData = data;
        this.lastUsedTick = lastUsedTick;
        this.recordLastUsedTick = recordLastUsedTick;
        cooldownTicks = data != null ? SimulationClock.SecondsToTicks(data.cooldownDuration) : 0;
    }

    // Retourne les données SO de l'ability
    public AbilityData GetData() => abilityData;

    // Vérifie si l'ability peut être exécutée (cooldown + Watersap)
    public virtual bool CanExecute(GameObject caster)
    {
        // Vérifie le cooldown
        if (cooldownTicks > 0 && lastUsedTick >= 0)
        {
            long currentTick = SimulationClockManager.Instance != null ? SimulationClockManager.Instance.CurrentTick : 0;
            long elapsedTicks = currentTick - lastUsedTick;
            if (elapsedTicks < cooldownTicks)
            {
                float remainingSeconds = (float)(cooldownTicks - elapsedTicks) / SimulationClock.TICKS_PER_SECOND;
                GameLogger.Log(GameLogger.Category.UI, $"⏳ {abilityData.abilityName} en cooldown ({remainingSeconds:F1}s restantes)", null);
                return false;
            }
        }

        // Vérifie le Watersap si un coût est défini
        if (abilityData.watersapCost > 0)
        {
            TeamComponent team = caster.GetComponent<TeamComponent>();
            if (team == null) return false;

            if (!WatersapManager.Instance.CanSpend(team.GetTeamID(), abilityData.watersapCost))
            {
                GameLogger.Log(GameLogger.Category.UI, $"💧 Watersap insuffisant pour {abilityData.abilityName} (coût : {abilityData.watersapCost})", null);
                HandleInsufficientWatersap(team.GetTeamID());
                return false;
            }
        }

        return true;
    }

    // Laisse une ability spécialisée présenter un feedback local sans modifier la validation.
    protected virtual void HandleInsufficientWatersap(int teamID) { }

    // Exécute l'ability avec une validation unique, puis mémorise le cooldown en cas de succès.
    public bool Execute(GameObject caster)
    {
        if (!CanExecute(caster)) return false;

        // Dépense le Watersap si un coût est défini
        if (SpendWatersapCost && abilityData.watersapCost > 0)
        {
            TeamComponent team = caster.GetComponent<TeamComponent>();
            if (team != null)
                WatersapManager.Instance.SpendWatersap(team.GetTeamID(), abilityData.watersapCost, SpendReason.Ability);
        }

        if (!ExecuteAbility(caster))
            return false;

        long currentTick = SimulationClockManager.Instance != null
            ? SimulationClockManager.Instance.CurrentTick
            : 0;
        recordLastUsedTick?.Invoke(currentTick);
        return true;
    }

    // Méthode abstraite : chaque ability concrète implémente sa logique ici
    // Retourne true si l'action a réellement eu lieu, false si elle a été rejetée en interne
    protected abstract bool ExecuteAbility(GameObject caster);
}
