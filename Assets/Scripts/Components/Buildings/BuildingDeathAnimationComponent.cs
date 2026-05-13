using UnityEngine;

// Joue l'animation de mort d'un bâtiment quand OnBuildingDestroyed est déclenché
// La destruction du GameObject est gérée par BuildingStatsComponent (Destroy avec délai)
// S'abonne à OnBuildingDestroyed — aucun polling dans Update
public class BuildingDeathAnimationComponent : MonoBehaviour
{
    [SerializeField] private string deathAnimName = "Death";

    private BuildingStatsComponent buildingStats;

    // Trouve BuildingStatsComponent et s'abonne à OnBuildingDestroyed
    private void Awake()
    {
        buildingStats = GetComponent<BuildingStatsComponent>();
        if (buildingStats != null)
        {
            buildingStats.OnBuildingDestroyed.AddListener(OnEntityDied);
        }
        else
        {
            Debug.LogWarning($"💀 BuildingDeathAnimationComponent {gameObject.name} : BuildingStatsComponent introuvable sur ce GameObject !");
        }
    }

    // Désabonne l'event lors de la destruction pour éviter les fuites mémoire
    private void OnDestroy()
    {
        if (buildingStats != null)
        {
            buildingStats.OnBuildingDestroyed.RemoveListener(OnEntityDied);
        }
    }

    // Déclenché par OnBuildingDestroyed — joue l'animation de mort
    // Le sprite persiste naturellement pendant les 10 secondes du Destroy différé géré par BuildingStatsComponent
    private void OnEntityDied()
    {
        GetComponent<Animator>()?.Play(deathAnimName);
    }
}
