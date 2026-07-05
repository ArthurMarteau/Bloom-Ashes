using System.Collections.Generic;
using UnityEngine;

// Gère la poursuite persistante d'une unité cible
// L'unité suit sa cible et reste adjacente à elle tant que l'ordre persiste
// S'intègre avec MovementController pour les déplacements case par case
public class ChaseController : MonoBehaviour, ITickable
{
    [Header("Chase Settings")]
    [SerializeField] private float checkInterval = 0.2f; // Intervalle de vérification position cible
    [SerializeField] private bool debugChase = true;

    // État de la chase
    private GameObject currentTarget;
    private bool isChasing = false;
    private Vector2Int lastTargetPosition;
    private int checkIntervalTicks;
    private long lastCheckTick;
    private long simulationEntityID = -1;
    private bool initialMovePending;

    // Références
    private MovementController movementController;
    private GameObject unitGameObject;

    // Initialise les références
    private void Awake()
    {
        unitGameObject = gameObject;
        checkIntervalTicks = SimulationClock.SecondsToTicks(checkInterval);

        movementController = GetComponent<MovementController>();
        if (movementController == null)
        {
            GameLogger.Error($"ChaseController sur {gameObject.name}: MovementController non trouvé !", this);
        }
    }

    // Enregistre la chase après l'attribution de l'entityID de l'unité
    private void Start()
    {
        simulationEntityID = EntityRegistry.Instance?.GetEntityID(gameObject) ?? -1;
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Register(simulationEntityID, this);
    }

    // Retire la chase de la boucle logique lors de sa destruction
    private void OnDestroy()
    {
        if (simulationEntityID >= 0)
            SimulationClockManager.Instance?.Unregister(simulationEntityID, this);
    }

    // Vérifie et met à jour la chase sur la boucle logique fixe
    public void SimTick(long tick)
    {
        if (!isActiveAndEnabled)
            return;

        if (!isChasing || currentTarget == null)
        {
            return;
        }

        if (initialMovePending)
        {
            initialMovePending = false;
            MoveTowardsTarget();
            return;
        }

        // NOUVEAU : Vérifie TOUJOURS si la cible a bougé, même pendant mouvement
        Vector2Int currentTargetPosition = GetLogicalPosition(currentTarget);

        // Si cible a bougé (changé de case)
        if (currentTargetPosition != lastTargetPosition)
        {
            lastTargetPosition = currentTargetPosition;

            if (debugChase)
            {
                GameLogger.Log(GameLogger.Category.Movement, $"⚡ ChaseController {gameObject.name}: Cible a bougé vers {currentTargetPosition} ! Recalcul immédiat", this);
            }

            // Recalcule le chemin (le MovementController gère le changement de direction)
            MoveTowardsTarget();
            return; // Sort pour ne pas vérifier à nouveau cette frame
        }

        // Si cible n'a pas bougé, vérifie seulement si on est à l'arrêt
        if (movementController != null && movementController.GetCurrentState() == MovementController.MovementState.Moving)
        {
            // En mouvement vers la bonne destination, on attend
            return;
        }

        // Vérifie périodiquement si on est bien adjacent (quand à l'arrêt)
        if (tick - lastCheckTick >= checkIntervalTicks)
        {
            lastCheckTick = tick;
            CheckTargetPosition();
        }
    }

    // Démarre la poursuite d'une cible
    // Interface principale appelée par OrderManager lors d'un clic droit sur unité
    public void StartChase(GameObject target)
    {
        if (target == null)
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Tentative de chase sur cible null", this);
            return;
        }

