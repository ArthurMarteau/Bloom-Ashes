using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Composant responsable de l'exécution du mouvement individuel d'une unité
// Attaché à chaque GameObject unité pour gérer son déplacement case par case
// Exécute les chemins calculés par PathFinding via coordination MovementManager
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

    // Variables d'état du mouvement
    private MovementState currentState = MovementState.Idle;
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int currentPathIndex = 0;
    private Vector2Int targetCell;
    private bool isMoving = false;

    // Références cached pour performance
    private MovementManager movementManager;
    private GameObject unitGameObject;
    private AnimatorController animatorController;

    // Variables de contrôle temporel
    private Coroutine movementCoroutine; 
    private Coroutine retryCoroutine;

    // Initialise les références et l'état par défaut
    private void Awake()
    {
        unitGameObject = gameObject;
        currentState = MovementState.Idle;
        
        // Cache la référence AnimatorController
        animatorController = GetComponent<AnimatorController>();
        if (animatorController == null)
        {
            Debug.LogWarning($"MovementController sur {gameObject.name}: AnimatorController non trouvé!");
        }
    }

    // Cache la référence MovementManager au démarrage
    private void Start()
    {
        movementManager = MovementManager.Instance;
        if (movementManager == null)
        {
            Debug.LogError($"MovementController sur {gameObject.name}: MovementManager non trouvé!");
        }
    }

    // Démarre un nouveau mouvement selon le chemin fourni par PathFinding
    // Interface principale pour MovementManager qui a calculé le trajet
    // Valide le chemin et lance la coroutine de déplacement
    public bool StartMovement(List<Vector2Int> path)
    {
        Debug.Log($"StartMovement appelé, position actuelle: {transform.position}");

        // Valide le chemin fourni
        if (path == null || path.Count == 0)
        {
            if (debugMovement)
            {
                Debug.LogWarning($"MovementController {gameObject.name}: Chemin invalide ou vide");
            }
            return false;
        }

        // Arrête tout mouvement en cours
        StopMovement();

        // Initialise les variables de mouvement
        currentPath = new List<Vector2Int>(path);
        currentPathIndex = 0;
        targetCell = Vector2Int.zero;
        isMoving = true;
        currentState = MovementState.Moving;

        // Lance la coroutine de déplacement
        movementCoroutine = StartCoroutine(FollowPath());

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Démarrage mouvement, {currentPath.Count} étapes");
        }

        return true;
    }

    // Arrête immédiatement le mouvement en cours
    // Interruption manuelle ou automatique (conflit, obstacle)
    // Nettoie l'état et arrête toutes les coroutines
    public void StopMovement()
    {
        // Arrête la coroutine de mouvement
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

        // Arrête la coroutine de retry si active
        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }

        // Remet à zéro les variables d'état
        isMoving = false;
        currentState = MovementState.Idle;
        currentPath.Clear();
        currentPathIndex = 0;

        // Arrête les animations et retourne à idle
        if (animatorController != null)
        {
            animatorController.StopAllAnimations();
        }

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Mouvement arrêté");
        }
    }

    // Retourne l'état actuel du contrôleur de mouvement
    // Interface pour debug et coordination avec autres systèmes
    public MovementState GetCurrentState()
    {
        return currentState;
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

    // Coroutine principale qui exécute le déplacement case par case
    // Boucle de mouvement qui respecte la grille et détecte les obstacles
    // Gère la progression étape par étape jusqu'à destination
    private IEnumerator FollowPath()
    {
        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Début suivi chemin");
        }

        // Boucle principale de déplacement
        while (currentPathIndex < currentPath.Count && isMoving)
        {
            // Récupère la prochaine case à atteindre
            targetCell = currentPath[currentPathIndex];

            // Gestion spéciale Rootborn : IdleReversed avant mouvement
            if (IsRootborn() && currentPathIndex == 0)
            {
                yield return StartCoroutine(PlayRootbornPreMovementAnimation());
            }

            // Démarre l'animation de marche
            if (animatorController != null)
            {
                Vector2Int currentPosition = GridManager.Instance.WorldToGrid(transform.position);
                animatorController.PlayWalkAnimation(currentPosition, targetCell);
            }

            // Vérifie que la case cible est toujours accessible
            if (!GridManager.Instance.IsWalkableFor(targetCell, unitGameObject))
            {
                if (debugMovement)
                {
                    Debug.LogWarning($"MovementController {gameObject.name}: Case {targetCell} devient inaccessible, recalcul nécessaire");
                }
                HandlePathBlocked();
                yield break;
            }

            // Lance le déplacement vers la case suivante
            yield return StartCoroutine(MoveToNextCell());

            // Vérifie si on a atteint le centre de la case
            if (IsAtCellCenter(targetCell))
            {
                currentPathIndex++;

                if (debugMovement)
                {
                    Debug.Log($"MovementController {gameObject.name}: Case {targetCell} atteinte ({currentPathIndex}/{currentPath.Count})");
                }
            }
            else
            {
                // Si on n'a pas atteint le centre, quelque chose bloque
                if (debugMovement)
                {
                    Debug.LogWarning($"MovementController {gameObject.name}: Impossible d'atteindre centre case {targetCell}");
                }
                HandlePathBlocked();
                yield break;
            }
        }

        // Mouvement terminé avec succès
        if (currentPathIndex >= currentPath.Count)
        {
            OnMovementCompleted();
        }
    }

    // Déplace physiquement l'unité vers la case suivante du chemin
    // Animation de déplacement fluide entre deux cases via interpolation
    // Respecte la vitesse configurée et la conversion grille/monde
    private IEnumerator MoveToNextCell()
    {
        // Récupère les positions de départ et d'arrivée en coordonnées monde
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = GridManager.Instance.GridToWorld(targetCell);

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

            // Vérifie en temps réel si la case cible est toujours libre
            if (GridManager.Instance.GetUnitAt(targetCell) != null &&
                GridManager.Instance.GetUnitAt(targetCell) != unitGameObject)
            {
                // Case occupée → retour animé à la case précédente
                Vector3 currentPosition = transform.position;

                // Joue l'animation de marche dans la direction opposée
                Vector2Int currentGridPos = GridManager.Instance.WorldToGrid(currentPosition);
                Vector2Int startGridPos = GridManager.Instance.WorldToGrid(startPosition);
                if (animatorController != null)
                {
                    animatorController.PlayWalkAnimation(currentGridPos, startGridPos);
                }

                float returnTime = (elapsedTime / movementSpeed) / 1f;
                float returnElapsed = 0f;

                while (returnElapsed < returnTime)
                {
                    float returnProgress = returnElapsed / returnTime;
                    transform.position = Vector3.Lerp(currentPosition, startPosition, returnProgress);
                    returnElapsed += Time.deltaTime;
                    yield return null;
                }

                transform.position = startPosition;
                if (animatorController != null)
                {
                    animatorController.PlayIdleAnimation();
                }
                HandlePathBlocked();
                yield break;
            }

            // Calcule la progression (0 à 1)
            float progression = elapsedTime / journeyTime;

            // Interpole la position
            transform.position = Vector3.Lerp(startPosition, targetPosition, progression);

            // Avance le temps
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Assure position finale exacte
        transform.position = targetPosition;
    }

    // Gère la situation où le chemin devient inaccessible
    // Demande un nouveau chemin au MovementManager pour contourner l'obstacle
    // Change l'état et notifie MovementManager pour recalcul
    private void HandlePathBlocked()
    {
        currentState = MovementState.Blocked;
        isMoving = false;

        if (debugMovement)
        {
            Debug.LogWarning($"MovementController {gameObject.name}: Case inaccessible");
        }

        // Arrête l'animation
        if (animatorController != null)
        {
            animatorController.PlayIdleAnimation();
        }

        // Lance retry automatique
        if (movementManager != null && currentPath.Count > 0)
        {
            Vector2Int finalDestination = currentPath[currentPath.Count - 1];
            retryCoroutine = StartCoroutine(RetryPathPeriodically(finalDestination));
        }
    }

    // Coroutine pour réessayer de demander un nouveau chemin périodiquement
    private IEnumerator RetryPathPeriodically(Vector2Int destination)
    {

        while (currentState == MovementState.Blocked)
        {

            yield return new WaitForSeconds(1f);

            // Réessaye le chemin
            if (movementManager != null)
            {
                movementManager.MoveUnitTo(unitGameObject, destination);
            }
        }
    }

    public void StartRetryToDestination(Vector2Int destination)
    {
        Debug.Log($"MovementController {gameObject.name}: StartRetryToDestination appelé vers {destination}");
        currentState = MovementState.Blocked;
        retryCoroutine = StartCoroutine(RetryPathPeriodically(destination));
    }

    // Actions finales quand la destination est atteinte avec succès
    // Nettoie l'état et notifie MovementManager de la réussite
    // Transition vers état final puis retour à idle (sauf si attaque en cours)
    private void OnMovementCompleted()
    {
        currentState = MovementState.Arrived;
        isMoving = false;

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Destination atteinte avec succès");
        }

        // Vérifie si une attaque est en cours
        AttackController attackController = GetComponent<AttackController>();
        bool isAttacking = attackController != null && attackController.IsAttacking();

        // Retourne à l'animation idle SEULEMENT si pas en train d'attaquer
        if (animatorController != null && !isAttacking)
        {
            animatorController.PlayIdleAnimation();
        }

        // Reset la session IdleReversed pour Rootborn (vraie fin de mouvement)
        if (animatorController != null && !isAttacking)
        {
            animatorController.ResetIdleReversedSession();
        }

        // Remet en état idle pour prochaine commande
        currentState = MovementState.Idle;
    }

    // Vérifie si l'unité est au centre exact d'une case donnée
    // Détermine quand une case est "occupée" selon la règle métier
    // Utilise tolerance configurée pour compenser imprécisions floating point
    private bool IsAtCellCenter(Vector2Int cellPosition)
    {
        Vector3 cellWorldCenter = GridManager.Instance.GridToWorld(cellPosition);
        float distance = Vector3.Distance(transform.position, cellWorldCenter);
        return distance <= centerTolerance;
    }

    // Coroutine pour gérer l'animation pré-mouvement du Rootborn
    // Bloque le mouvement jusqu'à ce que IdleReversed soit terminé
    private IEnumerator PlayRootbornPreMovementAnimation()
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

        if (debugMovement)
        {
            Debug.Log($"MovementController {gameObject.name}: Animation pré-mouvement Rootborn terminée");
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
        Debug.Log($"Chemin: {currentPath.Count} étapes");
        Debug.Log($"Index actuel: {currentPathIndex}");
        Debug.Log($"Case cible: {targetCell}");
        Debug.Log($"Position: {transform.position}");

        if (currentPath.Count > 0)
        {
            Debug.Log($"Chemin complet: {string.Join(" -> ", currentPath)}");
        }
    }

    // Dessine le chemin actuel dans la vue Scene pour debug visuel
    private void OnDrawGizmos()
    {
        if (currentPath == null || currentPath.Count == 0 || GridManager.Instance == null)
            return;

        // Dessine le chemin restant
        Gizmos.color = Color.blue;
        for (int i = currentPathIndex; i < currentPath.Count - 1; i++)
        {
            Vector3 currentPos = GridManager.Instance.GridToWorld(currentPath[i]);
            Vector3 nextPos = GridManager.Instance.GridToWorld(currentPath[i + 1]);
            Gizmos.DrawLine(currentPos, nextPos);
        }

        // Dessine la case cible actuelle
        if (currentPathIndex < currentPath.Count)
        {
            Gizmos.color = Color.red;
            Vector3 targetPos = GridManager.Instance.GridToWorld(currentPath[currentPathIndex]);
            Gizmos.DrawWireCube(targetPos, Vector3.one * 0.8f);
        }

        // Dessine la destination finale
        if (currentPath.Count > 0)
        {
            Gizmos.color = Color.green;
            Vector3 finalPos = GridManager.Instance.GridToWorld(currentPath[currentPath.Count - 1]);
            Gizmos.DrawWireSphere(finalPos, 0.3f);
        }
    }
}
