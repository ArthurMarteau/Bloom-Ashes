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
    [SerializeField] private float contestThreshold = 0.1f;  // Distance au centre de la case cible pour qu'une autre unité soit considérée "installée" (collision tardive)
    [SerializeField] private float retryInterval = 0.5f;     // Intervalle entre tentatives de recalcul quand bloquée

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

    private enum StepResult { Completed, CollisionReturned, Reversed }
    private StepResult lastStepResult;

    // ========== LOGIQUE (Source de vérité) ==========
    private Vector2Int logicalPosition;           // Case actuellement occupée (STABLE)
    private Queue<Vector2Int> logicalPath;        // Chemin logique à suivre
    private MovementState currentState = MovementState.Idle;

    // ========== VISUEL (Présentation uniquement) ==========
    private Vector2Int visualTargetCell;          // Case cible du Lerp en cours
    private bool isInterpolating = false;         // Interpolation en cours
    private Vector2Int previousLogicalPosition;   // Case quittée au dernier step (pour retour en cas d'interruption)
    private bool returnToOriginRequested = false; // Inversion de Lerp demandée vers previousLogicalPosition

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
    // Calcul TOUJOURS depuis logicalPosition (stable) pour PathFinding fiable
    // Remplace la queue sans arrêt brutal si déjà en mouvement
    public bool MoveTo(Vector2Int targetPosition)
    {
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        // Initialise logicalPosition si pas encore fait (fallback si Awake a échoué)
        if (logicalPosition == Vector2Int.zero && GridManager.Instance != null)
        {
            logicalPosition = GridManager.Instance.WorldToGrid(transform.position);
        }

        // Valide que la destination est dans les limites
        if (!GridManager.Instance.IsInBounds(targetPosition))
        {
            Debug.LogWarning($"MovementController {gameObject.name}: Destination {targetPosition} hors limites");
            return false;
        }

        // Vérifie si l'unité est prête (pas en IdleReversed)
        if (!IsReadyForMovement())
        {
            // Rootborn en train de sortir de terre, met l'ordre en attente
            pendingDestination = targetPosition;
            if (debugMovement)
            {
                Debug.Log($"MovementController {gameObject.name}: Ordre mis en attente pendant IdleReversed");
            }
            return true;
        }

        // LOG DIAGNOSTIC
        if (debugMovement)
        {
            Debug.Log($"🔍 MoveTo {gameObject.name}: logicalPos={logicalPosition}, target={targetPosition}, state={currentState}, isInterpolating={isInterpolating}");
        }

        // Unité prête, exécute immédiatement
        pendingDestination = null;

        // ========== INVERSION DE LERP ==========
        // Si un Lerp est en cours vers nextCell et qu'on demande previousLogicalPosition → inverser immédiatement
        if (isInterpolating && targetPosition == previousLogicalPosition)
        {
            logicalPath.Clear();
            returnToOriginRequested = true;
            return true;
        }

        // ========== CLÉ: Calcul depuis POSITION LOGIQUE (stable) ==========
        List<Vector2Int> path = PathFinding.FindPath(logicalPosition, targetPosition, unitGameObject);

        if (path == null || path.Count == 0)
        {
            finalDestination = targetPosition;
            Debug.LogWarning($"MovementController {gameObject.name}: Pas de chemin trouvé vers {targetPosition} — retry persistant");
            StartRetryToDestination(targetPosition);
            return false;
        }

        // Remplace la queue de chemin (pas de StopCoroutine brutal)
        logicalPath.Clear();
        finalDestination = targetPosition;
        foreach (var cell in path)
        {
            logicalPath.Enqueue(cell);
        }

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Nouveau chemin de {path.Count} cases depuis {logicalPosition} vers {targetPosition}");
        }

        // Si pas encore en interpolation, démarre la boucle
        if (!isInterpolating)
        {
            OnMovementStarted.Invoke();
            currentState = MovementState.Moving;
            interpolationCoroutine = StartCoroutine(InterpolationLoop());
        }

        return true;
    }

    // Boucle principale d'interpolation
    // Consomme la queue case par case, met à jour logicalPosition immédiatement
    // Interpole le visuel de manière asynchrone
    private IEnumerator InterpolationLoop()
    {
        isInterpolating = true;

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Début boucle interpolation");
        }

        while (logicalPath.Count > 0)
        {
            // Consomme la prochaine case de la queue
            Vector2Int nextCell = logicalPath.Dequeue();

            // Gestion spéciale Rootborn : IdleReversed avant PREMIER mouvement
            if (IsRootborn() && isFirstMoveOfSession)
            {
                yield return StartCoroutine(PlayRootbornPreMovementAnimation());
                isFirstMoveOfSession = false;
            }

            // Vérifie que la case est toujours accessible
            if (!GridManager.Instance.IsWalkableFor(nextCell, unitGameObject))
            {
                if (debugMovement)
                {
                    Debug.LogWarning($"MovementController {gameObject.name}: Case {nextCell} devient inaccessible");
                }
                HandlePathBlocked();
                yield break;
            }

            // ========== UPDATE LOGIQUE DIFFÉRÉE ==========
            previousLogicalPosition = logicalPosition;
            EntityRegistry.Instance?.SetLeaving(gameObject, previousLogicalPosition);
            visualTargetCell = nextCell;
            // logicalPosition et UpdateLogicalPosition NE SONT PAS mis à jour ici — décalés à la fin du Lerp pour éviter la "réservation forward" qui empêche la détection de collision tardive

            if (debugMovement)
            {
                Debug.Log($"MovementController {gameObject.name}: Logique {previousLogicalPosition} -> {nextCell}");
            }

            // Lance l'animation de marche
            if (animatorController != null)
            {
                animatorController.PlayWalkAnimation(previousLogicalPosition, nextCell);
            }

            // Interpole le visuel (peut être interrompu par collision)
            yield return StartCoroutine(InterpolateToCell(previousLogicalPosition, nextCell));

            // Consomme le résultat du step
            if (lastStepResult == StepResult.Reversed)
            {
                EntityRegistry.Instance?.ClearLeaving(gameObject);
                yield break;
            }

            if (lastStepResult == StepResult.CollisionReturned)
            {
                EntityRegistry.Instance?.ClearLeaving(gameObject);

                // Tenter recalcul A* immédiat depuis logicalPosition (inchangée car update différée)
                if (!finalDestination.HasValue) yield break;

                Vector2Int dest = finalDestination.Value;
                List<Vector2Int> retryPath = PathFinding.FindPath(logicalPosition, dest, unitGameObject);

                if (retryPath != null && retryPath.Count > 0)
                {
                    if (debugMovement)
                        Debug.Log($"MovementController {gameObject.name}: Recalcul après collision réussi vers {dest}");
                    logicalPath.Clear();
                    foreach (var c in retryPath) logicalPath.Enqueue(c);
                    continue; // Reprend la boucle while avec le nouveau path
                }

                // A* échoué → priorité auto-attaque si ennemi à portée (avant test encerclement)
                if (attackController != null)
                {
                    attackController.SetManualOrder(false);
                    GameObject nearestEnemy = attackController.FindNearestEnemyInRange();
                    if (nearestEnemy != null)
                    {
                        if (debugMovement)
                            Debug.Log($"MovementController {gameObject.name}: Bloquée + ennemi à portée → auto-attaque, ordre abandonné");
                        finalDestination = null;
                        attackController.StartAttack(nearestEnemy);
                        isInterpolating = false;
                        currentState = MovementState.Idle;
                        yield break;
                    }
                }

                // Pas de défense possible → retry persistant
                isInterpolating = false;
                currentState = MovementState.Blocked;
                // Autorise l'auto-attaque pendant le retry — si un ennemi entre à portée, l'unité ripostera
                attackController?.SetManualOrder(false);
                retryCoroutine = StartCoroutine(RetryPathPeriodically(dest));
                yield break;
            }

            // StepResult.Completed → step réussi
            logicalPosition = nextCell;
            EntityRegistry.Instance?.UpdateLogicalPosition(gameObject, logicalPosition);
            EntityRegistry.Instance?.ClearLeaving(gameObject);

            // Reset le compteur de collisions consécutives — l'unité a progressé
            consecutiveCollisions = 0;

            // Rafraîchit l'animation vers la vraie prochaine direction en cas de remplacement de queue mid-mouvement
            // Évite que l'unité continue à afficher l'animation de l'ancienne direction après un nouvel ordre
            if (logicalPath.Count > 0 && animatorController != null)
            {
                animatorController.PlayWalkAnimation(logicalPosition, logicalPath.Peek());
            }

        }

        // Mouvement terminé avec succès
        OnMovementCompleted();
        isInterpolating = false;
    }

    // Interpole le visuel entre deux cases avec détection collision temps réel
    // Gère le retour animé si case occupée pendant trajet
    // Respecte la logique de collision existante
    private IEnumerator InterpolateToCell(Vector2Int fromCell, Vector2Int toCell)
    {
        Vector3 startPosition = GridManager.Instance.GridToWorld(fromCell);
        Vector3 targetPosition = GridManager.Instance.GridToWorld(toCell);

        // Force position de départ exacte
        transform.position = startPosition;

        // Calcule la distance et le temps nécessaire
        float distance = Vector3.Distance(startPosition, targetPosition);
        float journeyTime = distance / movementSpeed;
        float elapsedTime = 0f;

        // Animation de déplacement par interpolation linéaire
        while (elapsedTime < journeyTime)
        {
            // ========== INVERSION DE LERP ==========
            // Demande de retour vers fromCell détectée → retour fluide depuis position actuelle
            if (returnToOriginRequested)
            {
                returnToOriginRequested = false;

                Vector3 currentPosition = transform.position;
                float distanceToReturn = Vector3.Distance(currentPosition, startPosition);
                float returnTime = distanceToReturn / movementSpeed;
                float returnElapsed = 0f;

                if (animatorController != null)
                    animatorController.PlayWalkAnimation(GridManager.Instance.WorldToGrid(currentPosition), fromCell);

                while (returnElapsed < returnTime)
                {
                    if (currentState != MovementState.Moving) yield break;
                    transform.position = Vector3.Lerp(currentPosition, startPosition, returnElapsed / returnTime);
                    returnElapsed += Time.deltaTime;
                    yield return null;
                }

                transform.position = startPosition;
                logicalPosition = fromCell;
                EntityRegistry.Instance?.UpdateLogicalPosition(gameObject, logicalPosition);
                EntityRegistry.Instance?.ClearLeaving(gameObject);

                if (animatorController != null && !(attackController != null && attackController.IsAttacking()))
                    animatorController.PlayIdleAnimation();

                lastStepResult = StepResult.Reversed;
                yield break;
            }

            // Vérifie qu'on n'a pas été arrêté pendant l'animation
            if (currentState != MovementState.Moving)
            {
                yield break;
            }

            // ========== DÉTECTION COLLISION TEMPS RÉEL ==========
            GameObject otherUnit = EntityRegistry.Instance?.GetUnitAtLogicalPosition(toCell);
            bool collision = false;
            if (otherUnit != null && otherUnit != unitGameObject)
            {
                float otherDistToCenter = Vector3.Distance(otherUnit.transform.position, GridManager.Instance.GridToWorld(toCell));
                collision = otherDistToCenter < contestThreshold;

                // Tie-breaker en zone grise : si l'autre approche du centre sans encore l'atteindre,
                // l'unité avec l'InstanceID le plus élevé cède. Rompt la symétrie pour éviter
                // que deux unités finissent simultanément sur la même case.
                if (!collision && otherDistToCenter < contestThreshold * 2f)
                {
                    if (unitGameObject.GetInstanceID() > otherUnit.GetInstanceID())
                    {
                        collision = true;
                        if (debugMovement)
                            Debug.Log($"MovementController {gameObject.name}: Tie-breaker — cède la case {toCell} à {otherUnit.name}");
                    }
                }
            }
            if (collision)
            {
                // Compteur de collisions consécutives sur la même case
                if (toCell == lastCollisionCell)
                {
                    consecutiveCollisions++;
                }
                else
                {
                    consecutiveCollisions = 1;
                    lastCollisionCell = toCell;
                }

                // Tentative de détour SEULEMENT à partir de la 2ème collision consécutive sur la même case
                if (consecutiveCollisions >= 2)
                {
                    Vector2Int? detour = FindDetourCell(toCell);
                    if (detour.HasValue)
                    {
                        if (debugMovement)
                            Debug.Log($"MovementController {gameObject.name}: Danse détectée sur {toCell} ({consecutiveCollisions}× consécutives), détour vers {detour.Value}");

                        // Reset compteur car on change de stratégie
                        consecutiveCollisions = 0;

                        yield return StartCoroutine(LerpToDetourCell(detour.Value));

                        lastStepResult = StepResult.CollisionReturned;
                        yield break;
                    }
                    // Pas de case détour libre → fallback retour classique ci-dessous
                }

                // Case occupée → retour animé à la case précédente
                Vector3 currentPosition = transform.position;

                // Joue l'animation de marche dans la direction opposée
                Vector2Int currentGridPos = GridManager.Instance.WorldToGrid(currentPosition);
                if (animatorController != null)
                {
                    animatorController.PlayWalkAnimation(currentGridPos, fromCell);
                }

                // Calcule le temps nécessaire pour retourner à startPosition
                float distanceToReturn = Vector3.Distance(currentPosition, startPosition);
                float totalDistance = Vector3.Distance(startPosition, targetPosition);
                float returnTime = (distanceToReturn / totalDistance) * journeyTime;
                float returnElapsed = 0f;

                // Lerp de retour vers startPosition
                while (returnElapsed < returnTime)
                {
                    float returnProgress = returnElapsed / returnTime;
                    transform.position = Vector3.Lerp(currentPosition, startPosition, returnProgress);
                    returnElapsed += Time.deltaTime;
                    yield return null;
                }

                // Force position finale exacte à startPosition
                transform.position = startPosition;

                // Restaure position logique à fromCell
                logicalPosition = fromCell;
                EntityRegistry.Instance?.ClearLeaving(gameObject);

                // Arrête l'animation de marche et retourne à Idle (sauf si en train d'attaquer)
                if (animatorController != null && !(attackController != null && attackController.IsAttacking()))
                {
                    animatorController.PlayIdleAnimation();
                }

                if (debugMovement)
                {
                    Debug.Log($"MovementController {gameObject.name}: Collision détectée, retour à {fromCell}");
                }

                lastStepResult = StepResult.CollisionReturned;
                yield break;
            }

            // Calcule la progression (0 à 1)
            float progression = elapsedTime / journeyTime;

            // Interpole la position visuelle
            transform.position = Vector3.Lerp(startPosition, targetPosition, progression);

            // Avance le temps
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Assure position finale exacte
        transform.position = targetPosition;
        lastStepResult = StepResult.Completed;
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
            Debug.LogWarning($"MovementController {gameObject.name}: Chemin bloqué à {logicalPosition}");
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
        consecutiveCollisions = 0;
        currentState = MovementState.Arrived;
        OnArrived.Invoke();

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Destination {logicalPosition} atteinte");
        }

        // Vérifie si une attaque est en cours
        bool isAttacking = attackController != null && attackController.IsAttacking();

        // Si ennemi à portée et pas déjà en train d'attaquer, déclenche l'attaque immédiatement
        if (!isAttacking && attackController != null)
        {
            GameObject nearestEnemy = attackController.FindNearestEnemyInRange();
            if (nearestEnemy != null)
            {
                attackController.StartAttack(nearestEnemy);
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

        // Si un Lerp est en cours, revenir à la case précédente (annule le step en cours)
        if (isInterpolating)
        {
            logicalPosition = previousLogicalPosition;
            EntityRegistry.Instance?.UpdateLogicalPosition(gameObject, logicalPosition);
            EntityRegistry.Instance?.ClearLeaving(gameObject);
        }

        // Snap visuel à la position logique
        transform.position = GridManager.Instance.GridToWorld(logicalPosition);

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
            Debug.Log($"MovementController {gameObject.name}: Mouvement arrêté à {logicalPosition}");
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
        return logicalPosition;
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
        while (true)
        {
            yield return new WaitForSeconds(retryInterval);

            // Vérifie si la destination est toujours accessible
            if (!GridManager.Instance.IsWalkableFor(destination, unitGameObject))
            {
                if (debugMovement)
                {
                    Debug.Log($"MovementController {gameObject.name}: Destination {destination} toujours bloquée");
                }
                continue;
            }

            // Tente de recalculer un chemin depuis position logique
            List<Vector2Int> newPath = PathFinding.FindPath(logicalPosition, destination, unitGameObject);

            if (newPath != null && newPath.Count > 0)
            {
                if (debugMovement)
                {
                    Debug.Log($"MovementController {gameObject.name}: Retry réussi, nouveau chemin trouvé");
                }

                // Arrête le retry
                retryCoroutine = null;

                // Lance le mouvement
                MoveTo(destination);
                yield break;
            }
        }
    }

    // Vérifie si le transform.position est au centre de la case cible
    // Utilise une tolérance configurable pour compenser imprécisions float
    private bool IsAtCellCenter(Vector2Int cell)
    {
        Vector3 cellWorldPosition = GridManager.Instance.GridToWorld(cell);
        float distance = Vector3.Distance(transform.position, cellWorldPosition);
        return distance < centerTolerance;
    }

    // Cherche une case adjacente libre pour détour, plus proche de finalDestination.
    // Exclut la case bloquée et previousLogicalPosition (on ne veut pas y revenir).
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
            Vector2Int cell = previousLogicalPosition + off;

            if (cell == blockedCell) continue;
            if (cell == previousLogicalPosition) continue;

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

        logicalPosition = detourCell;
        EntityRegistry.Instance?.UpdateLogicalPosition(gameObject, logicalPosition);
        EntityRegistry.Instance?.ClearLeaving(gameObject);

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
        Debug.Log($"Position LOGIQUE: {logicalPosition}");
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
        Vector3 logicalPos = GridManager.Instance.GridToWorld(logicalPosition);
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
            Vector3 targetPos = GridManager.Instance.GridToWorld(visualTargetCell);
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

