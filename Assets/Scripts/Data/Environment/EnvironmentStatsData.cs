using UnityEngine;

// ScriptableObject contenant les données de base d'une entité d'environnement.
[CreateAssetMenu(fileName = "New Environment Stats", menuName = "Bloom and Ashes/Data/Environment Stats")]
public class EnvironmentStatsData : ScriptableObject
{
    [Header("Identification")]
    public string entityName = "Environment";

    [Header("Portrait")]
    public Sprite portrait = null;

    [Header("Size")]
    [Tooltip("Largeur en cases de grille depuis l'ancrage bas-gauche")]
    public int sizeX = 1;
    [Tooltip("Hauteur en cases de grille depuis l'ancrage bas-gauche")]
    public int sizeY = 1;

    [Header("VFX Lifecycle")]
    [Tooltip("VFX joué au spawn de l'entité d'environnement")]
    public VFXData onSpawnVFX;
}
