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
    public int sizeX = 1;
    public int sizeY = 1;

    public enum EnvironmentType
    {
        Obstacle,  // Bloque le passage (layer Obstacle, ex: Tree)
        Walkable   // Traversable (layer EnvironmentWalkable, ex: Bush)
    }

    [Header("Type")]
    [Tooltip("Obstacle = bloque le passage / Walkable = traversable")]
    public EnvironmentType environmentType = EnvironmentType.Obstacle;
}
