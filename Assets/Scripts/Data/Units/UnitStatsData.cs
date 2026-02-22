using UnityEngine;

// ScriptableObject contenant les statistiques de base d'une unité
// Asset réutilisable : plusieurs prefabs peuvent partager le même SO
// Modifiable par designers sans toucher au code
// Exemple : Rootborn_Stats.asset, Warrior_Stats.asset, etc.
[CreateAssetMenu(fileName = "New Unit Stats", menuName = "Bloom & Ashes/Unit Stats Data")]
public class UnitStatsData : ScriptableObject
{
    [Header("Health")]
    [Tooltip("Points de vie maximum de l'unité")]
    public int maxHP = 100;

    [Header("Combat Stats")]
    [Tooltip("Dégâts infligés par attaque")]
    public int attackDamage = 10;
    
    [Tooltip("Portée d'attaque (1 = mêlée, 2+ = distance)")]
    public int attackRange = 1;
    
    [Tooltip("Réduction de dégâts (soustraction simple)")]
    public int defense = 0;
    
    [Tooltip("Temps entre chaque attaque (secondes)")]
    public float attackSpeed = 1.0f;

    [Header("Movement")]
    [Tooltip("Vitesse de déplacement (unités Unity/seconde)")]
    public float moveSpeed = 2.0f;

    [Header("Unit Type")]
    [Tooltip("Cocher pour les unités Rootborn (animation IdleReversed)")]
    public bool isRootborn = false;

    // Validation dans l'inspecteur Unity
    // Empêche des valeurs incohérentes lors de la création
    private void OnValidate()
    {
        maxHP = Mathf.Max(1, maxHP);
        attackDamage = Mathf.Max(0, attackDamage);
        attackRange = Mathf.Max(1, attackRange);
        defense = Mathf.Max(0, defense);
        attackSpeed = Mathf.Max(0.1f, attackSpeed);
        moveSpeed = Mathf.Max(0.1f, moveSpeed);
    }
}
