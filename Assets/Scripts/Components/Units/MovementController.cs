using System;
using System.Collections.Generic;
using UnityEngine;

// Composant responsable du mouvement logique tick-based et de sa présentation interpolée.
public class MovementController : MonoBehaviour, IEvictable, ICellListener, ITickable
{
    [Header("Movement Settings")]
    private float movementSpeed = 1f;
    private int ticksPerCell = SimulationClock.TICKS_PER_SECOND;
    private const int RETRY_INTERVAL_TICKS = SimulationClock.TICKS_PER_SECOND / 2;
    [SerializeField] private float detourRatioThreshold = 3f;
    [SerializeField] private float earlyYieldThreshold = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool debugMovement = true;

    public enum MovementState { Idle, Moving, Blocked, Arrived }
    public enum MoveState { Idle, WaitingPath, SteppingToAnchor, SteppingToCenter, Receding, Blocked }

    private Queue<Vector2Int> logicalPath;
    private MoveState moveState = MoveState.Idle;
    private Vector2Int currentCell;
    private Vector2Int previousStableCell;
    private Vector2Int targetCell;
    private Vector3 anchorWorldPos;
    private int stepProgressTicks;
    private int activeStepDurationTicks = 1;

    private Vector2Int? pendingDestination;
    private Vector2Int? orderedDestination;
    private Vector2Int? queuedCommandDestination;
    private bool queuedCommandStartsNewOrder;
    private bool queuedCommandIsPlayerOrder;
    private int consecutivePathFailures;
    private Vector2Int lastCollisionCell;
    private Vector2Int? recoveryDestination;
    private readonly List<Vector2Int> watchedCells = new List<Vector2Int>(3);
    private bool eventRepathPending;
    private bool repathRequested;
    private bool notificationRepathIssuedThisStep;
    private bool intraGroupRepathIssuedThisStep;
    private long currentOrderTick;
    private int ownerPlayerID;
    private long simulationEntityID = -1;
    private long recoveryPauseUntilTick = -1;
    private Vector2Int? recoveryPauseDestination;
    private long retryAtTick = -1;
    private Vector2Int? retryDestination;
    private long preMovementUntilTick = -1;
    private bool idleReversedCompletionPending;

    private GameObject unitGameObject;
    private AnimatorController animatorController;
    private UnitStatsComponent statsComponent;
    private AttackController attackController;
    private ITeamMember teamMember;
    private bool isFirstMoveOfSession = true;
    private long activePathRequestId;
    private Vector3 previousTickWorldPosition;
    private Vector3 currentTickWorldPosition;
    private bool visualFrozen;

    public UnityEngine.Events.UnityEvent OnMovementStarted = new();
    public UnityEngine.Events.UnityEvent OnArrived = new();
    public UnityEngine.Events.UnityEvent<bool> OnIdleStateChanged = new();

    // Expose l'état et les clés d'arbitrage sans recalcul ni accès composant pendant les ticks.
    public bool IsMoving => GetCurrentState() == MovementState.Moving;
    public float StepProgressRatio => stepProgressTicks / (float)activeStepDurationTicks;
    public long CurrentOrderTick => currentOrderTick;
    public int OwnerPlayerID => ownerPlayerID;

    // Convertit la vitesse SO en durée entière orthogonale, une seule fois à la lecture.
    public void SetMovementSpeed(float newSpeed)
    {
        movementSpeed = Mathf.Max(0.1f, newSpeed);
        ticksPerCell = Math.Max(1, (int)Math.Round(
            SimulationClock.TICKS_PER_SECOND / (double)movementSpeed,
            MidpointRounding.AwayFromZero));

        if (debugMovement)
            GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: {movementSpeed} cases/s -> {ticksPerCell} ticks/case", this);
    }

    // Initialise les références et la cellule de spawn avant l'entrée dans la simulation.
    private void Start()
    {
        unitGameObject = gameObject;
        logicalPath = new Queue<Vector2Int>();
        animatorController = GetComponent<AnimatorController>();
        statsComponent = GetComponent<UnitStatsComponent>();
        attackController = GetComponent<AttackController>();
        teamMember = GetComponent<ITeamMember>();
        ownerPlayerID = teamMember != null ? teamMember.GetTeamID() : 0;

        if (GridManager.Instance != null)
        {
            // Bootstrap Unity unique : aucune décision de simulation ne relit ensuite le Transform.
            Vector3 spawnWorldPosition = transform.position;
            currentCell = GridManager.Instance.WorldToGrid(spawnWorldPosition);
            previousStableCell = currentCell;
            EntityRegistry.Instance?.UpdateLogicalPosition(unitGameObject, currentCell);
            previousTickWorldPosition = GridManager.Instance.GridToWorld(currentCell);
            currentTickWorldPosition = previousTickWorldPosition;
            transform.position = currentTickWorldPosition;
        }

        simulationEntityID = EntityRegistry.Instance?.GetEntityID(unitGameObject) ?? -1;
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Register(simulationEntityID, this);
    }

