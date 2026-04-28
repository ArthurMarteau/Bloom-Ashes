using UnityEngine;

// Singleton gérant l'exécution de toutes les abilities
// Point central pour appeler une ability depuis l'UI ou tout autre système
public class AbilityManager : MonoBehaviour
{
    public static AbilityManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Tente d'exécuter l'ability d'une entité (Nexus, Unité, etc.)
    /// </summary>
    public void TryExecuteAbility(GameObject caster, AbilityData abilityData)
    {
        if (caster == null)
        {
            Debug.LogWarning("⚠️ AbilityManager : Caster null");
            return;
        }

        if (abilityData == null)
        {
            Debug.LogWarning($"⚠️ AbilityManager : AbilityData null sur {caster.name}");
            return;
        }

        IAbility ability = abilityData.CreateExecutor();
        ability.Execute(caster);
    }

    /// <summary>
    /// Vérifie si une ability peut être exécutée (pour griser un bouton UI)
    /// </summary>
    public bool CanExecuteAbility(GameObject caster, AbilityData abilityData)
    {
        if (caster == null || abilityData == null) return false;

        IAbility ability = abilityData.CreateExecutor();
        return ability.CanExecute(caster);
    }
}
