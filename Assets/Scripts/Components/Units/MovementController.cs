using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Composant responsable de l'exécution du mouvement individuel d'une unité
// Architecture LOGIQUE/VISUEL séparée pour stabilité lors spam d'ordres
// LOGIQUE = position grille (source de vérité) | VISUEL = transform.position (cosmétique)
public class MovementController : MonoBehaviour, IEvictable, ICellListener
{
    [Header("Movement Settings")]
    private float movementSpeed = 1f;          // Vitesse déplacement (unités Unity/seconde)
    private float retryInterval = 2f;     // Filet de sécurité pour les blocages sans notification
    [SerializeField] private float detourRatioThreshold = 3f; // Ratio chemin/Chebyshev au-delà duquel le chemin est considéré absurde

    // Modifie la vitesse de déplacement de l'unité
    // Appelé par UnitStats pour synchroniser les stats
    public void SetMovementSpeed(float newSpeed)
    {
        movementSpeed = Mathf.Max(0.1f, newSpeed);

        if (debugMovement)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: Vitesse mise à jour -> {movementSpeed}", this);
        }
    }

    [Header("Debug")]
    [SerializeField] private bool debugMovement = true;         // Affichage debug dans console

    // Énumération des états possibles du contrôleur de mouvement
    public enum MovementState
    {
        Idle,       // Unité immobile, prête à recevoir ordres
        Moving,     // Unité en déplacement vers case suivante
        Blocked,    // Mouvement bloqué par obstacle/unité
        Arrived     // Destination finale atteinte
    }

    // États internes explicites du pipeline de déplacement
    public enum MoveState
    {
        Idle,
        WaitingPath,
        SteppingToAnchor,
        SteppingToCenter,
        Receding,
        Blocked
    }

    // ========== LOGIQUE (Source de vérité) ==========
    private Queue<Vector2Int> logicalPath;        // Chemin logique à suivre
    private MoveState moveState = MoveState.Idle;

    // ========== VISUEL (Présentation uniquement) ==========
    private Vector2Int currentCell;               // Cellule logique validée (= dernier centre atteint)
    private Vector2Int previousStableCell;        // Cellule stable précédant le dernier commit réussi
    private Vector2Int targetCell;                // Cellule visée par step en cours
    private Vector3 anchorWorldPos;               // Ancrage géométrique cible/franchi
    private const float ANCHOR_EPSILON = 0.05f;
    private const float CENTER_EPSILON = 0.05f;

    // ========== ÉTAT ==========
    private Vector2Int? pendingDestination = null; // Ordre à consommer au prochain point stable ou après IdleReversed
    private Vector2Int? orderedDestination = null; // Destination persistante de l'ordre courant
    private int consecutivePathFailures;       // Échecs consécutifs de rejointe de la destination ordonnée
    private Vector2Int lastCollisionCell;      // Dernière case ayant causé une collision
    private Vector2Int? recoveryDestination;   // Destination ordonnée conservée pendant le détour local
    private readonly List<Vector2Int> watchedCells = new List<Vector2Int>(3);
    private bool repathRequested;
    private bool notificationRepathIssuedThisStep;
    private long currentOrderTick;
    private int ownerPlayerID;

    // Déclenché quand l'unité commence à se déplacer (premier step du chemin)
    public UnityEngine.Events.UnityEvent OnMovementStarted = new();

    // Déclenché quand l'unité atteint sa destination finale avec succès
    public UnityEngine.Events.UnityEvent OnArrived = new();

    // Déclenché quand l'unité entre ou sort de l'état Idle logique
    public UnityEngine.Events.UnityEvent<bool> OnIdleStateChanged = new();

    // Vrai si l'unité est actuellement en mouvement
    public bool IsMoving => GetCurrentState() == MovementState.Moving;

    // Références cached pour performance
    private GameObject unitGameObject;
    private AnimatorController animatorController;
    private UnitStatsComponent _statsComponent; // Cache pour IsRootborn() via SO
    private AttackController attackController;
    private ITeamMember teamMember;

    // Variables de contrôle temporel
    private Coroutine interpolationCoroutine;
    private Coroutine retryCoroutine;
    private bool isFirstMoveOfSession = true; // Reset quand mouvement s'arrête
    private long activePathRequestId = 0;

    private void Start()
    {
        unitGameObject = gameObject;
        logicalPath = new Queue<Vector2Int>();

        // Initialise position logique depuis position monde (avec vérification)
        if (GridManager.Instance != null)
        {
            currentCell = GridManager.Instance.WorldToGrid(transform.position);
            previousStableCell = currentCell;
            EntityRegistry.Instance?.UpdateLogicalPosition(gameObject, currentCell);
        }
        else
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"MovementController {gameObject.name}: GridManager.Instance est null dans Awake(), position logique sera initialisée plus tard", this);
        }

        // Cache la référence AnimatorController
        animatorController = GetComponent<AnimatorController>();
        if (animatorController == null)
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"MovementController sur {gameObject.name}: AnimatorController non trouvé!", this);
        }

        _statsComponent = GetComponent<UnitStatsComponent>();
        attackController = GetComponent<AttackController>();
        teamMember = GetComponent<ITeamMember>();
        ownerPlayerID = teamMember != null ? teamMember.GetTeamID() : 0;
    }

    // Annule la requête en attente si l'unité est détruite avant son callback
    private void OnDestroy()
    {
        CancelPendingPathRequest();
        ClearCellSubscriptions();
        EntityRegistry.Instance?.ClearLeaving(unitGameObject);
    }

    // Centralise toutes les transitions et synchronise l'état de mouvement du registre
    private void SetState(MoveState newState)
    {
        if (moveState == newState)
            return;

        MoveState previousState = moveState;
        moveState = newState;

        if (newState == MoveState.SteppingToAnchor)
            EntityRegistry.Instance?.SetMoving(unitGameObject, true);
        else if (newState == MoveState.Idle || newState == MoveState.Blocked)
            EntityRegistry.Instance?.SetMoving(unitGameObject, false);

        if (newState == MoveState.Blocked)
            notificationRepathIssuedThisStep = false;

        if (debugMovement && GameLogger.IsEnabled(GameLogger.Category.Movement))
            GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: état {previousState} → {newState}", this);
    }

    // Remplace les abonnements par les trois prochaines cellules du chemin courant
    private void RefreshWatchedCells(bool includeCurrentTarget = false)
    {
        if (EntityRegistry.Instance == null)
        {
            watchedCells.Clear();
            return;
        }

        foreach (Vector2Int cell in watchedCells)
            EntityRegistry.Instance.UnregisterCellListener(cell, this);
        watchedCells.Clear();

        if (includeCurrentTarget)
            RegisterWatchedCell(targetCell);

        foreach (Vector2Int cell in logicalPath)
        {
            RegisterWatchedCell(cell);
            if (watchedCells.Count >= 3)
                break;
        }
    }

    // Ajoute une cellule à la surveillance courante sans doublon
    private void RegisterWatchedCell(Vector2Int cell)
    {
        if (watchedCells.Count >= 3 || watchedCells.Contains(cell))
            return;

        EntityRegistry.Instance.RegisterCellListener(cell, this);
        watchedCells.Add(cell);
    }

    // Surveille uniquement la destination exacte lorsqu'une unité est bloquée
    private void WatchBlockedDestination()
    {
        ClearCellSubscriptions();
        if (orderedDestination.HasValue)
            RegisterWatchedCell(orderedDestination.Value);
    }

    // Retire tous les abonnements de cellules détenus par ce contrôleur
    private void ClearCellSubscriptions()
    {
        EntityRegistry.Instance?.UnregisterAllCellListeners(this);
        watchedCells.Clear();
    }

    // Déclenche un re-path lorsqu'une unité tierce occupe une cellule surveillée
    public void OnUnitEntered(Vector2Int cell, GameObject unit)
    {
        if (unit == unitGameObject || !watchedCells.Contains(cell) || notificationRepathIssuedThisStep)
            return;

        if (moveState != MoveState.SteppingToAnchor
            && moveState != MoveState.SteppingToCenter
            && moveState != MoveState.Blocked)
        {
            return;
        }

        notificationRepathIssuedThisStep = true;
        RequestEventRepath();
    }

    // Relance immédiatement une destination bloquée dès que son occupant la libère
    public void OnUnitLeft(Vector2Int cell, GameObject unit)
    {
        if (unit == unitGameObject || moveState != MoveState.Blocked
            || !orderedDestination.HasValue || orderedDestination.Value != cell
            || notificationRepathIssuedThisStep)
        {
            return;
        }

        if (EntityRegistry.Instance?.GetUnitAtLogicalPosition(cell) != null)
            return;

        notificationRepathIssuedThisStep = true;
        RequestEventRepath();
    }

    // Applique immédiatement le re-path ou le reporte au prochain point stable du step
    private void RequestEventRepath()
    {
        if (!orderedDestination.HasValue)
            return;

        if (moveState == MoveState.SteppingToCenter)
        {
            repathRequested = true;
            return;
        }

        MoveTo(orderedDestination.Value, false);
    }

    // Interface publique principale pour demander un déplacement vers une destination
    // Calcule l'A* sur cellules et adapte le step en cours selon la phase ancrage/centre.
    public bool MoveTo(Vector2Int targetPosition)
    {
        return MoveTo(targetPosition, true);
    }

    // Traite un ordre public ou une continuation interne en conservant le tick d'origine
    private bool MoveTo(Vector2Int targetPosition, bool startsNewOrder)
    {
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        if (currentCell == Vector2Int.zero && GridManager.Instance != null)
        {
            currentCell = GridManager.Instance.WorldToGrid(transform.position);
        }

        if (!GridManager.Instance.IsInBounds(targetPosition))
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"MovementController {gameObject.name}: Destination {targetPosition} hors limites", this);
            return false;
        }

        if (startsNewOrder)
        {
            currentOrderTick = SimulationClockManager.Instance != null ? SimulationClockManager.Instance.CurrentTick : 0;
            consecutivePathFailures = 0;
            recoveryDestination = null;
            repathRequested = false;
            notificationRepathIssuedThisStep = false;
            ClearCellSubscriptions();
        }

        CancelPendingPathRequest();

        if (!IsReadyForMovement())
        {
            pendingDestination = targetPosition;
            orderedDestination = targetPosition;
            if (debugMovement) GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: ordre en attente IdleReversed", this);
            return true;
        }

        if (debugMovement)
            GameLogger.Log(GameLogger.Category.Movement, $"🔍 MoveTo {gameObject.name}: currentCell={currentCell}, target={targetPosition}, état={moveState}", this);

        pendingDestination = null;

        // Un ordre reçu pendant le recede sera consommé au retour sur une cellule stable
        if (moveState == MoveState.Receding)
        {
            pendingDestination = targetPosition;
            orderedDestination = targetPosition;
            if (debugMovement) GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: ordre pendant recede → {targetPosition}", this);
            return true;
        }

        // La bifurcation reste libre avant le franchissement de l'ancrage
        if (moveState == MoveState.SteppingToAnchor)
        {
            orderedDestination = targetPosition;
            Vector2Int requestStartCell = currentCell;
            return QueuePathRequest(requestStartCell, targetPosition,
                path => HandleBifurcationPathResult(path, requestStartCell, targetPosition), false);
        }

        // Après l'ancrage, le centre courant est obligatoire avant consommation du nouvel ordre
        if (moveState == MoveState.SteppingToCenter)
        {
            pendingDestination = targetPosition;
            orderedDestination = targetPosition;

            if (targetPosition == currentCell)
            {
                logicalPath.Clear();
                if (debugMovement) GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: rebrousse vers {currentCell}", this);
            }
            else if (debugMovement)
            {
                GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: ordre en file → {targetPosition}", this);
            }
            return true;
        }

        orderedDestination = targetPosition;
        Vector2Int initialStartCell = currentCell;
        return QueuePathRequest(initialStartCell, targetPosition,
            path => HandleInitialPathResult(path, initialStartCell, targetPosition));
    }

    // Applique le résultat d'une demande initiale puis démarre le déplacement ou le retry existant
    private void HandleInitialPathResult(List<Vector2Int> path, Vector2Int requestStartCell, Vector2Int targetPosition)
    {
        if (currentCell != requestStartCell)
        {
            MoveTo(targetPosition, false);
            return;
        }

        if (path != null && path.Count > 0 && IsPathAbsurd(path, targetPosition))
        {
            if (debugMovement)
                GameLogger.Warn(GameLogger.Category.Movement, $"MovementController {gameObject.name}: chemin absurde vers {targetPosition} ({path.Count} steps / Chebyshev {Mathf.Max(Mathf.Abs(currentCell.x - targetPosition.x), Mathf.Abs(currentCell.y - targetPosition.y))}), ordre ignoré", this);
            HandleInitialPathFailure(targetPosition);
            return;
        }

        if (path == null || path.Count == 0)
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"MovementController {gameObject.name}: pas de chemin vers {targetPosition} — retry", this);
            HandleInitialPathFailure(targetPosition);
            return;
        }

        logicalPath.Clear();
        foreach (Vector2Int cell in path) logicalPath.Enqueue(cell);
        RefreshWatchedCells();

        if (debugMovement)
            GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: nouveau chemin {path.Count} cases depuis {currentCell} vers {targetPosition}", this);

        OnIdleStateChanged.Invoke(false);
        OnMovementStarted.Invoke();
        SetState(MoveState.SteppingToAnchor);
        interpolationCoroutine = StartCoroutine(InterpolationLoop());
    }

    // Route l'échec initial vers la récupération de l'ordre ou vers son retry normal
    private void HandleInitialPathFailure(Vector2Int targetPosition)
    {
        if (recoveryDestination.HasValue && recoveryDestination.Value != targetPosition)
        {
            Vector2Int destination = recoveryDestination.Value;
            recoveryDestination = null;
            StartRecoveryPause(destination);
            return;
        }

        RegisterPathFailure(targetPosition);
        if (consecutivePathFailures >= 2)
            StartFailureRecovery(targetPosition);
        else
            StartRetryToDestination(targetPosition);
    }

    // Applique une bifurcation si l'ancrage n'est pas franchi, sinon la convertit en destination pending
    private void HandleBifurcationPathResult(List<Vector2Int> path, Vector2Int requestStartCell, Vector2Int targetPosition)
    {
        if (moveState != MoveState.SteppingToAnchor || currentCell != requestStartCell)
        {
            pendingDestination = targetPosition;
            orderedDestination = targetPosition;

            if (moveState == MoveState.Idle || moveState == MoveState.Blocked)
                MoveTo(targetPosition, false);
            return;
        }

        if (path == null || path.Count == 0)
        {
            RegisterPathFailure(targetPosition);
            pendingDestination = targetPosition;
            orderedDestination = targetPosition;
            return;
        }

        if (IsPathAbsurd(path, targetPosition))
        {
            RegisterPathFailure(targetPosition);
            pendingDestination = targetPosition;
            orderedDestination = targetPosition;
            return;
        }

        logicalPath.Clear();
        foreach (Vector2Int cell in path) logicalPath.Enqueue(cell);
        orderedDestination = targetPosition;

        Vector2Int newFirstCell = logicalPath.Peek();
        logicalPath.Dequeue();
        Vector2Int direction = GetDirection(currentCell, newFirstCell);
        targetCell = newFirstCell;
        anchorWorldPos = ComputeAnchor(currentCell, direction);
        RefreshWatchedCells(true);

        if (animatorController != null)
            animatorController.PlayWalkAnimation(currentCell, newFirstCell);

        if (debugMovement) GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: bifurcation ToAnchor → {newFirstCell}", this);
    }

    // Place une demande dans le scheduler et ignore tout callback devenu obsolète
    private bool QueuePathRequest(Vector2Int startPosition, Vector2Int endPosition, System.Action<List<Vector2Int>> callback, bool enterWaitingState = true)
    {
        if (PathFindingManager.Instance == null)
        {
            GameLogger.Error($"MovementController {gameObject.name}: PathFindingManager introuvable", this);
            return false;
        }

        CancelPendingPathRequest();

        long requestId = 0;
        requestId = PathFindingManager.Instance.RequestPath(startPosition, endPosition, unitGameObject, path =>
        {
            if (activePathRequestId != requestId)
                return;

            activePathRequestId = 0;
            callback(path);
        });

        activePathRequestId = requestId;
        if (requestId > 0 && enterWaitingState)
            SetState(MoveState.WaitingPath);
        return requestId > 0;
    }

    // Annule la demande de chemin encore en attente pour cette unité
    private void CancelPendingPathRequest()
    {
        if (activePathRequestId == 0)
            return;

        PathFindingManager.Instance?.CancelRequest(activePathRequestId);
        activePathRequestId = 0;
    }

    // Boucle principale d'interpolation en steps ancrage -> centre.
    // Consomme le path cellule par cellule et ne commit la logique qu'au centre cible.
    private IEnumerator InterpolationLoop()
    {
        if (debugMovement && GameLogger.IsEnabled(GameLogger.Category.Movement)) GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: début boucle interpolation", this);

        while (logicalPath.Count > 0)
        {
            Vector2Int nextCell = logicalPath.Dequeue();

            if (IsRootborn() && isFirstMoveOfSession)
            {
                yield return StartCoroutine(PlayRootbornPreMovementAnimation());
                isFirstMoveOfSession = false;
            }

            if (!GridManager.Instance.IsWalkableFor(nextCell, unitGameObject))
            {
                if (debugMovement && GameLogger.IsEnabled(GameLogger.Category.Movement)) GameLogger.Warn(GameLogger.Category.Movement, $"MovementController {gameObject.name}: case {nextCell} inaccessible", this);
                HandlePathBlocked();
                yield break;
            }

            targetCell = nextCell;
            Vector2Int direction = GetDirection(currentCell, targetCell);
            anchorWorldPos = ComputeAnchor(currentCell, direction);
            SetState(MoveState.SteppingToAnchor);

            if (animatorController != null)
                animatorController.PlayWalkAnimation(currentCell, targetCell);

            if (debugMovement && GameLogger.IsEnabled(GameLogger.Category.Movement))
                GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: step {currentCell} → {targetCell} via ancrage", this);

            yield return StartCoroutine(ExecuteStep());

            if (repathRequested && orderedDestination.HasValue)
            {
                pendingDestination = orderedDestination.Value;
                repathRequested = false;
            }

            if (moveState == MoveState.Receding && consecutivePathFailures >= 2 && orderedDestination.HasValue)
            {
                StartFailureRecovery(orderedDestination.Value);
                yield break;
            }

            // Un recede termine toujours sur currentCell, seul point où un pending peut être consommé
            if (moveState == MoveState.Receding && pendingDestination.HasValue)
            {
                Vector2Int recedeDestination = pendingDestination.Value;
                pendingDestination = null;

                if (recedeDestination == currentCell)
                {
                    logicalPath.Clear();
                    OnMovementCompleted();
                    yield break;
                }

                List<Vector2Int> recedePath = null;
                bool recedePathCompleted = false;
                if (!QueuePathRequest(currentCell, recedeDestination, path =>
                    {
                        recedePath = path;
                        recedePathCompleted = true;
                    }))
                {
                    HandlePathBlocked();
                    yield break;
                }

                long recedeRequestId = activePathRequestId;
                while (!recedePathCompleted)
                {
                    if (activePathRequestId != recedeRequestId)
                        yield break;

                    yield return null;
                }

                if (recedePath == null || recedePath.Count == 0 || IsPathAbsurd(recedePath, recedeDestination))
                {
                    RegisterPathFailure(recedeDestination);
                    if (consecutivePathFailures >= 2)
                        StartFailureRecovery(recedeDestination);
                    else
                        HandleFailedRepath(recedeDestination);
                    yield break;
                }

                logicalPath.Clear();
                foreach (Vector2Int cell in recedePath) logicalPath.Enqueue(cell);
                RefreshWatchedCells();
                SetState(MoveState.SteppingToAnchor);
                continue;
            }

            if (moveState == MoveState.Receding)
            {
                if (!orderedDestination.HasValue)
                {
                    SetState(MoveState.Idle);
                    yield break;
                }

                Vector2Int dest = orderedDestination.Value;
                List<Vector2Int> retryPath = null;
                bool retryPathCompleted = false;

                if (!QueuePathRequest(currentCell, dest, path =>
                    {
                        retryPath = path;
                        retryPathCompleted = true;
                    }))
                {
                    HandlePathBlocked();
                    yield break;
                }

                long collisionRequestId = activePathRequestId;
                while (!retryPathCompleted)
                {
                    if (activePathRequestId != collisionRequestId)
                        yield break;

                    yield return null;
                }

                if (retryPath != null && retryPath.Count > 0 && !IsPathAbsurd(retryPath, dest))
                {
                    if (debugMovement && GameLogger.IsEnabled(GameLogger.Category.Movement)) GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: recalcul après collision réussi vers {dest}", this);
                    logicalPath.Clear();
                    foreach (var c in retryPath) logicalPath.Enqueue(c);
                    RefreshWatchedCells();
                    SetState(MoveState.SteppingToAnchor);
                    continue;
                }

                RegisterPathFailure(dest);
                if (consecutivePathFailures >= 2)
                    StartFailureRecovery(dest);
                else
                    HandleFailedRepath(dest);
                yield break;
            }

            // Commit réussi → currentCell est déjà mis à jour dans ExecuteStep.

            // Consomme pendingDestination (rebrousse, file, ordre pendant recede, etc.).
            if (pendingDestination.HasValue)
            {
                Vector2Int dest = pendingDestination.Value;
                pendingDestination = null;
                List<Vector2Int> newPath = null;
                bool pendingPathCompleted = false;

                if (!QueuePathRequest(currentCell, dest, path =>
                    {
                        newPath = path;
                        pendingPathCompleted = true;
                    }))
                {
                    HandlePathBlocked();
                    yield break;
                }

                long pendingRequestId = activePathRequestId;
                while (!pendingPathCompleted)
                {
                    if (activePathRequestId != pendingRequestId)
                        yield break;

                    yield return null;
                }

                if (newPath != null && newPath.Count > 0 && !IsPathAbsurd(newPath, dest))
                {
                    logicalPath.Clear();
                    foreach (var c in newPath) logicalPath.Enqueue(c);
                    RefreshWatchedCells();
                    if (debugMovement && GameLogger.IsEnabled(GameLogger.Category.Movement)) GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: pending consommé → {dest}", this);
                }
                else
                {
                    RegisterPathFailure(dest);
                    if (consecutivePathFailures >= 2)
                        StartFailureRecovery(dest);
                    else
                        HandleFailedRepath(dest);
                    yield break;
                }
            }

            if (logicalPath.Count > 0 && animatorController != null)
                animatorController.PlayWalkAnimation(currentCell, logicalPath.Peek());

            if (logicalPath.Count > 0)
                SetState(MoveState.SteppingToAnchor);
        }

        OnMovementCompleted();
    }

    // Calcule la position monde du point d'ancrage entre cell et la cellule voisine dans direction donnée.
    private Vector3 ComputeAnchor(Vector2Int cell, Vector2Int direction)
    {
        Vector3 center = GridManager.Instance.GridToWorld(cell);
        float halfSize = GridManager.Instance.GetCellSize() / 2f;
        return center + new Vector3(direction.x * halfSize, direction.y * halfSize, 0f);
    }

    // Retourne la direction unitaire (-1/0/+1 par axe) de from vers to.
    private Vector2Int GetDirection(Vector2Int from, Vector2Int to)
    {
        int dx = Mathf.Clamp(to.x - from.x, -1, 1);
        int dy = Mathf.Clamp(to.y - from.y, -1, 1);
        return new Vector2Int(dx, dy);
    }

    // Exécute un step en 2 phases : ToAnchor (centre courant -> ancrage), ToCenter (ancrage -> centre cible).
    // Le commit logique se fait uniquement au passage du centre, via TryClaimPosition atomique.
    private IEnumerator ExecuteStep()
    {
        Vector3 fromCenterWorld = GridManager.Instance.GridToWorld(currentCell);
        transform.position = fromCenterWorld;

        // Phase ToAnchor : déplacement libre tant que l'ancrage n'est pas franchi.
        while (moveState == MoveState.SteppingToAnchor)
        {
            Vector3 toAnchor = anchorWorldPos;
            Vector3 dir = toAnchor - transform.position;
            float dist = dir.magnitude;

            if (dist < ANCHOR_EPSILON)
            {
                transform.position = toAnchor;
                EnterSteppingToCenter();
                break;
            }

            float step = movementSpeed * Time.deltaTime;
            if (step >= dist)
            {
                transform.position = toAnchor;
                EnterSteppingToCenter();
                break;
            }

            transform.position += dir.normalized * step;
            yield return null;
        }

        // Phase ToCenter : une fois l'ancrage franchi, le centre cible est obligatoire.
        Vector3 centerWorld = GridManager.Instance.GridToWorld(targetCell);
        while (moveState == MoveState.SteppingToCenter)
        {
            if (pendingDestination.HasValue && pendingDestination.Value == currentCell)
            {
                SetState(MoveState.Receding);
                yield return StartCoroutine(RecedeToCurrentCenter());
                yield break;
            }

            GameObject occupant = EntityRegistry.Instance?.GetUnitAtLogicalPosition(targetCell);
            if (occupant != null && occupant != unitGameObject)
            {
                RegisterCollision(targetCell);
                if (debugMovement && GameLogger.IsEnabled(GameLogger.Category.Movement)) GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: collision targetCell {targetCell} occupée par {occupant.name}, recede", this);
                SetState(MoveState.Receding);
                yield return StartCoroutine(RecedeToCurrentCenter());
                yield break;
            }

            Vector3 toCenter = centerWorld - transform.position;
            float dist = toCenter.magnitude;

            if (dist < CENTER_EPSILON)
            {
                transform.position = centerWorld;
                if (!TryCommitTargetCell())
                {
                    SetState(MoveState.Receding);
                    yield return StartCoroutine(RecedeToCurrentCenter());
                }
                yield break;
            }

            float step = movementSpeed * Time.deltaTime;
            if (step >= dist)
            {
                transform.position = centerWorld;
                if (!TryCommitTargetCell())
                {
                    SetState(MoveState.Receding);
                    yield return StartCoroutine(RecedeToCurrentCenter());
                }
                yield break;
            }

            transform.position += toCenter.normalized * step;
            yield return null;
        }
    }

    // Ouvre la fenêtre de contestabilité de la cellule source pendant le Lerp vers le centre cible
    private void EnterSteppingToCenter()
    {
        EntityRegistry.Instance?.SetLeaving(unitGameObject, currentCell);
        SetState(MoveState.SteppingToCenter);
    }

    // Tente le commit atomique de targetCell et ferme la fenêtre leaving en cas de succès
    private bool TryCommitTargetCell()
    {
        if (EntityRegistry.Instance == null) return false;
        if (EntityRegistry.Instance.TryClaimPosition(unitGameObject, targetCell, currentOrderTick, ownerPlayerID))
        {
            previousStableCell = currentCell;
            currentCell = targetCell;
            consecutivePathFailures = 0;
            EntityRegistry.Instance.ClearLeaving(unitGameObject);
            notificationRepathIssuedThisStep = false;
            RefreshWatchedCells();
            return true;
        }
        return false;
    }

    // Retour visuel position actuelle -> ancrage franchi -> centre currentCell, sans commit logique.
    private IEnumerator RecedeToCurrentCenter()
    {
        Vector3 centerWorld = GridManager.Instance.GridToWorld(currentCell);

        if (animatorController != null)
            animatorController.PlayWalkAnimation(targetCell, currentCell);

        Vector3 anchor = anchorWorldPos;
        while (moveState == MoveState.Receding)
        {
            Vector3 dir = anchor - transform.position;
            float dist = dir.magnitude;
            if (dist < ANCHOR_EPSILON) { transform.position = anchor; break; }
            float step = movementSpeed * Time.deltaTime;
            if (step >= dist) { transform.position = anchor; break; }
            transform.position += dir.normalized * step;
            yield return null;
        }

        while (moveState == MoveState.Receding)
        {
            Vector3 dir = centerWorld - transform.position;
            float dist = dir.magnitude;
            if (dist < CENTER_EPSILON) { transform.position = centerWorld; break; }
            float step = movementSpeed * Time.deltaTime;
            if (step >= dist) { transform.position = centerWorld; break; }
            transform.position += dir.normalized * step;
            yield return null;
        }

        EntityRegistry.Instance?.ClearLeaving(unitGameObject);
        notificationRepathIssuedThisStep = false;
    }

    // Réagit à la perte d'une cellule claimée en rejouant le recede animé vers la cellule stable précédente
    public void OnEvicted(Vector2Int lostCell)
    {
        CancelPendingPathRequest();
        ClearCellSubscriptions();
        repathRequested = false;
        notificationRepathIssuedThisStep = false;

        if (recoveryDestination.HasValue)
        {
            orderedDestination = recoveryDestination.Value;
            recoveryDestination = null;
        }

        if (interpolationCoroutine != null)
        {
            StopCoroutine(interpolationCoroutine);
            interpolationCoroutine = null;
        }

        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        logicalPath.Clear();
        EntityRegistry.Instance?.ClearLeaving(unitGameObject);

        targetCell = lostCell;
        currentCell = previousStableCell;
        anchorWorldPos = ComputeAnchor(currentCell, GetDirection(currentCell, targetCell));
        EntityRegistry.Instance?.UpdateLogicalPosition(unitGameObject, currentCell);
        SetState(MoveState.Receding);
        interpolationCoroutine = StartCoroutine(ResumeAfterEviction());
    }

    // Termine le recede d'éviction puis relance immédiatement le re-path de l'ordre courant
    private IEnumerator ResumeAfterEviction()
    {
        yield return StartCoroutine(RecedeToCurrentCenter());

        Vector2Int? destination = pendingDestination ?? orderedDestination;
        pendingDestination = null;
        if (!destination.HasValue)
        {
            SetState(MoveState.Idle);
            yield break;
        }

        List<Vector2Int> recoveryPath = null;
        bool recoveryPathCompleted = false;
        if (!QueuePathRequest(currentCell, destination.Value, path =>
            {
                recoveryPath = path;
                recoveryPathCompleted = true;
            }))
        {
            HandleFailedRepath(destination.Value);
            yield break;
        }

        long recoveryRequestId = activePathRequestId;
        while (!recoveryPathCompleted)
        {
            if (activePathRequestId != recoveryRequestId)
                yield break;

            yield return null;
        }

        if (recoveryPath == null || recoveryPath.Count == 0 || IsPathAbsurd(recoveryPath, destination.Value))
        {
            RegisterPathFailure(destination.Value);
            if (consecutivePathFailures >= 2)
                StartFailureRecovery(destination.Value);
            else
                HandleFailedRepath(destination.Value);
            yield break;
        }

        logicalPath.Clear();
        foreach (Vector2Int cell in recoveryPath) logicalPath.Enqueue(cell);
        RefreshWatchedCells();
        orderedDestination = destination.Value;
        SetState(MoveState.SteppingToAnchor);
        interpolationCoroutine = StartCoroutine(InterpolationLoop());
    }

    // Applique le flux d'échec commun aux re-paths après collision ou éviction
    private void HandleFailedRepath(Vector2Int destination)
    {
        if (attackController != null)
        {
            attackController.SetManualOrder(false);
            GameObject nearestEnemy = attackController.FindNearestEnemyInRange();
            if (nearestEnemy != null)
            {
                if (debugMovement && GameLogger.IsEnabled(GameLogger.Category.Movement))
                    GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: bloquée + ennemi à portée → auto-attaque", this);
                ClearCellSubscriptions();
                orderedDestination = null;
                attackController.InterruptChaseAndAttack(nearestEnemy);
                SetState(MoveState.Idle);
                return;
            }
        }

        SetState(MoveState.Blocked);
        attackController?.SetManualOrder(false);
        if (animatorController != null && !(attackController != null && attackController.IsAttacking()))
            animatorController.PlayIdleAnimation();
        WatchBlockedDestination();
        retryCoroutine = StartCoroutine(RetryPathPeriodically(destination));
    }

    // Enregistre un échec de chemin uniquement pour la destination de l'ordre actif
    private void RegisterPathFailure(Vector2Int destination)
    {
        Vector2Int? activeDestination = recoveryDestination ?? orderedDestination;
        if (!activeDestination.HasValue || activeDestination.Value != destination)
            return;

        consecutivePathFailures++;
    }

    // Enregistre les collisions répétées sur une même case dans le compteur d'échecs unique
    private void RegisterCollision(Vector2Int blockedCell)
    {
        if (blockedCell == lastCollisionCell)
        {
            consecutivePathFailures++;
        }
        else
        {
            consecutivePathFailures = 1;
            lastCollisionCell = blockedCell;
        }
    }

    // Lance un détour local adjacent avant une pause, sans remplacer la destination de l'ordre joueur
    private void StartFailureRecovery(Vector2Int destination)
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
            retryCoroutine = null;
            if (debugMovement && GameLogger.IsEnabled(GameLogger.Category.Movement))
                GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: {consecutivePathFailures} échecs, détour local vers {detour.Value}", this);
            if (!MoveTo(detour.Value, false))
            {
                recoveryDestination = null;
                StartRecoveryPause(destination);
            }
            return;
        }

        StartRecoveryPause(destination);
    }

    // Place l'unité en attente avant de retenter sa destination exacte
    private void StartRecoveryPause(Vector2Int destination)
    {
        orderedDestination = destination;
        SetState(MoveState.Blocked);
        WatchBlockedDestination();
        attackController?.SetManualOrder(false);
        if (animatorController != null && !(attackController != null && attackController.IsAttacking()))
            animatorController.PlayIdleAnimation();

        retryCoroutine = StartCoroutine(PauseBeforeRetry(destination));
    }

    // Attend un délai désynchronisé puis relance un path depuis la position actuelle
    private IEnumerator PauseBeforeRetry(Vector2Int destination)
    {
        yield return new WaitForSeconds(Random.Range(0.8f, 1.2f));

        if (moveState != MoveState.Blocked || !orderedDestination.HasValue || orderedDestination.Value != destination)
            yield break;

        consecutivePathFailures = 0;
        retryCoroutine = null;
        MoveTo(destination, false);
    }

    // Gère la situation où le chemin devient inaccessible
    // Lance un retry automatique local pour recalculer le chemin
    // Change l'état et démarre la coroutine de retry périodique
    private void HandlePathBlocked()
    {
        EntityRegistry.Instance?.ClearLeaving(unitGameObject);
        logicalPath.Clear();

        if (debugMovement)
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"MovementController {gameObject.name}: Chemin bloqué à {currentCell}", this);
        }

        if (animatorController != null && !(attackController != null && attackController.IsAttacking()))
        {
            animatorController.PlayIdleAnimation();
        }

        if (!orderedDestination.HasValue)
        {
            ClearCellSubscriptions();
            SetState(MoveState.Idle);
            return;
        }

        // Lance retry persistant — la collision en cours de Lerp est gérée directement dans InterpolationLoop
        SetState(MoveState.Blocked);
        WatchBlockedDestination();
        // Autorise l'auto-attaque pendant le retry — si un ennemi entre à portée, l'unité ripostera
        attackController?.SetManualOrder(false);
        retryCoroutine = StartCoroutine(RetryPathPeriodically(orderedDestination.Value));
    }

    // Appelé quand le mouvement se termine avec succès
    // Nettoie l'état et déclenche animations de fin
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
            interpolationCoroutine = null;
            StartRecoveryPause(destination);
            return;
        }

        orderedDestination = null;
        consecutivePathFailures = 0;
        SetState(MoveState.Idle);
        OnArrived.Invoke();

        if (debugMovement)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: Destination {currentCell} atteinte", this);
        }

        // Vérifie si une attaque est en cours
        bool isAttacking = attackController != null && attackController.IsAttacking();

        // Si ennemi à portée et pas déjà en train d'attaquer, déclenche l'attaque immédiatement
        if (!isAttacking && attackController != null)
        {
            GameObject nearestEnemy = attackController.FindNearestEnemyInRange();
            if (nearestEnemy != null)
            {
                attackController.InterruptChaseAndAttack(nearestEnemy);
                return;
            }
        }

        // Animation idle SEULEMENT si pas en train d'attaquer
        if (animatorController != null && !isAttacking)
        {
            animatorController.PlayIdleAnimation();
        }

        // Reset état pour prochain mouvement
        OnIdleStateChanged.Invoke(true);
        isFirstMoveOfSession = true;

        // Lève le blocage auto-attaque — l'unité est arrivée, elle peut de nouveau auto-attaquer
        if (attackController != null)
        {
            attackController.SetManualOrder(false);
        }

        // Reset le flag IdleReversed dans AnimatorController pour prochain mouvement
        if (IsRootborn() && animatorController != null && !isAttacking)
        {
            animatorController.ResetIdleReversedSession();
        }
    }

    // Arrête immédiatement le mouvement en cours
    // Interruption manuelle ou automatique (conflit, obstacle)
    // Nettoie l'état, arrête toutes les coroutines
    public void StopMovement()
    {
        CancelPendingPathRequest();
        ClearCellSubscriptions();
        repathRequested = false;
        notificationRepathIssuedThisStep = false;
        EntityRegistry.Instance?.ClearLeaving(unitGameObject);
        orderedDestination = null;
        pendingDestination = null;
        recoveryDestination = null;
        consecutivePathFailures = 0;

        // Arrête la coroutine d'interpolation
        if (interpolationCoroutine != null)
        {
            StopCoroutine(interpolationCoroutine);
            interpolationCoroutine = null;
        }

        // Arrête la coroutine de retry si active
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        // Vide la queue
        logicalPath.Clear();

        // Réaffirme la cellule logique validée avant le snap visuel
        EntityRegistry.Instance?.UpdateLogicalPosition(gameObject, currentCell);

        // Snap visuel à la cellule logique validée
        transform.position = GridManager.Instance.GridToWorld(currentCell);

        // Remet à zéro les variables d'état
        SetState(MoveState.Idle);
        isFirstMoveOfSession = true;

        // Arrête les animations et retourne à idle
        if (animatorController != null)
        {
            animatorController.StopAllAnimations();
        }

        if (debugMovement)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: Mouvement arrêté à {currentCell}", this);
        }
    }

    // Stoppe le mouvement en conservant la position visuelle actuelle (pas de snap au centre de case)
    // Utilisé pour freeze en place : mort, sacrifice — l'unité reste là où elle est physiquement
    public void FreezeInPlace()
    {
        CancelPendingPathRequest();
        ClearCellSubscriptions();
        repathRequested = false;
        notificationRepathIssuedThisStep = false;
        EntityRegistry.Instance?.ClearLeaving(unitGameObject);
        orderedDestination = null;
        pendingDestination = null;
        recoveryDestination = null;
        consecutivePathFailures = 0;

        // Arrête la coroutine d'interpolation
        if (interpolationCoroutine != null)
        {
            StopCoroutine(interpolationCoroutine);
            interpolationCoroutine = null;
        }

        // Arrête la coroutine de retry si active
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        // Vide la queue
        logicalPath.Clear();

        // Remet à zéro les variables d'état sans modifier la position visuelle ni le registre logique
        SetState(MoveState.Idle);
        isFirstMoveOfSession = true;

        // L'animator est géré par UnitFreezer avant cet appel — ne pas toucher ici
        if (debugMovement)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: Mouvement figé en place à {transform.position}", this);
        }
    }

    // Vide la queue de chemin sans téléporter l'unité
    // L'unité termine son interpolation vers la case actuelle puis s'arrête
    public void ClearPathQueue()
    {
        if (logicalPath != null)
        {
            logicalPath.Clear();
        }

        if (debugMovement)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: Queue de chemin vidée", this);
        }
    }

    // Retourne l'état actuel du contrôleur de mouvement
    // Interface pour debug et coordination avec autres systèmes
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

    // Force la sortie d'idle logique sans changer l'état de mouvement
    public void ForceExitIdle()
    {
        if (moveState == MoveState.Idle)
            OnIdleStateChanged.Invoke(false);
    }

    // Notifie que l'idle logique est restauré si l'unité est toujours idle
    public void NotifyIdleRestored()
    {
        if (moveState == MoveState.Idle)
            OnIdleStateChanged.Invoke(true);
    }

    // Retourne la position de grille LOGIQUE (source de vérité)
    // Utilisé par AttackController et ChaseController pour calculs de distance/portée
    public Vector2Int GetCurrentGridPosition()
    {
        return currentCell;
    }

    // Vérifie si l'unité est prête à recevoir un nouvel ordre de mouvement
    // Retourne false si une animation pré-mouvement (IdleReversed) est en cours
    public bool IsReadyForMovement()
    {
        // Si c'est un Rootborn et que l'IdleReversed est en cours, pas prêt
        if (IsRootborn() && animatorController != null && animatorController.IsPlayingSpecialAnimation())
        {
            return false;
        }

        return true;
    }

    // Joue l'animation IdleReversed pour les Rootborn avant le mouvement
    // Attend que l'animation se termine complètement avant de continuer
    private IEnumerator PlayRootbornPreMovementAnimation()
    {
        if (animatorController != null)
        {
            bool animationComplete = false;

            // Lance l'IdleReversed avec callback
            animatorController.PlayRootbornIdleReversed(() =>
            {
                animationComplete = true;
            });

            // Attend que le callback soit appelé
            while (!animationComplete)
            {
                yield return null;
            }

            if (debugMovement)
            {
                GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: IdleReversed terminée", this);
            }
        }
    }

    // Lance un retry automatique vers une destination
    // Utilisé quand PathFinding échoue initialement
    private void StartRetryToDestination(Vector2Int destination)
    {
        orderedDestination = destination;
        SetState(MoveState.Blocked);
        WatchBlockedDestination();

        if (debugMovement)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: Retry vers {destination}", this);
        }

        // Autorise l'auto-attaque pendant le retry — si un ennemi entre à portée, l'unité ripostera
        attackController?.SetManualOrder(false);
        retryCoroutine = StartCoroutine(RetryPathPeriodically(destination));
    }

    // Coroutine pour réessayer de calculer un nouveau chemin périodiquement
    // Retry automatique local sans dépendance externe
    // Continue jusqu'à succès ou arrêt manuel
    private IEnumerator RetryPathPeriodically(Vector2Int destination)
    {
        while (moveState == MoveState.Blocked)
        {
            yield return new WaitForSeconds(retryInterval);

            if (moveState != MoveState.Blocked)
                yield break;

            // Conserve la destination exacte tant qu'une autre unité l'occupe
            GameObject destinationOccupant = EntityRegistry.Instance?.GetUnitAtLogicalPosition(destination);
            bool destinationOccupiedByUnit = destinationOccupant != null && destinationOccupant != unitGameObject;

            List<Vector2Int> newPath = null;
            bool retryPathCompleted = false;
            if (!QueuePathRequest(currentCell, destination, path =>
                {
                    newPath = path;
                    retryPathCompleted = true;
                }))
            {
                yield break;
            }

            long retryRequestId = activePathRequestId;
            while (!retryPathCompleted)
            {
                if (activePathRequestId != retryRequestId)
                    yield break;

                yield return null;
            }

            bool pathFailed = newPath == null || newPath.Count == 0 || IsPathAbsurd(newPath, destination);

            // Une destination campée reste la destination exacte, le détour n'est qu'une récupération locale temporaire
            if (destinationOccupiedByUnit)
            {
                if (pathFailed)
                    RegisterPathFailure(destination);

                if (consecutivePathFailures >= 2)
                {
                    retryCoroutine = null;
                    StartFailureRecovery(destination);
                    yield break;
                }

                SetState(MoveState.Blocked);
                continue;
            }

            if (pathFailed)
            {
                RegisterPathFailure(destination);
                if (consecutivePathFailures >= 2)
                {
                    retryCoroutine = null;
                    StartFailureRecovery(destination);
                    yield break;
                }

                SetState(MoveState.Blocked);
                continue;
            }

            retryCoroutine = null;
            if (debugMovement)
                GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: Retry réussi vers {destination}", this);
            MoveTo(destination, false);
            yield break;
        }
    }

    // Vérifie si le chemin vers une destination est absurde (détour excessif)
    // Retourne true si longueurChemin > distanceChebyshev * detourRatioThreshold
    private bool IsPathAbsurd(List<Vector2Int> path, Vector2Int destination)
    {
        if (path == null || path.Count == 0) return false;

        int chebyshev = Mathf.Max(
            Mathf.Abs(currentCell.x - destination.x),
            Mathf.Abs(currentCell.y - destination.y));

        if (chebyshev == 0) return false;

        return path.Count > chebyshev * detourRatioThreshold;
    }

    // Cherche une case adjacente libre depuis la position actuelle, plus proche de orderedDestination
    private Vector2Int? FindDetourCell()
    {
        if (!orderedDestination.HasValue) return null;

        Vector2Int target = orderedDestination.Value;
        Vector2Int[] offsets = {
            new Vector2Int(-1, 0), new Vector2Int(1, 0),
            new Vector2Int(0, 1),  new Vector2Int(0, -1),
            new Vector2Int(-1, 1), new Vector2Int(1, 1),
            new Vector2Int(-1,-1), new Vector2Int(1,-1)
        };

        Vector2Int? bestCell = null;
        int bestDistance = int.MaxValue;

        foreach (var off in offsets)
        {
            Vector2Int cell = currentCell + off;

            if (!GridManager.Instance.IsWalkableFor(cell, unitGameObject)) continue;
            GameObject occupant = EntityRegistry.Instance?.GetUnitAtLogicalPosition(cell);
            if (occupant != null && occupant != unitGameObject) continue;

            int dist = Mathf.Max(Mathf.Abs(cell.x - target.x), Mathf.Abs(cell.y - target.y));
            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    // Callback appelé par AnimatorController quand IdleReversed se termine
    // Exécute l'ordre de mouvement mis en attente
    public void OnIdleReversedComplete()
    {
        if (pendingDestination.HasValue)
        {
            Vector2Int destination = pendingDestination.Value;
            pendingDestination = null;

            if (debugMovement)
            {
                GameLogger.Log(GameLogger.Category.Movement, $"MovementController {gameObject.name}: Exécution ordre en attente vers {destination}", this);
            }

            // Exécute l'ordre différé
            MoveTo(destination, false);
        }
    }

    // Vérifie si cette unité est un Rootborn via le ScriptableObject (UnitStatsData.isRootborn)
    // Retourne false si UnitStatsComponent absent plutôt que de deviner via le nom de l'objet
    private bool IsRootborn()
    {
        if (_statsComponent != null) return _statsComponent.IsRootborn();
        return false;
    }

    // Méthode de debug pour afficher l'état du contrôleur
    [ContextMenu("Debug Movement Controller")]
    private void DebugControllerState()
    {
        GameLogger.Log(GameLogger.Category.Movement, $"=== MovementController Debug - {gameObject.name} ===", this);
        GameLogger.Log(GameLogger.Category.Movement, $"État interne: {moveState} | API: {GetCurrentState()}", this);
        GameLogger.Log(GameLogger.Category.Movement, $"En mouvement: {IsMoving}", this);
        GameLogger.Log(GameLogger.Category.Movement, $"Position LOGIQUE: {currentCell}", this);
        GameLogger.Log(GameLogger.Category.Movement, $"Position VISUELLE: {transform.position} → {GridManager.Instance.WorldToGrid(transform.position)}", this);
        GameLogger.Log(GameLogger.Category.Movement, $"Queue: {logicalPath.Count} cases restantes", this);
        GameLogger.Log(GameLogger.Category.Movement, $"Requête path active: {activePathRequestId != 0}", this);
    }

    // Dessine le chemin actuel dans la vue Scene pour debug visuel
    private void OnDrawGizmos()
    {
        if (logicalPath == null || logicalPath.Count == 0 || GridManager.Instance == null)
            return;

        // Dessine position logique
        Gizmos.color = Color.yellow;
        Vector3 logicalPos = GridManager.Instance.GridToWorld(currentCell);
        Gizmos.DrawWireCube(logicalPos, Vector3.one * 1.2f);

        // Dessine la queue restante
        Gizmos.color = Color.blue;
        Vector2Int[] pathArray = logicalPath.ToArray();
        Vector3 previousPos = logicalPos;

        foreach (var cell in pathArray)
        {
            Vector3 cellPos = GridManager.Instance.GridToWorld(cell);
            Gizmos.DrawLine(previousPos, cellPos);
            previousPos = cellPos;
        }

        // Dessine la case cible actuelle
        if (moveState == MoveState.SteppingToAnchor || moveState == MoveState.SteppingToCenter || moveState == MoveState.Receding)
        {
            Gizmos.color = Color.red;
            Vector3 targetPos = GridManager.Instance.GridToWorld(targetCell);
            Gizmos.DrawWireCube(targetPos, Vector3.one * 0.8f);
        }

        // Dessine la destination finale
        if (pathArray.Length > 0)
        {
            Gizmos.color = Color.green;
            Vector3 finalPos = GridManager.Instance.GridToWorld(pathArray[pathArray.Length - 1]);
            Gizmos.DrawWireSphere(finalPos, 0.3f);
        }
    }
}