    // Retire le contrôleur de tous les systèmes partagés.
    private void OnDestroy()
    {
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Unregister(simulationEntityID, this);
        CancelPendingPathRequest();
        ClearCellSubscriptions();
        EntityRegistry.Instance?.ClearLeaving(unitGameObject);
        EntityRegistry.Instance?.ClearIncoming(unitGameObject);
    }

    // Interpole les deux poses logiques encadrant le tick sans prendre de décision.
    private void LateUpdate()
    {
        if (visualFrozen || GridManager.Instance == null)
            return;

        float alpha = SimulationClockManager.Instance != null
            ? SimulationClockManager.Instance.InterpolationAlpha
            : 1f;
        transform.position = Vector3.Lerp(previousTickWorldPosition, currentTickWorldPosition, alpha);
    }

    // Exécute toutes les décisions de mouvement au tick logique.
    public void SimTick(long tick)
    {
        if (!isActiveAndEnabled || GridManager.Instance == null)
            return;

        previousTickWorldPosition = currentTickWorldPosition;

        if (queuedCommandDestination.HasValue)
        {
            Vector2Int destination = queuedCommandDestination.Value;
            bool startsNewOrder = queuedCommandStartsNewOrder;
            queuedCommandDestination = null;
            queuedCommandIsPlayerOrder = false;
            RequestMove(destination, startsNewOrder, tick);
        }

        if (eventRepathPending)
        {
            eventRepathPending = false;
            ProcessEventRepath(tick);
        }

        if (preMovementUntilTick >= 0 && tick >= preMovementUntilTick)
        {
            preMovementUntilTick = -1;
            if (pendingDestination.HasValue)
            {
                Vector2Int destination = pendingDestination.Value;
                pendingDestination = null;
                RequestMove(destination, false, tick);
            }
            else
            {
                BeginNextStep(tick);
            }
        }

        if (idleReversedCompletionPending)
        {
            idleReversedCompletionPending = false;
            if (preMovementUntilTick < 0 && pendingDestination.HasValue)
            {
                Vector2Int destination = pendingDestination.Value;
                pendingDestination = null;
                RequestMove(destination, false, tick);
            }
        }

        if (recoveryPauseDestination.HasValue && tick >= recoveryPauseUntilTick)
        {
            Vector2Int destination = recoveryPauseDestination.Value;
            CancelRecoveryPause();
            if (moveState == MoveState.Blocked && orderedDestination == destination)
            {
                consecutivePathFailures = 0;
                RequestMove(destination, false, tick);
            }
        }

        if (retryDestination.HasValue && tick >= retryAtTick)
        {
            Vector2Int destination = retryDestination.Value;
            CancelRetry();
            if (moveState == MoveState.Blocked && orderedDestination == destination)
                QueueRetryPath(destination);
        }

        if (moveState == MoveState.SteppingToAnchor || moveState == MoveState.SteppingToCenter)
            AdvanceStep(tick);
        else if (moveState == MoveState.Receding)
            AdvanceRecede(tick);

        currentTickWorldPosition = EvaluateLogicalWorldPosition();
    }

    // Met en file un ordre public afin que sa consommation ait lieu au SimTick.
    public bool MoveTo(Vector2Int targetPosition)
    {
        if (GridManager.Instance == null || !GridManager.Instance.IsInBounds(targetPosition))
            return false;

        visualFrozen = false;
        queuedCommandDestination = targetPosition;
        queuedCommandStartsNewOrder = true;
        queuedCommandIsPlayerOrder = true;
        return true;
    }

    // Met en file un ordre système sans pouvoir écraser un ordre joueur encore non consommé.
    public bool MoveToInternal(Vector2Int targetPosition)
    {
        if (GridManager.Instance == null || !GridManager.Instance.IsInBounds(targetPosition))
            return false;

        if (queuedCommandDestination.HasValue && queuedCommandIsPlayerOrder)
            return false;

        visualFrozen = false;
        queuedCommandDestination = targetPosition;
        queuedCommandStartsNewOrder = true;
        queuedCommandIsPlayerOrder = false;
        return true;
    }

    // Traite un ordre au tick courant et choisit son point de départ stable.
    private bool RequestMove(Vector2Int targetPosition, bool startsNewOrder, long tick)
    {
        CancelRecoveryPause();
        CancelRetry();

        if (!GridManager.Instance.IsInBounds(targetPosition))
            return false;

        if (startsNewOrder)
        {
            currentOrderTick = tick;
            consecutivePathFailures = 0;
            recoveryDestination = null;
            repathRequested = false;
            notificationRepathIssuedThisStep = false;
            intraGroupRepathIssuedThisStep = false;
            ClearCellSubscriptions();
        }

        CancelPendingPathRequest();
        orderedDestination = targetPosition;

        if (!IsReadyForMovement())
        {
            pendingDestination = targetPosition;
            return true;
        }

        if (moveState == MoveState.Receding)
        {
            pendingDestination = targetPosition;
            return true;
        }

        if (moveState == MoveState.SteppingToAnchor)
        {
            Vector2Int requestStartCell = currentCell;
            return QueuePathRequest(requestStartCell, targetPosition,
                path => HandleBifurcationPathResult(path, requestStartCell, targetPosition));
        }

        if (moveState == MoveState.SteppingToCenter)
        {
            pendingDestination = targetPosition;
            if (targetPosition == currentCell)
                logicalPath.Clear();
            return true;
        }

        pendingDestination = null;
        Vector2Int initialStartCell = currentCell;
        return QueuePathRequest(initialStartCell, targetPosition,
            path => HandleInitialPathResult(path, initialStartCell, targetPosition), true);
    }

