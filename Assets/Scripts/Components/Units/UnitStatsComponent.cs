using UnityEngine;
using UnityEngine.Events;

// Composant Unity gérant l'état de santé et stats d'une unité
// Pont entre ScriptableObject (données) et Systems (logique)
// Responsable du cycle de vie Unity (events, intégration composants)
// Interface principale pour le système de combat
public class UnitStatsComponent : MonoBehaviour, IDamageable, ISelectable
{
    [Header("Stats Data")]
    [SerializeField] private UnitStatsData baseStats; // ScriptableObject avec stats de base

    [Header("Runtime State")]
    private int currentHP;
    private bool isDead = false;

    [Header("Events")]
    // Initialisation directe pour éviter NullRef lors des AddListener
    public UnityEvent OnDamaged = new UnityEvent();    // Déclenché quand l'unité prend des dégâts
    public UnityEvent OnHealed = new UnityEvent();     // Déclenché quand l'unité est soignée
    public UnityEvent OnDeath = new UnityEvent();      // Déclenché quand l'unité meurt

    [Header("Debug")]
    [SerializeField] private bool debugStats = true;

    // Logique pure de calcul (POCO - testable)
    // Initialisé directement pour éviter NullRef si appelé avant Awake()
    private UnitStatsLogic statsLogic = new UnitStatsLogic();
    private MovementController cachedMovementController;

