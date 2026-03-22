using UnityEngine;

// ScriptableObject contenant les statistiques de base d'un bâtiment
// Réutilisable : Nexus, Great Oak, Great Furnace, etc. partagent la même classe
// Modifiable par les designers sans toucher au code
// Exemple : Nexus_Stats.asset, GreatOak_Stats.asset
[CreateAssetMenu(fileName = "New Building Stats", menuName = "Bloom & Ashes/Building Stats Data")]
public class BuildingStatsData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Nom du bâtiment affiché dans les logs et l'UI")]
    public string buildingName = "Bâtiment";

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

    // Validation dans l'inspecteur Unity
    // Empêche des valeurs incohérentes lors de la création de l'asset
    private void OnValidate()
    {
        maxHP = Mathf.Max(1, maxHP);
        defense = Mathf.Max(0, defense);
        sizeX = Mathf.Max(1, sizeX);
        sizeY = Mathf.Max(1, sizeY);
    }
}