    // Applique un chemin initial calculé dans le budget du tick.
    private void HandleInitialPathResult(List<Vector2Int> path, Vector2Int requestStartCell, Vector2Int destination)
    {
        if (currentCell != requestStartCell)
        {
            if (!queuedCommandDestination.HasValue || !queuedCommandIsPlayerOrder)
            {
                queuedCommandDestination = destination;
                queuedCommandStartsNewOrder = false;
                queuedCommandIsPlayerOrder = false;
            }
            return;
        }

        if (path == null || path.Count == 0 || IsPathAbsurd(path, destination))
        {
            HandleInitialPathFailure(destination);
            return;
        }

        LoadPath(path);
        OnIdleStateChanged.Invoke(false);
        OnMovementStarted.Invoke();
        BeginNextStep(SimulationClockManager.Instance?.CurrentTick ?? 0);
    }

    // Applique une bifurcation tant que l'ancrage n'a pas été franchi.
    private void HandleBifurcationPathResult(List<Vector2Int> path, Vector2Int requestStartCell, Vector2Int destination)
    {
        if (moveState != MoveState.SteppingToAnchor || currentCell != requestStartCell)
        {
            pendingDestination = destination;
            return;
        }

        if (path == null || path.Count == 0 || IsPathAbsurd(path, destination))
        {
            RegisterPathFailure(destination);
            pendingDestination = destination;
            return;
        }

        Vector2Int firstCell = path[0];
        Vector2Int currentDirection = GetDirection(currentCell, targetCell);
        Vector2Int newDirection = GetDirection(currentCell, firstCell);
        if (firstCell == targetCell && newDirection == currentDirection)
        {
            // Le premier step reste identique : seule la suite du chemin est remplacée.
            LoadPath(path);
            logicalPath.Dequeue();
            RefreshWatchedCells(true);
            return;
        }

        // Toute autre bifurcation revient au centre sur la géométrie actuelle avant de re-path.
        pendingDestination = destination;
        StartRecede();
    }

    // Charge une liste de cellules dans la queue stable.
    private void LoadPath(List<Vector2Int> path)
    {
        logicalPath.Clear();
        foreach (Vector2Int cell in path)
            logicalPath.Enqueue(cell);
        RefreshWatchedCells();
    }

    // Démarre le prochain step ou termine le mouvement.
    private void BeginNextStep(long tick)
    {
        if (logicalPath.Count == 0)
        {
            OnMovementCompleted();
            return;
        }

        if (IsRootborn() && isFirstMoveOfSession)
        {
            isFirstMoveOfSession = false;
            preMovementUntilTick = tick + SimulationClock.SecondsToTicks(0.5f);
            SetState(MoveState.WaitingPath);
            animatorController?.PlayRootbornIdleReversed(null);
            return;
        }

        Vector2Int nextCell = logicalPath.Dequeue();
        if (!GridManager.Instance.IsWalkableFor(nextCell, unitGameObject))
        {
            HandlePathBlocked();
            return;
        }

        EntityRegistry registry = EntityRegistry.Instance;
        GameObject concurrent = registry?.GetIncomingUnit(nextCell);
        MovementController concurrentMovement = concurrent != null && concurrent != unitGameObject
            ? registry.GetMovementController(concurrent)
            : null;
        if (IsSameMovementGroup(concurrentMovement))
        {
            if (orderedDestination.HasValue && !intraGroupRepathIssuedThisStep)
            {
                intraGroupRepathIssuedThisStep = true;
                QueueStableRepath(orderedDestination.Value);
            }
            else
            {
                HandlePathBlocked();
            }
            return;
        }

        ConfigureStep(nextCell);
        RefreshWatchedCells(true);
        SetState(MoveState.SteppingToAnchor);
        animatorController?.PlayWalkAnimation(currentCell, targetCell);
    }

    // Configure la géométrie et la durée entière orthogonale ou diagonale.
    private void ConfigureStep(Vector2Int nextCell)
    {
        targetCell = nextCell;
        anchorWorldPos = ComputeAnchor(currentCell, GetDirection(currentCell, targetCell));
        activeStepDurationTicks = GetStepDuration(currentCell, targetCell);
        stepProgressTicks = 0;
        intraGroupRepathIssuedThisStep = false;
        EntityRegistry.Instance?.SetIncoming(unitGameObject, targetCell);
    }