        if (target == unitGameObject)
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Une unité ne peut pas se chaser elle-même", this);
            return;
        }

        // Arrête la chase précédente si existe
        if (isChasing)
        {
            StopChase();
        }

        currentTarget = target;
        isChasing = true;
        initialMovePending = true;
        lastTargetPosition = GetLogicalPosition(target);
        lastCheckTick = SimulationClockManager.Instance != null ? SimulationClockManager.Instance.CurrentTick : 0;

        if (debugChase)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Début chase de {target.name}", this);
        }

        // Le premier mouvement sera décidé au prochain SimTick
    }

    // Arrête la poursuite en cours
    // Appelé lors d'un nouvel ordre manuel ou destruction de la cible
    public void StopChase()
    {
        if (!isChasing)
        {
            return;
        }

        isChasing = false;
        initialMovePending = false;
        currentTarget = null;

        if (debugChase)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Chase arrêtée", this);
        }
    }

    // Vérifie si la cible a bougé et réagit en conséquence
    // Appelé périodiquement dans SimTick
    private void CheckTargetPosition()
    {
        if (currentTarget == null)
        {
            // Cible détruite ou disparue
            if (debugChase)
            {
                GameLogger.Log(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Cible disparue, arrêt chase", this);
            }
            StopChase();
            return;
        }

        Vector2Int currentTargetPosition = GetLogicalPosition(currentTarget);

        // Vérifie si la cible a changé de case
        if (currentTargetPosition != lastTargetPosition)
        {
            lastTargetPosition = currentTargetPosition;

            if (debugChase)
            {
                GameLogger.Log(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Cible a bougé vers {currentTargetPosition}, recalcul", this);
            }

            MoveTowardsTarget();
        }
        else
        {
            // Cible n'a pas bougé, vérifie si on est adjacent
            Vector2Int myPosition = movementController.GetCurrentGridPosition();

            if (!IsAdjacentTo(myPosition, currentTargetPosition))
            {
                // Pas encore adjacent, continue d'approcher
                MoveTowardsTarget();
            }
            // Sinon on est déjà adjacent, on reste en surveillance
        }
    }

    // Calcule et exécute le mouvement vers la cible
    // Trouve la case la plus proche (pas forcément adjacente) et s'y déplace
    // Utilise la position de grille actuelle pour calculs
    private void MoveTowardsTarget()
    {
        if (movementController == null || currentTarget == null)
        {
            return;
        }

        Vector2Int myPosition = movementController.GetCurrentGridPosition();
        Vector2Int targetPosition = GetLogicalPosition(currentTarget);

        // Vérifie si déjà adjacent
        if (IsAdjacentTo(myPosition, targetPosition))
        {
            if (debugChase)
            {
                GameLogger.Log(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Déjà adjacent à {currentTarget.name}, surveillance", this);
            }
            return;
        }

        // Trouve la case adjacente walkable la plus proche sans lancer de calcul A*
        Vector2Int bestCell = FindClosestAccessibleCell(targetPosition, myPosition);

        if (bestCell == Vector2Int.zero)
        {
            if (debugChase)
            {
                GameLogger.Warn(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Aucune case accessible vers {currentTarget.name}", this);
            }
            return;
        }

        // Envoie l'ordre de mouvement
        GameLogger.Log(GameLogger.Category.Movement, $"📤 Chase: Envoi ordre mouvement vers {bestCell}", this);
        if (!movementController.MoveToInternal(bestCell))
        {
            // Un ordre joueur en attente reste prioritaire ; la chase réessaiera lors de son prochain check.
            return;
        }

        if (debugChase)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Mouvement vers {bestCell} (cible: {targetPosition})", this);
        }
    }

    // Trouve la première case adjacente walkable triée par distance Chebyshev depuis l'unité
    private Vector2Int FindClosestAccessibleCell(Vector2Int targetPosition, Vector2Int myPosition)
    {
        // Toutes les 8 directions adjacentes via RangeOffsets (source unique de vérité)
        Vector2Int[] adjacentOffsets = RangeOffsets.GetOffsets(1);

        Vector2Int bestAdjacentCell = Vector2Int.zero;
        int bestDistance = int.MaxValue;
        bool foundWalkableCell = false;

        // Sélectionne la case walkable la plus proche sans déclencher d'A* intermédiaire
        foreach (Vector2Int offset in adjacentOffsets)
        {
            Vector2Int adjacentCell = targetPosition + offset;

            // Vérifie que la position est dans les limites
            if (!GridManager.Instance.IsInBounds(adjacentCell))
            {
                continue;
            }

            // Vérifie que la position est accessible
            if (!GridManager.Instance.IsWalkableFor(adjacentCell, unitGameObject))
            {
                continue;
            }

            int distance = CombatSystem.CalculateChebyshevDistance(myPosition, adjacentCell);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestAdjacentCell = adjacentCell;
                foundWalkableCell = true;
            }
        }

        // Si case adjacente trouvée, c'est la meilleure option
        if (foundWalkableCell)
        {
            if (debugChase)
            {
                GameLogger.Log(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Meilleure case adjacente {bestAdjacentCell} à distance {bestDistance}", this);
            }
            return bestAdjacentCell;
        }

        // Sinon, retourne simplement la position de la cible
        // Le pathfinding ira aussi près que possible
        if (debugChase)
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"ChaseController {gameObject.name}: Aucune case adjacente accessible, tentative vers cible directement", this);
        }
        return targetPosition;
    }

    // Vérifie si deux positions sont adjacentes (8 directions)
    private bool IsAdjacentTo(Vector2Int posA, Vector2Int posB)
    {
        int deltaX = Mathf.Abs(posA.x - posB.x);
        int deltaY = Mathf.Abs(posA.y - posB.y);

        // Adjacent si écart de maximum 1 case en X et Y
        return deltaX <= 1 && deltaY <= 1 && (deltaX + deltaY) > 0;
    }

    // Retourne si l'unité est actuellement en chase
    public bool IsChasing()
    {
        return isChasing;
    }

    // Retourne si la chase est satisfaite par l'adjacence ou par un blocage au plus proche
    public bool IsSatisfied()
    {
        if (!isChasing)
            return true;

        if (movementController == null || currentTarget == null)
            return false;

        MovementController.MovementState movementState = movementController.GetCurrentState();
        if (movementState == MovementController.MovementState.Blocked)
            return true;

        if (movementState == MovementController.MovementState.Moving)
            return false;

        Vector2Int myPosition = movementController.GetCurrentGridPosition();
        Vector2Int targetPosition = GetLogicalPosition(currentTarget);
        return IsAdjacentTo(myPosition, targetPosition);
    }

    // Retourne la cible actuelle (peut être null)
    public GameObject GetCurrentTarget()
    {
        return currentTarget;
    }

    // Retourne la position logique de la cible sans consulter son Transform.
    private Vector2Int GetLogicalPosition(GameObject target)
    {
        if (EntityRegistry.Instance != null && EntityRegistry.Instance.TryGetLogicalPosition(target, out Vector2Int position))
            return position;

        MovementController targetMovement = target != null ? target.GetComponent<MovementController>() : null;
        return targetMovement != null ? targetMovement.GetCurrentGridPosition() : Vector2Int.zero;
    }

    // Debug pour afficher l'état de la chase
    [ContextMenu("Debug Chase State")]
    private void DebugChaseState()
    {
        GameLogger.Log(GameLogger.Category.Movement, $"=== ChaseController Debug - {gameObject.name} ===", this);
        GameLogger.Log(GameLogger.Category.Movement, $"En chase: {isChasing}", this);
        GameLogger.Log(GameLogger.Category.Movement, $"Cible: {(currentTarget != null ? currentTarget.name : "Aucune")}", this);
        GameLogger.Log(GameLogger.Category.Movement, $"Dernière position cible: {lastTargetPosition}", this);
    }
}
