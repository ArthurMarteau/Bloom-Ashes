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
    /// Exécute l'ability avec une validation CanExecute unique avant d'agir
    /// Retourne true si l'action interne a réussi, false sinon
    /// </summary>
    bool Execute(GameObject caster);

    /// <summary>
    /// Retourne les données ScriptableObject de l'ability
    /// </summary>
    AbilityData GetData();
}