    // Avance d'un tick vers l'ancrage ou le centre cible.
    private void AdvanceStep(long tick)
    {
        EntityRegistry registry = EntityRegistry.Instance;
        GameObject concurrent = registry?.GetIncomingUnit(targetCell);
        if (concurrent != null && concurrent != unitGameObject)
        {
            MovementController concurrentMovement = registry.GetMovementController(concurrent);
            if (IsSameMovementGroup(concurrentMovement))
            {
                StartRecede();
                return;
            }

            bool concurrentWins = concurrentMovement != null
                && (concurrentMovement.CurrentOrderTick < currentOrderTick
                    || (concurrentMovement.CurrentOrderTick == currentOrderTick
                        && concurrentMovement.OwnerPlayerID < ownerPlayerID));
            if (concurrentWins && concurrentMovement.StepProgressRatio >= earlyYieldThreshold)
            {
                RegisterCollision(targetCell);
                StartRecede();
                return;
            }
        }

        if (moveState == MoveState.SteppingToCenter)
        {
            if (pendingDestination == currentCell)
            {
                StartRecede();
                return;
            }

            GameObject occupant = EntityRegistry.Instance?.GetUnitAtLogicalPosition(targetCell);
            if (occupant != null && occupant != unitGameObject)
            {
                RegisterCollision(targetCell);
                StartRecede();
                return;
            }
        }

        stepProgressTicks = Math.Min(activeStepDurationTicks, stepProgressTicks + 1);
        if (moveState == MoveState.SteppingToAnchor && stepProgressTicks >= GetAnchorTick())
            EnterSteppingToCenter();

        if (stepProgressTicks < activeStepDurationTicks)
            return;

        if (!TryCommitTargetCell())
        {
            RegisterCollision(targetCell);
            StartRecede();
            return;
        }

        if (repathRequested && orderedDestination.HasValue)
        {
            pendingDestination = orderedDestination.Value;
            repathRequested = false;
        }

        ConsumeAfterStablePoint(tick);
    }

    // Recule d'un tick sur le même chemin géométrique.
    private void AdvanceRecede(long tick)
    {
        stepProgressTicks = Math.Max(0, stepProgressTicks - 1);
        if (stepProgressTicks > 0)
            return;

        EntityRegistry.Instance?.ClearLeaving(unitGameObject);
        notificationRepathIssuedThisStep = false;

        if (consecutivePathFailures >= 2 && orderedDestination.HasValue)
        {
            StartFailureRecovery(orderedDestination.Value, tick);
            return;
        }

        ConsumeAfterStablePoint(tick);
    }

    // Consomme un ordre pending ou poursuit le chemin depuis un centre stable.
    private void ConsumeAfterStablePoint(long tick)
    {
        if (pendingDestination.HasValue)
        {
            Vector2Int destination = pendingDestination.Value;
            pendingDestination = null;
            if (destination == currentCell)
            {
                logicalPath.Clear();
                OnMovementCompleted();
                return;
            }

            QueueStableRepath(destination);
            return;
        }

        if (moveState == MoveState.Receding && orderedDestination.HasValue)
        {
            QueueStableRepath(orderedDestination.Value);
            return;
        }

        BeginNextStep(tick);
    }

    // Recalcule un chemin depuis une cellule stable après collision, recede ou ordre pending.
    private void QueueStableRepath(Vector2Int destination)
    {
        QueuePathRequest(currentCell, destination, path =>
        {
            if (path == null || path.Count == 0 || IsPathAbsurd(path, destination))
            {
                if (IsOrderedDestinationCamped(destination))
                {
                    StartRetryToDestination(destination);
                    return;
                }
                RegisterPathFailure(destination);
                if (consecutivePathFailures >= 2)
                    StartFailureRecovery(destination, SimulationClockManager.Instance?.CurrentTick ?? 0);
                else
                    HandleFailedRepath(destination);
                return;
            }

            LoadPath(path);
            orderedDestination = destination;
            BeginNextStep(SimulationClockManager.Instance?.CurrentTick ?? 0);
        }, true);
    }

    // Passe en retour arrière en conservant la progression atteinte.
    private void StartRecede()
    {
        EntityRegistry.Instance?.ClearIncoming(unitGameObject);
        SetState(MoveState.Receding);
        animatorController?.PlayWalkAnimation(targetCell, currentCell);
    }

    // Ouvre la fenêtre de contestabilité au franchissement logique de l'ancrage.
    private void EnterSteppingToCenter()
    {
        EntityRegistry.Instance?.SetLeaving(unitGameObject, currentCell);
        SetState(MoveState.SteppingToCenter);
    }

    // Commit atomiquement la cible au tick final.
    private bool TryCommitTargetCell()
    {
        if (EntityRegistry.Instance == null
            || !EntityRegistry.Instance.TryClaimPosition(unitGameObject, targetCell, currentOrderTick, ownerPlayerID))
            return false;

        previousStableCell = currentCell;
        currentCell = targetCell;
        consecutivePathFailures = 0;
        EntityRegistry.Instance.ClearIncoming(unitGameObject);
        EntityRegistry.Instance.ClearLeaving(unitGameObject);
        notificationRepathIssuedThisStep = false;
        RefreshWatchedCells();
        return true;
    }

