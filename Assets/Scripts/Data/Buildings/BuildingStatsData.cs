using System.Collections.Generic;
using UnityEngine;

// ScriptableObject contenant les statistiques de base d'un bâtiment
// Réutilisable : Nexus, Great Oak, Great Furnace, etc. partagent la même classe
// Modifiable par les designers sans toucher au code
// Exemple : Nexus_Stats.asset, GreatOak_Stats.asset
[CreateAssetMenu(fileName = "New Building Stats", menuName = "Bloom and Ashes/Data/Building Stats")]
public class BuildingStatsData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Nom du bâtiment affiché dans les logs et l'UI")]
    public string buildingName = "Bâtiment";

    [TextArea(2, 4)]
    public string description = "";

    [Header("Portrait")]
    [Tooltip("Sprite du portrait affiché dans le panneau de sélection")]
    public Sprite portrait = null;

    [Header("Health")]
    [Tooltip("Points de vie maximum du bâtiment")]
    public int maxHP = 100;

    [Header("Combat")]
    [Tooltip("Réduction de dégâts par attaque (soustraction simple)")]
    public int defense = 0;

    [Header("Invulnerability")]
    [Tooltip("Si vrai, le bâtiment ignore tous les dégâts entrants (ex: Nexus)")]
    public bool isInvulnerable = false;

    [Header("Size")]
    [Tooltip("Largeur du bâtiment en tiles (1 = 1x1, 2 = 2x2, etc.)")]
    public int sizeX = 1;
    [Tooltip("Hauteur du bâtiment en tiles (1 = 1x1, 2 = 2x2, etc.)")]
    public int sizeY = 1;

    [Header("Production")]
    [Tooltip("Si vrai, ce bâtiment est un Nexus : il spawne des Rootborn directement sur sa tile")]
    public bool isNexus = false;

    [Tooltip("Unités déblocables par un Rootborn adjacent (bâtiments de faction uniquement)")]
    public List<UnitStatsData> producibleUnits = new List<UnitStatsData>();

    [Tooltip("Tier maximum atteignable via upgrades (1 = pas d'upgrade possible)")]
    [Range(1, 4)]
    public int maxTier = 1;

    [Header("Prefab")]
    [Tooltip("Prefab du bâtiment instancié lors d'un build depuis une Foundation (laisser null si non constructible)")]
    public GameObject buildingPrefab = null;

    [Tooltip("Foundation à spawner à la destruction de ce bâtiment (null = pas de respawn)")]
    public FoundationStatsData defaultFoundation;

    [Header("Ability")]
    [Tooltip("Ability de ce bâtiment (null si pas d'ability)")]
    public AbilityData abilityData = null;

    [HideInInspector] public FoundationStatsData originFoundation;

    [Header("VFX Lifecycle")]
    [Tooltip("VFX joué au spawn du bâtiment")]
    public VFXData onSpawnVFX;

    [Tooltip("VFX joué au début de la séquence de mort")]
    public VFXData onDeathVFX;

    [Tooltip("Durée d'attente avant Destroy après la destruction (remplace le 1f hardcodé dans DestroySequence)")]
    public float deathDelay = 1f;

    [Tooltip("VFX joué au début de la phase Hatching du Bud (null pour les vrais bâtiments)")]
    public VFXData onHatchVFX;

    [Tooltip("Durée de l'animation Hatching avant spawn de l'unité finale (remplace hatchingDuration dans BudComponent)")]
    public float hatchingDuration = 1f;

    // Validation dans l'inspecteur Unity
    // Empêche des valeurs incohérentes lors de la création de l'asset
    private void OnValidate()
    {
        maxHP = Mathf.Max(1, maxHP);
        defense = Mathf.Max(0, defense);
        sizeX = Mathf.Max(1, sizeX);
        sizeY = Mathf.Max(1, sizeY);
        maxTier = Mathf.Clamp(maxTier, 1, 4);
        deathDelay = Mathf.Max(0f, deathDelay);
        hatchingDuration = Mathf.Max(0f, hatchingDuration);
    }
}
