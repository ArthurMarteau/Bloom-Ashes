using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gère l'attaque persistante d'une cible ennemie
// L'unité poursuit et attaque sa cible tant que l'ordre persiste
// S'intègre avec MovementController pour se positionner à portée
public class AttackController : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float checkInterval = 0.2f;       // Intervalle vérification position/état cible
    [SerializeField] private bool debugAttack = true;
    [SerializeField] private bool autoAttackEnabled = true;    // Active l'attaque automatique des ennemis à portée

    // État de l'attaque
    private GameObject currentTarget;
    private bool isAttacking = false;
    private float lastAttackTime;
    private float lastCheckTime;  
    private bool isPerformingAttack = false;

    // Références
    private MovementController movementController;
    private UnitStats unitStats;
    private TeamComponent teamComponent;
    private GameObject unitGameObject;
    private Coroutine damageCoroutine;
    private AnimatorController animatorController; // Cache pour AnimatorController

    // Offsets prédéfinis pour chaque portée d'attaque (même logique que ChaseController)
    // Range 1 : Mêlée - 8 cases adjacentes
    private static readonly Vector2Int[] range1Offsets = {
    new Vector2Int(0, 1),   // Nord
    new Vector2Int(1, 0),   // Est
    new Vector2Int(0, -1),  // Sud
    new Vector2Int(-1, 0),  // Ouest
    new Vector2Int(1, 1),   // Nord-Est
    new Vector2Int(1, -1),  // Sud-Est
    new Vector2Int(-1, -1), // Sud-Ouest
    new Vector2Int(-1, 1)   // Nord-Ouest
};

    // Range 2 : Portée 1 case - 16 cases (carré 5x5 sans centre)
    private static readonly Vector2Int[] range2Offsets = {
    // Reprend range1
    new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0),
    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1),
    // Deuxième couronne
    new Vector2Int(0, 2), new Vector2Int(2, 0), new Vector2Int(0, -2), new Vector2Int(-2, 0),
    new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
    new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2)
};

    // Range 3 : Portée 2 cases - 24 cases (carré 7x7 sans centre et coins extrêmes)
    private static readonly Vector2Int[] range3Offsets = {
    // Reprend range2
    new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0),
    new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1),
    new Vector2Int(0, 2), new Vector2Int(2, 0), new Vector2Int(0, -2), new Vector2Int(-2, 0),
    new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
    new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2),
    // Troisième couronne
    new Vector2Int(0, 3), new Vector2Int(3, 0), new Vector2Int(0, -3), new Vector2Int(-3, 0),
    new Vector2Int(1, 3), new Vector2Int(3, 1), new Vector2Int(3, -1), new Vector2Int(1, -3),
    new Vector2Int(-1, -3), new Vector2Int(-3, -1), new Vector2Int(-3, 1), new Vector2Int(-1, 3),
    new Vector2Int(2, 2), new Vector2Int(2, -2), new Vector2Int(-2, -2), new Vector2Int(-2, 2)
};

    // Initialise les références
    private void Awake()
    {
        unitGameObject = gameObject;
        unitStats = GetComponent<UnitStats>();
        teamComponent = GetComponent<TeamComponent>();

        if (unitStats == null)
        {
            Debug.LogError($"AttackController sur {gameObject.name}: UnitStats non trouvé !");
        }

        if (teamComponent == null)
        {
            Debug.LogError($"AttackController sur {gameObject.name}: TeamComponent non trouvé !");
        }

        animatorController = GetComponent<AnimatorController>();
        if (animatorController == null)
        {
            Debug.LogWarning($"AttackController sur {gameObject.name}: AnimatorController non trouvé!");
        }

        movementController = GetComponent<MovementController>();
        if (movementController == null)
        {
            Debug.LogError($"AttackController sur {gameObject.name}: MovementController non trouvé !");
        }
    }

    private void Update()
    {
        // Vérifie si l'unité est morte (ne doit plus rien faire)
        if (unitStats != null && unitStats.IsDead())
        {
            return;
        }


        if (!isAttacking || currentTarget == null)
        {
            // Si aucune attaque en cours, vérifie automatiquement les ennemis à portée
            if (autoAttackEnabled && Time.time - lastCheckTime >= checkInterval)
            {
                lastCheckTime = Time.time;
                CheckForAutoAttack();
            }
            return;
        }

        // Vérifie l'état du mouvement
        MovementController mc = GetComponent<MovementController>();
        if (mc != null)
        {
            MovementController.MovementState state = mc.GetCurrentState();


            // Si en mouvement, on attend
            if (state == MovementController.MovementState.Moving)
            {
                return;
            }

            // Si vient d'arriver (Idle ou Arrived), force un check immédiat
            if (state == MovementController.MovementState.Idle || state == MovementController.MovementState.Arrived)
            {
                // Ne pas interrompre une attaque en cours d'exécution
                if (isPerformingAttack)
                {
                    if (debugAttack)
                    {
                        Debug.Log($"🔄 Update {gameObject.name}: Attaque en cours, on attend");
                    }
                    return;
                }

                if (debugAttack)
                {
                    Debug.Log($"🔄 Update {gameObject.name}: État Idle/Arrived → Appel ProcessAttackLogic");
                }
                // Force ProcessAttackLogic immédiatement sans attendre checkInterval
                ProcessAttackLogic();
                lastCheckTime = Time.time;
                return;
            }
        }

        // Vérifie périodiquement l'état de la cible (fallback)
        if (Time.time - lastCheckTime >= checkInterval)
        {
            lastCheckTime = Time.time;
            ProcessAttackLogic();
        }
    }

    // Vérifie automatiquement s'il y a des ennemis à portée et déclenche l'attaque
    // Appelé uniquement si l'unité n'a pas d'ordre en cours
    private void CheckForAutoAttack()
    {
        // Vérifie si l'unité attaquante est morte
        if (unitStats.IsDead())
        {
            if (debugAttack)
            {
                Debug.LogWarning($"AttackController {gameObject.name}: Unité morte, ne peut pas attaquer");
            }
            return;
        }

        // Vérifie que l'unité n'est pas sous un autre ordre ou morte
        if (!CanAutoAttack())
        {
            return;
        }

        // Recherche tous les ennemis à portée
        GameObject nearestEnemy = FindNearestEnemyInRange();

        if (nearestEnemy != null)
        {
            if (debugAttack)
            {
                Debug.Log($"🎯 AttackController {gameObject.name}: Ennemi détecté à portée ({nearestEnemy.name}), attaque automatique !");
            }
            StartAttack(nearestEnemy);
        }
    }

    // Vérifie si l'unité peut déclencher une attaque automatique
    // Retourne false si l'unité est déjà sous un autre ordre
    private bool CanAutoAttack()
    {
        // Vérifie MovementController
        if (movementController != null)
        {
            MovementController.MovementState state = movementController.GetCurrentState();
            if (state == MovementController.MovementState.Moving ||
                state == MovementController.MovementState.Blocked)
            {
                return false;
            }
        }

        return true;
    }

    // Trouve l'ennemi le plus proche à portée d'attaque
    // Retourne null si aucun ennemi trouvé
    private GameObject FindNearestEnemyInRange()
    {
        if (unitStats == null || teamComponent == null)
        {
            return null;
        }

        Vector2Int myPosition = movementController.GetCurrentGridPosition();
        int attackRange = unitStats.GetAttackRange();

        GameObject nearestEnemy = null;
        int shortestDistance = int.MaxValue;

        // Récupère toutes les unités ennemies dans la scène
        float boxSize = (attackRange * 2 + 1) * GridManager.Instance.GetCellSize();
        Vector2 center = transform.position;

        // Détecte tous les colliders dans la zone (layer Units = 8)
        Collider2D[] nearbyColliders = Physics2D.OverlapBoxAll(center, new Vector2(boxSize, boxSize), 0f, 1 << 8);

        foreach (Collider2D collider in nearbyColliders)
        {
            UnitStats unitStat = collider.GetComponent<UnitStats>();
            if (unitStat == null) continue;

            GameObject otherUnit = unitStat.gameObject;

            // Ignore soi-même
            if (otherUnit == unitGameObject)
            {
                continue;
            }

            // Vérifie que c'est un ennemi vivant
            if (!teamComponent.IsEnemy(otherUnit) || unitStat.IsDead())
            {
                continue;
            }

            // Calcule la distance
            Vector2Int enemyPosition = GridManager.Instance.WorldToGrid(otherUnit.transform.position);
            int distance = Mathf.Max(Mathf.Abs(myPosition.x - enemyPosition.x),
                                    Mathf.Abs(myPosition.y - enemyPosition.y));

            // Vérifie si à portée
            if (distance <= attackRange && distance < shortestDistance)
            {
                shortestDistance = distance;
                nearestEnemy = otherUnit;
            }
        }

        return nearestEnemy;
    }

    // Démarre l'attaque d'une cible
    // Interface principale appelée par OrderManager lors d'un clic droit sur ennemi
    public void StartAttack(GameObject target)
    {
        if (debugAttack)
        {
            Debug.Log($"🎯 StartAttack appelé pour {gameObject.name} → cible {(target != null ? target.name : "null")}");
        }

        if (target == null)
        {
            Debug.LogWarning($"AttackController {gameObject.name}: Tentative d'attaque sur cible null");
            return;
        }

        if (target == unitGameObject)
        {
            Debug.LogWarning($"AttackController {gameObject.name}: Une unité ne peut pas s'attaquer elle-même");
            return;
        }

        // Vérifie que la cible est bien ennemie
        if (teamComponent != null && !teamComponent.IsEnemy(target))
        {
            Debug.LogWarning($"AttackController {gameObject.name}: {target.name} n'est pas un ennemi");
            return;
        }

        // Arrête l'attaque précédente si existe
        if (isAttacking)
        {
            StopAttack();
        }

        currentTarget = target;
        isAttacking = true;
        lastCheckTime = Time.time;
        lastAttackTime = 0f; // Permet attaque immédiate si à portée

        if (debugAttack)
        {
            Debug.Log($"AttackController {gameObject.name}: Début attaque de {target.name}");
        }

        // Lance immédiatement la logique d'attaque
        ProcessAttackLogic();
    }

    // Arrête l'attaque en cours
    // Appelé lors d'un nouvel ordre manuel ou destruction de la cible
    public void StopAttack()
    {
        if (!isAttacking)
        {
            return;
        }

        isAttacking = false;
        isPerformingAttack = false;
        currentTarget = null;

        // Arrête la coroutine de dégâts si active
        if (damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
            damageCoroutine = null;
        }

        // Retourne à l'animation Idle
        if (animatorController != null)
        {
            animatorController.PlayIdleAnimation();
        }

        if (debugAttack)
        {
            Debug.Log($"AttackController {gameObject.name}: Attaque arrêtée, retour en Idle");
        }
    }

    // Logique principale d'attaque appelée périodiquement
    // Vérifie validité cible, portée, et exécute attaque ou déplacement
    private void ProcessAttackLogic()
    {
        // Vérifie que la cible est toujours valide
        if (!IsTargetValid())
        {
            if (debugAttack)
            {
                Debug.Log($"AttackController {gameObject.name}: Cible invalide, arrêt attaque");
            }
            StopAttack();
            return;
        }

        // Vérifie si la cible est à portée
        if (IsTargetInRange())
        {
            // À portée → attaque si cooldown écoulé
            if (Time.time - lastAttackTime >= unitStats.GetAttackSpeed())
            {
                PerformAttack();
                lastAttackTime = Time.time;
            }
        }
        else
        {
            // Hors portée → annule les dégâts en cours et déplace vers la cible
            if (damageCoroutine != null)
            {
                StopCoroutine(damageCoroutine);
                damageCoroutine = null;

                if (debugAttack)
                {
                    Debug.Log($"AttackController {gameObject.name}: Cible hors portée, annulation des dégâts en cours");
                }
            }

            MoveToRange();
        }
    }

    // Vérifie si la cible est toujours valide
    // Retourne false si cible détruite, morte, ou plus ennemie
    private bool IsTargetValid()
    {
        // Cible détruite
        if (currentTarget == null)
        {
            return false;
        }

        // Cible morte
        UnitStats targetStats = currentTarget.GetComponent<UnitStats>();
        if (targetStats != null && targetStats.IsDead())
        {
            return false;
        }

        // Cible plus ennemie (changement d'équipe)
        if (teamComponent != null && !teamComponent.IsEnemy(currentTarget))
        {
            return false;
        }

        return true;
    }

    // Vérifie si la cible est à portée d'attaque ET si l'unité est au centre de sa case
    // Utilise la portée définie dans UnitStats
    // Utilise la position de grille actuelle (transform.position converti)
    private bool IsTargetInRange()
    {
        if (unitStats == null || currentTarget == null)
        {
            return false;
        }

        // Utilise uniquement les positions LOGIQUES
        Vector2Int myPosition = movementController.GetCurrentGridPosition();
        Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);

        int attackRange = unitStats.GetAttackRange();
        int distance = Mathf.Max(Mathf.Abs(myPosition.x - targetPosition.x), Mathf.Abs(myPosition.y - targetPosition.y));

        bool inRange = distance <= attackRange;

        if (debugAttack && inRange)
        {
            Debug.Log($"AttackController {gameObject.name}: Au centre ET à portée → Prêt à attaquer");
        }

        return inRange;
    }

    // Déplace l'unité à portée de la cible
    // Utilise MovementController pour le déplacement
    // Utilise la position de grille actuelle pour calculs
    private void MoveToRange()
    {
        if (movementController == null || currentTarget == null)
        {
            return;
        }

        Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);
        Vector2Int myPosition = movementController.GetCurrentGridPosition();

        int attackRange = unitStats.GetAttackRange();

        // DEBUG: Vérifie si on est déjà à portée
        int currentDistance = Mathf.Max(Mathf.Abs(myPosition.x - targetPosition.x), Mathf.Abs(myPosition.y - targetPosition.y));
        if (debugAttack)
        {
            Debug.Log($"🔍 MoveToRange: Position {myPosition}, Cible {targetPosition}, Distance {currentDistance}/{attackRange}");
        }

        // Trouve une position à portée de la cible
        Vector2Int bestPosition = FindPositionInRange(targetPosition, myPosition, attackRange);

        if (bestPosition == Vector2Int.zero)
        {
            if (debugAttack)
            {
                Debug.LogWarning($"AttackController {gameObject.name}: Aucune position accessible à portée de {currentTarget.name}");
            }
            return;
        }

        // Vérifie qu'on ne demande pas de bouger vers notre position actuelle
        if (bestPosition == myPosition)
        {
            if (debugAttack)
            {
                Debug.LogWarning($"⚠️ MoveToRange: bestPosition == myPosition ! Pas de mouvement nécessaire");
            }
            return;
        }

        if (bestPosition == Vector2Int.zero)
        {
            if (debugAttack)
            {
                Debug.LogWarning($"AttackController {gameObject.name}: Aucune position accessible à portée de {currentTarget.name}");
            }
            return;
        }

        // Envoie l'ordre de mouvement (le MovementController gère le changement de direction)
        movementController.MoveTo(bestPosition);

        if (debugAttack)
        {
            Debug.Log($"AttackController {gameObject.name}: Déplacement vers {bestPosition} (cible: {targetPosition}, portée: {attackRange})");
        }
    }

    // Trouve la case à portée d'attaque avec le chemin le plus court
    // Utilise des listes d'offsets prédéfinies (même logique que ChaseController)
    // Calcule le vrai chemin vers chaque case et retourne la meilleure
    private Vector2Int FindPositionInRange(Vector2Int targetPosition, Vector2Int myPosition, int attackRange)
    {
        // Sélectionne la liste d'offsets selon la portée
        Vector2Int[] offsets;
        switch (attackRange)
        {
            case 1:
                offsets = range1Offsets;
                break;
            case 2:
                offsets = range2Offsets;
                break;
            case 3:
                offsets = range3Offsets;
                break;
            default:
                if (debugAttack)
                {
                    Debug.LogWarning($"AttackController {gameObject.name}: Range {attackRange} non supportée, utilisation range 1");
                }
                offsets = range1Offsets;
                break;
        }

        Vector2Int bestCell = Vector2Int.zero;
        int shortestPathLength = int.MaxValue;

        // Cherche la case avec le chemin le plus court
        foreach (Vector2Int offset in offsets)
        {
            Vector2Int cellInRange = targetPosition + offset;

            // Vérifie limites de grille
            if (!GridManager.Instance.IsInBounds(cellInRange))
            {
                continue;
            }

            // Vérifie que la case est accessible
            if (!GridManager.Instance.IsWalkableFor(cellInRange, unitGameObject))
            {
                continue;
            }

            // Calcule le vrai chemin depuis notre position
            List<Vector2Int> path = PathFinding.FindPath(myPosition, cellInRange, unitGameObject);

            // Ignore si pas de chemin trouvé
            if (path == null || path.Count == 0)
            {
                continue;
            }

            // Compare la longueur du chemin
            if (path.Count < shortestPathLength)
            {
                shortestPathLength = path.Count;
                bestCell = cellInRange;
            }
        }

        // Retourne la meilleure case trouvée
        if (bestCell != Vector2Int.zero)
        {
            if (debugAttack)
            {
                Debug.Log($"AttackController {gameObject.name}: Meilleure case trouvée {bestCell} avec chemin de {shortestPathLength} cases (range {attackRange})");
            }
            return bestCell;
        }

        // Aucune case à portée accessible
        if (debugAttack)
        {
            Debug.LogWarning($"AttackController {gameObject.name}: Aucune case à portée {attackRange} accessible");
        }

        return Vector2Int.zero;
    }

    // Exécute l'attaque sur la cible
    // Déclenche l'animation puis inflige les dégâts après le délai
    // Gère l'IdleReversed du Rootborn avant la première attaque
    private void PerformAttack()
    {
        // Empêche les doubles appels pendant qu'une attaque est en cours
        if (isPerformingAttack)
        {
            if (debugAttack)
            {
                Debug.LogWarning($"AttackController {gameObject.name}: PerformAttack() bloqué - attaque déjà en cours");
            }
            return;
        }

        if (currentTarget == null || unitStats == null)
        {
            return;
        }

        UnitStats targetStats = currentTarget.GetComponent<UnitStats>();

        if (targetStats == null)
        {
            Debug.LogWarning($"AttackController {gameObject.name}: Cible {currentTarget.name} n'a pas de UnitStats");
            return;
        }

        if (debugAttack)
        {
            Debug.Log($"⚔️ AttackController {gameObject.name}: Début attaque sur {currentTarget.name}");
        }

        // Marque qu'une attaque est en cours
        isPerformingAttack = true;

        // Gestion spéciale Rootborn : IdleReversed avant première attaque de la session
        if (IsRootborn())
        {
            StartCoroutine(PlayRootbornPreAttackAnimation(targetStats));
        }
        else
        {
            // Lance directement l'attaque normale
            ExecuteAttackAnimation(targetStats);
        }
    }

    private IEnumerator DealDamageAfterAnimation(UnitStats targetStats, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Vérifie que la cible existe toujours, n'est pas morte, ET est toujours à portée
        if (targetStats != null && !targetStats.IsDead() && IsTargetInRange())
        {
            int damage = unitStats.GetAttackDamage();
            targetStats.TakeDamage(damage, unitGameObject);
            Debug.Log($"🔍 DealDamageAfterAnimation appelé pour {gameObject.name} -> {targetStats.gameObject.name}");

            if (debugAttack)
            {
                Debug.Log($"💥 AttackController {gameObject.name}: Dégâts infligés ({damage})");
            }
        }
        else if (debugAttack && targetStats != null && !targetStats.IsDead())
        {
            Debug.Log($"AttackController {gameObject.name}: Dégâts annulés - cible hors portée ou pas au centre");
        }

        // Réinitialise le flag pour permettre la prochaine attaque
        isPerformingAttack = false;
    }

    // Coroutine pour gérer l'animation pré-attaque du Rootborn
    // Bloque l'attaque jusqu'à ce que IdleReversed soit terminé
    private IEnumerator PlayRootbornPreAttackAnimation(UnitStats targetStats)
    {
        if (animatorController == null || !IsRootborn())
        {
            yield break;
        }

        bool animationCompleted = false;

        // Lance l'animation IdleReversed avec callback
        animatorController.PlayRootbornIdleReversed(() => {
            animationCompleted = true;
        });

        // Attend que l'animation soit terminée
        while (!animationCompleted)
        {
            yield return null;
        }

        if (debugAttack)
        {
            Debug.Log($"AttackController {gameObject.name}: Animation pré-attaque Rootborn terminée");
        }

        // Lance l'attaque normale maintenant
        ExecuteAttackAnimation(targetStats);
    }

    // Exécute l'animation d'attaque et inflige les dégâts
    // Méthode séparée pour être appelée après IdleReversed ou directement
    private void ExecuteAttackAnimation(UnitStats targetStats)
    {
        float attackSpeed = unitStats.GetAttackSpeed();

        if (animatorController != null && currentTarget != null)
        {
            Vector2Int myPosition = GridManager.Instance.WorldToGrid(transform.position);
            Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);
            animatorController.PlayAttackAnimation(myPosition, targetPosition, attackSpeed);
        }

        damageCoroutine = StartCoroutine(DealDamageAfterAnimation(targetStats, attackSpeed));
    }

    // Vérifie si cette unité est un Rootborn
    // Utilise le nom du GameObject pour identifier les Rootborn
    private bool IsRootborn()
    {
        return gameObject.name.Contains("Rootborn");
    }

    // Retourne si l'unité est actuellement en train d'attaquer
    public bool IsAttacking()
    {
        return isAttacking;
    }

    // Retourne la cible actuelle (peut être null)
    public GameObject GetCurrentTarget()
    {
        return currentTarget;
    }

    // Debug pour afficher l'état de l'attaque
    [ContextMenu("Debug Attack State")]
    private void DebugAttackState()
    {
        Debug.Log($"=== AttackController Debug - {gameObject.name} ===");
        Debug.Log($"En attaque: {isAttacking}");
        Debug.Log($"Cible: {(currentTarget != null ? currentTarget.name : "Aucune")}");
        Debug.Log($"À portée: {(isAttacking ? IsTargetInRange() : false)}");
        Debug.Log($"Portée d'attaque: {(unitStats != null ? unitStats.GetAttackRange() : 0)} cases");
        Debug.Log($"Temps depuis dernière attaque: {Time.time - lastAttackTime:F2}s");
    }
}
