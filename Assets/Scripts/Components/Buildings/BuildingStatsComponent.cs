using UnityEngine;
using UnityEngine.Events;

// Composant Unity gérant l'état de santé d'un bâtiment
// Implémente IDamageable pour être ciblable par AttackController
// L'équipe est gérée par TeamComponent (cohérence avec les unités)
public class BuildingStatsComponent : MonoBehaviour, IDamageable
{
    [Header("Stats Data")]
    [SerializeField] private BuildingStatsData baseStats; // ScriptableObject avec stats de base

    // Cache de TeamComponent pour les logs
    private TeamComponent teamComponent;

    [Header("Events")]
    // Initialisation directe pour éviter NullRef lors des AddListener
    public UnityEvent OnDamaged = new UnityEvent();           // Déclenché quand le bâtiment prend des dégâts
    public UnityEvent OnHealed = new UnityEvent();            // Déclenché quand le bâtiment est soigné
    public UnityEvent OnBuildingDestroyed = new UnityEvent(); // Déclenché quand le bâtiment est détruit

    [Header("Debug")]
    [SerializeField] private bool debugStats = true;

    // État runtime
    private int currentHP;
    private bool isDestroyed = false;

    // Initialise les HP au maximum au démarrage
    private void Awake()
    {
        if (baseStats == null)
        {
            Debug.LogError($"🏛️ BuildingStatsComponent sur {gameObject.name}: baseStats non assigné !");
            enabled = false;
            return;
        }

        currentHP = baseStats.maxHP;
        teamComponent = GetComponent<TeamComponent>();

        if (teamComponent == null)
            Debug.LogWarning($"⚠️ BuildingStatsComponent {gameObject.name}: TeamComponent manquant — équipe non définie");

        if (debugStats)
        {
            string invulnerableStr = baseStats.isInvulnerable ? " | INVULNÉRABLE" : "";
            int teamID = teamComponent != null ? teamComponent.GetTeamID() : 0;
            Debug.Log($"🏛️ BuildingStatsComponent {gameObject.name} ({baseStats.buildingName}): HP={currentHP}, DEF={baseStats.defense}{invulnerableStr}, Équipe={teamID}");
        }

        // Auto-enregistrement dans EntityRegistry (initialisé avant via [DefaultExecutionOrder(-100)])
        if (EntityRegistry.Instance != null)
        {
            EntityRegistry.Instance.RegisterEntity(this, gameObject);
        }
        else if (debugStats)
        {
            Debug.LogWarning($"⚠️ BuildingStatsComponent {gameObject.name}: EntityRegistry introuvable, enregistrement ignoré");
        }
    }

    // Désenregistre le bâtiment de l'EntityRegistry lors de la destruction
    private void OnDestroy()
    {
        if (EntityRegistry.Instance != null)
        {
            EntityRegistry.Instance.UnregisterEntity(gameObject);
        }
    }

    // === IDamageable ===

    // Applique des dégâts au bâtiment
    // Les dégâts sont ignorés si le bâtiment est invulnérable (ex: Nexus)
    public void TakeDamage(int damage, GameObject attacker = null)
    {
        if (isDestroyed)
        {
            return;
        }

        // Bâtiment invulnérable (Nexus) : absorbe sans effet
        if (baseStats.isInvulnerable)
        {
            if (debugStats)
            {
                Debug.Log($"🛡️ BuildingStatsComponent {gameObject.name} ({baseStats.buildingName}): Invulnérable — {damage} dégâts ignorés");
            }
            return;
        }

        // Applique la réduction par la défense (minimum 1 — toute attaque inflige au moins 1 dégât)
        int effectiveDamage = Mathf.Max(1, damage - baseStats.defense);
        currentHP -= effectiveDamage;
        currentHP = Mathf.Max(0, currentHP);

        if (debugStats)
        {
            Debug.Log($"💥 BuildingStatsComponent {gameObject.name} ({baseStats.buildingName}): {damage} dégâts (effectif: {effectiveDamage}) → HP: {currentHP}/{baseStats.maxHP}");
        }

        OnDamaged?.Invoke();

        // Vérifie la destruction
        if (currentHP <= 0)
        {
            DestroyBuilding();
        }
    }