    // Calcule la pose logique théorique du tick sur centre -> ancrage -> centre.
    private Vector3 EvaluateLogicalWorldPosition()
    {
        Vector3 center = GridManager.Instance.GridToWorld(currentCell);
        if (moveState != MoveState.SteppingToAnchor
            && moveState != MoveState.SteppingToCenter
            && moveState != MoveState.Receding)
            return center;

        int anchorTick = GetAnchorTick();
        if (stepProgressTicks <= anchorTick)
            return Vector3.Lerp(center, anchorWorldPos, stepProgressTicks / (float)anchorTick);

        Vector3 targetCenter = GridManager.Instance.GridToWorld(targetCell);
        int centerTicks = Math.Max(1, activeStepDurationTicks - anchorTick);
        return Vector3.Lerp(anchorWorldPos, targetCenter,
            (stepProgressTicks - anchorTick) / (float)centerTicks);
    }

    // Calcule l'ancrage géométrique historique sans en modifier la forme.
    private Vector3 ComputeAnchor(Vector2Int cell, Vector2Int direction)
    {
        Vector3 center = GridManager.Instance.GridToWorld(cell);
        float halfSize = GridManager.Instance.GetCellSize() / 2f;
        return center + new Vector3(direction.x * halfSize, direction.y * halfSize, 0f);
    }

    // Retourne une direction normalisée sur les deux axes.
    private Vector2Int GetDirection(Vector2Int from, Vector2Int to)
    {
        return new Vector2Int(Mathf.Clamp(to.x - from.x, -1, 1), Mathf.Clamp(to.y - from.y, -1, 1));
    }

    // Retourne la durée du step avec le ratio A* diagonal 14/10.
    private int GetStepDuration(Vector2Int from, Vector2Int to)
    {
        Vector2Int direction = GetDirection(from, to);
        return direction.x != 0 && direction.y != 0
            ? Math.Max(1, (int)Math.Round(ticksPerCell * 14d / 10d, MidpointRounding.AwayFromZero))
            : ticksPerCell;
    }

    // Retourne le tick de franchissement de l'ancrage, arrondi au plus proche.
    private int GetAnchorTick()
    {
        return Math.Max(1, (int)Math.Round(activeStepDurationTicks / 2d, MidpointRounding.AwayFromZero));
    }

    // Réagit à une éviction en revenant depuis le centre perdu vers le centre stable précédent.
    public void OnEvicted(Vector2Int lostCell)
    {
        CancelPendingPathRequest();
        CancelRecoveryPause();
        CancelRetry();
        ClearCellSubscriptions();
        logicalPath.Clear();
        EntityRegistry.Instance?.ClearLeaving(unitGameObject);
        EntityRegistry.Instance?.ClearIncoming(unitGameObject);

        if (recoveryDestination.HasValue)
        {
            orderedDestination = recoveryDestination.Value;
            recoveryDestination = null;
        }

        targetCell = lostCell;
        currentCell = previousStableCell;
        anchorWorldPos = ComputeAnchor(currentCell, GetDirection(currentCell, targetCell));
        activeStepDurationTicks = GetStepDuration(currentCell, targetCell);
        stepProgressTicks = activeStepDurationTicks;
        EntityRegistry.Instance?.UpdateLogicalPosition(unitGameObject, currentCell);
        StartRecede();
    }

    // Ajoute une demande au scheduler déterministe et ignore les callbacks obsolètes.
    private bool QueuePathRequest(Vector2Int start, Vector2Int end, Action<List<Vector2Int>> callback, bool enterWaitingState = false)
    {
        if (PathFindingManager.Instance == null)
            return false;

        CancelPendingPathRequest();
        long requestId = 0;
        requestId = PathFindingManager.Instance.RequestPath(start, end, unitGameObject, path =>
        {
            if (activePathRequestId != requestId)
                return;
            activePathRequestId = 0;
            callback(path);
        });
        activePathRequestId = requestId;
        if (enterWaitingState && requestId > 0)
            SetState(MoveState.WaitingPath);
        return requestId > 0;
    }

    // Annule la requête de path encore en file.
    private void CancelPendingPathRequest()
    {
        if (activePathRequestId == 0)
            return;
        PathFindingManager.Instance?.CancelRequest(activePathRequestId);
        activePathRequestId = 0;
    }

    // Centralise les transitions et l'index moving du registre.
    private void SetState(MoveState newState)
    {
        if (moveState == newState)
            return;
        moveState = newState;
        if (newState == MoveState.SteppingToAnchor || newState == MoveState.SteppingToCenter || newState == MoveState.Receding)
            EntityRegistry.Instance?.SetMoving(unitGameObject, true);
        else if (newState == MoveState.Idle || newState == MoveState.Blocked)
            EntityRegistry.Instance?.SetMoving(unitGameObject, false);
        if (newState == MoveState.Blocked)
        {
            notificationRepathIssuedThisStep = false;
            intraGroupRepathIssuedThisStep = false;
        }
    }

    // Indique si deux unités appartiennent au même ordre groupé et à la même équipe.
    private bool IsSameMovementGroup(MovementController concurrentMovement)
    {
        return concurrentMovement != null
            && concurrentMovement.CurrentOrderTick == currentOrderTick
            && concurrentMovement.OwnerPlayerID == ownerPlayerID;
    }

