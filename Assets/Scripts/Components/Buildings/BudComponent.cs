using System.Collections;
using UnityEngine;

// Unité intermédiaire statique entre le Rootborn et l'unité de combat finale
// Spawné par RootbornProductionController.ExecuteTransform, remplacé par l'unité finale à la fin du buildTime
// Ne se déplace pas — reste sur sa tile jusqu'à l'éclosion ou l'annulation
public class BudComponent : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Prefab du Rootborn restitué si la production est annulée")]
    [SerializeField] private GameObject rootbornPrefab;

    [Header("Animation")]
    [Tooltip("Durée de l'animation Hatching avant le spawn de l'unité finale (secondes)")]
    [SerializeField] private float hatchingDuration = 1f;

    // Unité en cours de production — assignée via Initialize
    private UnitStatsData unitInProduction;

    // Équipe propriétaire — propagée à l'unité finale et au Rootborn de repli
    private int teamID;

    // Temps écoulé depuis le début de la production
    private float elapsedTime;

    // Vrai si Cancel() a été appelé — empêche la coroutine de continuer
    private bool isCancelled;

    // Retourne la progression de la production (0.0 → 1.0), clampée, pour les barres de progression UI
    public float Progress
    {
        get
        {
            if (unitInProduction == null || unitInProduction.buildTime <= 0f)
                return 1f;

            return Mathf.Clamp01(elapsedTime / unitInProduction.buildTime);
        }
    }

    // Retourne le ScriptableObject de l'unité en production, pour l'affichage du portrait UI
    public UnitStatsData UnitInProduction => unitInProduction;

    // Initialise le Bud avec l'unité à produire, les HP hérités du Rootborn et l'équipe du propriétaire
    // currentHP : HP courants du Rootborn au moment de la transformation — propagés au BuildingStatsComponent si présent
    public void Initialize(UnitStatsData unitData, int currentHP, int team)
    {
        unitInProduction = unitData;
        teamID = team;
        elapsedTime = 0f;
        isCancelled = false;

        // Propagation des HP du Rootborn au BuildingStatsComponent du Bud si présent
        // Permet au Bud d'être damageable avec les HP restants du Rootborn
        BuildingStatsComponent buildingStats = GetComponent<BuildingStatsComponent>();
        if (buildingStats != null)
            buildingStats.SetCurrentHP(currentHP);

        // Correction 3 : enregistrement dans GridManager — active le Collider2D pour que GetBuildingAt détecte le Bud
        // Le Bud doit être sur le layer Buildings (8) dans le prefab pour être visible par GridManager
        Collider2D budCollider = GetComponent<Collider2D>();
        if (budCollider != null)
            budCollider.enabled = true;

        // Animation d'attente pendant le buildTime
        Animator animator = GetComponent<Animator>();
        if (animator != null)
            animator.Play("Idle_Bud");

        StartCoroutine(ProductionRoutine());
    }

    // Coroutine principale : incrémente le timer frame par frame puis déclenche l'éclosion
    private IEnumerator ProductionRoutine()
    {
        // Attente active — incrémentation manuelle pour exposer Progress à l'UI
        while (elapsedTime < unitInProduction.buildTime)
        {
            if (isCancelled) yield break;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (isCancelled) yield break;

        // Déclenchement de l'animation d'éclosion si un Animator est présent
        Animator animator = GetComponent<Animator>();
        if (animator != null)
            animator.Play("Hatching");

        yield return new WaitForSeconds(hatchingDuration);

        if (isCancelled) yield break;

        SpawnFinalUnit();
    }

    // Libère la tile dans GridManager, instancie l'unité finale, puis détruit le Bud
    private void SpawnFinalUnit()
    {
        // Libération de la tile : désactive le Collider2D avant l'Instantiate pour que
        // la tile apparaisse libre quand l'unité finale s'enregistre dans l'EntityRegistry
        Collider2D budCollider = GetComponent<Collider2D>();
        if (budCollider != null)
            budCollider.enabled = false;

        if (unitInProduction.unitPrefab == null)
        {
            Debug.LogWarning($"🌱 BudComponent {gameObject.name} : SpawnFinalUnit — unitPrefab null pour {unitInProduction.unitName}");
            Destroy(gameObject);
            return;
        }

        GameObject spawnedUnit = Instantiate(unitInProduction.unitPrefab, transform.position, Quaternion.identity);

        TeamComponent spawnedTeam = spawnedUnit.GetComponent<TeamComponent>();
        if (spawnedTeam != null)
            spawnedTeam.SetTeamID(teamID);

        Debug.Log($"🌱 BudComponent {gameObject.name} : ✅ {unitInProduction.unitName} éclos (équipe {teamID})");

        Destroy(gameObject);
    }

    // Annule la production et restitue un Rootborn à la position du Bud
    // Cas d'usage : ordre de mouvement reçu pendant la production, mort du bâtiment adjacent, etc.
    public void Cancel()
    {
        isCancelled = true;
        StopAllCoroutines();

        // Libération de la tile avant l'Instantiate du Rootborn
        Collider2D budCollider = GetComponent<Collider2D>();
        if (budCollider != null)
            budCollider.enabled = false;

        if (rootbornPrefab == null)
        {
            Debug.LogWarning($"🌱 BudComponent {gameObject.name} : Cancel — rootbornPrefab non assigné, Rootborn non restitué");
            Destroy(gameObject);
            return;
        }

        GameObject rootborn = Instantiate(rootbornPrefab, transform.position, Quaternion.identity);

        TeamComponent rootbornTeam = rootborn.GetComponent<TeamComponent>();
        if (rootbornTeam != null)
            rootbornTeam.SetTeamID(teamID);

        Debug.Log($"🌱 BudComponent {gameObject.name} : production annulée — Rootborn restitué (équipe {teamID})");

        Destroy(gameObject);
    }

    // Affiche l'état courant de la production depuis l'éditeur
    [ContextMenu("Debug: Production State")]
    private void DebugProductionState()
    {
        if (unitInProduction == null)
        {
            Debug.Log($"🌱 BudComponent {gameObject.name} : non initialisé");
            return;
        }

        Debug.Log($"🌱 BudComponent {gameObject.name} : {unitInProduction.unitName} | Équipe {teamID} | Progression {Progress * 100f:F0}% ({elapsedTime:F1}s / {unitInProduction.buildTime:F1}s) | Annulé : {isCancelled}");
    }
}
