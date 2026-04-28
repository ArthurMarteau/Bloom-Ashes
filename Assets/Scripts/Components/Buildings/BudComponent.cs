using System.Collections;
using UnityEngine;
using UnityEngine.Events;

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

    [Header("Events")]
    [Tooltip("Déclenché au début de la phase Hatching (juste avant le SpawnFinalUnit)")]
    public UnityEvent<BudComponent> OnHatchingStarted;

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

    // Durée de l'animation Hatching — exposée pour synchronisation des VFX
    public float HatchingDuration => hatchingDuration;

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
            animator.Play("Idle");

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

        // Notifie les systèmes externes (VFX, audio, etc.) que le Hatching commence
        // Passe la référence de ce Bud pour que les listeners puissent accéder à hatchingDuration via une méthode publique
        OnHatchingStarted?.Invoke(this);

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

        // Notifie le système de remplacement AVANT destruction
        if (EntitySpawnManager.Instance != null)
        {
            EntitySpawnManager.Instance.NotifyEntityReplaced(gameObject, spawnedUnit);
        }

        Destroy(gameObject);
    }

    // Annule la production : stoppe la coroutine en cours, restitue un Rootborn et détruit le Bud
    public void Cancel()
    {
        isCancelled = true;
        StopAllCoroutines();

        // Étape 1 : Récupère les HP actuels du Bud
        int budCurrentHP = 0;
        BuildingStatsComponent budStats = GetComponent<BuildingStatsComponent>();
        if (budStats != null)
            budCurrentHP = budStats.GetCurrentHP();

        // Étape 2 : Libération de la tile
        Collider2D budCollider = GetComponent<Collider2D>();
        if (budCollider != null)
            budCollider.enabled = false;

        // Étape 3 : Validation prefab Rootborn
        if (rootbornPrefab == null)
        {
            Debug.LogWarning($"🌱 BudComponent {gameObject.name} : Cancel — rootbornPrefab non assigné");
            Destroy(gameObject);
            return;
        }

        // Étape 4 : Spawn Rootborn
        GameObject rootborn = Instantiate(rootbornPrefab, transform.position, Quaternion.identity);

        // Étape 5 : Propagation équipe
        TeamComponent rootbornTeam = rootborn.GetComponent<TeamComponent>();
        if (rootbornTeam != null)
            rootbornTeam.SetTeamID(teamID);

        // Étape 6 : Propagation HP du Bud au Rootborn
        UnitStatsComponent rootbornStats = rootborn.GetComponent<UnitStatsComponent>();
        if (rootbornStats != null && budCurrentHP > 0)
            rootbornStats.SetCurrentHP(budCurrentHP);

        Debug.Log($"🌱 BudComponent {gameObject.name} : production annulée — Rootborn restitué ({budCurrentHP} HP)");

        // Étape 7 : Notifie le système de remplacement
        if (EntitySpawnManager.Instance != null)
            EntitySpawnManager.Instance.NotifyEntityReplaced(gameObject, rootborn);

        // Étape 8 : Destruction Bud
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