    // Soigne le bâtiment d'un certain montant (limité au MaxHP)
    public void Heal(int amount)
    {
        if (isDestroyed)
        {
            return;
        }

        int previousHP = currentHP;
        currentHP = Mathf.Min(currentHP + amount, baseStats.maxHP);
        int actualHealing = currentHP - previousHP;

        if (actualHealing > 0)
        {
            if (debugStats)
            {
                Debug.Log($"💚 BuildingStatsComponent {gameObject.name} ({baseStats.buildingName}): Soigné de {actualHealing} → HP: {currentHP}/{baseStats.maxHP}");
            }
            OnHealed?.Invoke();
        }
    }

    // Retourne les HP actuels
    public int GetCurrentHP()
    {
        return currentHP;
    }

    // Retourne les HP maximum
    public int GetMaxHP()
    {
        if (baseStats == null) return 1;
        return baseStats.maxHP;
    }

    // Retourne le pourcentage de vie (0.0 à 1.0)
    public float GetHealthPercentage()
    {
        if (baseStats == null || baseStats.maxHP <= 0) return 0f;
        return (float)currentHP / baseStats.maxHP;
    }

    // Vérifie si le bâtiment est détruit
    public bool IsDead()
    {
        return isDestroyed;
    }



    // === Destruction ===

    // Gère la destruction du bâtiment
    // Désactive le collider, déclenche l'événement, puis détruit le GameObject
    private void DestroyBuilding()
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;

        if (debugStats)
        {
            Debug.Log($"💀 BuildingStatsComponent {gameObject.name} ({baseStats.buildingName}): Détruit !");
        }

        OnBuildingDestroyed?.Invoke();

        // Désactive le collider pour libérer la case sur la grille
        Collider2D buildingCollider = GetComponent<Collider2D>();
        if (buildingCollider != null)
        {
            buildingCollider.enabled = false;
        }

        // Détruit le GameObject après un délai (prépare les animations futures)
        Destroy(gameObject, 3f);
    }

    // === Getters publics supplémentaires ===

    // Retourne le nom du bâtiment (pour logs et UI)
    public string GetBuildingName()
    {
        if (baseStats == null) return "Inconnu";
        return baseStats.buildingName;
    }

    // Retourne si le bâtiment est invulnérable
    public bool IsInvulnerable()
    {
        if (baseStats == null) return false;
        return baseStats.isInvulnerable;
    }



    // === Debug ===

    // Affiche l'état complet du bâtiment dans la console
    [ContextMenu("Debug Building Stats")]
    private void DebugBuildingStats()
    {
        if (baseStats == null)
        {
            Debug.LogWarning($"BuildingStatsComponent {gameObject.name}: baseStats non assigné !");
            return;
        }

        Debug.Log($"=== BuildingStatsComponent Debug - {gameObject.name} ===");
        Debug.Log($"Nom: {baseStats.buildingName}");
        Debug.Log($"HP: {currentHP}/{baseStats.maxHP} ({GetHealthPercentage() * 100:F1}%)");
        Debug.Log($"Défense: {baseStats.defense}");
        Debug.Log($"Invulnérable: {baseStats.isInvulnerable}");
        Debug.Log($"Équipe: {(teamComponent != null ? teamComponent.GetTeamID() : 0)}");
        Debug.Log($"Détruit: {isDestroyed}");
    }

    // Inflige 10 dégâts pour tester la résistance et l'invulnérabilité
    [ContextMenu("Test: Take 10 Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(10);
    }

    // Détruit instantanément le bâtiment (pour tests)
    [ContextMenu("Test: Destroy Building")]
    private void TestDestroyBuilding()
    {
        if (baseStats != null)
        {
            TakeDamage(currentHP + baseStats.defense + 1);
        }
    }

    // Soigne complètement le bâtiment (pour tests)
    [ContextMenu("Test: Full Heal")]
    private void TestFullHeal()
    {
        if (baseStats != null)
        {
            Heal(baseStats.maxHP);
        }
    }
}
