using System.Collections.Generic;
using UnityEngine;

// Gère la poursuite persistante d'une unité cible
// L'unité suit sa cible et reste adjacente à elle tant que l'ordre persiste
// S'intègre avec MovementController pour les déplacements case par case
public class ChaseController : MonoBehaviour
{
    [Header("Chase Settings")]
    [SerializeField] private float checkInterval = 0.2f; // Intervalle de vérification position cible
    [SerializeField] private bool debugChase = true;

    // État de la chase
    private GameObject currentTarget;
    private bool isChasing = false;
    private Vector2Int lastTargetPosition;
    private float lastCheckTime;

    // Références
    private MovementController movementController;
    private GameObject unitGameObject;

    // Initialise les références
    private void Awake()
    {
        unitGameObject = gameObject;

        movementController = GetComponent<MovementController>();
        if (movementController == null)
        {
            Debug.LogError($"ChaseController sur {gameObject.name}: MovementController non trouvé !");
        }
    }

    // Vérifie et met à jour la chase chaque frame
    private void Update()
    {
        if (!isChasing || currentTarget == null)
        {
            return;
        }

        // NOUVEAU : Vérifie TOUJOURS si la cible a bougé, même pendant mouvement
        Vector2Int currentTargetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);

        // Si cible a bougé (changé de case)
        if (currentTargetPosition != lastTargetPosition)
        {
            lastTargetPosition = currentTargetPosition;

            if (debugChase)
            {
                Debug.Log($"⚡ ChaseController {gameObject.name}: Cible a bougé vers {currentTargetPosition} ! Recalcul immédiat");
            }

            // Recalcule le chemin (le MovementController gère le changement de direction)
            MoveTowardsTarget();
            return; // Sort pour ne pas vérifier à nouveau cette frame
        }

        // Si cible n'a pas bougé, vérifie seulement si on est à l'arrêt
        MovementController movementCtrl = GetComponent<MovementController>();
        if (movementCtrl != null && movementCtrl.GetCurrentState() == MovementController.MovementState.Moving)
        {
            // En mouvement vers la bonne destination, on attend
            return;
        }

        // Vérifie périodiquement si on est bien adjacent (quand à l'arrêt)
        if (Time.time - lastCheckTime >= checkInterval)
        {
            lastCheckTime = Time.time;
            CheckTargetPosition();
        }
    }

    // Démarre la poursuite d'une cible
    // Interface principale appelée par OrderManager lors d'un clic droit sur unité
    public void StartChase(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning($"ChaseController {gameObject.name}: Tentative de chase sur cible null");
            return;
        }

        if (target == unitGameObject)
        {
            Debug.LogWarning($"ChaseController {gameObject.name}: Une unité ne peut pas se chaser elle-même");
            return;
        }

        // Arrête la chase précédente si existe
        if (isChasing)
        {
            StopChase();
        }

        currentTarget = target;
        isChasing = true;
        lastTargetPosition = GridManager.Instance.WorldToGrid(target.transform.position);
        lastCheckTime = Time.time;

        if (debugChase)
        {
            Debug.Log($"ChaseController {gameObject.name}: Début chase de {target.name}");
        }

        // Lance immédiatement le premier mouvement
        MoveTowardsTarget();
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
        currentTarget = null;

        if (debugChase)
        {
            Debug.Log($"ChaseController {gameObject.name}: Chase arrêtée");
        }
    }

    // Vérifie si la cible a bougé et réagit en conséquence
    // Appelé périodiquement dans Update()
    private void CheckTargetPosition()
    {
        if (currentTarget == null)
        {
            // Cible détruite ou disparue
            if (debugChase)
            {
                Debug.Log($"ChaseController {gameObject.name}: Cible disparue, arrêt chase");
            }
            StopChase();
            return;
        }

        Vector2Int currentTargetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);

        // Vérifie si la cible a changé de case
        if (currentTargetPosition != lastTargetPosition)
        {
            lastTargetPosition = currentTargetPosition;

            if (debugChase)
            {
                Debug.Log($"ChaseController {gameObject.name}: Cible a bougé vers {currentTargetPosition}, recalcul");
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
        Vector2Int targetPosition = GridManager.Instance.WorldToGrid(currentTarget.transform.position);

        // Vérifie si déjà adjacent
        if (IsAdjacentTo(myPosition, targetPosition))
        {
            if (debugChase)
            {
                Debug.Log($"ChaseController {gameObject.name}: Déjà adjacent à {currentTarget.name}, surveillance");
            }
            return;
        }

        // NOUVEAU : Trouve la case accessible la plus proche de la cible (pas forcément adjacente)
        Vector2Int bestCell = FindClosestAccessibleCell(targetPosition, myPosition);

        if (bestCell == Vector2Int.zero)
        {
            if (debugChase)
            {
                Debug.LogWarning($"ChaseController {gameObject.name}: Aucune case accessible vers {currentTarget.name}");
            }
            return;
        }

        // Envoie l'ordre de mouvement
        Debug.Log($"📤 Chase: Envoi ordre mouvement vers {bestCell}");
        movementController.MoveTo(bestCell);

        if (debugChase)
        {
            Debug.Log($"ChaseController {gameObject.name}: Mouvement vers {bestCell} (cible: {targetPosition})");
        }
    }

    // Trouve la case accessible à portée avec le chemin le plus court
    // Utilise PathFinding pour calculer le chemin réel vers chaque case adjacente
    // Prend en compte les obstacles pour trouver la vraie case optimale
    private Vector2Int FindClosestAccessibleCell(Vector2Int targetPosition, Vector2Int myPosition)
    {
        // Toutes les 8 directions adjacentes via RangeOffsets (source unique de vérité)
        Vector2Int[] adjacentOffsets = RangeOffsets.GetOffsets(1);

        Vector2Int bestAdjacentCell = Vector2Int.zero;
        int shortestPathLength = int.MaxValue;

        // Cherche la case adjacente avec le chemin le plus court
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

            // Calcule le chemin réel depuis notre position actuelle
            List<Vector2Int> path = PathFinding.FindPath(myPosition, adjacentCell, unitGameObject);

            // Si pas de chemin trouvé, ignore cette case
            if (path == null || path.Count == 0)
            {
                continue;
            }

            // Compare la longueur du chemin
            if (path.Count < shortestPathLength)
            {
                shortestPathLength = path.Count;
                bestAdjacentCell = adjacentCell;
            }
        }

        // Si case adjacente trouvée, c'est la meilleure option
        if (bestAdjacentCell != Vector2Int.zero)
        {
            if (debugChase)
            {
                Debug.Log($"ChaseController {gameObject.name}: Meilleure case trouvée {bestAdjacentCell} avec chemin de {shortestPathLength} cases");
            }
            return bestAdjacentCell;
        }

        // Sinon, retourne simplement la position de la cible
        // Le pathfinding ira aussi près que possible
        if (debugChase)
        {
            Debug.LogWarning($"ChaseController {gameObject.name}: Aucune case adjacente accessible, tentative vers cible directement");
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

    // Retourne la cible actuelle (peut être null)
    public GameObject GetCurrentTarget()
    {
        return currentTarget;
    }

    // Debug pour afficher l'état de la chase
    [ContextMenu("Debug Chase State")]
    private void DebugChaseState()
    {
        Debug.Log($"=== ChaseController Debug - {gameObject.name} ===");
        Debug.Log($"En chase: {isChasing}");
        Debug.Log($"Cible: {(currentTarget != null ? currentTarget.name : "Aucune")}");
        Debug.Log($"Dernière position cible: {lastTargetPosition}");
    }
}