    // Reçoit une occupation de cellule et reporte sa décision au SimTick.
    public void OnUnitEntered(Vector2Int cell, GameObject unit)
    {
        if (unit == unitGameObject || !watchedCells.Contains(cell) || notificationRepathIssuedThisStep)
            return;
        if (moveState != MoveState.SteppingToAnchor && moveState != MoveState.SteppingToCenter && moveState != MoveState.Blocked)
            return;
        notificationRepathIssuedThisStep = true;
        eventRepathPending = true;
    }

    // Reçoit une libération de destination bloquée et reporte sa décision au SimTick.
    public void OnUnitLeft(Vector2Int cell, GameObject unit)
    {
        if (unit == unitGameObject || moveState != MoveState.Blocked || orderedDestination != cell
            || notificationRepathIssuedThisStep || EntityRegistry.Instance?.GetUnitAtLogicalPosition(cell) != null)
            return;
        notificationRepathIssuedThisStep = true;
        eventRepathPending = true;
    }

    // Applique au tick le re-path demandé par une notification.
    private void ProcessEventRepath(long tick)
    {
        if (!orderedDestination.HasValue)
            return;
        if (moveState == MoveState.SteppingToCenter)
            repathRequested = true;
        else
            RequestMove(orderedDestination.Value, false, tick);
    }

    // Met à jour les trois cellules surveillées.
    private void RefreshWatchedCells(bool includeCurrentTarget = false)
    {
        ClearCellSubscriptions();
        if (includeCurrentTarget)
            RegisterWatchedCell(targetCell);
        foreach (Vector2Int cell in logicalPath)
        {
            RegisterWatchedCell(cell);
            if (watchedCells.Count >= 3)
                break;
        }
    }

    // Ajoute une cellule à la surveillance sans doublon.
    private void RegisterWatchedCell(Vector2Int cell)
    {
        if (EntityRegistry.Instance == null || watchedCells.Count >= 3 || watchedCells.Contains(cell))
            return;
        EntityRegistry.Instance.RegisterCellListener(cell, this);
        watchedCells.Add(cell);
    }

    // Surveille uniquement la destination exacte bloquée.
    private void WatchBlockedDestination()
    {
        ClearCellSubscriptions();
        if (orderedDestination.HasValue)
            RegisterWatchedCell(orderedDestination.Value);
    }

    // Retire tous les listeners de cellules.
    private void ClearCellSubscriptions()
    {
        EntityRegistry.Instance?.UnregisterAllCellListeners(this);
        watchedCells.Clear();
    }

    // Route un premier échec vers le retry ou la récupération anti-boucle.
    private void HandleInitialPathFailure(Vector2Int destination)
    {
        if (IsOrderedDestinationCamped(destination))
        {
            StartRetryToDestination(destination);
            return;
        }

        if (recoveryDestination.HasValue && recoveryDestination.Value != destination)
        {
            Vector2Int originalDestination = recoveryDestination.Value;
            recoveryDestination = null;
            StartRecoveryPause(originalDestination);
            return;
        }
        RegisterPathFailure(destination);
        if (consecutivePathFailures >= 2)
            StartFailureRecovery(destination, SimulationClockManager.Instance?.CurrentTick ?? 0);
        else
            StartRetryToDestination(destination);
    }

    // Place l'unité en blocage persistant après un re-path échoué.
    private void HandleFailedRepath(Vector2Int destination)
    {
        if (attackController != null)
        {
            attackController.SetManualOrder(false);
            GameObject nearestEnemy = attackController.FindNearestEnemyInRange();
            if (nearestEnemy != null)
            {
                ClearCellSubscriptions();
                orderedDestination = null;
                attackController.InterruptChaseAndAttack(nearestEnemy);
                SetState(MoveState.Idle);
                return;
            }
        }

        SetState(MoveState.Blocked);
        attackController?.SetManualOrder(false);
        if (animatorController != null && !(attackController?.IsAttacking() ?? false))
            animatorController.PlayIdleAnimation();
        WatchBlockedDestination();
        ScheduleRetry(destination);
    }

    // Lance un retry déterministe après deux secondes en ticks.
    private void StartRetryToDestination(Vector2Int destination)
    {
        orderedDestination = destination;
        SetState(MoveState.Blocked);
        WatchBlockedDestination();
        attackController?.SetManualOrder(false);
        if (animatorController != null && !(attackController?.IsAttacking() ?? false))
            animatorController.PlayIdleAnimation();
        ScheduleRetry(destination);
    }

    // Programme l'échéance logique du retry.
    private void ScheduleRetry(Vector2Int destination)
    {
        long tick = SimulationClockManager.Instance?.CurrentTick ?? 0;
        retryDestination = destination;
        retryAtTick = tick + RETRY_INTERVAL_TICKS;
    }

    // Annule un retry logique.
    private void CancelRetry()
    {
        retryDestination = null;
        retryAtTick = -1;
    }

