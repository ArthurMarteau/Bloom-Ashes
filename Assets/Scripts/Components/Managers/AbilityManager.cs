using UnityEngine;
using UnityEngine.Events;

// Singleton gérant l'exécution de toutes les abilities
// Point central pour appeler une ability depuis l'UI ou tout autre système
public class AbilityManager : MonoBehaviour
{
    public static AbilityManager Instance { get; private set; }

    [Header("Events")]
    // Déclenché après chaque exécution réussie d'une ability (CanExecute = true)
    public UnityEvent<GameObject, AbilityData> OnAbilityExecuted = new UnityEvent<GameObject, AbilityData>();

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
        if (!ability.CanExecute(caster)) return;
        if (ability.Execute(caster))
            OnAbilityExecuted.Invoke(caster, abilityData);
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
