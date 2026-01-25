using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Events;

// Gère les statistiques et l'état de santé d'une unité
// Responsable des HP, dégâts, défense et mort
// Interface principale pour le système de combat
public class UnitStats : MonoBehaviour
{
    [Header("Health Points")]
    [SerializeField] private int maxHP = 100;
    private int currentHP;

    [Header("Combat Stats")]
    [SerializeField] private int attackDamage = 10;      // Dégâts infligés par attaque
    [SerializeField] private int attackRange = 1;        // Portée d'attaque (1 = mêlée, 3+ = distance)
    [SerializeField] private int defense = 0;            // Réduction de dégâts (soustraction simple)
    [SerializeField] private float attackSpeed = 1.0f;   // Temps entre chaque attaque (secondes)

    [Header("Movement Stats")]
    [SerializeField] private float moveSpeed = 2.0f;     // Vitesse de déplacement (unités/seconde)

    [Header("Events")]
    public UnityEvent OnDamaged;    // Déclenché quand l'unité prend des dégâts
    public UnityEvent OnHealed;     // Déclenché quand l'unité est soignée
    public UnityEvent OnDeath;      // Déclenché quand l'unité meurt

    [Header("Debug")]
    [SerializeField] private bool debugStats = true;

    private bool isDead = false;

    // Initialise les HP au maximum au démarrage
    private void Awake()
    {
        currentHP = maxHP;

        if (debugStats)
        {
            Debug.Log($"UnitStats {gameObject.name}: HP={maxHP}, ATK={attackDamage}, DEF={defense}, Range={attackRange}, Speed={moveSpeed}");
        }

        MovementController movementController = GetComponent<MovementController>();
        if (movementController != null)
        {
            movementController.SetMovementSpeed(moveSpeed);
        }
    }

    // Applique des dégâts à l'unité avec formule damage - defense
    // Déclenche l'événement OnDamaged et vérifie la mort
    public void TakeDamage(int damage, GameObject attacker = null)
    {
        if (isDead)
        {
            return; // Unité déjà morte, ignore les dégâts
        }

        // Calcule les dégâts après réduction par la défense
        int effectiveDamage = Mathf.Max(0, damage - defense);

        // Applique les dégâts
        currentHP -= effectiveDamage;
        currentHP = Mathf.Max(0, currentHP); // Ne peut pas descendre sous 0

        if (debugStats)
        {
            Debug.Log($"UnitStats {gameObject.name}: Dégâts reçus {damage} (effectif: {effectiveDamage}) -> HP: {currentHP}/{maxHP}");
        }

        // Déclenche l'événement de dégâts
        OnDamaged?.Invoke();

        // Vérifie si l'unité meurt
        if (currentHP <= 0)
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
    }

    // Soigne l'unité d'un certain montant
    // Ne peut pas dépasser MaxHP
    public void Heal(int amount)
    {
        if (isDead)
        {
            return; // Unité morte, ne peut pas être soignée
        }

        int oldHP = currentHP;
        currentHP += amount;
        currentHP = Mathf.Min(currentHP, maxHP); // Ne dépasse pas le maximum

        int actualHealing = currentHP - oldHP;

        if (debugStats)
        {
            Debug.Log($"UnitStats {gameObject.name}: Soigné de {actualHealing} -> HP: {currentHP}/{maxHP}");
        }

        // Déclenche l'événement de soin
        if (actualHealing > 0)
        {
            OnHealed?.Invoke();
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
            Debug.Log($"💀 UnitStats {gameObject.name}: Unité morte");
        }

        // Déclenche l'événement de mort
        OnDeath?.Invoke();

        // Désactive les composants de gameplay (mouvement, chase, etc.)
        DisableGameplayComponents();

        // Abaisse le sprite pour qu'il soit toujours derrière les unités vivantes
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = -1; // Valeur basse pour être en dessous

            if (debugStats)
            {
                Debug.Log($"UnitStats {gameObject.name}: Sorting order abaissé à -1 (mort)");
            }
        }

        // Joue l'animation de mort
        AnimatorController animatorController = GetComponent<AnimatorController>();
        if (animatorController != null)
        {
            animatorController.PlayDeathAnimation();
        }

