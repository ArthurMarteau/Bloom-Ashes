using System.Collections.Generic;
using UnityEngine;

// Service singleton pour orchestrer tous les mouvements d'unités
// Gère la coordination entre unités, évite les conflits et optimise les performances
// Interface principale pour demander des déplacements depuis l'extérieur
public class MovementService : MonoBehaviour
{
    [Header("Movement Service Settings")]
    [SerializeField] private int maxMovementsPerFrame = 5; // Limite de traitements par frame
    [SerializeField] private bool debugMovements = true;   // Affichage debug dans console

    // Instance singleton pour accès global
    public static MovementService Instance { get; private set; }

    // Dictionnaire des unités actuellement en mouvement
    // Clé = GameObject unité, Valeur = MovementController associé
    private Dictionary<GameObject, MovementController> activeMovements = new Dictionary<GameObject, MovementController>();

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
        public int priority;              // Priorité (0 = normal, plus haut = prioritaire)

        public MovementRequest(GameObject unit, Vector2Int target, int priority = 0)
        {
            this.unit = unit;
            this.targetPosition = target;
            this.priority = priority;
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
            Debug.Log("MovementService initialisé");
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
    public bool MoveUnitTo(GameObject unit, Vector2Int targetPosition, int priority = 0)
    {
        // Valide que l'unité existe et a un tag valide
        if (unit == null)
        {
            Debug.LogError("MovementService: Tentative de déplacer unité null");
            return false;
        }

        if (!UnitTypeHelper.IsValidUnit(unit))
        {
            Debug.LogWarning($"MovementService: {unit.name} n'a pas de tag d'unité valide");
            return false;
        }

        // Vérifie que la destination est dans les limites
        if (!GridManager.Instance.IsInBounds(targetPosition))
        {
            Debug.LogWarning($"MovementService: Destination {targetPosition} hors limites");
            return false;
        }

        // Vérifie que la destination est accessible pour cette unité
        if (!GridManager.Instance.IsWalkableFor(targetPosition, unit))
        {
            Debug.LogWarning($"MovementService: Destination {targetPosition} inaccessible pour {UnitTypeHelper.GetDisplayName(unit)}");
            return false;
        }

        // Si l'unité bouge déjà, arrête le mouvement précédent
        if (IsUnitMoving(unit))
        {
            StopMovement(unit);
        }

        // Ajoute la demande à la file d'attente
        MovementRequest request = new MovementRequest(unit, targetPosition, priority);
        movementQueue.Enqueue(request);

        if (debugMovements)
        {
            Debug.Log($"MovementService: Demande déplacement {unit.name} vers {targetPosition} (Queue: {movementQueue.Count})");
        }

        return true;
    }

    // Arrête immédiatement le mouvement d'une unité
    // Utilisé pour annulation manuelle ou interruption par collision
    // Nettoie les structures de données et notifie le contrôleur
    public void StopMovement(GameObject unit)
    {
        if (unit == null) return;

        // Retire de la liste des mouvements actifs
        if (activeMovements.TryGetValue(unit, out MovementController controller))
        {
            controller.StopMovement();
            activeMovements.Remove(unit);

            if (debugMovements)
            {
                Debug.Log($"MovementService: Arrêt mouvement {unit.name}");
            }
        }
    }

    // Vérifie si une unité est actuellement en déplacement
    public bool IsUnitMoving(GameObject unit)
    {
        return unit != null && activeMovements.ContainsKey(unit);
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
    private bool ExecuteMovementRequest(MovementRequest request)
    {
        GameObject unit = request.unit;
        Vector2Int targetPosition = request.targetPosition;

        // Récupère la position actuelle de l'unité
        Vector2Int currentPosition = GridManager.Instance.WorldToGrid(unit.transform.position);

        // Calcule le chemin via PathFinding
        List<Vector2Int> calculatedPath = PathFinding.FindPath(currentPosition, targetPosition, unit);

        if (calculatedPath.Count == 0)
        {
            if (debugMovements)
            {
                Debug.LogWarning($"MovementService: Aucun chemin trouvé pour {unit.name} vers {targetPosition}");
            }
            return false;
        }

        // Récupère ou ajoute le MovementController
        MovementController controller = unit.GetComponent<MovementController>();
        if (controller == null)
        {
            controller = unit.AddComponent<MovementController>();
            if (debugMovements)
            {
                Debug.Log($"MovementService: MovementController ajouté à {unit.name}");
            }
        }

        // Lance le mouvement
        bool movementStarted = controller.StartMovement(calculatedPath);

        if (movementStarted)
        {
            // Ajoute aux mouvements actifs
            activeMovements[unit] = controller;

            if (debugMovements)
            {
                Debug.Log($"MovementService: Mouvement démarré pour {unit.name}, {calculatedPath.Count} étapes");
            }
        }
        else
        {
            if (debugMovements)
            {
                Debug.LogWarning($"MovementService: Échec démarrage mouvement pour {unit.name}");
            }
        }

        return movementStarted;
    }

    // Callback appelé quand une unité termine son mouvement avec succès
    // Nettoie les structures de données et libère les ressources
    // Interface pour MovementController qui notifie la fin
    public void OnMovementCompleted(GameObject unit)
    {
        if (unit == null) return;

        // Retire de la liste des mouvements actifs
        activeMovements.Remove(unit);

        if (debugMovements)
        {
            Debug.Log($"MovementService: Mouvement complété pour {unit.name}");
        }
    }

    // Callback appelé quand un mouvement échoue (blocage, obstacle)
    // Gère les échecs et peut déclencher recalcul ou annulation
    // Interface pour MovementController qui notifie les problèmes
    public void OnMovementFailed(GameObject unit, string reason)
    {
        if (unit == null) return;

        // Retire de la liste des mouvements actifs
        activeMovements.Remove(unit);

        if (debugMovements)
        {
            Debug.LogWarning($"MovementService: Mouvement échoué pour {unit.name} - Raison: {reason}");
        }

        // TODO: Ici on pourrait implémenter logique de retry ou alternative
        // Par exemple: essayer un autre chemin, attendre, ou annuler définitivement
    }

    // Retourne le nombre d'unités actuellement en mouvement
    // Utilisé pour debug et optimisation des performances
    // Interface pour monitoring système
    public int GetActiveMovementCount()
    {
        return activeMovements.Count;
    }

    // Retourne le nombre de demandes en attente
    // Utilisé pour debug et optimisation des performances
    // Interface pour monitoring système
    public int GetQueuedMovementCount()
    {
        return movementQueue.Count;
    }

    // Arrête tous les mouvements en cours
    // Utilisé pour pause jeu, changement de scène, ou situations d'urgence
    // Nettoie complètement le système
    public void StopAllMovements()
    {
        // Arrête tous les mouvements actifs
        List<GameObject> unitsToStop = new List<GameObject>(activeMovements.Keys);
        foreach (GameObject unit in unitsToStop)
        {
            StopMovement(unit);
        }

        // Vide la file d'attente
        movementQueue.Clear();

        if (debugMovements)
        {
            Debug.Log("MovementService: Tous les mouvements arrêtés");
        }
    }

    // Méthode de debug pour afficher l'état du service
    [ContextMenu("Debug Movement Service State")]
    private void DebugServiceState()
    {
        Debug.Log($"=== MovementService Debug ===");
        Debug.Log($"Mouvements actifs: {activeMovements.Count}");
        Debug.Log($"Demandes en queue: {movementQueue.Count}");
        Debug.Log($"Processing queue: {processingQueue}");

        foreach (var kvp in activeMovements)
        {
            Debug.Log($"  {kvp.Key.name} -> {kvp.Value.GetCurrentState()}");
        }
    }
}
