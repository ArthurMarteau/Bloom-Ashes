using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Unité intermédiaire statique entre le Rootborn et l'unité de combat finale
// Spawné par TransformationController.ExecuteTransform, remplacé par l'unité finale à la fin du buildTime
// Ne se déplace pas — reste sur sa tile jusqu'à l'éclosion ou l'annulation
public class BudComponent : MonoBehaviour
{
    private const float FallbackBuildTime = 10f;

    [Header("Animation")]
    [Tooltip("Durée de l'animation Hatching avant le spawn de l'unité finale (secondes)")]
    [SerializeField] private float hatchingDuration = 1f;

    // Unité en cours de production — assignée via Initialize
    private UnitStatsData unitInProduction;

    private UnitStatsData respawnStatsData;
    private float productionBuildTime;

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
            if (productionBuildTime <= 0f)
                return 1f;

            return Mathf.Clamp01(elapsedTime / productionBuildTime);
        }
    }

    // Retourne le ScriptableObject de l'unité en production, pour l'affichage du portrait UI
    public UnitStatsData UnitInProduction => unitInProduction;

    // Durée de l'animation Hatching — exposée pour synchronisation des VFX
    public float HatchingDuration => hatchingDuration;

    // Initialise le Bud avec l'unité finale à produire, les stats à restituer si annulé, les HP hérités et l'équipe propriétaire
    public void Initialize(UnitStatsData unitData, UnitStatsData respawnStatsData, int currentHP, int maxHP, int team)
    {
        unitInProduction = unitData;
        this.respawnStatsData = respawnStatsData;
        productionBuildTime = GetBuildTime(unitInProduction);
        teamID = team;
        elapsedTime = 0f;
        isCancelled = false;

        PrepareBud(currentHP, maxHP);
        StartCoroutine(ProductionRoutine());
    }

    // Coroutine principale : incrémente le timer frame par frame puis déclenche l'éclosion
    private IEnumerator ProductionRoutine()
    {
        // Attente active — incrémentation manuelle pour exposer Progress à l'UI
        while (elapsedTime < productionBuildTime)
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

        if (unitInProduction == null)
        {
            Debug.LogWarning($"🌱 BudComponent {gameObject.name} : ProductionRoutine — unitInProduction null, spawn final impossible");
            Destroy(gameObject);
            yield break;
        }

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

        if (respawnStatsData?.unitPrefab == null)
        {
            Debug.LogError($"🌱 BudComponent {gameObject.name} : Cancel — respawnStatsData ou unitPrefab non assigné");
            Destroy(gameObject);
            return;
        }

        GameObject respawned = Instantiate(respawnStatsData.unitPrefab, transform.position, Quaternion.identity);

        TeamComponent t = respawned.GetComponent<TeamComponent>();
        if (t != null)
            t.SetTeamID(teamID);

        UnitStatsComponent stats = respawned.GetComponent<UnitStatsComponent>();
        if (stats != null)
            stats.SetCurrentHP(budCurrentHP);

        Debug.Log($"🌱 BudComponent {gameObject.name} : production annulée — {respawned.name} restitué ({budCurrentHP} HP)");

        // Étape 7 : Notifie le système de remplacement
        if (EntitySpawnManager.Instance != null)
            EntitySpawnManager.Instance.NotifyEntityReplaced(gameObject, respawned);

        // Étape 8 : Destruction Bud
        Destroy(gameObject);
    }

    private void PrepareBud(int currentHP, int maxHP)
    {
        // Propagation des HP au BuildingStatsComponent du Bud si présent
        BuildingStatsComponent buildingStats = GetComponent<BuildingStatsComponent>();
        if (buildingStats != null)
        {
            buildingStats.SetMaxHP(maxHP);
            buildingStats.SetCurrentHP(currentHP);
        }

        Collider2D budCollider = GetComponent<Collider2D>();
        if (budCollider != null)
            budCollider.enabled = true;

        Animator animator = GetComponent<Animator>();
        if (animator != null)
            animator.Play("Idle");
    }

    private float GetBuildTime(UnitStatsData unitData)
    {
        if (unitData == null || unitData.buildTime <= 0f)
            return FallbackBuildTime;

        return unitData.buildTime;
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

        Debug.Log($"🌱 BudComponent {gameObject.name} : {unitInProduction.unitName} | Équipe {teamID} | Progression {Progress * 100f:F0}% ({elapsedTime:F1}s / {productionBuildTime:F1}s) | Annulé : {isCancelled}");
    }
}
