using UnityEngine;

// Gère les ordres donnés aux unités sélectionnées
// Fait le lien entre SelectionManager et les controllers d'unités (MovementController, AttackController, etc.)
// Extensible pour différents types d'ordres (mouvement, attaque, patrouille, etc.)
public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private SelectionManager selectionManager;

    private Camera mainCamera;

    // Initialise le singleton et les références
    private void Awake()
    {
        // Pattern Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Trouve la caméra principale
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("OrderManager: Pas de caméra principale trouvée !");
        }

        // Trouve les références si non assignées
        if (selectionManager == null)
        {
            selectionManager = FindAnyObjectByType<SelectionManager>();
            if (selectionManager == null)
            {
                Debug.LogError("OrderManager: SelectionManager non trouvé !");
            }
        }

        Debug.Log("OrderManager initialisé");
    }

    // Écoute les inputs d'ordres chaque frame
    private void Update()
    {
        HandleOrderInput();
    }

    // Point d'entrée pour gérer tous les types d'ordres
    // Actuellement : clic droit = mouvement
    // Extensible pour : touche A = attaque, P = patrouille, etc.
    private void HandleOrderInput()
    {
        // Clic droit = ordre de mouvement
        if (Input.GetMouseButtonDown(1))
        {
            HandleMovementOrder();
        }
    }

    // Traite les ordres de mouvement, chase ou attaque via clic droit
    // Détecte automatiquement : ennemi = attaque, allié/neutre = chase, case vide = mouvement
    private void HandleMovementOrder()
    {
        // Vérifie qu'il y a des unités sélectionnées
        if (selectionManager == null || !selectionManager.HasUnitsSelected())
        {
            Debug.Log("OrderManager: Aucune unité sélectionnée pour ordre");
            return;
        }

        // Convertit la position de la souris en rayon 3D
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Vérifie d'abord s'il y a une unité sous le curseur
        RaycastHit2D unitHit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, 1 << 8); // Layer Units

        if (unitHit.collider != null)
        {
            // Unité détectée → Vérifie si ennemi ou allié
            GameObject targetUnit = unitHit.collider.gameObject;
            HandleUnitTargetOrder(targetUnit);
        }
        else
        {
            // Pas d'unité → Ordre de mouvement classique
            HandleSimpleMovementOrder();
        }
    }

    // Gère l'ordre sur une unité cible (attaque si ennemi, chase si allié/neutre)
    // Détermine automatiquement le comportement selon la relation d'équipe
    private void HandleUnitTargetOrder(GameObject targetUnit)
    {
        var selectedUnits = selectionManager.GetSelectedUnits();
        int attackOrders = 0;
        int chaseOrders = 0;

        foreach (GameObject unit in selectedUnits)
        {
            // Ne peut pas se cibler soi-même
            if (unit == targetUnit)
            {
                continue;
            }

            // Récupère le TeamComponent de l'unité
            TeamComponent teamComponent = unit.GetComponent<TeamComponent>();
            if (teamComponent == null)
            {
                Debug.LogWarning($"OrderManager: {unit.name} n'a pas de TeamComponent");
                continue;
            }

            // Vérifie si la cible est ennemie
            if (teamComponent.IsEnemy(targetUnit))
            {
                // ENNEMI → Ordre d'attaque
                AttackController attackController = unit.GetComponent<AttackController>();
                if (attackController != null)
                {
                    // Arrête la chase si active
                    ChaseController chaseController = unit.GetComponent<ChaseController>();
                    if (chaseController != null && chaseController.IsChasing())
                    {
                        chaseController.StopChase();
                    }

                    attackController.StartAttack(targetUnit);
                    attackOrders++;
                }
                else
                {
                    Debug.LogWarning($"OrderManager: {unit.name} n'a pas de AttackController");
                }
            }
            else
            {
                // ALLIÉ/NEUTRE → Ordre de chase
                ChaseController chaseController = unit.GetComponent<ChaseController>();
                if (chaseController != null)
                {
                    // Arrête l'attaque si active
                    AttackController attackController = unit.GetComponent<AttackController>();
                    if (attackController != null && attackController.IsAttacking())
                    {
                        attackController.StopAttack();
                    }

                    chaseController.StartChase(targetUnit);
                    chaseOrders++;
                }
                else
                {
                    Debug.LogWarning($"OrderManager: {unit.name} n'a pas de ChaseController");
                }
            }
        }

        if (attackOrders > 0)
        {
            Debug.Log($"OrderManager: Ordre d'attaque sur {targetUnit.name} : {attackOrders}/{selectedUnits.Count} unités");
        }
        if (chaseOrders > 0)
        {
            Debug.Log($"OrderManager: Ordre de chase sur {targetUnit.name} : {chaseOrders}/{selectedUnits.Count} unités");
        }
    }

    // Gère l'ordre de mouvement simple vers une position
    private void HandleSimpleMovementOrder()
    {
        // Convertit la position de la souris en coordonnées de grille
        Vector3 mouseWorldPosition = GetMouseWorldPosition();
        Vector2Int targetGridPosition = GridManager.Instance.WorldToGrid(mouseWorldPosition);

        // Vérifie que la destination est dans les limites
        if (!GridManager.Instance.IsInBounds(targetGridPosition))
        {
            Debug.LogWarning($"OrderManager: Ordre de mouvement hors limites : {targetGridPosition}");
            return;
        }

        // Envoie l'ordre de mouvement à toutes les unités sélectionnées
        var selectedUnits = selectionManager.GetSelectedUnits();
        int successfulOrders = 0;

        foreach (GameObject unit in selectedUnits)
        {
            // Annule la chase en cours si existe
            ChaseController chaseController = unit.GetComponent<ChaseController>();
            if (chaseController != null && chaseController.IsChasing())
            {
                chaseController.StopChase();
            }

            // Annule l'attaque en cours si existe
            AttackController attackController = unit.GetComponent<AttackController>();
            if (attackController != null && attackController.IsAttacking())
            {
                attackController.StopAttack();
            }

            // Envoie l'ordre de mouvement directement au MovementController
            MovementController movementController = unit.GetComponent<MovementController>();
            if (movementController != null)
            {
                bool orderSuccessful = movementController.MoveTo(targetGridPosition);
                if (orderSuccessful)
                {
                    successfulOrders++;
                }
            }
            else
            {
                Debug.LogWarning($"OrderManager: {unit.name} n'a pas de MovementController");
            }
        }

        Debug.Log($"OrderManager: Ordre de mouvement vers {targetGridPosition} : {successfulOrders}/{selectedUnits.Count} unités");
    }

    // Convertit la position de la souris en position monde
    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPosition = Input.mousePosition;
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        mouseWorldPosition.z = 0f;
        return mouseWorldPosition;
    }
}