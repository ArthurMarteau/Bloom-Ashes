using UnityEngine;

// ScriptableObject abstrait contenant les données communes à toutes les abilities
// Les abilities concrètes héritent de cette classe et ajoutent leurs données spécifiques
// Note : pas de [CreateAssetMenu] sur la classe abstraite — utiliser les sous-classes concrètes
public abstract class AbilityData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Nom de l'ability affiché dans l'UI")]
    public string abilityName = "Ability";

    [Header("Visuel")]
    [Tooltip("Icône affichée sur le bouton d'ability")]
    public Sprite icon;

    [Header("Coût")]
    [Tooltip("Coût en Watersap pour utiliser l'ability (0 = gratuit ou géré en interne)")]
    public int watersapCost = 0;

    [Header("Cooldown")]
    [Tooltip("Temps de recharge en secondes (0 = pas de cooldown)")]
    public float cooldownDuration = 0f;

    [Header("VFX")]
    [Tooltip("VFX joué après exécution réussie (laissé vide = aucun VFX)")]
    public VFXData vfxOnExecute;

    [Header("Description")]
    [Tooltip("Description affichée au survol (tooltip)")]
    [TextArea(2, 4)]
    public string description = "";

    // Méthode abstraite : chaque ability concrète implémente sa propre logique d'executor
    public abstract IAbility CreateExecutor();
}
