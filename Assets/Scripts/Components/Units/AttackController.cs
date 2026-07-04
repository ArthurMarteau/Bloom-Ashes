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

    public enum AttackState
    {
        Idle,         // Pas d'ordre d'attaque actif
        Approaching,  // Ordre actif, hors portée — en cours de déplacement vers la cible
        Ready,        // À portée, prêt à frapper ou en attente de cooldown
        Performing    // Animation + dégâts en cours d'exécution
    }

    // État de l'attaque
    private GameObject currentTarget;
    private float lastAttackTime;      // Temps de la dernière attaque (pour cooldown)
    private float lastAutoAttackCheckTime;
    private float lastAttackLogicCheckTime;
    private bool hasManualOrder = false; // Bloque l'auto-attaque pendant un ordre manuel en cours
    private GameObject pendingChaseTarget;
    private IDamageable pendingChaseTargetDamageable;
    private Vector2Int lastPendingChaseTargetPos;

    // Références
    private MovementController movementController;
    private UnitStatsComponent unitStatsComponent;
    private ITeamMember teamMember;
    private GameObject unitGameObject;
    private Coroutine damageCoroutine;
    private Coroutine approachRetryCoroutine;
    private AnimatorController animatorController; // Cache pour AnimatorController
    private ChaseController chaseController;
    private AttackState currentState = AttackState.Idle;

    // Initialise les références
    private void Awake()
    {
        unitGameObject = gameObject;
        unitStatsComponent = GetComponent<UnitStatsComponent>();
        teamMember = GetComponent<ITeamMember>();
        chaseController = GetComponent<ChaseController>();

        if (unitStatsComponent == null)
        {
            GameLogger.Error($"AttackController sur {gameObject.name}: UnitStatsComponent non trouvé !", this);
        }

        if (teamMember == null)
        {
            GameLogger.Error($"AttackController sur {gameObject.name}: ITeamMember (TeamComponent) non trouvé !", this);
        }

        animatorController = GetComponent<AnimatorController>();
        if (animatorController == null)
        {
            GameLogger.Warn(GameLogger.Category.Combat, $"AttackController sur {gameObject.name}: AnimatorController non trouvé!", this);
        }

        movementController = GetComponent<MovementController>();
        if (movementController == null)
        {
            GameLogger.Error($"AttackController sur {gameObject.name}: MovementController non trouvé !", this);
        }
    }

    private void Update()
    {
        // Vérifie si l'unité est morte (ne doit plus rien faire)
        if (unitStatsComponent != null && unitStatsComponent.IsDead())
        {
            return;
        }


        if (currentState == AttackState.Idle || currentTarget == null)
        {
            // Si aucune attaque en cours, vérifie automatiquement les ennemis à portée
            if (autoAttackEnabled && Time.time - lastAutoAttackCheckTime >= checkInterval)
            {
                lastAutoAttackCheckTime = Time.time;
                CheckForAutoAttack();
            }
            return;
        }

        // La chase en attente reste prioritaire si sa cible change de case pendant l'attaque auto
        if (Time.time - lastAutoAttackCheckTime >= checkInterval)
        {
            lastAutoAttackCheckTime = Time.time;
            if (CheckPendingChaseTargetMovement())
                return;
        }

        // Vérifie l'état du mouvement
        if (movementController != null)
        {
            MovementController.MovementState state = movementController.GetCurrentState();


            // Si en mouvement, on attend
            if (state == MovementController.MovementState.Moving)
            {
                return;
            }

            // Si vient d'arriver (Idle ou Arrived), force un check immédiat
            if (state == MovementController.MovementState.Idle ||
                state == MovementController.MovementState.Arrived ||
                state == MovementController.MovementState.Blocked)
            {
                // Ne pas interrompre une attaque en cours d'exécution
                if (currentState == AttackState.Performing)
                {
                    if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
                    {
                        GameLogger.Log(GameLogger.Category.Combat, $"🔄 Update {gameObject.name}: Attaque en cours, on attend", this);
                    }
                    return;
                }

                if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
                {
                    GameLogger.Log(GameLogger.Category.Combat, $"🔄 Update {gameObject.name}: État Idle/Arrived → Appel ProcessAttackLogic", this);
                }
                // Force ProcessAttackLogic immédiatement sans attendre checkInterval
                ProcessAttackLogic();
                lastAttackLogicCheckTime = Time.time;
                return;
            }
        }

        // Vérifie périodiquement l'état de la cible (fallback)
        if (Time.time - lastAttackLogicCheckTime >= checkInterval)
        {
            lastAttackLogicCheckTime = Time.time;
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
                GameLogger.Warn(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Unité morte, ne peut pas attaquer", this);
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
                GameLogger.Log(GameLogger.Category.Combat, $"🎯 AttackController {gameObject.name}: Ennemi détecté à portée ({nearestEnemy.name}), attaque automatique !", this);
            }
            InterruptChaseAndAttack(nearestEnemy);
        }
    }

    // Vérifie si l'unité peut déclencher une attaque automatique
    // Retourne false si l'unité est déjà sous un autre ordre
    private bool CanAutoAttack()
    {
        // Bloqué si un ordre manuel de mouvement est en cours
        if (hasManualOrder)
        {
            return false;
        }

        // Autorise l'auto-attaque uniquement quand l'approche de chase est satisfaite
        if (chaseController != null && chaseController.IsChasing())
        {
            return chaseController.IsSatisfied();
        }

        // Vérifie MovementController
        if (movementController != null)
        {
            MovementController.MovementState state = movementController.GetCurrentState();
            if (state == MovementController.MovementState.Moving)
            {
                return false;
            }
        }

        return true;
    }

    // Active le flag d'ordre manuel — bloque l'auto-attaque jusqu'à l'arrivée
    // Appelé par OrderManager lors d'un ordre de mouvement explicite
    public void SetManualOrder(bool value)
    {
        hasManualOrder = value;

        if (debugAttack)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Ordre manuel → {value}", this);
        }
    }

    // Retourne si un ordre manuel bloque actuellement l'auto-attaque
    public bool HasManualOrder() => hasManualOrder;

    // Stocke une cible de chase à reprendre après l'attaque courante
    public void SetPendingChaseTarget(GameObject target)
    {
        pendingChaseTarget = target;
        pendingChaseTargetDamageable = null;

        if (target == null)
            return;

        lastPendingChaseTargetPos = GridManager.Instance.WorldToGrid(target.transform.position);
        pendingChaseTargetDamageable = target.GetComponent<IDamageable>();
    }

    // Surveille au tick périodique la cible de chase suspendue pendant une attaque automatique
    // Retourne true si l'attaque a été interrompue pour reprendre la chase
    private bool CheckPendingChaseTargetMovement()
    {
        if (pendingChaseTarget == null)
        {
            pendingChaseTarget = null;
            pendingChaseTargetDamageable = null;
            return false;
        }

        IDamageable damageable = pendingChaseTargetDamageable ?? pendingChaseTarget.GetComponent<IDamageable>();
        if (damageable != null && damageable.IsDead())
        {
            pendingChaseTarget = null;
            pendingChaseTargetDamageable = null;
            return false;
        }

        pendingChaseTargetDamageable = damageable;
        Vector2Int currentPendingTargetPos = GridManager.Instance.WorldToGrid(pendingChaseTarget.transform.position);
        if (currentPendingTargetPos == lastPendingChaseTargetPos)
            return false;

        lastPendingChaseTargetPos = currentPendingTargetPos;
        StopAttack();
        return true;
    }

    // Interrompt une chase automatique pour attaquer une cible prioritaire
    public void InterruptChaseAndAttack(GameObject enemy)
    {
        if (chaseController != null && chaseController.IsChasing())
        {
            GameObject chaseTarget = chaseController.GetCurrentTarget();
            chaseController.StopChase();
            SetPendingChaseTarget(chaseTarget);
        }

        StartAttack(enemy);
    }

    // Trouve l'ennemi le plus proche à portée d'attaque
    // Utilise EntityRegistry au lieu de Physics2D.OverlapBoxAll — O(n) sur entités enregistrées, zéro Physics2D
    // Retourne null si aucun ennemi trouvé ou si EntityRegistry indisponible
    public GameObject FindNearestEnemyInRange()
    {
        if (unitStatsComponent == null || teamMember == null)
        {
            return null;
        }

        if (EntityRegistry.Instance == null)
        {
            GameLogger.Warn(GameLogger.Category.Combat, $"⚠️ AttackController {gameObject.name}: EntityRegistry introuvable, auto-attaque désactivée", this);
            return null;
        }

        Vector2Int myPosition = movementController.GetCurrentGridPosition();
        int attackRange = unitStatsComponent.GetAttackRange();

        // Récupère les ennemis vivants à portée via le registre centralisé (pas de Physics2D.OverlapBoxAll)
        // FindEnemiesInRange filtre : vivant + équipe ennemie + distance Chebyshev <= attackRange
        List<GameObject> enemies = EntityRegistry.Instance.FindEnemiesInRange(myPosition, attackRange, teamMember.GetTeamID());

        GameObject nearestEnemy = null;
        int shortestDistance = int.MaxValue;

        foreach (GameObject enemy in enemies)
        {
            // Ignore soi-même (cas défensif)
            if (enemy == unitGameObject) continue;

            // Ignore les cibles invulnérables
            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsInvulnerable()) continue;

            // Trouve le plus proche par distance Chebyshev
            Vector2Int enemyPosition = GridManager.Instance.WorldToGrid(enemy.transform.position);
            int distance = Mathf.Max(Mathf.Abs(myPosition.x - enemyPosition.x),
                                     Mathf.Abs(myPosition.y - enemyPosition.y));

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                nearestEnemy = enemy;
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
            GameLogger.Log(GameLogger.Category.Combat, $"🎯 StartAttack appelé pour {gameObject.name} → cible {(target != null ? target.name : "null")}", this);
        }

        if (target == null)
        {
            GameLogger.Warn(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Tentative d'attaque sur cible null", this);
            return;
        }

        if (target == unitGameObject)
        {
            GameLogger.Warn(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Une unité ne peut pas s'attaquer elle-même", this);
            return;
        }

        // Rejette les cibles invulnérables (ex: Nexus en phase d'invulnérabilité initiale)
        // Évite qu'une unité s'acharne inutilement contre une entité imperméable aux dégâts
        IDamageable targetDamageableCheck = target.GetComponent<IDamageable>();
        if (targetDamageableCheck != null && targetDamageableCheck.IsInvulnerable())
        {
            if (debugAttack)
            {
                GameLogger.Log(GameLogger.Category.Combat, $"🛡️ AttackController {gameObject.name}: {target.name} est invulnérable — ordre d'attaque ignoré", this);
            }
            return;
        }

        // Vérifie que la cible est bien ennemie (via ITeamMember)
        if (teamMember != null)
        {
            ITeamMember targetTeamMember = target.GetComponent<ITeamMember>();
            if (targetTeamMember == null || !teamMember.IsEnemy(targetTeamMember))
            {
                GameLogger.Warn(GameLogger.Category.Combat, $"AttackController {gameObject.name}: {target.name} n'est pas un ennemi ou n'a pas d'ITeamMember", this);
                return;
            }
        }

        // Arrête l'attaque précédente si existe
        if (currentState != AttackState.Idle)
        {
            StopAttack();
        }

        movementController?.ForceExitIdle();

        currentTarget = target;
        currentState = AttackState.Approaching;  // Sera Ready dès qu'à portée via ProcessAttackLogic
        lastAttackLogicCheckTime = Time.time;

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
                    GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Queue vidée, finit interpolation puis attaque", this);
                }
            }
        }

        if (debugAttack)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Début attaque de {target.name}", this);
        }

        // Lance immédiatement la logique d'attaque
        ProcessAttackLogic();
    }

    // Arrête l'attaque en cours
    // Appelé lors d'un nouvel ordre manuel ou destruction de la cible
    public void StopAttack()
    {
        if (currentState == AttackState.Idle)
        {
            return;
        }

        GameObject resumeTarget = pendingChaseTarget;
        pendingChaseTarget = null;
        pendingChaseTargetDamageable = null;

        if (approachRetryCoroutine != null)
        {
            StopCoroutine(approachRetryCoroutine);
            approachRetryCoroutine = null;
        }

        currentState = AttackState.Idle;
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
            GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Attaque arrêtée, retour en Idle", this);
        }

        if (resumeTarget != null)
        {
            IDamageable resumeDamageable = resumeTarget.GetComponent<IDamageable>();
            if (resumeDamageable == null || !resumeDamageable.IsDead())
            {
                chaseController?.StartChase(resumeTarget);

                if (debugAttack)
                {
                    GameLogger.Log(GameLogger.Category.Combat, $"🔄 Reprise chase sur {resumeTarget.name}", this);
                }
            }
        }

        movementController?.NotifyIdleRestored();
    }

    // Logique principale d'attaque appelée périodiquement
    // Vérifie validité cible, portée, et exécute attaque ou déplacement
    private void ProcessAttackLogic()
    {
        // Vérifie que la cible est toujours valide
        if (!IsTargetValid())
        {
            if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
            {
                GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Cible invalide, arrêt attaque", this);
            }
            StopAttack();
            return;
        }

        // Vérifie si la cible est à portée
        if (IsTargetInRange())
        {
            // Transition vers Ready dès qu'on est à portée (sauf si Performing en cours, qu'on ne casse pas)
            if (currentState == AttackState.Approaching)
            {
                currentState = AttackState.Ready;
            }

            // Vérifie que l'unité est bien centrée sur sa case avant de jouer AttackIdle
            if (movementController != null)
            {
                MovementController.MovementState state = movementController.GetCurrentState();

                // Si en mouvement, on attend d'être centré
                if (state == MovementController.MovementState.Moving)
                {
                    if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
                    {
                        GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: En mouvement, attend d'être centré avant AttackIdle", this);
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
                    if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
                    {
                        GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Rootborn attend IdleReversed avant AttackIdle", this);
                    }

                    // Lance directement l'attaque pour déclencher IdleReversed
                    float attackSpeedR = unitStatsComponent.GetAttackSpeed();
                    bool cooldownReadyR = Time.time - lastAttackTime >= attackSpeedR;

                    if (currentState != AttackState.Performing && cooldownReadyR)
                    {
                        PerformAttack();
                        lastAttackTime = Time.time;
                    }
                    return;
                }
            }

            // Lance l'attaque si aucune attaque n'est en cours ET si le cooldown est écoulé
            float attackSpeed = unitStatsComponent.GetAttackSpeed();
            bool cooldownReady = Time.time - lastAttackTime >= attackSpeed;

            // Joue AttackIdle seulement si on n'est pas en train d'attaquer
            // et qu'aucune animation Hit n'est en cours (évite d'écraser le trigger Hit)
            if (currentState != AttackState.Performing && animatorController != null)
            {
                if (!animatorController.IsPlayingHitAnimation())
                {
                    Vector2Int myPosition = GridManager.Instance.WorldToGrid(transform.position);
                    Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);
                    animatorController.PlayIdleAttackAnimation(myPosition, targetPosition);
                }
            }

            if (currentState != AttackState.Performing && cooldownReady)
            {
                PerformAttack();
                lastAttackTime = Time.time; // Enregistre le temps de cette attaque

                if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
                {
                    GameLogger.Log(GameLogger.Category.Combat, $"⚔️ AttackController {gameObject.name}: Attaque lancée, prochain cooldown dans {attackSpeed}s", this);
                }
            }
            }
            else
            {
                // Transition vers Approaching si on était Ready (la cible s'est éloignée)
                if (currentState == AttackState.Ready)
                {
                    currentState = AttackState.Approaching;
                }

                // Hors portée → annule les dégâts en cours et déplace vers la cible
                if (damageCoroutine != null)
                {
                    StopCoroutine(damageCoroutine);
                    damageCoroutine = null;

                    if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
                    {
                        GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Cible hors portée, annulation des dégâts en cours", this);
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

        // Cible morte (via IDamageable : fonctionne pour unités ET bâtiments)
        IDamageable targetDamageable = currentTarget.GetComponent<IDamageable>();
        if (targetDamageable != null && targetDamageable.IsDead())
        {
            return false;
        }

        // Cible devenue invulnérable en cours d'attaque — inutile de continuer
        if (targetDamageable != null && targetDamageable.IsInvulnerable())
        {
            return false;
        }

        // Cible plus ennemie (changement d'équipe) — comparaison via ITeamMember
        if (teamMember != null)
        {
            ITeamMember targetTeamMember = currentTarget.GetComponent<ITeamMember>();
            if (targetTeamMember == null || !teamMember.IsEnemy(targetTeamMember))
            {
                return false;
            }
        }

        return true;
    }

    // Vérifie si la cible est à portée d'attaque ET si l'unité est au centre de sa case
    // Gère les bâtiments multi-tiles : vérifie la portée par rapport à toutes les tiles occupées
    private bool IsTargetInRange()
    {
        if (unitStatsComponent == null || currentTarget == null)
            return false;

        Vector2Int myPosition = movementController.GetCurrentGridPosition();
        int attackRange = unitStatsComponent.GetAttackRange();

        // Vérifie si la cible est un bâtiment multi-tiles
        BuildingStatsComponent buildingTarget = currentTarget.GetComponent<BuildingStatsComponent>();
        if (buildingTarget != null)
        {
            bool inRange = CombatSystem.IsInRangeOfOccupiedTiles(myPosition, buildingTarget.GetOccupiedTiles(), attackRange);
            if (debugAttack && inRange)
                GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: À portée du bâtiment multi-tiles", this);
            return inRange;
        }

        // Cible standard (unité 1x1)
        Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);
        bool inRangeStandard = CombatSystem.IsInRange(myPosition, targetPosition, attackRange);

        if (debugAttack && inRangeStandard)
            GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Au centre ET à portée → Prêt à attaquer", this);

        return inRangeStandard;
    }

    // Déplace l'unité à portée de la cible
    // Gère les bâtiments multi-tiles : cherche la meilleure position autour de toutes les tiles occupées
    private void MoveToRange()
    {
        if (movementController == null || currentTarget == null)
            return;

        Vector2Int myPosition = movementController.GetCurrentGridPosition();
        int attackRange = unitStatsComponent.GetAttackRange();

        Vector2Int bestPosition = Vector2Int.zero;

        // Vérifie si la cible est un bâtiment multi-tiles
        BuildingStatsComponent buildingTarget = currentTarget.GetComponent<BuildingStatsComponent>();
        if (buildingTarget != null)
        {
            // Utilise toutes les tiles occupées pour trouver la meilleure position d'attaque
            bestPosition = CombatSystem.FindBestAttackPositionForOccupiedTiles(
                buildingTarget.GetOccupiedTiles(),
                myPosition,
                attackRange,
                pos => GridManager.Instance.IsInBounds(pos) && GridManager.Instance.IsWalkableFor(pos, unitGameObject)
            );

            if (debugAttack)
            {
                Vector2Int anchor = buildingTarget.GetAnchorGridPosition();
                GameLogger.Log(GameLogger.Category.Combat, $"🔍 MoveToRange (bâtiment): Position {myPosition}, Ancre {anchor}, Taille {buildingTarget.GetSize()}", this);
            }
        }
        else
        {
            // Cible standard (unité 1x1)
            Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);

            if (debugAttack)
            {
                int currentDistance = CombatSystem.CalculateChebyshevDistance(myPosition, targetPosition);
                GameLogger.Log(GameLogger.Category.Combat, $"🔍 MoveToRange: Position {myPosition}, Cible {targetPosition}, Distance {currentDistance}/{attackRange}", this);
            }

            bestPosition = CombatSystem.FindBestAttackPosition(
                targetPosition,
                myPosition,
                attackRange,
                pos => GridManager.Instance.IsInBounds(pos) && GridManager.Instance.IsWalkableFor(pos, unitGameObject)
            );
        }

        if (bestPosition == Vector2Int.zero)
        {
            if (debugAttack)
                GameLogger.Warn(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Aucune position accessible à portée de {currentTarget.name} — retry dans 1s", this);

            if (approachRetryCoroutine == null)
                approachRetryCoroutine = StartCoroutine(RetryApproachPeriodically());

            return;
        }

        if (bestPosition == myPosition)
        {
            if (debugAttack)
                GameLogger.Warn(GameLogger.Category.Combat, $"⚠️ MoveToRange: bestPosition == myPosition ! Pas de mouvement nécessaire", this);
            return;
        }

        if (approachRetryCoroutine != null)
        {
            StopCoroutine(approachRetryCoroutine);
            approachRetryCoroutine = null;
        }

        movementController.MoveTo(bestPosition);

        if (debugAttack)
            GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Déplacement vers {bestPosition} (portée: {attackRange})", this);
    }

    // Relance périodiquement la logique d'approche tant que la cible reste valide, hors portée et sans position accessible
    private IEnumerator RetryApproachPeriodically()
    {
        while (currentTarget != null && IsTargetValid() && !IsTargetInRange())
        {
            yield return new WaitForSeconds(1f);
            if (currentTarget != null && IsTargetValid())
                ProcessAttackLogic();
        }

        approachRetryCoroutine = null;
    }

    // Exécute l'attaque sur la cible
    // Déclenche l'animation puis inflige les dégâts après le délai
    // Gère l'IdleReversed du Rootborn avant la première attaque
    private void PerformAttack()
    {
        // Empêche les doubles appels pendant qu'une attaque est en cours
        if (currentState == AttackState.Performing)
        {
            if (debugAttack)
            {
                GameLogger.Warn(GameLogger.Category.Combat, $"AttackController {gameObject.name}: PerformAttack() bloqué - attaque déjà en cours", this);
            }
            return;
        }

        if (currentTarget == null || unitStatsComponent == null)
        {
            return;
        }

        // Cible doit implémenter IDamageable (unité ou bâtiment)
        IDamageable targetDamageable = currentTarget.GetComponent<IDamageable>();

        if (targetDamageable == null)
        {
            GameLogger.Warn(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Cible {currentTarget.name} n'implémente pas IDamageable !", this);
            return;
        }

        if (debugAttack)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"⚔️ AttackController {gameObject.name}: Début attaque sur {currentTarget.name}", this);
        }

        // Marque qu'une attaque est en cours
        currentState = AttackState.Performing;

        // Gestion spéciale Rootborn : IdleReversed avant première attaque de la session
        if (IsRootborn())
        {
            StartCoroutine(PlayRootbornPreAttackAnimation(targetDamageable));
        }
        else
        {
            // Lance directement l'attaque normale
            ExecuteAttackAnimation(targetDamageable);
        }
    }

    // Coroutine pour gérer l'animation pré-attaque du Rootborn
    // Bloque l'attaque jusqu'à ce que IdleReversed soit terminé
    private IEnumerator PlayRootbornPreAttackAnimation(IDamageable targetDamageable)
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
            GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Animation pré-attaque Rootborn terminée", this);
        }

        // Lance l'attaque normale maintenant
        ExecuteAttackAnimation(targetDamageable);
    }

    // Exécute l'animation d'attaque et inflige les dégâts immédiatement
    private void ExecuteAttackAnimation(IDamageable targetDamageable)
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
        damageCoroutine = StartCoroutine(DealDamageAtEndOfAnimation(targetDamageable));
    }

    // Inflige les dégâts à la fin de l'animation et reset le flag
    private IEnumerator DealDamageAtEndOfAnimation(IDamageable targetDamageable)
    {
        // Attend la fin de l'animation d'attaque (durée fixe)
        yield return new WaitForSeconds(0.2f);

        // Calcule le temps de trajet du projectile si attaque à distance
        float projectileTravelTime = 0f;
        int attackRange = unitStatsComponent.GetAttackRange();

        // Si portée > 1, calcule le temps de trajet et délègue les dégâts au projectile
        if (attackRange > 1 && currentTarget != null)
        {
            // Capture les références avant tout yield (l'attaquant peut mourir pendant le vol)
            Transform capturedTargetTransform = currentTarget.transform;
            int capturedDamage = unitStatsComponent.GetAttackDamage();

            // Calcule la distance en cases entre attaquant et cible
            Vector2Int myPosition = GridManager.Instance.WorldToGrid(transform.position);
            Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);
            int distanceInCells = Mathf.Abs(targetPosition.x - myPosition.x) + Mathf.Abs(targetPosition.y - myPosition.y);

            // Calcule le temps de trajet du projectile
            projectileTravelTime = distanceInCells * projectileTravelTimePerCell;

            if (debugAttack)
            {
                GameLogger.Log(GameLogger.Category.Combat, $"🏹 AttackController {gameObject.name}: Projectile lancé, distance {distanceInCells} cases, trajet {projectileTravelTime}s", this);
            }

            // Spawne le projectile avec payload de dégâts autonomes
            if (projectilePrefab != null)
            {
                Vector3 startPos = transform.position;

                GameObject projectile = Instantiate(projectilePrefab, startPos, Quaternion.identity);
                ProjectileVisual visual = projectile.GetComponent<ProjectileVisual>();

                if (visual != null)
                {
                    // Le projectile est autonome : suit la cible (homing) et applique les dégâts à l'impact
                    visual.Initialize(startPos, capturedTargetTransform, projectileTravelTime, targetDamageable, unitGameObject, capturedDamage);
                }
            }

            // Pas de yield ici : le projectile gère les dégâts de façon autonome
            // L'attaquant peut enchaîner l'attaque suivante pendant le vol
        }
        else if (attackRange <= 1)
        {
            // Attaque mêlée : inflige les dégâts directement via IDamageable
            if (targetDamageable != null && !targetDamageable.IsDead() && !targetDamageable.IsInvulnerable() && IsTargetInRange())
            {
                int damage = unitStatsComponent.GetAttackDamage();
                targetDamageable.TakeDamage(damage, unitGameObject);

                if (debugAttack)
                {
                    GameLogger.Log(GameLogger.Category.Combat, $"💥 AttackController {gameObject.name}: Dégâts infligés ({damage}) à {(currentTarget != null ? currentTarget.name : "cible")} via IDamageable", this);
                }
            }
        }

        // Récupère attackSpeed depuis UnitStatsComponent
        float attackSpeed = unitStatsComponent.GetAttackSpeed();

        // Calcule le cooldown restant (attackSpeed - animation - projectile)
        float totalAttackTime = AnimatorController.ATTACK_ANIMATION_DURATION + projectileTravelTime;
        float cooldown = Mathf.Max(0f, attackSpeed - totalAttackTime);

        if (debugAttack && cooldown > 0f)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"⏳ AttackController {gameObject.name}: Cooldown de {cooldown}s avant prochaine attaque", this);
        }

        // Attend le cooldown
        yield return new WaitForSeconds(cooldown);

        // Repasse en Ready si on est toujours en attaque, sinon laisse Idle (StopAttack a pu être appelé)
        if (currentState == AttackState.Performing)
        {
            currentState = AttackState.Ready;
        }

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
        return currentState != AttackState.Idle;
    }

    public AttackState GetCurrentState()
    {
        return currentState;
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
        GameLogger.Log(GameLogger.Category.Combat, $"=== AttackController Debug - {gameObject.name} ===", this);
        GameLogger.Log(GameLogger.Category.Combat, $"En attaque: {currentState}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Cible: {(currentTarget != null ? currentTarget.name : "Aucune")}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"À portée: {(currentState != AttackState.Idle ? IsTargetInRange() : false)}", this);
        GameLogger.Log(GameLogger.Category.Combat, $"Portée d'attaque: {(unitStatsComponent != null ? unitStatsComponent.GetAttackRange() : 0)} cases", this);
    }
}