        // Détruit l'unité après l'animation (10s pour test, ajuster selon durée réelle)
        Destroy(gameObject, 10f);
    }

    // Désactive tous les composants de gameplay lors de la mort
    // Empêche l'unité morte de bouger, être sélectionnée, etc.
    private void DisableGameplayComponents()
    {
        // Désactive le mouvement sans snap (pour mort naturelle à la position visuelle)
        MovementController movementController = GetComponent<MovementController>();
        if (movementController != null)
        {
            // Arrête juste les coroutines sans forcer un snap de position
            if (movementController.enabled)
            {
                movementController.StopAllCoroutines(); // Arrête les interpolations
            }
            movementController.enabled = false; // Désactive le composant
        }

        // Désactive la chase
        ChaseController chaseController = GetComponent<ChaseController>();
        if (chaseController != null)
        {
            chaseController.StopChase();
            chaseController.enabled = false; 
        }

        // Désactive l'attaque
        AttackController attackController = GetComponent<AttackController>();
        if (attackController != null)
        {
            attackController.StopAttack();
            attackController.enabled = false;
        }

        // Désactive le collider pour ne plus bloquer le pathfinding
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        if (debugStats)
        {
            Debug.Log($"UnitStats {gameObject.name}: Composants de gameplay désactivés");
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
        return maxHP;
    }

    // Retourne le pourcentage de vie (0.0 à 1.0)
    public float GetHealthPercentage()
    {
        if (maxHP == 0) return 0f;
        return (float)currentHP / maxHP;
    }

    // Retourne les dégâts d'attaque
    public int GetAttackDamage()
    {
        return attackDamage;
    }

    // Retourne la portée d'attaque (en cases)
    public int GetAttackRange()
    {
        return attackRange;
    }

    // Retourne la défense
    public int GetDefense()
    {
        return defense;
    }

    // Retourne la vitesse de déplacement
    public float GetMoveSpeed()
    {
        return moveSpeed;
    }

    // Retourne la vitesse d'attaque
    public float GetAttackSpeed() { return attackSpeed; }

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

    // === SETTERS PUBLICS (pour buffs/debuffs futurs) ===

    // Modifie les HP max (buffs, équipement)
    public void SetMaxHP(int newMaxHP)
    {
        maxHP = Mathf.Max(1, newMaxHP);
        currentHP = Mathf.Min(currentHP, maxHP); // Ajuste HP actuel si nécessaire

        if (debugStats)
        {
            Debug.Log($"UnitStats {gameObject.name}: MaxHP modifié -> {maxHP}");
        }
    }

    // Modifie l'attaque (buffs, équipement)
    public void SetAttackDamage(int newAttack)
    {
        attackDamage = Mathf.Max(0, newAttack);

        if (debugStats)
        {
            Debug.Log($"UnitStats {gameObject.name}: Attaque modifiée -> {attackDamage}");
        }
    }

    // Modifie la défense (buffs, équipement)
    public void SetDefense(int newDefense)
    {
        defense = Mathf.Max(0, newDefense);

        if (debugStats)
        {
            Debug.Log($"UnitStats {gameObject.name}: Défense modifiée -> {defense}");
        }
    }

    // Modifie la vitesse (buffs, sorts)
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = Mathf.Max(0.1f, newSpeed);

        // Met à jour MovementController si présent
        MovementController movementController = GetComponent<MovementController>();
        if (movementController != null)
        {
            movementController.SetMovementSpeed(newSpeed);
        }

        if (debugStats)
        {
            Debug.Log($"UnitStats {gameObject.name}: Vitesse modifiée -> {moveSpeed}");
        }
    }

    // === MÉTHODES DE DEBUG ===

    // Affiche les stats actuelles dans la console
    [ContextMenu("Debug Current Stats")]
    private void DebugCurrentStats()
    {
        Debug.Log($"=== UnitStats Debug - {gameObject.name} ===");
        Debug.Log($"HP: {currentHP}/{maxHP} ({GetHealthPercentage() * 100:F1}%)");
        Debug.Log($"Attaque: {attackDamage}");
        Debug.Log($"Défense: {defense}");
        Debug.Log($"Portée: {attackRange} cases");
        Debug.Log($"Vitesse: {moveSpeed}");
        Debug.Log($"Mort: {isDead}");
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
        Heal(maxHP);
    }

    [ContextMenu("Test: Take 20 Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(20);
    }

    // Double la vitesse pour test (pour tests)
    [ContextMenu("Test: Double Speed")]
    private void TestDoubleSpeed()
    {
        SetMoveSpeed(moveSpeed * 2f);
    }

    // Réinitialise la vitesse à 2 (pour tests)
    [ContextMenu("Test: Reset Speed")]
    private void TestResetSpeed()
    {
        SetMoveSpeed(1f);
    }
}
