using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Composant responsable de l'exécution du mouvement individuel d'une unité
// Architecture LOGIQUE/VISUEL séparée pour stabilité lors spam d'ordres
// LOGIQUE = position grille (source de vérité) | VISUEL = transform.position (cosmétique)
public class MovementController : MonoBehaviour
{
    [Header("Movement Settings")]
    private float movementSpeed = 1f;          // Vitesse déplacement (unités Unity/seconde)
    private float centerTolerance = 0.1f;      // Distance pour considérer case "atteinte"
    [SerializeField] private float retryInterval = 0.5f;     // Intervalle entre tentatives de recalcul quand bloquée
    [SerializeField] private float detourRatioThreshold = 3f; // Ratio chemin/Chebyshev au-delà duquel le chemin est considéré absurde

    // Modifie la vitesse de déplacement de l'unité
    // Appelé par UnitStats pour synchroniser les stats
    public void SetMovementSpeed(float newSpeed)
    {
        movementSpeed = Mathf.Max(0.1f, newSpeed);

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Vitesse mise à jour -> {movementSpeed}");
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

    private enum StepResult { Completed, CollisionReturned }
    private StepResult lastStepResult;
    private enum StepPhase { ToAnchor, ToCenter }
    private StepPhase currentPhase;

    // ========== LOGIQUE (Source de vérité) ==========
    private Vector2Int logicalPosition;           // Case actuellement occupée (STABLE)
    private Queue<Vector2Int> logicalPath;        // Chemin logique à suivre
    private MovementState currentState = MovementState.Idle;

    // ========== VISUEL (Présentation uniquement) ==========
    private bool isInterpolating = false;         // Interpolation en cours
    private Vector2Int currentCell;               // Cellule logique validée (= dernier centre atteint)
    private Vector2Int targetCell;                // Cellule visée par step en cours
    private Vector3 anchorWorldPos;               // Ancrage géométrique cible/franchi
    private bool hasCrossedAnchor;                // false = phase ToAnchor, true = phase ToCenter
    private bool isReceding = false;              // Retour en cours vers currentCell après collision/rebrousse
    private bool recedeToCurrentRequested = false; // Rebrousse demandé après franchissement d'ancrage
    private const float ANCHOR_EPSILON = 0.05f;
    private const float CENTER_EPSILON = 0.05f;

    // ========== ÉTAT ==========
    private Vector2Int? pendingDestination = null; // Ordre en attente pendant IdleReversed
    private Vector2Int? finalDestination = null;  // Destination finale persistante — l'unité ne lâche jamais son ordre tant que ce champ est non-null
    private int consecutiveCollisions = 0;     // Compteur collisions sur la même case
    private Vector2Int lastCollisionCell;      // Dernière case ayant causé une collision

    // Déclenché quand l'unité commence à se déplacer (premier step du chemin)
    public UnityEngine.Events.UnityEvent OnMovementStarted = new();

    // Déclenché quand l'unité atteint sa destination finale avec succès
    public UnityEngine.Events.UnityEvent OnArrived = new();

    // Vrai si l'unité est actuellement en mouvement
    public bool IsMoving => currentState == MovementState.Moving;

    // Références cached pour performance
    private GameObject unitGameObject;
    private AnimatorController animatorController;
    private UnitStatsComponent _statsComponent; // Cache pour IsRootborn() via SO
    private AttackController attackController;

    // Variables de contrôle temporel
    private Coroutine interpolationCoroutine;
    private Coroutine retryCoroutine;
    private bool isFirstMoveOfSession = true; // Reset quand mouvement s'arrête

    private void Start()
    {
        unitGameObject = gameObject;
        currentState = MovementState.Idle;
        logicalPath = new Queue<Vector2Int>();

        // Initialise position logique depuis position monde (avec vérification)
        if (GridManager.Instance != null)
        {
            logicalPosition = GridManager.Instance.WorldToGrid(transform.position);
            EntityRegistry.Instance?.UpdateLogicalPosition(gameObject, logicalPosition);
            currentCell = logicalPosition;
        }
        else
        {
            Debug.LogWarning($"MovementController {gameObject.name}: GridManager.Instance est null dans Awake(), position logique sera initialisée plus tard");
        }

        // Cache la référence AnimatorController
        animatorController = GetComponent<AnimatorController>();
        if (animatorController == null)
        {
            Debug.LogWarning($"MovementController sur {gameObject.name}: AnimatorController non trouvé!");
        }

        _statsComponent = GetComponent<UnitStatsComponent>();
        attackController = GetComponent<AttackController>();
    }

    // Interface publique principale pour demander un déplacement vers une destination
    // Calcule l'A* sur cellules et adapte le step en cours selon la phase ancrage/centre.
    public bool MoveTo(Vector2Int targetPosition)
    {
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        if (logicalPosition == Vector2Int.zero && GridManager.Instance != null)
        {
            logicalPosition = GridManager.Instance.WorldToGrid(transform.position);
            currentCell = logicalPosition;
        }

        if (!GridManager.Instance.IsInBounds(targetPosition))
        {
            Debug.LogWarning($"MovementController {gameObject.name}: Destination {targetPosition} hors limites");
            return false;
        }

        if (!IsReadyForMovement())
        {
            pendingDestination = targetPosition;
            if (debugMovement) Debug.Log($"MovementController {gameObject.name}: ordre en attente IdleReversed");
            return true;
        }

        if (debugMovement)
            Debug.Log($"🔍 MoveTo {gameObject.name}: currentCell={currentCell}, target={targetPosition}, phase={(isInterpolating ? currentPhase.ToString() : "Idle")}, crossed={hasCrossedAnchor}");

        pendingDestination = null;

        // ===== EN COURS DE STEP =====
        if (isInterpolating)
        {
            if (isReceding)
            {
                pendingDestination = targetPosition;
                finalDestination = targetPosition;
                if (debugMovement) Debug.Log($"[FinalDest] {gameObject.name}: finalDestination = {finalDestination} | caller = MoveTo/isReceding");
                if (debugMovement) Debug.Log($"MovementController {gameObject.name}: ordre pendant recede → {targetPosition}");
                return true;
            }

            // Phase ToAnchor : bifurcation libre dans currentCell.
            if (!hasCrossedAnchor)
            {
                List<Vector2Int> path = PathFinding.FindPath(currentCell, targetPosition, unitGameObject);
                if (path == null || path.Count == 0)
                {
                    if (debugMovement) Debug.LogWarning($"MovementController {gameObject.name}: pas de chemin, ordre ignoré");
                    return false;
                }

                logicalPath.Clear();
                foreach (var c in path) logicalPath.Enqueue(c);
                finalDestination = targetPosition;
                if (debugMovement) Debug.Log($"[FinalDest] {gameObject.name}: finalDestination = {finalDestination} | caller = MoveTo/ToAnchor");

                Vector2Int newFirstCell = logicalPath.Peek();
                logicalPath.Dequeue();
                Vector2Int dir = GetDirection(currentCell, newFirstCell);
                targetCell = newFirstCell;
                anchorWorldPos = ComputeAnchor(currentCell, dir);

                if (animatorController != null)
                    animatorController.PlayWalkAnimation(currentCell, newFirstCell);

                if (debugMovement) Debug.Log($"MovementController {gameObject.name}: bifurcation ToAnchor → {newFirstCell}");
                return true;
            }

            // Phase ToCenter : ancrage franchi. Le centre cible est obligatoire avant nouvel ordre.
            if (targetPosition == currentCell)
            {
                logicalPath.Clear();
                pendingDestination = null;
                finalDestination = currentCell;
                if (debugMovement) Debug.Log($"[FinalDest] {gameObject.name}: finalDestination = {finalDestination} | caller = MoveTo/recedeToCurrent");
                recedeToCurrentRequested = true;
                if (debugMovement) Debug.Log($"MovementController {gameObject.name}: rebrousse vers {currentCell}");
                return true;
            }

            pendingDestination = targetPosition;
            finalDestination = targetPosition;
            if (debugMovement) Debug.Log($"[FinalDest] {gameObject.name}: finalDestination = {finalDestination} | caller = MoveTo/queued");
            if (debugMovement) Debug.Log($"MovementController {gameObject.name}: ordre en file → {targetPosition}");
            return true;
        }

        // ===== HORS STEP =====
        List<Vector2Int> initialPath = PathFinding.FindPath(currentCell, targetPosition, unitGameObject);
        if (initialPath != null && initialPath.Count > 0 && IsPathAbsurd(initialPath, targetPosition))
        {
            if (debugMovement)
                Debug.LogWarning($"MovementController {gameObject.name}: chemin absurde vers {targetPosition} ({initialPath.Count} steps / Chebyshev {Mathf.Max(Mathf.Abs(currentCell.x - targetPosition.x), Mathf.Abs(currentCell.y - targetPosition.y))}), ordre ignoré");
            return false;
        }

        if (initialPath == null || initialPath.Count == 0)
        {
            finalDestination = targetPosition;
            if (debugMovement) Debug.Log($"[FinalDest] {gameObject.name}: finalDestination = {finalDestination} | caller = MoveTo/horsStep/noPath");
            Debug.LogWarning($"MovementController {gameObject.name}: pas de chemin vers {targetPosition} — retry");
            StartRetryToDestination(targetPosition);
            return false;
        }

        logicalPath.Clear();
        finalDestination = targetPosition;
        if (debugMovement) Debug.Log($"[FinalDest] {gameObject.name}: finalDestination = {finalDestination} | caller = MoveTo/horsStep");
        foreach (var c in initialPath) logicalPath.Enqueue(c);

        if (debugMovement)
            Debug.Log($"MovementController {gameObject.name}: nouveau chemin {initialPath.Count} cases depuis {currentCell} vers {targetPosition}");

        OnMovementStarted.Invoke();
        currentState = MovementState.Moving;
        interpolationCoroutine = StartCoroutine(InterpolationLoop());
        return true;
    }

    // Boucle principale d'interpolation en steps ancrage -> centre.
    // Consomme le path cellule par cellule et ne commit la logique qu'au centre cible.
    private IEnumerator InterpolationLoop()
    {
        isInterpolating = true;
        if (debugMovement) Debug.Log($"MovementController {gameObject.name}: début boucle interpolation");

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
                if (debugMovement) Debug.LogWarning($"MovementController {gameObject.name}: case {nextCell} inaccessible");
                HandlePathBlocked();
                yield break;
            }

            targetCell = nextCell;
            Vector2Int direction = GetDirection(currentCell, targetCell);
            anchorWorldPos = ComputeAnchor(currentCell, direction);
            currentPhase = StepPhase.ToAnchor;
            hasCrossedAnchor = false;

            if (animatorController != null)
                animatorController.PlayWalkAnimation(currentCell, targetCell);

            if (debugMovement)
                Debug.Log($"MovementController {gameObject.name}: step {currentCell} → {targetCell} via ancrage");

            yield return StartCoroutine(ExecuteStep());

            if (lastStepResult == StepResult.CollisionReturned)
            {
                if (!finalDestination.HasValue) yield break;
                Vector2Int dest = finalDestination.Value;
                List<Vector2Int> retryPath = PathFinding.FindPath(currentCell, dest, unitGameObject);
                if (retryPath != null && retryPath.Count > 0)
                {
                    // Reset compteur car A* propose un chemin (peut-être contournement implicite)
                    if (targetCell == lastCollisionCell)
                        consecutiveCollisions++;
                    else
                    {
                        consecutiveCollisions = 1;
                        lastCollisionCell = targetCell;
                    }

                    // Si A* renvoie le MÊME premier step que la case bloquée et 2× collisions → détour
                    if (consecutiveCollisions >= 2 && retryPath[0] == lastCollisionCell)
                    {
                        Vector2Int? detour = FindDetourCell(targetCell);
                        if (detour.HasValue)
                        {
                            if (debugMovement)
                                Debug.Log($"MovementController {gameObject.name}: boucle sur {targetCell} ({consecutiveCollisions}×), détour vers {detour.Value}");
                            consecutiveCollisions = 0;
                            List<Vector2Int> detourPath = PathFinding.FindPath(detour.Value, dest, unitGameObject);
                            logicalPath.Clear();
                            logicalPath.Enqueue(detour.Value);
                            if (detourPath != null)
                                foreach (var c in detourPath) logicalPath.Enqueue(c);
                            continue;
                        }
                    }

                    if (debugMovement) Debug.Log($"MovementController {gameObject.name}: recalcul après collision réussi vers {dest}");
                    logicalPath.Clear();
                    foreach (var c in retryPath) logicalPath.Enqueue(c);
                    continue;
                }

                if (attackController != null)
                {
                    attackController.SetManualOrder(false);
                    GameObject nearestEnemy = attackController.FindNearestEnemyInRange();
                    if (nearestEnemy != null)
                    {
                        if (debugMovement) Debug.Log($"MovementController {gameObject.name}: bloquée + ennemi à portée → auto-attaque");
                        finalDestination = null;
                        if (debugMovement) Debug.Log($"[FinalDest] {gameObject.name}: finalDestination = null | caller = HandlePathBlocked/autoAttack");
                        attackController.InterruptChaseAndAttack(nearestEnemy);
                        isInterpolating = false;
                        currentState = MovementState.Idle;
                        yield break;
                    }
                }

                isInterpolating = false;
                currentState = MovementState.Blocked;
                attackController?.SetManualOrder(false);
                if (animatorController != null && !(attackController != null && attackController.IsAttacking()))
                {
                    animatorController.PlayIdleAnimation();
                }
                retryCoroutine = StartCoroutine(RetryPathPeriodically(dest));
                yield break;
            }

            // StepResult.Completed → currentCell mis à jour dans ExecuteStep.
            consecutiveCollisions = 0;

            // Consomme pendingDestination (rebrousse, file, ordre pendant recede, etc.).
            if (pendingDestination.HasValue)
            {
                Vector2Int dest = pendingDestination.Value;
                pendingDestination = null;
                List<Vector2Int> newPath = PathFinding.FindPath(currentCell, dest, unitGameObject);
                if (newPath != null && newPath.Count > 0)
                {
                    logicalPath.Clear();
                    foreach (var c in newPath) logicalPath.Enqueue(c);
                    finalDestination = dest;
                    if (debugMovement) Debug.Log($"[FinalDest] {gameObject.name}: finalDestination = {finalDestination} | caller = MoveTo/pendingConsumed");
                    if (debugMovement) Debug.Log($"MovementController {gameObject.name}: pending consommé → {dest}");
                }
            }

            if (logicalPath.Count > 0 && animatorController != null)
                animatorController.PlayWalkAnimation(currentCell, logicalPath.Peek());
        }

