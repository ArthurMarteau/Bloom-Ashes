using System.Collections.Generic;
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
    public UnityEvent OnTierUpgraded = new UnityEvent();      // Déclenché quand le bâtiment monte de tier

    [Header("Debug")]
    [SerializeField] private bool debugStats = true;

    // État runtime
    private int currentHP;
    private int runtimeMaxHP;
    private bool isDestroyed = false;
    private int currentTier = 1;

    // Initialise les HP au maximum au démarrage
    private void Awake()
    {
        if (baseStats == null)
        {
            GameLogger.Error($"🏛️ BuildingStatsComponent sur {gameObject.name}: baseStats non assigné !", this);
            enabled = false;
            return;
        }

        runtimeMaxHP = baseStats.maxHP;
        currentHP = runtimeMaxHP;
        teamComponent = GetComponent<TeamComponent>();

        if (teamComponent == null)
            GameLogger.Warn(GameLogger.Category.Combat, $"⚠️ BuildingStatsComponent {gameObject.name}: TeamComponent manquant — équipe non définie", this);

        if (debugStats)
        {
            string invulnerableStr = baseStats.isInvulnerable ? " | INVULNÉRABLE" : "";
            int teamID = teamComponent != null ? teamComponent.GetTeamID() : 0;
            GameLogger.Log(GameLogger.Category.Combat, $"🏛️ BuildingStatsComponent {gameObject.name} ({baseStats.buildingName}): HP={currentHP}, DEF={baseStats.defense}{invulnerableStr}, Équipe={teamID}", this);
        }

        // Auto-enregistrement dans EntityRegistry (initialisé avant via [DefaultExecutionOrder(-100)])
        if (EntityRegistry.Instance != null)
        {
            EntityRegistry.Instance.RegisterEntity(this, gameObject);
        }
        else if (debugStats)
        {
            GameLogger.Warn(GameLogger.Category.Combat, $"⚠️ BuildingStatsComponent {gameObject.name}: EntityRegistry introuvable, enregistrement ignoré", this);
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
                GameLogger.Log(GameLogger.Category.Combat, $"🛡️ BuildingStatsComponent {gameObject.name} ({baseStats.buildingName}): Invulnérable — {damage} dégâts ignorés", this);
            }
            return;
        }

        // Applique la réduction par la défense (minimum 1 — toute attaque inflige au moins 1 dégât)
        int effectiveDamage = Mathf.Max(1, damage - baseStats.defense);
        currentHP -= effectiveDamage;
        currentHP = Mathf.Max(0, currentHP);

        if (debugStats)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"💥 BuildingStatsComponent {gameObject.name} ({baseStats.buildingName}): {damage} dégâts (effectif: {effectiveDamage}) → HP: {currentHP}/{GetMaxHP()}", this);
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
        currentHP = Mathf.Min(currentHP + amount, GetMaxHP());
        int actualHealing = currentHP - previousHP;

        if (actualHealing > 0)
        {
            if (debugStats)
            {
                GameLogger.Log(GameLogger.Category.Combat, $"💚 BuildingStatsComponent {gameObject.name} ({baseStats.buildingName}): Soigné de {actualHealing} → HP: {currentHP}/{GetMaxHP()}", this);
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
        return Mathf.Max(1, runtimeMaxHP);
    }

    // Retourne le pourcentage de vie (0.0 à 1.0)
    public float GetHealthPercentage()
    {
        int maxHP = GetMaxHP();
        if (maxHP <= 0) return 0f;
        return (float)currentHP / maxHP;
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
            GameLogger.Log(GameLogger.Category.Combat, $"💀 BuildingStatsComponent {gameObject.name} ({baseStats.buildingName}): Détruit !", this);
        }

        OnBuildingDestroyed?.Invoke();

        // Désactive le collider pour libérer la case sur la grille
        Collider2D buildingCollider = GetComponent<Collider2D>();
        if (buildingCollider != null)
        {
            buildingCollider.enabled = false;
        }

        // Détruit le GameObject après un délai (prépare les animations futures)
        EntitySpawnManager.Instance?.NotifyEntityDestroyed(gameObject);
        Destroy(gameObject, 10f);
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

    // Retourne la tile d'ancrage (bas-gauche) du bâtiment en coordonnées de grille
    // Pour un bâtiment 1x1, c'est simplement sa position sur la grille
    // Pour un bâtiment 2x2, c'est le coin bas-gauche des 4 tiles occupées
    public Vector2Int GetAnchorGridPosition()
    {
        if (GridManager.Instance == null) return Vector2Int.zero;
        return GridManager.Instance.WorldToGrid(transform.position);
    }

    // Retourne toutes les tiles occupées par ce bâtiment à partir de la tile d'ancrage
    // Utilisé par CombatSystem pour calculer les positions d'attaque valides autour du bâtiment
    // Ex: bâtiment 2x2 ancré en (3,3) → retourne (3,3), (4,3), (3,4), (4,4)
    public System.Collections.Generic.List<Vector2Int> GetOccupiedTiles()
    {
        var tiles = new System.Collections.Generic.List<Vector2Int>();

        if (baseStats == null || GridManager.Instance == null) return tiles;

        Vector2Int anchor = GetAnchorGridPosition();

        for (int x = 0; x < baseStats.sizeX; x++)
        {
            for (int y = 0; y < baseStats.sizeY; y++)
            {
                tiles.Add(new Vector2Int(anchor.x + x, anchor.y + y));
            }
        }

        return tiles;
    }

    // Retourne la valeur de défense du bâtiment
    public int GetDefense()
    {
        if (baseStats == null) return 0;
        return baseStats.defense;
    }

    // Retourne le tier actuel du bâtiment (1 à 4)
    public int GetCurrentTier() => currentTier;

    // Retourne le tier maximum — toujours 4 pour toutes les factions
    public int GetMaxTier() => 4;

    // Tente de monter le bâtiment d'un tier — dépense le Watersap si possible
    // Retourne true si la montée de tier a réussi, false sinon
    public bool UpgradeTier(int teamID, int cost)
    {
        if (currentTier >= 4)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"🏛️ BuildingStatsComponent {gameObject.name} : tier max atteint ({currentTier}/4)", this);
            return false;
        }

        if (WatersapManager.Instance == null || !WatersapManager.Instance.CanSpend(teamID, cost))
            return false;

        WatersapManager.Instance.SpendWatersap(teamID, cost);
        currentTier++;
        OnTierUpgraded.Invoke();

        GameLogger.Log(GameLogger.Category.Combat, $"🏛️ BuildingStatsComponent {gameObject.name} : tier {currentTier - 1} → {currentTier} (équipe {teamID})", this);
        return true;
    }

    // Retourne toutes les unités de producibleUnits sans filtre par tier
    // Utilisé par le panel Rootborn — le filtre (grisé/verrouillé) est géré côté UI
    public List<UnitStatsData> GetUnlockedUnits()
    {
        if (baseStats == null || baseStats.producibleUnits == null)
            return new List<UnitStatsData>();

        return new List<UnitStatsData>(baseStats.producibleUnits);
    }

    // Retourne uniquement les unités dont tierRequired <= currentTier
    // Utilisé par le panel bâtiment — seules les unités disponibles sont affichées
    public List<UnitStatsData> GetAvailableUnits()
    {
        if (baseStats == null || baseStats.producibleUnits == null)
            return new List<UnitStatsData>();

        List<UnitStatsData> available = new List<UnitStatsData>();
        foreach (UnitStatsData unit in baseStats.producibleUnits)
        {
            if (unit != null && unit.tierRequired <= currentTier)
                available.Add(unit);
        }
        return available;
    }

    // Retourne le ScriptableObject de stats de base (pour l'UI)
    public BuildingStatsData GetBaseStats()
    {
        return baseStats;
    }

    // Remplace les HP courants par une valeur externe (utilisé par BudComponent pour hériter des HP du Rootborn)
    // Clampée entre 0 et maxHP pour éviter les incohérences
    public void SetCurrentHP(int hp)
    {
        if (baseStats == null) return;
        currentHP = Mathf.Clamp(hp, 0, GetMaxHP());
    }

    public void SetMaxHP(int maxHP)
    {
        if (baseStats == null) return;
        runtimeMaxHP = Mathf.Max(1, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0, runtimeMaxHP);
    }

    // Retourne la taille du bâtiment en tiles (X, Y)
    public Vector2Int GetSize()
    {
        if (baseStats == null) return Vector2Int.one;
        return new Vector2Int(baseStats.sizeX, baseStats.sizeY);
    }



    // === Debug ===

    // Affiche l'état complet du bâtiment dans la console
    [ContextMenu("Debug Building Stats")]
    private void DebugBuildingStats()
    {
        if (baseStats == null)
        {
            GameLogger.Warn(GameLogger.Category.Combat, $"BuildingStatsComponent {gameObject.name}: baseStats non assigné !", this);
            return;
        }

        GameLogger.Log(GameLogger.Category.Combat, $"=== BuildingStatsComponent Debug - {gameObject.name} ===", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Nom: {baseStats.buildingName}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"HP: {currentHP}/{GetMaxHP()} ({GetHealthPercentage() * 100:F1}%)", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Défense: {baseStats.defense}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Invulnérable: {baseStats.isInvulnerable}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Équipe: {(teamComponent != null ? teamComponent.GetTeamID() : 0)}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Détruit: {isDestroyed}", this);
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
