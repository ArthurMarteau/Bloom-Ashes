using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gère l'attaque persistante d'une cible ennemie
// L'unité poursuit et attaque sa cible tant que l'ordre persiste
// S'intègre avec MovementController pour se positionner à portée
public class AttackController : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float attackInterval = 1f;        // Temps entre chaque attaque (secondes)
    [SerializeField] private float checkInterval = 0.2f;       // Intervalle vérification position/état cible
    [SerializeField] private bool debugAttack = true;

    // État de l'attaque
    private GameObject currentTarget;
    private bool isAttacking = false;
    private float lastAttackTime;
    private float lastCheckTime;

    // Références
    private MovementController movementController;
    private UnitStats unitStats;
    private TeamComponent teamComponent;
    private GameObject unitGameObject;
    private Coroutine damageCoroutine;
    private AnimatorController animatorController; // Cache pour AnimatorController

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

    // Vérifie et met à jour l'attaque chaque frame
    private void Update()
    {
        if (!isAttacking || currentTarget == null)
        {
            return;
        }

        // Vérifie TOUJOURS si la cible a bougé, même pendant mouvement
        Vector2Int currentTargetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);

        // Si cible a bougé (changé de case) et qu'on n'est pas à portée
        if (!IsTargetInRange())
        {
            // Vérifie si on est en mouvement
            MovementController mc = GetComponent<MovementController>();
            if (mc != null && mc.GetCurrentState() == MovementController.MovementState.Moving)
            {
                // En mouvement, on attend sans recalculer constamment
                return;
            }
        }

        // Vérifie périodiquement l'état de la cible
        if (Time.time - lastCheckTime >= checkInterval)
        {
            lastCheckTime = Time.time;
            ProcessAttackLogic();
        }
    }

    // Démarre l'attaque d'une cible
    // Interface principale appelée par OrderManager lors d'un clic droit sur ennemi
    public void StartAttack(GameObject target)
    {
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
        currentTarget = null;

        // Arrête la coroutine de dégâts si active
        if (damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
            damageCoroutine = null;
        }

        if (debugAttack)
        {
            Debug.Log($"AttackController {gameObject.name}: Attaque arrêtée et dégâts annulés");
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
            if (Time.time - lastAttackTime >= attackInterval)
            {
                PerformAttack();
                lastAttackTime = Time.time;
            }
        }
        else
        {
            // Hors portée → déplace vers la cible
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
    // Utilise la position logique (pas transform.position pendant Lerp)
    private bool IsTargetInRange()
    {
        if (unitStats == null || currentTarget == null)
        {
            return false;
        }

        // Utilise la position logique au lieu de transform.position
        Vector2Int myPosition = movementController.GetCurrentLogicalCell();
        Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);

        // Vérifie ensuite la portée d'attaque
        int attackRange = unitStats.GetAttackRange();
        int distance = Mathf.Max(Mathf.Abs(myPosition.x - targetPosition.x), Mathf.Abs(myPosition.y - targetPosition.y));

        bool inRange = distance <= attackRange;

        if (debugAttack && inRange)
        {
            Debug.Log($"AttackController {gameObject.name}: À portée → Prêt à attaquer");
        }

        return inRange;
    }

    // Déplace l'unité à portée de la cible
    // Utilise MovementController pour le déplacement
    // Utilise la position logique pour calculs
    private void MoveToRange()
    {
        if (movementController == null || currentTarget == null)
        {
            return;
        }

        Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);
        Vector2Int myPosition = movementController.GetCurrentLogicalCell();

        int attackRange = unitStats.GetAttackRange();

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

        // Arrête le mouvement en cours avant d'en lancer un nouveau
        if (movementController != null)
        {
            movementController.StopMovement();
        }

        // Envoie l'ordre de mouvement
        movementController.MoveTo(bestPosition);

        if (debugAttack)
        {
            Debug.Log($"AttackController {gameObject.name}: Déplacement vers {bestPosition} (cible: {targetPosition}, portée: {attackRange})");
        }
    }

    // Trouve une position accessible à portée d'attaque avec le chemin le plus court
    // Utilise PathFinding pour calculer le chemin réel vers chaque case à portée
    // Prend en compte les obstacles pour trouver la vraie case optimale
    private Vector2Int FindPositionInRange(Vector2Int targetPosition, Vector2Int myPosition, int attackRange)
    {
        Vector2Int bestPosition = Vector2Int.zero;
        int shortestPathLength = int.MaxValue;

        // Parcourt toutes les cases dans un carré autour de la cible
        for (int x = -attackRange; x <= attackRange; x++)
        {
            for (int y = -attackRange; y <= attackRange; y++)
            {
                Vector2Int candidatePosition = targetPosition + new Vector2Int(x, y);

                // Vérifie que la position est dans les limites
                if (!GridManager.Instance.IsInBounds(candidatePosition))
                {
                    continue;
                }

                // Vérifie que la position est accessible
                if (!GridManager.Instance.IsWalkableFor(candidatePosition, unitGameObject))
                {
                    continue;
                }

                // Vérifie que c'est bien à portée (Chebyshev distance)
                int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                if (distance > attackRange)
                {
                    continue;
                }

                // Calcule le chemin réel depuis notre position actuelle
                List<Vector2Int> path = PathFinding.FindPath(myPosition, candidatePosition, unitGameObject);

                // Si pas de chemin trouvé, ignore cette case
                if (path == null || path.Count == 0)
                {
                    continue;
                }

                // Compare la longueur du chemin
                if (path.Count < shortestPathLength)
                {
                    shortestPathLength = path.Count;
                    bestPosition = candidatePosition;
                }
            }
        }

        if (bestPosition != Vector2Int.zero && debugAttack)
        {
            Debug.Log($"AttackController {gameObject.name}: Meilleure position trouvée {bestPosition} avec chemin de {shortestPathLength} cases");
        }

        return bestPosition;
    }

    // Exécute l'attaque sur la cible
    // Déclenche l'animation puis inflige les dégâts après le délai
    // Gère l'IdleReversed du Rootborn avant la première attaque
    private void PerformAttack()
    {
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

    // Coroutine pour infliger les dégâts après le délai d'animation
    // Synchronisé avec le moment où l'animation du coup se déclenche (80% du temps)
    private IEnumerator DealDamageAfterAnimation(UnitStats targetStats, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Vérifie que la cible existe toujours
        if (targetStats != null && !targetStats.IsDead())
        {
            int damage = unitStats.GetAttackDamage();
            targetStats.TakeDamage(damage);

            if (debugAttack)
            {
                Debug.Log($"💥 AttackController {gameObject.name}: Dégâts infligés ({damage})");
            }
        }
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
        if (animatorController != null && currentTarget != null)
        {
            Vector2Int myPosition = GridManager.Instance.WorldToGrid(transform.position);
            Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);
            animatorController.PlayAttackAnimation(myPosition, targetPosition, attackInterval);
        }

        // Inflige les dégâts après le délai d'animation (80% du temps)
        damageCoroutine = StartCoroutine(DealDamageAfterAnimation(targetStats, attackInterval * 0.8f));
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