    // Calcule le retry dans le budget pathfinding du tick.
    private void QueueRetryPath(Vector2Int destination)
    {
        GameObject occupant = EntityRegistry.Instance?.GetUnitAtLogicalPosition(destination);
        bool destinationOccupied = occupant != null
            && occupant != unitGameObject
            && !EntityRegistry.Instance.IsMoving(occupant);
        QueuePathRequest(currentCell, destination, path =>
        {
            bool failed = path == null || path.Count == 0 || IsPathAbsurd(path, destination);
            if (destinationOccupied)
            {
                StartRetryToDestination(destination);
                return;
            }
            if (failed)
            {
                RegisterPathFailure(destination);
                if (consecutivePathFailures >= 2)
                    StartFailureRecovery(destination, SimulationClockManager.Instance?.CurrentTick ?? 0);
                else
                    StartRetryToDestination(destination);
                return;
            }
            LoadPath(path);
            BeginNextStep(SimulationClockManager.Instance?.CurrentTick ?? 0);
        });
    }

    // Enregistre un échec uniquement pour l'ordre actif.
    private void RegisterPathFailure(Vector2Int destination)
    {
        Vector2Int? activeDestination = recoveryDestination ?? orderedDestination;
        if (activeDestination == destination)
            consecutivePathFailures++;
    }

    // Indique si l'ordre actif vise exactement une destination occupée par une unité immobile.
    private bool IsOrderedDestinationCamped(Vector2Int destination)
    {
        if (!orderedDestination.HasValue || orderedDestination.Value != destination || EntityRegistry.Instance == null)
            return false;

        GameObject occupant = EntityRegistry.Instance.GetUnitAtLogicalPosition(destination);
        return occupant != null && occupant != unitGameObject && !EntityRegistry.Instance.IsMoving(occupant);
    }

    // Agrège les collisions répétées sur la même cellule.
    private void RegisterCollision(Vector2Int blockedCell)
    {
        consecutivePathFailures = blockedCell == lastCollisionCell ? consecutivePathFailures + 1 : 1;
        lastCollisionCell = blockedCell;
    }

    // Lance un détour adjacent puis la pause déterministe anti-boucle.
    private void StartFailureRecovery(Vector2Int destination, long tick)
    {
        CancelPendingPathRequest();
        logicalPath.Clear();
        pendingDestination = null;
        orderedDestination = destination;
        SetState(MoveState.Blocked);

        Vector2Int? detour = FindDetourCell();
        if (detour.HasValue)
        {
            recoveryDestination = destination;
            if (!RequestMove(detour.Value, false, tick))
            {
                recoveryDestination = null;
                StartRecoveryPause(destination);
            }
            return;
        }
        StartRecoveryPause(destination);
    }

    // Programme la pause rng de récupération en ticks.
    private void StartRecoveryPause(Vector2Int destination)
    {
        orderedDestination = destination;
        SetState(MoveState.Blocked);
        WatchBlockedDestination();
        attackController?.SetManualOrder(false);
        animatorController?.PlayIdleAnimation();
        SimulationClockManager clock = SimulationClockManager.Instance;
        int pauseTicks = clock != null ? clock.SimulationRandom.NextRange(40, 61) : 50;
        recoveryPauseDestination = destination;
        recoveryPauseUntilTick = (clock?.CurrentTick ?? 0) + pauseTicks;
    }

    // Annule la pause anti-boucle.
    private void CancelRecoveryPause()
    {
        recoveryPauseDestination = null;
        recoveryPauseUntilTick = -1;
    }

