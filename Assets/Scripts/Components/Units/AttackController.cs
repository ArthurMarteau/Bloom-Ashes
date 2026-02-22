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
    [SerializeField] private float projectileTravelTimePerCell = 0.2f;
    [SerializeField] private GameObject projectilePrefab; // Prefab du projectile visuel

    // État de l'attaque
    private GameObject currentTarget;
    private bool isAttacking = false;
    private float lastAttackTime;      // Temps de la dernière attaque (pour cooldown)
    private float lastCheckTime;  
    private bool isPerformingAttack = false;

    // Références
    private MovementController movementController;
    private UnitStatsComponent unitStatsComponent;
    private TeamComponent teamComponent;
    private GameObject unitGameObject;
    private Coroutine damageCoroutine;
    private AnimatorController animatorController; // Cache pour AnimatorController

    // Initialise les références
    private void Awake()
    {
        unitGameObject = gameObject;
        unitStatsComponent = GetComponent<UnitStatsComponent>();
        teamComponent = GetComponent<TeamComponent>();

        if (unitStatsComponent == null)
        {
            Debug.LogError($"AttackController sur {gameObject.name}: UnitStatsComponent non trouvé !");
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
        if (unitStatsComponent != null && unitStatsComponent.IsDead())
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
        if (unitStatsComponent.IsDead())
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
        if (unitStatsComponent == null || teamComponent == null)
        {
            return null;
        }

        Vector2Int myPosition = movementController.GetCurrentGridPosition();
        int attackRange = unitStatsComponent.GetAttackRange();

        GameObject nearestEnemy = null;
        int shortestDistance = int.MaxValue;

        // Récupère toutes les unités ennemies dans la scène
        float boxSize = (attackRange * 2 + 1) * GridManager.Instance.GetCellSize();
        Vector2 center = transform.position;

        // Détecte tous les colliders dans la zone (layer Units = 8)
        Collider2D[] nearbyColliders = Physics2D.OverlapBoxAll(center, new Vector2(boxSize, boxSize), 0f, 1 << 8);

        foreach (Collider2D collider in nearbyColliders)
        {
            UnitStatsComponent unitStat = collider.GetComponent<UnitStatsComponent>();
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

        // Vide la queue de mouvement pour attaquer après la case actuelle
        // L'unité termine son interpolation en cours de manière fluide
        if (movementController != null)
        {
            MovementController.MovementState state = movementController.GetCurrentState();

            if (state == MovementController.MovementState.Moving)
            {
                movementController.ClearPathQueue();

                if (debugAttack)
                {
                    Debug.Log($"AttackController {gameObject.name}: Queue vidée, finit interpolation puis attaque");
                }
            }
        }

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
            // Vérifie que l'unité est bien centrée sur sa case avant de jouer AttackIdle
            if (movementController != null)
            {
                MovementController.MovementState state = movementController.GetCurrentState();

                // Si en mouvement, on attend d'être centré
                if (state == MovementController.MovementState.Moving)
                {
                    if (debugAttack)
                    {
                        Debug.Log($"AttackController {gameObject.name}: En mouvement, attend d'être centré avant AttackIdle");
                    }
                    return;
                }
            }

            // Vérifie si c'est un Rootborn qui n'a pas encore joué IdleReversed
            // Dans ce cas, on attend que PerformAttack() gère l'IdleReversed d'abord
            if (IsRootborn() && animatorController != null)
            {
                // Vérifie via une méthode publique si IdleReversed doit être joué
                if (!animatorController.HasPlayedIdleReversedThisSession())
                {
                    // Ne joue pas AttackIdle, laisse PerformAttack() gérer IdleReversed d'abord
                    if (debugAttack)
                    {
                        Debug.Log($"AttackController {gameObject.name}: Rootborn attend IdleReversed avant AttackIdle");
                    }

                    // Lance directement l'attaque pour déclencher IdleReversed
                    float attackSpeedR = unitStatsComponent.GetAttackSpeed();
                    bool cooldownReadyR = Time.time - lastAttackTime >= attackSpeedR;

                    if (!isPerformingAttack && cooldownReadyR)
                    {
                        PerformAttack();
                        lastAttackTime = Time.time;
                    }
                    return;
                }
            }

            // Cible à portée, joue Idle_Attack
            if (animatorController != null)
            {
                Vector2Int myPosition = GridManager.Instance.WorldToGrid(transform.position);
                Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);
                animatorController.PlayIdleAttackAnimation(myPosition, targetPosition);
            }

            // Lance l'attaque si aucune attaque n'est en cours ET si le cooldown est écoulé
            float attackSpeed = unitStatsComponent.GetAttackSpeed();
            bool cooldownReady = Time.time - lastAttackTime >= attackSpeed;

            // Lance l'attaque si aucune attaque n'est en cours
            // Le cooldown est géré automatiquement dans la coroutine DealDamageAtEndOfAnimation
            if (!isPerformingAttack && cooldownReady)
            {
                PerformAttack();
                lastAttackTime = Time.time; // Enregistre le temps de cette attaque

                if (debugAttack)
                {
                    Debug.Log($"⚔️ AttackController {gameObject.name}: Attaque lancée, prochain cooldown dans {attackSpeed}s");
                }
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
        UnitStatsComponent targetStats = currentTarget.GetComponent<UnitStatsComponent>();
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
    // Utilise la portée définie dans UnitStatsComponent
    // Utilise la position de grille actuelle (transform.position converti)
    private bool IsTargetInRange()
    {
        if (unitStatsComponent == null || currentTarget == null)
        {
            return false;
        }

        // Utilise uniquement les positions LOGIQUES
        Vector2Int myPosition = movementController.GetCurrentGridPosition();
        Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);

        int attackRange = unitStatsComponent.GetAttackRange();
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

        int attackRange = unitStatsComponent.GetAttackRange();

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
    // Délègue la sélection des offsets à RangeOffsets (source unique de vérité)
    // Calcule le vrai chemin vers chaque case et retourne la meilleure
    private Vector2Int FindPositionInRange(Vector2Int targetPosition, Vector2Int myPosition, int attackRange)
    {
        // Sélectionne la liste d'offsets selon la portée via RangeOffsets
        Vector2Int[] offsets = RangeOffsets.GetOffsets(attackRange);

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

        if (currentTarget == null || unitStatsComponent == null)
        {
            return;
        }

        UnitStatsComponent targetStats = currentTarget.GetComponent<UnitStatsComponent>();

        if (targetStats == null)
        {
            Debug.LogWarning($"AttackController {gameObject.name}: Cible {currentTarget.name} n'a pas de UnitStatsComponent");
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

    // Coroutine pour gérer l'animation pré-attaque du Rootborn
    // Bloque l'attaque jusqu'à ce que IdleReversed soit terminé
    private IEnumerator PlayRootbornPreAttackAnimation(UnitStatsComponent targetStats)
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

    // Exécute l'animation d'attaque et inflige les dégâts immédiatement
    private void ExecuteAttackAnimation(UnitStatsComponent targetStats)
    {
        if (animatorController != null && currentTarget != null)
        {
            Vector2Int myPosition = GridManager.Instance.WorldToGrid(transform.position);
            Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);

            // Joue l'animation
            animatorController.PlayAttackAnimation(myPosition, targetPosition);
        }

        // Arrête la coroutine précédente si elle existe
        if (damageCoroutine != null)
        {
            StopCoroutine(damageCoroutine);
        }

        // Inflige les dégâts à la fin de l'animation
        damageCoroutine = StartCoroutine(DealDamageAtEndOfAnimation(targetStats));
    }

    // Inflige les dégâts à la fin de l'animation et reset le flag
    private IEnumerator DealDamageAtEndOfAnimation(UnitStatsComponent targetStats)
    {
        // Attend la fin de l'animation d'attaque (durée fixe)
        yield return new WaitForSeconds(0.2f);

        // Calcule le temps de trajet du projectile si attaque à distance
        float projectileTravelTime = 0f;
        int attackRange = unitStatsComponent.GetAttackRange();

        // Si portée > 1, calcule le temps de trajet
        if (attackRange > 1 && currentTarget != null)
        {
            // Calcule la distance en cases entre attaquant et cible
            Vector2Int myPosition = GridManager.Instance.WorldToGrid(transform.position);
            Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);
            int distanceInCells = Mathf.Abs(targetPosition.x - myPosition.x) + Mathf.Abs(targetPosition.y - myPosition.y);

            // Calcule le temps de trajet du projectile
            projectileTravelTime = distanceInCells * projectileTravelTimePerCell;

            if (debugAttack)
            {
                Debug.Log($"🏹 AttackController {gameObject.name}: Projectile lancé, distance {distanceInCells} cases, trajet {projectileTravelTime}s");
            }

            // NOUVEAU CODE ICI
            // Spawne le projectile visuel
            if (projectilePrefab != null && currentTarget != null)
            {
                Vector3 startPos = transform.position;
                Vector3 endPos = currentTarget.transform.position;

                GameObject projectile = Instantiate(projectilePrefab, startPos, Quaternion.identity);
                ProjectileVisual visual = projectile.GetComponent<ProjectileVisual>();

                if (visual != null)
                {
                    visual.Initialize(startPos, endPos, projectileTravelTime);
                }
            }

            // Attend que le projectile arrive
            yield return new WaitForSeconds(projectileTravelTime);
        }

        // Inflige les dégâts
        if (targetStats != null && !targetStats.IsDead() && IsTargetInRange())
        {
            int damage = unitStatsComponent.GetAttackDamage();
            targetStats.TakeDamage(damage, unitGameObject);

            if (debugAttack)
            {
                Debug.Log($"💥 AttackController {gameObject.name}: Dégâts infligés ({damage}) à fin d'animation");
            }
        }

        // Récupère attackSpeed depuis UnitStatsComponent
        float attackSpeed = unitStatsComponent.GetAttackSpeed();

        // Calcule le cooldown restant (attackSpeed - animation - projectile)
        float totalAttackTime = AnimatorController.ATTACK_ANIMATION_DURATION + projectileTravelTime;
        float cooldown = Mathf.Max(0f, attackSpeed - totalAttackTime);

        if (debugAttack && cooldown > 0f)
        {
            Debug.Log($"⏳ AttackController {gameObject.name}: Cooldown de {cooldown}s avant prochaine attaque");
        }

        // Attend le cooldown
        yield return new WaitForSeconds(cooldown);

        // Réinitialise le flag
        isPerformingAttack = false;

        // Libère la référence
        damageCoroutine = null;
    }

    // Vérifie si cette unité est un Rootborn via le ScriptableObject (UnitStatsData.isRootborn)
    private bool IsRootborn()
    {
        if (unitStatsComponent != null) return unitStatsComponent.IsRootborn();
        return false;
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
        Debug.Log($"Portée d'attaque: {(unitStatsComponent != null ? unitStatsComponent.GetAttackRange() : 0)} cases");
    }
}
