using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Gère l'attaque persistante d'une cible ennemie
// L'unité poursuit et attaque sa cible tant que l'ordre persiste
// S'intègre avec MovementController pour se positionner à portée
public class AttackController : MonoBehaviour, ITickable
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
    private int checkIntervalTicks;
    private long lastAttackTick;
    private long lastAutoAttackCheckTick;
    private long lastAttackLogicCheckTick;
    private long cooldownUntilTick;
    private long projectileAtTick;
    private long performingUntilTick;
    private long approachRetryAtTick;
    private long preAttackUntilTick;
    private long simulationEntityID = -1;
    private long performingTargetEntityID = -1;
    private IDamageable preAttackTargetDamageable;
    private Transform performingTargetTransform;
    private float performingProjectileTravelTime;
    private bool projectilePending;
    private bool waitingPreAttack;
    private bool performingRangedAttack;
    private bool hasManualOrder = false; // Bloque l'auto-attaque pendant un ordre manuel en cours
    private GameObject pendingChaseTarget;
    private IDamageable pendingChaseTargetDamageable;
    private Vector2Int lastPendingChaseTargetPos;

    // Références
    private MovementController movementController;
    private UnitStatsComponent unitStatsComponent;
    private ITeamMember teamMember;
    private GameObject unitGameObject;
    private AnimatorController animatorController; // Cache pour AnimatorController
    private ChaseController chaseController;
    private AttackState currentState = AttackState.Idle;

    // Initialise les références
    private void Awake()
    {
        unitGameObject = gameObject;
        checkIntervalTicks = SimulationClock.SecondsToTicks(checkInterval);
        unitStatsComponent = GetComponent<UnitStatsComponent>();
        teamMember = GetComponent<ITeamMember>();
        chaseController = GetComponent<ChaseController>();

        if (unitStatsComponent == null)
        {
            GameLogger.Error($"AttackController sur {gameObject.name}: UnitStatsComponent non trouvé !", this);
        }
        else
        {
            cooldownUntilTick = SimulationClock.SecondsToTicks(unitStatsComponent.GetAttackSpeed());
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

        if (unitStatsComponent != null)
        {
            unitStatsComponent.OnDamagedBy.AddListener(HandleDamagedBy);
        }
    }

    // Enregistre le contrôleur après l'attribution de l'entityID par UnitStatsComponent.Awake
    private void Start()
    {
        simulationEntityID = EntityRegistry.Instance?.GetEntityID(gameObject) ?? -1;
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Register(simulationEntityID, this);
    }

    // Retire l'abonnement de riposte lors de la destruction du contrôleur
    private void OnDestroy()
    {
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Unregister(simulationEntityID, this);

        if (unitStatsComponent != null)
        {
            unitStatsComponent.OnDamagedBy.RemoveListener(HandleDamagedBy);
        }
    }

    // Déclenche une riposte automatique avec les mêmes gardes que l'ancien flux de UnitStatsComponent
    private void HandleDamagedBy(GameObject attacker)
    {
        if (attacker == null || unitStatsComponent == null || unitStatsComponent.GetCurrentHP() <= 0)
        {
            return;
        }

        IDamageable attackerDamageable = attacker.GetComponent<IDamageable>();
        if (attackerDamageable == null || attackerDamageable.IsDead() || attackerDamageable.IsInvulnerable())
        {
            return;
        }

        ITeamMember attackerTeamMember = attacker.GetComponent<ITeamMember>();
        if (teamMember == null || attackerTeamMember == null || PlayerManager.Instance == null
            || !PlayerManager.Instance.AreEnemies(teamMember.GetTeamID(), attackerTeamMember.GetTeamID()))
        {
            return;
        }

        if (!enabled || IsAttacking() || HasManualOrder())
        {
            return;
        }

        bool isChasing = chaseController != null && chaseController.IsChasing();
        if (isChasing && !chaseController.IsSatisfied())
        {
            return;
        }

        if (movementController != null
            && movementController.GetCurrentState() == MovementController.MovementState.Moving
            && !isChasing)
        {
            return;
        }

        InterruptChaseAndAttack(attacker);

        if (debugAttack)
        {
            GameLogger.Log(GameLogger.Category.Combat, $"⚔️ Riposte auto sur {attacker.name}", this);
        }
    }

    // Exécute les décisions d'attaque sur la boucle logique fixe
    public void SimTick(long tick)
    {
        // Termine les attaques déjà engagées même si l'attaquant vient d'être désactivé ou tué
        UpdatePerformingAttack(tick);

        if (!isActiveAndEnabled)
            return;

        // Vérifie si l'unité est morte (ne doit plus rien faire)
        if (unitStatsComponent != null && unitStatsComponent.IsDead())
        {
            return;
        }

        if (currentState == AttackState.Idle || currentTarget == null)
        {
            // Si aucune attaque en cours, vérifie automatiquement les ennemis à portée
            if (autoAttackEnabled && tick - lastAutoAttackCheckTick >= checkIntervalTicks)
            {
                lastAutoAttackCheckTick = tick;
                CheckForAutoAttack();
            }
            return;
        }

        // La chase en attente reste prioritaire si sa cible change de case pendant l'attaque auto
        if (tick - lastAutoAttackCheckTick >= checkIntervalTicks)
        {
            lastAutoAttackCheckTick = tick;
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
                lastAttackLogicCheckTick = tick;
                return;
            }
        }

        // Vérifie périodiquement l'état de la cible (fallback)
        if (tick - lastAttackLogicCheckTick >= checkIntervalTicks)
        {
            lastAttackLogicCheckTick = tick;
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

        lastPendingChaseTargetPos = GetLogicalPosition(target);
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
        Vector2Int currentPendingTargetPos = GetLogicalPosition(pendingChaseTarget);
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
            Vector2Int enemyPosition = GetLogicalPosition(enemy);
            int distance = CombatSystem.CalculateChebyshevDistance(myPosition, enemyPosition);

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

        // Vérifie que la cible peut être attaquée sur ordre explicite (via ITeamMember)
        if (teamMember != null)
        {
            ITeamMember targetTeamMember = target.GetComponent<ITeamMember>();
            if (targetTeamMember == null || PlayerManager.Instance == null
                || !PlayerManager.Instance.CanBeAttacked(teamMember.GetTeamID(), targetTeamMember.GetTeamID()))
            {
                GameLogger.Warn(GameLogger.Category.Combat, $"AttackController {gameObject.name}: {target.name} ne peut pas être attaqué ou n'a pas d'ITeamMember", this);
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
        lastAttackLogicCheckTick = GetCurrentTick();

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

        // La logique d'approche ou de frappe sera traitée au prochain SimTick
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

        currentState = AttackState.Idle;
        currentTarget = null;
        approachRetryAtTick = 0;
        projectilePending = false;
        waitingPreAttack = false;
        performingTargetEntityID = -1;
        preAttackTargetDamageable = null;
        performingTargetTransform = null;

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
                    bool cooldownReadyR = GetCurrentTick() >= cooldownUntilTick;

                    if (currentState != AttackState.Performing && cooldownReadyR)
                    {
                        PerformAttack();
                    }
                    return;
                }
            }

            // Lance l'attaque si aucune attaque n'est en cours ET si le cooldown est écoulé
            float attackSpeed = unitStatsComponent.GetAttackSpeed();
            bool cooldownReady = GetCurrentTick() >= cooldownUntilTick;

            // Joue AttackIdle seulement si on n'est pas en train d'attaquer
            // et qu'aucune animation Hit n'est en cours (évite d'écraser le trigger Hit)
            if (currentState != AttackState.Performing && animatorController != null)
            {
                if (!animatorController.IsPlayingHitAnimation())
                {
                    Vector2Int myPosition = movementController.GetCurrentGridPosition();
                    Vector2Int targetPosition = GetLogicalPosition(currentTarget);
                    animatorController.PlayIdleAttackAnimation(myPosition, targetPosition);
                }
            }

            if (currentState != AttackState.Performing && cooldownReady)
            {
                PerformAttack();

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

        // Cible plus attaquable (changement d'équipe) — comparaison via ITeamMember
        if (teamMember != null)
        {
            ITeamMember targetTeamMember = currentTarget.GetComponent<ITeamMember>();
            if (targetTeamMember == null || PlayerManager.Instance == null
                || !PlayerManager.Instance.CanBeAttacked(teamMember.GetTeamID(), targetTeamMember.GetTeamID()))
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
            bool inRange = CombatSystem.IsInRangeOfOccupiedTiles(
                myPosition, EntityRegistry.Instance.GetBuildingOccupiedCells(currentTarget), attackRange);
            if (debugAttack && inRange && GameLogger.IsEnabled(GameLogger.Category.Combat))
                GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: À portée du bâtiment multi-tiles", this);
            return inRange;
        }

        // Cible standard (unité 1x1)
        Vector2Int targetPosition = GetLogicalPosition(currentTarget);
        bool inRangeStandard = CombatSystem.IsInRange(myPosition, targetPosition, attackRange);

        if (debugAttack && inRangeStandard && GameLogger.IsEnabled(GameLogger.Category.Combat))
            GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Au centre ET à portée → Prêt à attaquer", this);

        return inRangeStandard;
    }

    // Déplace l'unité à portée de la cible
    // Gère les bâtiments multi-tiles : cherche la meilleure position autour de toutes les tiles occupées
    private void MoveToRange()
    {
        if (movementController == null || currentTarget == null)
            return;

        long currentTick = GetCurrentTick();
        if (approachRetryAtTick > currentTick)
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
                EntityRegistry.Instance.GetBuildingOccupiedCells(currentTarget),
                myPosition,
                attackRange,
                pos => GridManager.Instance.IsInBounds(pos) && GridManager.Instance.IsWalkableFor(pos, unitGameObject)
            );

            if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
            {
                Vector2Int anchor = GetLogicalPosition(currentTarget);
                GameLogger.Log(GameLogger.Category.Combat, $"🔍 MoveToRange (bâtiment): Position {myPosition}, Ancre {anchor}, Taille {buildingTarget.GetSize()}", this);
            }
        }
        else
        {
            // Cible standard (unité 1x1)
            Vector2Int targetPosition = GetLogicalPosition(currentTarget);

            if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
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
            if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
                GameLogger.Warn(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Aucune position accessible à portée de {currentTarget.name} — retry dans 1s", this);

            approachRetryAtTick = currentTick + SimulationClock.TICKS_PER_SECOND;

            return;
        }

        if (bestPosition == myPosition)
        {
            if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
                GameLogger.Warn(GameLogger.Category.Combat, $"⚠️ MoveToRange: bestPosition == myPosition ! Pas de mouvement nécessaire", this);
            return;
        }

        if (!movementController.MoveToInternal(bestPosition))
        {
            // Un ordre joueur en attente reste prioritaire ; l'approche sera réévaluée au prochain check.
            return;
        }

        approachRetryAtTick = 0;

        if (debugAttack && GameLogger.IsEnabled(GameLogger.Category.Combat))
            GameLogger.Log(GameLogger.Category.Combat, $"AttackController {gameObject.name}: Déplacement vers {bestPosition} (portée: {attackRange})", this);
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
        lastAttackTick = GetCurrentTick();
        cooldownUntilTick = lastAttackTick + SimulationClock.SecondsToTicks(unitStatsComponent.GetAttackSpeed());

        // Gestion spéciale Rootborn : IdleReversed avant première attaque de la session
        if (IsRootborn() && animatorController != null && !animatorController.HasPlayedIdleReversedThisSession())
        {
            waitingPreAttack = true;
            preAttackTargetDamageable = targetDamageable;
            preAttackUntilTick = lastAttackTick + SimulationClock.SecondsToTicks(0.5f);
            animatorController.PlayRootbornIdleReversed(null);
        }
        else
        {
            // Lance directement l'attaque normale
            ExecuteAttackAnimation(targetDamageable);
        }
    }

    // Exécute l'animation et programme projectile, dégâts et fin de cooldown en ticks
    private void ExecuteAttackAnimation(IDamageable targetDamageable)
    {
        if (animatorController != null && currentTarget != null)
        {
            Vector2Int myPosition = movementController.GetCurrentGridPosition();
            Vector2Int targetPosition = GetLogicalPosition(currentTarget);

            // Joue l'animation
            animatorController.PlayAttackAnimation(myPosition, targetPosition);
        }

        long currentTick = GetCurrentTick();
        int animationTicks = SimulationClock.SecondsToTicks(0.2f);
        int attackRange = unitStatsComponent.GetAttackRange();
        performingRangedAttack = attackRange > 1;
        performingTargetEntityID = EntityRegistry.Instance?.GetEntityID(currentTarget) ?? -1;
        performingTargetTransform = currentTarget != null ? currentTarget.transform : null;
        performingProjectileTravelTime = 0f;
        projectilePending = false;

        if (performingRangedAttack && currentTarget != null)
        {
            Vector2Int myPosition = movementController.GetCurrentGridPosition();
            Vector2Int targetPosition = GetLogicalPosition(currentTarget);
            int distanceInCells = Mathf.Abs(targetPosition.x - myPosition.x) + Mathf.Abs(targetPosition.y - myPosition.y);
            performingProjectileTravelTime = distanceInCells * projectileTravelTimePerCell;
            projectileAtTick = currentTick + animationTicks;
            projectilePending = projectilePrefab != null;

            if (debugAttack)
            {
                GameLogger.Log(GameLogger.Category.Combat, $"🏹 AttackController {gameObject.name}: Projectile programmé, distance {distanceInCells} cases, trajet {performingProjectileTravelTime}s", this);
            }
        }

        int travelTicks = SimulationClock.SecondsToTicks(performingProjectileTravelTime);
        long damageAtTick = currentTick + animationTicks + travelTicks;
        int damage = unitStatsComponent.GetAttackDamage();
        SimulationClockManager.Instance?.ScheduleDamage(
            performingTargetEntityID,
            simulationEntityID,
            damage,
            damageAtTick);
        performingUntilTick = System.Math.Max(cooldownUntilTick, damageAtTick);
    }

    // Avance les échéances d'une attaque Performing sur le tick courant
    private void UpdatePerformingAttack(long tick)
    {
        if (waitingPreAttack && tick >= preAttackUntilTick)
        {
            waitingPreAttack = false;
            IDamageable targetDamageable = preAttackTargetDamageable;
            preAttackTargetDamageable = null;
            if (currentState == AttackState.Performing && targetDamageable != null)
                ExecuteAttackAnimation(targetDamageable);
        }

        if (projectilePending && tick >= projectileAtTick)
        {
            projectilePending = false;
            SpawnVisualProjectile();
        }

        if (currentState == AttackState.Performing && !waitingPreAttack && tick >= performingUntilTick)
        {
            currentState = AttackState.Ready;
            performingTargetEntityID = -1;
            performingTargetTransform = null;
        }
    }

    // Instancie uniquement la représentation visuelle du projectile
    private void SpawnVisualProjectile()
    {
        if (projectilePrefab == null || performingTargetTransform == null)
            return;

        IDamageable visualTargetDamageable = ResolveScheduledTargetDamageable();
        if (visualTargetDamageable == null)
            return;

        Vector3 startPosition = transform.position;
        GameObject projectile = Instantiate(projectilePrefab, startPosition, Quaternion.identity);
        ProjectileVisual visual = projectile.GetComponent<ProjectileVisual>();
        if (visual != null)
            visual.Initialize(startPosition, performingTargetTransform, performingProjectileTravelTime, visualTargetDamageable, null, 0);
    }

    // Résout la cible programmée via son entityID pour annuler les dégâts si elle est sortie de la simulation.
    private IDamageable ResolveScheduledTargetDamageable()
    {
        if (performingTargetEntityID < 0 || EntityRegistry.Instance == null)
            return null;

        GameObject target = EntityRegistry.Instance.GetGameObjectByEntityID(performingTargetEntityID);
        if (target == null || !EntityRegistry.Instance.IsEntityAlive(target))
            return null;

        return target.GetComponent<IDamageable>();
    }

    // Retourne le tick logique courant sans dépendre de l'horloge Unity
    private long GetCurrentTick()
    {
        return SimulationClockManager.Instance != null ? SimulationClockManager.Instance.CurrentTick : 0;
    }

    // Retourne la position logique d'une cible sans lire sa représentation visuelle.
    private Vector2Int GetLogicalPosition(GameObject target)
    {
        if (EntityRegistry.Instance != null && EntityRegistry.Instance.TryGetLogicalPosition(target, out Vector2Int position))
            return position;

        MovementController targetMovement = target != null ? target.GetComponent<MovementController>() : null;
        return targetMovement != null ? targetMovement.GetCurrentGridPosition() : Vector2Int.zero;
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
