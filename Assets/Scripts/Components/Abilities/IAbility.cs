using UnityEngine;

// Interface définissant le contrat des abilities
// Toutes les logiques d'ability doivent implémenter cette interface
public interface IAbility
{
    /// <summary>
    /// Vérifie si l'ability peut être exécutée (Watersap suffisant, pas en cooldown, etc.)
    /// </summary>
    bool CanExecute(GameObject caster);

    /// <summary>
    /// Exécute l'ability (vérifie CanExecute en interne avant d'agir)
    /// </summary>
    void Execute(GameObject caster);

    /// <summary>
    /// Retourne les données ScriptableObject de l'ability
    /// </summary>
    AbilityData GetData();
}
