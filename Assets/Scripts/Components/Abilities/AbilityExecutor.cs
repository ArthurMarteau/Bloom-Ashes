using UnityEngine;

// Classe abstraite de base pour toutes les logiques d'ability
// Fournit la gestion du cooldown et la vérification Watersap
// Les abilities concrètes implémentent ExecuteAbility()
public abstract class AbilityExecutor : IAbility
{
    protected AbilityData abilityData;
    protected float lastUsedTime = -999f;

    public AbilityExecutor(AbilityData data)
    {
        abilityData = data;
    }

    // Retourne les données SO de l'ability
    public AbilityData GetData() => abilityData;

    // Vérifie si l'ability peut être exécutée (cooldown + Watersap)
    public virtual bool CanExecute(GameObject caster)
    {
        // Vérifie le cooldown
        if (abilityData.cooldownDuration > 0f)
        {
            float timeSinceLastUse = Time.time - lastUsedTime;
            if (timeSinceLastUse < abilityData.cooldownDuration)
            {
                Debug.Log($"⏳ {abilityData.abilityName} en cooldown ({abilityData.cooldownDuration - timeSinceLastUse:F1}s restantes)");
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
                Debug.Log($"💧 Watersap insuffisant pour {abilityData.abilityName} (coût : {abilityData.watersapCost})");
                return false;
            }
        }

        return true;
    }

    // Exécute l'ability : vérifie les conditions, dépense le Watersap, puis délègue à ExecuteAbility()
    public void Execute(GameObject caster)
    {
        if (!CanExecute(caster)) return;

        // Dépense le Watersap si un coût est défini
        if (abilityData.watersapCost > 0)
        {
            TeamComponent team = caster.GetComponent<TeamComponent>();
            if (team != null)
                WatersapManager.Instance.SpendWatersap(team.GetTeamID(), abilityData.watersapCost);
        }

        // Enregistre le temps d'utilisation pour le cooldown
        lastUsedTime = Time.time;

        // Exécute la logique spécifique de l'ability concrète
        ExecuteAbility(caster);
    }

    // Méthode abstraite : chaque ability concrète implémente sa logique ici
    protected abstract void ExecuteAbility(GameObject caster);
}
