using UnityEngine;

// Singleton gérant l'exécution de toutes les abilities
// Point central pour appeler une ability depuis l'UI ou tout autre système
public class AbilityManager : MonoBehaviour
{
    public static AbilityManager Instance { get; private set; }

    // Initialise le singleton
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Tente d'exécuter l'ability d'une entité (Nexus, Unité, etc.)
    public void TryExecuteAbility(GameObject caster, AbilityData abilityData)
    {
        if (caster == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, "⚠️ AbilityManager : Caster null", this);
            return;
        }

        if (abilityData == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, $"⚠️ AbilityManager : AbilityData null sur {caster.name}", this);
            return;
        }

        IAbility ability = abilityData.CreateExecutor();
        if (!ability.CanExecute(caster)) return;

        if (ability.Execute(caster) && abilityData.vfxOnExecute != null)
            VFXApplier.Apply(abilityData.vfxOnExecute, caster);
    }

    // Vérifie si une ability peut être exécutée (pour griser un bouton UI)
    public bool CanExecuteAbility(GameObject caster, AbilityData abilityData)
    {
        if (caster == null || abilityData == null) return false;

        IAbility ability = abilityData.CreateExecutor();
        return ability.CanExecute(caster);
    }
}