    // Cherche un détour adjacent libre en ordre stable.
    private Vector2Int? FindDetourCell()
    {
        if (!orderedDestination.HasValue)
            return null;
        Vector2Int[] offsets = {
            new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(-1, 1), new Vector2Int(1, 1), new Vector2Int(-1, -1), new Vector2Int(1, -1)
        };
        Vector2Int? best = null;
        int bestDistance = int.MaxValue;
        foreach (Vector2Int offset in offsets)
        {
            Vector2Int cell = currentCell + offset;
            if (!GridManager.Instance.IsWalkableFor(cell, unitGameObject))
                continue;
            GameObject occupant = EntityRegistry.Instance?.GetUnitAtLogicalPosition(cell);
            if (occupant != null && occupant != unitGameObject)
                continue;
            int distance = CombatSystem.CalculateChebyshevDistance(cell, orderedDestination.Value);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = cell;
            }
        }
        return best;
    }

    // Détecte les détours excessifs sans dépendre du visuel.
    private bool IsPathAbsurd(List<Vector2Int> path, Vector2Int destination)
    {
        if (path == null || path.Count == 0)
            return false;
        int distance = CombatSystem.CalculateChebyshevDistance(currentCell, destination);
        return distance > 0 && path.Count > distance * detourRatioThreshold;
    }

    // Gère une cellule devenue inaccessible avant le début du step.
    private void HandlePathBlocked()
    {
        EntityRegistry.Instance?.ClearLeaving(unitGameObject);
        EntityRegistry.Instance?.ClearIncoming(unitGameObject);
        logicalPath.Clear();
        animatorController?.PlayIdleAnimation();
        if (!orderedDestination.HasValue)
        {
            ClearCellSubscriptions();
            SetState(MoveState.Idle);
            return;
        }
        SetState(MoveState.Blocked);
        WatchBlockedDestination();
        attackController?.SetManualOrder(false);
        ScheduleRetry(orderedDestination.Value);
    }

    // Termine une destination et conserve les événements historiques.
    private void OnMovementCompleted()
    {
        ClearCellSubscriptions();
        repathRequested = false;
        notificationRepathIssuedThisStep = false;
        EntityRegistry.Instance?.ClearLeaving(unitGameObject);

        if (recoveryDestination.HasValue)
        {
            Vector2Int destination = recoveryDestination.Value;
            recoveryDestination = null;
            StartRecoveryPause(destination);
            return;
        }

        orderedDestination = null;
        consecutivePathFailures = 0;
        SetState(MoveState.Idle);
        OnArrived.Invoke();
        bool attacking = attackController?.IsAttacking() ?? false;
        if (!attacking && attackController != null)
        {
            GameObject enemy = attackController.FindNearestEnemyInRange();
            if (enemy != null)
            {
                attackController.InterruptChaseAndAttack(enemy);
                return;
            }
        }
        if (!attacking)
            animatorController?.PlayIdleAnimation();
        OnIdleStateChanged.Invoke(true);
        isFirstMoveOfSession = true;
        attackController?.SetManualOrder(false);
        if (IsRootborn() && !attacking)
            animatorController?.ResetIdleReversedSession();
    }

    // Arrête la logique et laisse la présentation rejoindre la cellule validée sans téléportation.
    public void StopMovement()
    {
        ResetMovementState();
        EntityRegistry.Instance?.UpdateLogicalPosition(unitGameObject, currentCell);
        Vector3 center = GridManager.Instance.GridToWorld(currentCell);
        previousTickWorldPosition = center;
        currentTickWorldPosition = center;
        visualFrozen = false;
        animatorController?.StopAllAnimations();
    }

    // Arrête la logique sans modifier la pose visuelle courante.
    public void FreezeInPlace()
    {
        ResetMovementState();
        visualFrozen = true;
    }

    // Nettoie l'état commun aux deux formes d'arrêt.
    private void ResetMovementState()
    {
        CancelPendingPathRequest();
        CancelRecoveryPause();
        CancelRetry();
        ClearCellSubscriptions();
        EntityRegistry.Instance?.ClearLeaving(unitGameObject);
        EntityRegistry.Instance?.ClearIncoming(unitGameObject);
        queuedCommandDestination = null;
        queuedCommandIsPlayerOrder = false;
        intraGroupRepathIssuedThisStep = false;
        orderedDestination = null;
        pendingDestination = null;
        recoveryDestination = null;
        logicalPath.Clear();
        SetState(MoveState.Idle);
        isFirstMoveOfSession = true;
    }

    // Vide le chemin sans téléporter l'unité.
    public void ClearPathQueue()
    {
        logicalPath?.Clear();
    }

    // Retourne l'état public historique.
    public MovementState GetCurrentState()
    {
        switch (moveState)
        {
            case MoveState.WaitingPath:
            case MoveState.SteppingToAnchor:
            case MoveState.SteppingToCenter:
            case MoveState.Receding:
                return MovementState.Moving;
            case MoveState.Blocked:
                return MovementState.Blocked;
            default:
                return MovementState.Idle;
        }
    }

    // Force la sortie d'idle logique.
    public void ForceExitIdle()
    {
        if (moveState == MoveState.Idle)
            OnIdleStateChanged.Invoke(false);
    }

    // Notifie le retour d'idle si l'état est toujours compatible.
    public void NotifyIdleRestored()
    {
        if (moveState == MoveState.Idle)
            OnIdleStateChanged.Invoke(true);
    }

    // Retourne la position grille source de vérité.
    public Vector2Int GetCurrentGridPosition()
    {
        return currentCell;
    }

    // Indique si aucune animation spéciale ne bloque un nouvel ordre.
    public bool IsReadyForMovement()
    {
        return !(IsRootborn() && animatorController != null && animatorController.IsPlayingSpecialAnimation());
    }

    // Compatibilité callback Animator : la décision reste consommée au tick par l'échéance fixe.
    public void OnIdleReversedComplete()
    {
        idleReversedCompletionPending = true;
    }

    // Retourne le type Rootborn depuis les données d'unité.
    private bool IsRootborn()
    {
        return statsComponent != null && statsComponent.IsRootborn();
    }

    // Affiche l'état logique sans reconvertir la position visuelle.
    [ContextMenu("Debug Movement Controller")]
    private void DebugControllerState()
    {
        GameLogger.Log(GameLogger.Category.Movement,
            $"MovementController {gameObject.name}: {moveState}, cellule={currentCell}, progression={stepProgressTicks}/{activeStepDurationTicks}", this);
    }

    // Dessine le chemin logique dans la vue Scene.
    private void OnDrawGizmos()
    {
        if (logicalPath == null || logicalPath.Count == 0 || GridManager.Instance == null)
            return;

        Vector3 previous = GridManager.Instance.GridToWorld(currentCell);
        Gizmos.color = Color.blue;
        foreach (Vector2Int cell in logicalPath)
        {
            Vector3 next = GridManager.Instance.GridToWorld(cell);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