    // Initialise les HP au maximum au démarrage
    private void Awake()
    {
        // Valide que le SO est assigné
        if (baseStats == null)
        {
            GameLogger.Error($"UnitStatsComponent sur {gameObject.name}: BaseStats non assigné !", this);
            enabled = false;
            return;
        }

        // Initialise les HP
        currentHP = baseStats.maxHP;

        if (debugStats)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"UnitStatsComponent {gameObject.name}: HP={baseStats.maxHP}, ATK={baseStats.attackDamage}, DEF={baseStats.defense}, Range={baseStats.attackRange}, Speed={baseStats.moveSpeed}", this);
        }

        // Synchronise la vitesse avec MovementController
        cachedMovementController = GetComponent<MovementController>();
        if (cachedMovementController != null)
        {
            cachedMovementController.SetMovementSpeed(baseStats.moveSpeed);
        }

        // Auto-enregistrement dans EntityRegistry (initialisé avant via [DefaultExecutionOrder(-100)])
        if (EntityRegistry.Instance != null)
        {
            EntityRegistry.Instance.RegisterEntity(this, gameObject);
        }
        else if (debugStats)
        {
            GameLogger.Warn(GameLogger.Category.Combat, $"⚠️ UnitStatsComponent {gameObject.name}: EntityRegistry introuvable, enregistrement ignoré", this);
        }

    }

    // Désenregistre l'unité de l'EntityRegistry lors de la destruction
    private void OnDestroy()
    {
        if (EntityRegistry.Instance != null)
        {
            EntityRegistry.Instance.UnregisterEntity(gameObject);
        }
    }

    // Déclenche le VFX de spawn — appelé uniquement après un Instantiate dynamique
    public void NotifySpawned()
    {
        if (baseStats?.onSpawnVFX != null)
            VFXApplier.Apply(baseStats.onSpawnVFX, gameObject);
    }

    // Applique des dégâts à l'unité avec formule damage - defense
    // Déclenche l'événement OnDamaged et vérifie la mort
    public void TakeDamage(int damage, GameObject attacker = null)
    {
        if (isDead)
        {
            return; // Unité déjà morte, ignore les dégâts
        }

        // Délègue le calcul à la logique pure
        int effectiveDamage = statsLogic.CalculateEffectiveDamage(damage, baseStats.defense);

        // Applique les dégâts
        currentHP -= effectiveDamage;
        currentHP = Mathf.Max(0, currentHP);

        cachedMovementController?.ForceExitIdle();

        if (debugStats)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"UnitStatsComponent {gameObject.name}: Dégâts reçus {damage} (effectif: {effectiveDamage}) -> HP: {currentHP}/{baseStats.maxHP}", this);
        }

        // Déclenche l'événement de dégâts
        OnDamaged?.Invoke();

        if (debugStats)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"🔔 UnitStatsComponent {gameObject.name}: Event OnDamaged déclenché, {OnDamaged.GetPersistentEventCount()} listeners", this);
        }

        // Vérifie si l'unité meurt
        if (statsLogic.ShouldDie(currentHP))
        {
            Die();
            return;
        }

        // Joue l'animation Hit si attaquant fourni
        if (attacker != null)
        {
            AnimatorController animatorController = GetComponent<AnimatorController>();
            if (animatorController != null)
            {
                Vector2Int attackerPos = GridManager.Instance.WorldToGrid(attacker.transform.position);
                Vector2Int victimPos = GridManager.Instance.WorldToGrid(transform.position);
                animatorController.PlayHitAnimation(attackerPos, victimPos);
            }
        }

        // Déclenche une riposte automatique si l'attaquant est une cible ennemie valide
        if (attacker == null)
        {
            return;
        }

        IDamageable attackerDamageable = attacker.GetComponent<IDamageable>();
        if (attackerDamageable == null || attackerDamageable.IsDead() || attackerDamageable.IsInvulnerable())
        {
            return;
        }

        ITeamMember localTeamMember = GetComponent<ITeamMember>();
        ITeamMember attackerTeamMember = attacker.GetComponent<ITeamMember>();
        if (localTeamMember == null || attackerTeamMember == null || !localTeamMember.IsEnemy(attackerTeamMember))
        {
            return;
        }

        AttackController attackController = GetComponent<AttackController>();
        if (attackController == null || !attackController.enabled)
        {
            return;
        }

        if (attackController.IsAttacking())
        {
            return;
        }

        if (attackController.HasManualOrder())
        {
            return;
        }

        ChaseController chaseController = GetComponent<ChaseController>();
        bool isChasing = chaseController != null && chaseController.IsChasing();
        MovementController movementController = GetComponent<MovementController>();
        if (movementController != null &&
            movementController.GetCurrentState() == MovementController.MovementState.Moving &&
            !isChasing)
        {
            return;
        }

        attackController.InterruptChaseAndAttack(attacker);

        if (debugStats)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"⚔️ Riposte auto sur {attacker.name}", this);
        }
    }

    // Soigne l'unité d'un certain montant
    // Ne peut pas dépasser MaxHP
    public void Heal(int amount)
    {
        if (isDead)
        {
            return; // Unité morte, ne peut pas être soignée
        }

        // Délègue le calcul à la logique pure
        var (newHP, actualHealing) = statsLogic.CalculateHealing(currentHP, baseStats.maxHP, amount);
        currentHP = newHP;

        if (debugStats)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"UnitStatsComponent {gameObject.name}: Soigné de {actualHealing} -> HP: {currentHP}/{baseStats.maxHP}", this);
        }

        // Déclenche l'événement de soin
        if (actualHealing > 0)
        {
            OnHealed?.Invoke();
        }
    }

    // Définit directement les HP actuels (utilisé pour propagation HP Bud→Rootborn, etc.)
    // Clampe automatiquement entre 0 et maxHP
    public void SetCurrentHP(int hp)
    {
        if (isDead) return;

        currentHP = Mathf.Clamp(hp, 0, baseStats.maxHP);

        if (debugStats)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"UnitStatsComponent {gameObject.name}: HP définis à {currentHP}/{baseStats.maxHP}", this);
        }
    }

    // Gère la mort de l'unité
    // Déclenche l'événement OnDeath et désactive les composants
    private void Die()
    {
        if (isDead)
        {
            return; // Déjà mort
        }

        isDead = true;

        if (debugStats)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"💀 UnitStatsComponent {gameObject.name}: Unité morte", this);
        }

        // Déclenche l'événement de mort
        OnDeath?.Invoke();

        if (baseStats.onDeathVFX != null)
            VFXApplier.Apply(baseStats.onDeathVFX, gameObject);

        // Désactive les composants de gameplay
        DisableGameplayComponents();

        // Libère la case logique immédiatement — autres unités peuvent traverser le cadavre
        if (EntityRegistry.Instance != null)
        {
            EntityRegistry.Instance.ReleaseLogicalPosition(gameObject);
        }

        // Abaisse le sprite pour qu'il soit derrière les unités vivantes
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = -1;

            if (debugStats)
            {
                GameLogger.Log(GameLogger.Category.Combat, $"UnitStatsComponent {gameObject.name}: Sorting order abaissé à -1 (mort)", this);
            }
        }

        // Joue l'animation de mort
        AnimatorController animatorController = GetComponent<AnimatorController>();
        if (animatorController != null)
        {
            animatorController.PlayDeathAnimation();
        }

        // Détruit l'unité après l'animation
        EntitySpawnManager.Instance?.NotifyEntityDestroyed(gameObject);
        Destroy(gameObject, baseStats.deathDelay);
    }

    // Désactive tous les composants de gameplay lors de la mort
    // Empêche l'unité morte de bouger, être sélectionnée, etc.
    private void DisableGameplayComponents()
    {
        UnitFreezer.Freeze(gameObject, disableComponents: true, freezeAnimator: false);

        if (debugStats)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"UnitStatsComponent {gameObject.name}: Composants de gameplay désactivés", this);
        }
    }

    // === GETTERS PUBLICS ===

    // Retourne les HP actuels
    public int GetCurrentHP()
    {
        return currentHP;
    }

    // Retourne les HP maximum
    public int GetMaxHP()
    {
        // Protection contre appel avant Awake
        if (baseStats == null) return 100;
        return baseStats.maxHP;
    }

    // Retourne le pourcentage de vie (0.0 à 1.0)
    public float GetHealthPercentage()
    {
        // Protection contre appel avant Awake
        if (baseStats == null) return 0f;
        return statsLogic.CalculateHealthPercentage(currentHP, baseStats.maxHP);
    }

    // Retourne les dégâts d'attaque de base
    public int GetAttackDamage()
    {
        if (baseStats == null) return 0;
        return baseStats.attackDamage;
    }

    // Retourne la portée d'attaque (en cases)
    public int GetAttackRange()
    {
        if (baseStats == null) return 1;
        return baseStats.attackRange;
    }

    // Retourne la défense
    public int GetDefense()
    {
        if (baseStats == null) return 0;
        return baseStats.defense;
    }

    // Retourne la vitesse de déplacement
    public float GetMoveSpeed()
    {
        if (baseStats == null) return 1f;
        return baseStats.moveSpeed;
    }

    // Retourne la vitesse d'attaque
    public float GetAttackSpeed()
    {
        if (baseStats == null) return 1f;
        return baseStats.attackSpeed;
    }

    // Vérifie si l'unité est morte
    public bool IsDead()
    {
        return isDead;
    }

    // Vérifie si l'unité est vivante
    public bool IsAlive()
    {
        return !isDead;
    }

    // Les unités ne sont jamais invulnérables — implémentation du contrat IDamageable
    // Seuls certains bâtiments (ex: Nexus) peuvent l'être via BuildingStatsData.isInvulnerable
    public bool IsInvulnerable()
    {
        return false;
    }

    // Retourne si l'unité est un Rootborn (lu depuis le ScriptableObject)
    public bool IsRootborn()
    {
        if (baseStats == null) return false;
        return baseStats.isRootborn;
    }

    // Retourne le ScriptableObject de stats (utilisé par UnitTypeHelper)
    public UnitStatsData GetBaseStats()
    {
        return baseStats;
    }

    public Sprite GetPortrait()
    {
        return GetBaseStats()?.portrait;
    }

    public string GetDisplayName()
    {
        return GetBaseStats()?.unitName ?? gameObject.name;
    }

    // === MÉTHODES DE DEBUG ===

    // Affiche les stats actuelles dans la console
    [ContextMenu("Debug Current Stats")]
    private void DebugCurrentStats()
    {
        if (baseStats == null)
        {
            GameLogger.Warn(GameLogger.Category.Combat, $"UnitStatsComponent {gameObject.name}: baseStats non assigné!", this);
            return;
        }

        GameLogger.Log(GameLogger.Category.Combat, $"=== UnitStatsComponent Debug - {gameObject.name} ===", this);
        GameLogger.Log(GameLogger.Category.Combat, $"HP: {currentHP}/{baseStats.maxHP} ({GetHealthPercentage() * 100:F1}%)", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Attaque: {baseStats.attackDamage}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Défense: {baseStats.defense}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Portée: {baseStats.attackRange} cases", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Vitesse: {baseStats.moveSpeed}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Mort: {isDead}", this);
    }

    // Tue instantanément l'unité (pour tests)
    [ContextMenu("Kill Unit")]
    private void KillUnit()
    {
        TakeDamage(currentHP);
    }

    // Soigne complètement l'unité (pour tests)
    [ContextMenu("Full Heal")]
    private void FullHeal()
    {
        if (baseStats == null) return;
        Heal(baseStats.maxHP);
    }

    // Test de dégâts
    [ContextMenu("Test: Take 20 Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(20);
    }

}
