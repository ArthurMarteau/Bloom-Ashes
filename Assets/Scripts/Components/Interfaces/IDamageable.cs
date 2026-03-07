using UnityEngine;

// Interface abstraisant toute entité pouvant prendre des dégâts
// Permet à AttackController de cibler uniformément les unités ET les bâtiments
// Implémentée par : UnitStatsComponent, BuildingStatsComponent (Phase 4.3)
public interface IDamageable
{
    // Applique des dégâts à l'entité (formule : damage - defense)
    // attacker peut être null si les dégâts viennent de l'environnement
    void TakeDamage(int damage, GameObject attacker = null);

    // Soigne l'entité d'un certain montant (limité au MaxHP)
    void Heal(int amount);

    // Retourne les HP actuels de l'entité
    int GetCurrentHP();

    // Retourne les HP maximum de l'entité
    int GetMaxHP();

    // Retourne le pourcentage de vie (0.0 à 1.0)
    float GetHealthPercentage();

    // Vérifie si l'entité est morte
    bool IsDead();

    // Vérifie si l'entité est invulnérable (ignore tous les dégâts entrants)
    // Permet de filtrer les cibles sans coupler au type concret (UnitStatsComponent vs BuildingStatsComponent)
    bool IsInvulnerable();
}
