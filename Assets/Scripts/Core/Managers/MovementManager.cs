using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Service singleton pour orchestrer tous les mouvements d'unités
// Gère la coordination entre unités, évite les conflits et optimise les performances
// Interface principale pour demander des déplacements depuis l'extérieur
public class MovementManager : MonoBehaviour
{
    [Header("Movement Manager Settings")]
    [SerializeField] private int maxMovementsPerFrame = 5; // Limite de traitements par frame
    [SerializeField] private bool debugMovements = true;   // Affichage debug dans console

    // Instance singleton pour accès global
    public static MovementManager Instance { get; private set; }

    // File d'attente des demandes de déplacement en attente
    // Traitement séquentiel pour éviter surcharge système
    private Queue<MovementRequest> movementQueue = new Queue<MovementRequest>();

    // Flag pour éviter traitement simultané de la queue
    private bool processingQueue = false;

    // Structure pour encapsuler une demande de mouvement
    // Contient toutes les informations nécessaires pour traiter le déplacement
    private class MovementRequest
    {
        public GameObject unit;           // Unité à déplacer
        public Vector2Int targetPosition; // Destination finale

        public MovementRequest(GameObject unit, Vector2Int target)
        {
            this.unit = unit;
            this.targetPosition = target;
        }
    }

    // Initialise le singleton et les structures de données
    private void Awake()
    {
        // Configure le singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persiste entre scènes
            Debug.Log("MovementManager initialisé");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Traite la file d'attente des mouvements chaque frame
    // Limite le nombre de traitements pour maintenir les performances
    private void Update()
    {
        ProcessMovementQueue();
    }

    // Interface principale pour demander le déplacement d'une unité
    // Point d'entrée depuis MovementController (clic droit) ou systèmes IA
    // Ajoute la demande à la queue et retourne succès/échec immédiat
    public bool MoveUnitTo(GameObject unit, Vector2Int targetPosition)
    {
        // Valide que l'unité existe et a un tag valide
        if (unit == null)
        {
            Debug.LogError("MovementManager: Tentative de déplacer unité null");
            return false;
        }

        if (!UnitTypeHelper.IsValidUnit(unit))
        {
            Debug.LogWarning($"MovementManager: {unit.name} n'a pas de tag d'unité valide");
            return false;
        }

        // Vérifie que la destination est dans les limites
        if (!GridManager.Instance.IsInBounds(targetPosition))
        {
            Debug.LogWarning($"MovementManager: Destination {targetPosition} hors limites");
            return false;
        }

        // Vérifie que la destination est accessible pour cette unité
        if (!GridManager.Instance.IsWalkableFor(targetPosition, unit))
        {
            Debug.LogWarning($"MovementManager: Destination {targetPosition} inaccessible pour {UnitTypeHelper.GetDisplayName(unit)}");
            return false;
        }

        // Ajoute la demande à la file d'attente
        MovementRequest request = new MovementRequest(unit, targetPosition);
        movementQueue.Enqueue(request);

        if (debugMovements)
        {
            Debug.Log($"MovementManager: Demande déplacement {unit.name} vers {targetPosition} (Queue: {movementQueue.Count})");
        }

        return true;
    }

    // Traite les demandes en attente dans la file d'attente
    // Limite le nombre de traitements par frame pour maintenir les performances
    // Traitement séquentiel pour éviter surcharge système
    private void ProcessMovementQueue()
    {
        // Évite traitement simultané
        if (processingQueue || movementQueue.Count == 0)
            return;

        processingQueue = true;
        int processedThisFrame = 0;

        // Traite jusqu'à maxMovementsPerFrame demandes par frame
        while (movementQueue.Count > 0 && processedThisFrame < maxMovementsPerFrame)
        {
            MovementRequest request = movementQueue.Dequeue();

            // Vérifie que l'unité existe encore
            if (request.unit != null)
            {
                ExecuteMovementRequest(request);
            }

            processedThisFrame++;
        }

        processingQueue = false;
    }

    // Exécute une demande de mouvement spécifique
    // Calcule le chemin via PathFinding puis lance le MovementController
    // Gère les échecs de pathfinding et met à jour les structures de données
    // Méthode principale - lance le processus
    private bool ExecuteMovementRequest(MovementRequest request)
    {
        GameObject unit = request.unit;
        Vector2Int targetPosition = request.targetPosition;

        MovementController movementController = unit.GetComponent<MovementController>();
        if (movementController == null)
        {
            Debug.LogError($"MovementManager: Pas de MovementController sur {unit.name}");
            return false;
        }

        // Vérifie si l'unité est prête (pas d'IdleReversed en cours pour Rootborn)
        if (!movementController.IsReadyForMovement())
        {
            if (debugMovements)
            {
                Debug.Log($"MovementManager: {unit.name} pas prêt (animation en cours), ordre remis en queue");
            }

            // Remet l'ordre en queue pour réessayer plus tard
            movementQueue.Enqueue(request);
            return false;
        }

        // Arrête tout mouvement en cours
        movementController.StopMovement();

        // Lance la coroutine qui attend 1 frame puis exécute
        StartCoroutine(ExecuteAfterStop(movementController, unit, targetPosition));
        return true;
    }

    private IEnumerator ExecuteAfterStop(MovementController movementController, GameObject unit, Vector2Int targetPosition)
    {
        yield return null;

        // Utilise la position physique réelle au lieu d'une case arrondie
        Vector3 actualPosition = unit.transform.position;
        Debug.Log($"🔍 ExecuteAfterStop: Position physique = {actualPosition}"); // NOUVEAU
        Vector3 targetWorldPosition = GridManager.Instance.GridToWorld(targetPosition);
        float distanceToTarget = Vector3.Distance(actualPosition, targetWorldPosition);

        // Vérifie si déjà à destination (physiquement proche)
        Vector2Int currentGridPosition = GridManager.Instance.WorldToGrid(actualPosition);
        Debug.Log($"🔍 ExecuteAfterStop: Case grille = {currentGridPosition}, Cible = {targetPosition}"); // NOUVEAU
        if (currentGridPosition == targetPosition && distanceToTarget < 0.1f)
        {
            Debug.Log($"MovementManager: {unit.name} déjà à destination {targetPosition}");
            yield break;
        }

        // Calcule le chemin depuis la position physique réelle
        List<Vector2Int> path = PathFinding.FindPath(actualPosition, targetPosition, unit);

        if (path == null || path.Count == 0)
        {
            Debug.Log($"🔍 ExecuteAfterStop: Chemin calculé = {string.Join(" -> ", path)}"); // NOUVEAU
            Debug.LogWarning($"MovementManager: Pas de chemin trouvé pour {unit.name} vers {targetPosition}, lancement retry");
            movementController.StartRetryToDestination(targetPosition);
            yield break;
        }

        if (path.Count > 1)
        {
            Vector3 firstCellCenter = GridManager.Instance.GridToWorld(path[0]);
            float distanceFromFirstCell = Vector3.Distance(actualPosition, firstCellCenter);

            if (distanceFromFirstCell > 0.1f) // Si l'unité est à plus de 30% de la case
            {
                path.RemoveAt(0); // Retire la première case, l'unité ira directement vers la suivante
                Debug.Log($"🔍 Première case retirée, nouveau chemin : {string.Join(" -> ", path)}");
            }
        }

        // Lance le mouvement
        movementController.StartMovement(path);
        Debug.Log($"MovementManager: Mouvement démarré pour {unit.name}, {path.Count} étapes");
    }
}
