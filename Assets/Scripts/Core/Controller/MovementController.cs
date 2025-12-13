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
    private float centerTolerance = 1f;      // Distance pour considérer case "atteinte"

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

    // ========== LOGIQUE (Source de vérité) ==========
    private Vector2Int logicalPosition;           // Case actuellement occupée (STABLE)
    private Queue<Vector2Int> logicalPath;        // Chemin logique à suivre
    private MovementState currentState = MovementState.Idle;

    // ========== VISUEL (Présentation uniquement) ==========
    private Vector2Int visualTargetCell;          // Case cible du Lerp en cours
    private bool isInterpolating = false;         // Interpolation en cours

    // ========== ÉTAT ==========
    private bool isMoving = false;
    private Vector2Int? pendingDestination = null; // Ordre en attente pendant IdleReversed

    // Références cached pour performance
    private GameObject unitGameObject;
    private AnimatorController animatorController;

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
    }

    // Interface publique principale pour demander un déplacement vers une destination
    // Calcul TOUJOURS depuis logicalPosition (stable) pour PathFinding fiable
    // Remplace la queue sans arrêt brutal si déjà en mouvement
    public bool MoveTo(Vector2Int targetPosition)
    {
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

        // Valide que la destination est accessible pour cette unité
        if (!GridManager.Instance.IsWalkableFor(targetPosition, unitGameObject))
        {
            Debug.LogWarning($"MovementController {gameObject.name}: Destination {targetPosition} inaccessible");
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
            return true; // Ordre accepté mais différé
        }

        // Unité prête, exécute immédiatement
        pendingDestination = null;

        // ========== CLÉ: Calcul depuis POSITION LOGIQUE (stable) ==========
        List<Vector2Int> path = PathFinding.FindPath(logicalPosition, targetPosition, unitGameObject);

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning($"MovementController {gameObject.name}: Pas de chemin trouvé vers {targetPosition}");
            StartRetryToDestination(targetPosition);
            return false;
        }

        // Remplace la queue de chemin (pas de StopCoroutine brutal)
        logicalPath.Clear();
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
            isMoving = true;
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

            // ========== UPDATE LOGIQUE IMMÉDIATE ==========
            Vector2Int previousLogicalPosition = logicalPosition;
            logicalPosition = nextCell;
            visualTargetCell = nextCell;

            if (debugMovement)
            {
                Debug.Log($"MovementController {gameObject.name}: Logique {previousLogicalPosition} -> {logicalPosition}");
            }

            // Lance l'animation de marche
            if (animatorController != null)
            {
                animatorController.PlayWalkAnimation(previousLogicalPosition, nextCell);
            }

            // Interpole le visuel (peut être interrompu par collision)
            yield return StartCoroutine(InterpolateToCell(previousLogicalPosition, nextCell));

            // Vérifie si le Lerp s'est terminé correctement
            if (!IsAtCellCenter(nextCell))
            {
                if (debugMovement)
                {
                    Debug.LogWarning($"MovementController {gameObject.name}: Interpolation interrompue");
                }
                HandlePathBlocked();
                yield break;
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
            // Vérifie qu'on n'a pas été arrêté pendant l'animation
            if (!isMoving)
            {
                yield break;
            }

            // ========== DÉTECTION COLLISION TEMPS RÉEL ==========
            if (GridManager.Instance.GetUnitAt(toCell) != null &&
                GridManager.Instance.GetUnitAt(toCell) != unitGameObject)
            {
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

                // Arrête l'animation de marche et retourne à Idle
                if (animatorController != null)
                {
                    animatorController.PlayIdleAnimation();
                }

                if (debugMovement)
                {
                    Debug.Log($"MovementController {gameObject.name}: Collision détectée, retour à {fromCell}");
                }

                HandlePathBlocked();
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
    }

    // Gère la situation où le chemin devient inaccessible
    // Lance un retry automatique local pour recalculer le chemin
    // Change l'état et démarre la coroutine de retry périodique
    private void HandlePathBlocked()
    {
        currentState = MovementState.Blocked;
        isMoving = false;
        isInterpolating = false;

        // Vide la queue
        logicalPath.Clear();

        if (debugMovement)
        {
            Debug.LogWarning($"MovementController {gameObject.name}: Chemin bloqué à {logicalPosition}");
        }

        // Arrête l'animation
        if (animatorController != null)
        {
            animatorController.PlayIdleAnimation();
        }

        // Note: Le retry est géré en externe ou peut être ajouté ici si besoin
    }

    // Appelé quand le mouvement se termine avec succès
    // Nettoie l'état et déclenche animations de fin
    private void OnMovementCompleted()
    {
        isMoving = false;
        currentState = MovementState.Arrived;

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Destination {logicalPosition} atteinte");
        }

        // Vérifie si une attaque est en cours
        AttackController attackController = GetComponent<AttackController>();
        bool isAttacking = attackController != null && attackController.IsAttacking();

        // Animation idle SEULEMENT si pas en train d'attaquer
        if (animatorController != null && !isAttacking)
        {
            animatorController.PlayIdleAnimation();
        }

        // Reset état pour prochain mouvement
        currentState = MovementState.Idle;
        isFirstMoveOfSession = true;

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

        // Snap visuel à la position logique
        transform.position = GridManager.Instance.GridToWorld(logicalPosition);

        // Remet à zéro les variables d'état
        isMoving = false;
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

        retryCoroutine = StartCoroutine(RetryPathPeriodically(destination));
    }

    // Coroutine pour réessayer de calculer un nouveau chemin périodiquement
    // Retry automatique local sans dépendance externe
    // Continue jusqu'à succès ou arrêt manuel
    private IEnumerator RetryPathPeriodically(Vector2Int destination)
    {
        float retryInterval = 0.5f;

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

    // Vérifie si cette unité est un Rootborn
    // Utilise le nom du GameObject pour identifier les Rootborn
    private bool IsRootborn()
    {
        return gameObject.name.Contains("Rootborn");
    }

    // Méthode de debug pour afficher l'état du contrôleur
    [ContextMenu("Debug Movement Controller")]
    private void DebugControllerState()
    {
        Debug.Log($"=== MovementController Debug - {gameObject.name} ===");
        Debug.Log($"État: {currentState}");
        Debug.Log($"En mouvement: {isMoving}");
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