        OnMovementCompleted();
        isInterpolating = false;
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
        while (!hasCrossedAnchor)
        {
            if (currentState != MovementState.Moving) yield break;

            Vector3 toAnchor = anchorWorldPos;
            Vector3 dir = toAnchor - transform.position;
            float dist = dir.magnitude;

            if (dist < ANCHOR_EPSILON)
            {
                transform.position = toAnchor;
                hasCrossedAnchor = true;
                currentPhase = StepPhase.ToCenter;
                break;
            }

            float step = movementSpeed * Time.deltaTime;
            if (step >= dist)
            {
                transform.position = toAnchor;
                hasCrossedAnchor = true;
                currentPhase = StepPhase.ToCenter;
                break;
            }

            transform.position += dir.normalized * step;
            yield return null;
        }

        // Phase ToCenter : une fois l'ancrage franchi, le centre cible est obligatoire.
        Vector3 centerWorld = GridManager.Instance.GridToWorld(targetCell);
        while (true)
        {
            if (currentState != MovementState.Moving) yield break;

            if (recedeToCurrentRequested)
            {
                recedeToCurrentRequested = false;
                yield return StartCoroutine(RecedeToCurrentCenter());
                lastStepResult = StepResult.Completed;
                yield break;
            }

            GameObject occupant = EntityRegistry.Instance?.GetUnitAtLogicalPosition(targetCell);
            if (occupant != null && occupant != unitGameObject)
            {
                RegisterCollision(targetCell);
                if (debugMovement) Debug.Log($"MovementController {gameObject.name}: collision targetCell {targetCell} occupée par {occupant.name}, recede");
                yield return StartCoroutine(RecedeToCurrentCenter());
                lastStepResult = StepResult.CollisionReturned;
                yield break;
            }

            Vector3 toCenter = centerWorld - transform.position;
            float dist = toCenter.magnitude;

            if (dist < CENTER_EPSILON)
            {
                transform.position = centerWorld;
                if (TryCommitTargetCell()) lastStepResult = StepResult.Completed;
                else
                {
                    yield return StartCoroutine(RecedeToCurrentCenter());
                    lastStepResult = StepResult.CollisionReturned;
                }
                yield break;
            }

            float step = movementSpeed * Time.deltaTime;
            if (step >= dist)
            {
                transform.position = centerWorld;
                if (TryCommitTargetCell()) lastStepResult = StepResult.Completed;
                else
                {
                    yield return StartCoroutine(RecedeToCurrentCenter());
                    lastStepResult = StepResult.CollisionReturned;
                }
                yield break;
            }

            transform.position += toCenter.normalized * step;
            yield return null;
        }
    }

    // Tente le commit atomique de targetCell. Met à jour currentCell et logicalPosition si succès.
    private bool TryCommitTargetCell()
    {
        if (EntityRegistry.Instance == null) return false;
        if (EntityRegistry.Instance.TryClaimPosition(unitGameObject, targetCell))
        {
            currentCell = targetCell;
            logicalPosition = targetCell;
            return true;
        }
        return false;
    }

    // Retour visuel position actuelle -> ancrage franchi -> centre currentCell, sans commit logique.
    private IEnumerator RecedeToCurrentCenter()
    {
        isReceding = true;
        Vector3 centerWorld = GridManager.Instance.GridToWorld(currentCell);

        if (animatorController != null)
            animatorController.PlayWalkAnimation(targetCell, currentCell);

        if (hasCrossedAnchor)
        {
            Vector3 anchor = anchorWorldPos;
            while (true)
            {
                if (currentState != MovementState.Moving) yield break;
                Vector3 dir = anchor - transform.position;
                float dist = dir.magnitude;
                if (dist < ANCHOR_EPSILON) { transform.position = anchor; break; }
                float step = movementSpeed * Time.deltaTime;
                if (step >= dist) { transform.position = anchor; break; }
                transform.position += dir.normalized * step;
                yield return null;
            }
            hasCrossedAnchor = false;
            currentPhase = StepPhase.ToAnchor;
        }

        while (true)
        {
            if (currentState != MovementState.Moving) yield break;
            Vector3 dir = centerWorld - transform.position;
            float dist = dir.magnitude;
            if (dist < CENTER_EPSILON) { transform.position = centerWorld; break; }
            float step = movementSpeed * Time.deltaTime;
            if (step >= dist) { transform.position = centerWorld; break; }
            transform.position += dir.normalized * step;
            yield return null;
        }
        isReceding = false;
    }

    // Enregistre une collision sur targetCell pour conserver le comportement de retry progressif.
    private void RegisterCollision(Vector2Int blockedCell)
    {
        if (blockedCell == lastCollisionCell)
        {
            consecutiveCollisions++;
        }
        else
        {
            consecutiveCollisions = 1;
            lastCollisionCell = blockedCell;
        }
    }

    // Gère la situation où le chemin devient inaccessible
    // Lance un retry automatique local pour recalculer le chemin
    // Change l'état et démarre la coroutine de retry périodique
    private void HandlePathBlocked()
    {
        currentState = MovementState.Idle;
        isInterpolating = false;
        logicalPath.Clear();

        if (debugMovement)
        {
            Debug.LogWarning($"MovementController {gameObject.name}: Chemin bloqué à {currentCell}");
        }

        if (animatorController != null && !(attackController != null && attackController.IsAttacking()))
        {
            animatorController.PlayIdleAnimation();
        }

        if (!finalDestination.HasValue)
        {
            return;
        }

        // Lance retry persistant — la collision en cours de Lerp est gérée directement dans InterpolationLoop
        currentState = MovementState.Blocked;
        // Autorise l'auto-attaque pendant le retry — si un ennemi entre à portée, l'unité ripostera
        attackController?.SetManualOrder(false);
        retryCoroutine = StartCoroutine(RetryPathPeriodically(finalDestination.Value));
    }

    // Appelé quand le mouvement se termine avec succès
    // Nettoie l'état et déclenche animations de fin
    private void OnMovementCompleted()
    {
        finalDestination = null;
        if (debugMovement) Debug.Log($"[FinalDest] {gameObject.name}: finalDestination = null | caller = OnMovementCompleted");
        consecutiveCollisions = 0;
        currentState = MovementState.Arrived;
        OnArrived.Invoke();

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Destination {currentCell} atteinte");
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
        currentState = MovementState.Idle;
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
        finalDestination = null;
        if (debugMovement) Debug.Log($"[FinalDest] {gameObject.name}: finalDestination = null | caller = StopMovement");
        pendingDestination = null;
        recedeToCurrentRequested = false;
        isReceding = false;
        consecutiveCollisions = 0;

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

        // Si un step est en cours, snap à currentCell (cellule logique validée).
        if (isInterpolating)
        {
            logicalPosition = currentCell;
            EntityRegistry.Instance?.UpdateLogicalPosition(gameObject, logicalPosition);
        }

        // Snap visuel à la cellule logique validée
        transform.position = GridManager.Instance.GridToWorld(currentCell);

        // Remet à zéro les variables d'état
        isInterpolating = false;
        currentState = MovementState.Idle;
        isFirstMoveOfSession = true;

        // Arrête les animations et retourne à idle
        if (animatorController != null)
        {
            animatorController.StopAllAnimations();
        }

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Mouvement arrêté à {currentCell}");
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
            Debug.Log($"MovementController {gameObject.name}: Queue de chemin vidée");
        }
    }

    // Retourne l'état actuel du contrôleur de mouvement
    // Interface pour debug et coordination avec autres systèmes
    public MovementState GetCurrentState()
    {
        return currentState;
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
                Debug.Log($"MovementController {gameObject.name}: IdleReversed terminée");
            }
        }
    }

    // Lance un retry automatique vers une destination
    // Utilisé quand PathFinding échoue initialement
    private void StartRetryToDestination(Vector2Int destination)
    {
        currentState = MovementState.Blocked;

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Retry vers {destination}");
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
        int emptyPathRetries = 0;
        const int emptyPathThreshold = 2;

        while (true)
        {
            yield return new WaitForSeconds(retryInterval);

            // Cherche une case accessible autour de la destination en rayon croissant (1→2→3)
            Vector2Int? reachableTarget = FindReachableNearDestination(destination);
            if (!reachableTarget.HasValue)
            {
                if (debugMovement)
                    Debug.Log($"MovementController {gameObject.name}: Aucune case accessible autour de {destination} — retry");
                continue;
            }

            List<Vector2Int> newPath = PathFinding.FindPath(currentCell, reachableTarget.Value, unitGameObject);
            if (newPath != null && newPath.Count > 0 && IsPathAbsurd(newPath, destination))
            {
                if (debugMovement)
                    Debug.LogWarning($"MovementController {gameObject.name}: retry — chemin absurde vers {destination}, on attend");
                continue;
            }

            if (newPath != null && newPath.Count == 0)
            {
                emptyPathRetries++;
                if (emptyPathRetries >= emptyPathThreshold)
                {
                    Vector2Int? detour = FindDetourCell(currentCell);
                    if (detour.HasValue)
                    {
                        if (debugMovement)
                            Debug.Log($"MovementController {gameObject.name}: retry bloqué ({emptyPathRetries}× path vide), détour vers {detour.Value}");
                        emptyPathRetries = 0;
                        retryCoroutine = null;
                        MoveTo(detour.Value);
                        yield break;
                    }
                }
                continue;
            }

            if (newPath != null && newPath.Count > 0)
                emptyPathRetries = 0;

            if (newPath != null && newPath.Count > 0)
            {
                retryCoroutine = null;
                Vector2Int target = pendingDestination ?? reachableTarget.Value;
                if (debugMovement)
                    Debug.Log($"MovementController {gameObject.name}: Retry réussi vers {target}");
                MoveTo(target);
                yield break;
            }
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

    // Vérifie si le transform.position est au centre de la case cible
    // Utilise une tolérance configurable pour compenser imprécisions float
    private bool IsAtCellCenter(Vector2Int cell)
    {
        Vector3 cellWorldPosition = GridManager.Instance.GridToWorld(cell);
        float distance = Vector3.Distance(transform.position, cellWorldPosition);
        return distance < centerTolerance;
    }

    // Cherche une case walkable et accessible autour de destination en rayon croissant
    // Retourne la case la plus proche de destination avec un chemin valide, null si rien trouvé
    private Vector2Int? FindReachableNearDestination(Vector2Int destination)
    {
        // Rayon 0 : destination elle-même d'abord
        if (GridManager.Instance.IsWalkableFor(destination, unitGameObject) &&
            EntityRegistry.Instance?.GetUnitAtLogicalPosition(destination) == null)
            return destination;

        for (int radius = 1; radius <= 3; radius++)
        {
            Vector2Int? best = null;
            int bestDist = int.MaxValue;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue; // bord du rayon uniquement

                    Vector2Int cell = destination + new Vector2Int(dx, dy);
                    if (!GridManager.Instance.IsInBounds(cell)) continue;
                    if (!GridManager.Instance.IsWalkableFor(cell, unitGameObject)) continue;

                    GameObject occupant = EntityRegistry.Instance?.GetUnitAtLogicalPosition(cell);
                    if (occupant != null && occupant != unitGameObject) continue;

                    int dist = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = cell;
                    }
                }
            }

            if (best.HasValue) return best;
        }

        return null;
    }

    // Cherche une case adjacente libre pour détour, plus proche de finalDestination.
    // Exclut la case bloquée et currentCell (on ne veut pas y revenir).
    private Vector2Int? FindDetourCell(Vector2Int blockedCell)
    {
        if (!finalDestination.HasValue) return null;

        Vector2Int target = finalDestination.Value;
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

            if (cell == blockedCell) continue;
            if (cell == currentCell) continue;

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

    // Lerp animé depuis position courante vers la case détour, met à jour logicalPosition à la fin.
    private IEnumerator LerpToDetourCell(Vector2Int detourCell)
    {
        Vector3 currentPosition = transform.position;
        Vector3 detourPosition = GridManager.Instance.GridToWorld(detourCell);

        Vector2Int currentGridPos = GridManager.Instance.WorldToGrid(currentPosition);
        if (animatorController != null)
        {
            animatorController.PlayWalkAnimation(currentGridPos, detourCell);
        }

        float distanceToDetour = Vector3.Distance(currentPosition, detourPosition);
        float detourTime = distanceToDetour / movementSpeed;
        float elapsed = 0f;

        while (elapsed < detourTime)
        {
            if (currentState != MovementState.Moving) yield break;
            transform.position = Vector3.Lerp(currentPosition, detourPosition, elapsed / detourTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = detourPosition;

        currentCell = detourCell;
        logicalPosition = detourCell;
        EntityRegistry.Instance?.TryClaimPosition(unitGameObject, detourCell);

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Détour vers {detourCell} terminé");
        }
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
                Debug.Log($"MovementController {gameObject.name}: Exécution ordre en attente vers {destination}");
            }

            // Exécute l'ordre différé
            MoveTo(destination);
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
        Debug.Log($"=== MovementController Debug - {gameObject.name} ===");
        Debug.Log($"État: {currentState}");
        Debug.Log($"En mouvement: {IsMoving}");
        Debug.Log($"Position LOGIQUE: {currentCell}");
        Debug.Log($"Position VISUELLE: {transform.position} → {GridManager.Instance.WorldToGrid(transform.position)}");
        Debug.Log($"Queue: {logicalPath.Count} cases restantes");
        Debug.Log($"Interpolation active: {isInterpolating}");
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
        if (isInterpolating)
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

