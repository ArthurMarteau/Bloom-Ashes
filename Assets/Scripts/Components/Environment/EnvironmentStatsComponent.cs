using UnityEngine;

// Composant simple exposant les données de base d'une entité d'environnement.
public class EnvironmentStatsComponent : MonoBehaviour
{
    [SerializeField] private EnvironmentStatsData baseStats;

    // Retourne le ScriptableObject de stats de base.
    public EnvironmentStatsData GetBaseStats() => baseStats;
}
