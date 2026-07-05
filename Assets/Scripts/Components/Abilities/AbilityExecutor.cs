using UnityEngine;

// Classe abstraite de base pour toutes les logiques d'ability
// Fournit la gestion du cooldown et la vérification Watersap
// Les abilities concrètes implémentent ExecuteAbility()
public abstract class AbilityExecutor : IAbility
{
    protected AbilityData abilityData;
    protected long lastUsedTick = -1;
    protected int cooldownTicks;

    public AbilityExecutor(AbilityData data)
    {
        abilityData = data;
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
                return false;
            }
        }

        return true;
    }

    // Exécute l'ability : vérifie les conditions, dépense le Watersap, puis délègue à ExecuteAbility()
    // Retourne true si l'action interne a réussi, false si CanExecute échoue ou si l'action est rejetée
    public bool Execute(GameObject caster)
    {
        if (!CanExecute(caster)) return false;

        // Dépense le Watersap si un coût est défini
        if (abilityData.watersapCost > 0)
        {
            TeamComponent team = caster.GetComponent<TeamComponent>();
            if (team != null)
                WatersapManager.Instance.SpendWatersap(team.GetTeamID(), abilityData.watersapCost);
        }

        // Enregistre le temps d'utilisation pour le cooldown
        lastUsedTick = SimulationClockManager.Instance != null ? SimulationClockManager.Instance.CurrentTick : 0;

        // Exécute la logique spécifique et propage son résultat
        return ExecuteAbility(caster);
    }

    // Méthode abstraite : chaque ability concrète implémente sa logique ici
    // Retourne true si l'action a réellement eu lieu, false si elle a été rejetée en interne
    protected abstract bool ExecuteAbility(GameObject caster);
}
