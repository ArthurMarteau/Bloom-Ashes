using UnityEngine;

// Logique pure de calcul des statistiques d'unité
// POCO (Plain Old C# Object) - PAS de MonoBehavior
// 100% testable sans Unity - fonctions pures
// Utilisé par UnitStatsComponent pour tous les calculs
public class UnitStatsLogic
{
    // Calcule les dégâts effectifs après application de la défense
    // Formule simple : dégâts - défense (minimum 0)
    // Utilisé lors de TakeDamage pour déterminer les HP perdus
    public int CalculateEffectiveDamage(int rawDamage, int defense)
    {
        return Mathf.Max(0, rawDamage - defense);
    }

    // Calcule le pourcentage de vie restante (0.0 à 1.0)
    // Utilisé par HealthBar pour affichage visuel
    // Gère division par zéro (retourne 0 si maxHP = 0)
    public float CalculateHealthPercentage(int currentHP, int maxHP)
    {
        if (maxHP <= 0) return 0f;
        return Mathf.Clamp01((float)currentHP / maxHP);
    }

    // Calcule les HP après soin sans dépasser le maximum
    // Retourne les HP ajustés et le montant réellement soigné
    // Utilisé par Heal pour appliquer les soins correctement
    public (int newHP, int actualHealing) CalculateHealing(int currentHP, int maxHP, int healAmount)
    {
        int newHP = Mathf.Min(currentHP + healAmount, maxHP);
        int actualHealing = newHP - currentHP;
        return (newHP, actualHealing);
    }

    // Vérifie si l'unité devrait mourir selon ses HP
    // Logique de mort séparée pour faciliter tests et modifications futures
    // Retourne true si HP <= 0
    public bool ShouldDie(int currentHP)
    {
        return currentHP <= 0;
    }

    // Valide qu'une stat est dans les limites acceptables
    // Utilisé par les setters pour éviter valeurs négatives ou invalides
    // Retourne la valeur clampée dans la plage [min, max]
    public int ClampStat(int value, int min, int max)
    {
        return Mathf.Clamp(value, min, max);
    }

    // Valide une stat float dans les limites acceptables
    // Version float pour vitesse, attackSpeed, etc.
    public float ClampStat(float value, float min, float max)
    {
        return Mathf.Clamp(value, min, max);
    }
}
