using UnityEngine;

// ScriptableObject contenant les statistiques de base d'une unité
// Asset réutilisable : plusieurs prefabs peuvent partager le même SO
// Modifiable par designers sans toucher au code
// Exemple : Rootborn_Stats.asset, Warrior_Stats.asset, etc.
[CreateAssetMenu(fileName = "New Unit Stats", menuName = "Bloom and Ashes/Data/Unit Stats")]
public class UnitStatsData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Nom affiché dans l'UI (ex: Rootborn, Luminescent Mycena)")]
    public string unitName = "Unit";

    [TextArea(2, 4)]
    public string description = "";

    [Header("Portrait")]
    [Tooltip("Sprite du portrait affiché dans le panneau de sélection")]
    public Sprite portrait = null;

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

    [Tooltip("Type de déplacement : Ground, Flying ou Swimming")]
    public UnitMovementType movementType = UnitMovementType.Ground;

    [Header("Ability")]
    [Tooltip("Ability de cette unité (null si pas d'ability)")]
    public AbilityData abilityData = null;

    [Header("Production")]
    [Tooltip("Prefab de l'unité à instancier lors de la production")]
    public GameObject unitPrefab = null;

    [Tooltip("Coût en Watersap pour produire cette unité (0 = non produisible)")]
    public int watersapCost = 0;

    [Tooltip("Durée de production en secondes")]
    public float buildTime = 0f;

    [Tooltip("Tier du bâtiment requis pour débloquer (1 = disponible de base, 2/3/4 = nécessite upgrade)")]
    [Range(1, 4)]
    public int tierRequired = 1;

    [Tooltip("Icône affichée dans l'UI de production (peut être null si non produisible)")]
    public Sprite productionIcon = null;

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
        watersapCost = Mathf.Max(0, watersapCost);
        buildTime = Mathf.Max(0f, buildTime);
        tierRequired = Mathf.Clamp(tierRequired, 1, 4);
    }
}
