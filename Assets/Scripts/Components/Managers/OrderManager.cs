using System.Collections.Generic;
using UnityEngine;

// Gère les ordres donnés aux unités sélectionnées
// Fait le lien entre SelectionManager et les controllers d'unités (MovementController, AttackController, etc.)
// Extensible pour différents types d'ordres (mouvement, attaque, patrouille, etc.)
// Consomme les events de InputManager — aucun accès direct à Input.*
public class OrderManager : MonoBehaviour
{
    public static OrderManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private SelectionManager selectionManager;

    [Header("Detection")]
    // LayerMask combinant Units + Buildings + Environment_Walkable pour la détection au clic droit
    [SerializeField] private LayerMask targetableLayer;

    // Référence à l'InputManager pour l'abonnement aux events
    private InputManager inputManager;

    // Cache anti-spam : ignore les ordres identiques consécutifs (1 slot)
    private GameObject lastTargetUnit; // Dernière unité/bâtiment ciblé (attaque ou chase)

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

        // Trouve les références si non assignées
        if (selectionManager == null)
        {
            selectionManager = FindAnyObjectByType<SelectionManager>();
            if (selectionManager == null)
            {
                GameLogger.Error("OrderManager: SelectionManager non trouvé !", this);
            }
        }

        GameLogger.Log(GameLogger.Category.Selection, "OrderManager initialisé", this);
    }

    // S'abonne aux events de InputManager pour découpler la lecture d'input
    private void Start()
    {
        inputManager = InputManager.Instance;
        if (inputManager == null)
        {
            GameLogger.Error("OrderManager: InputManager introuvable dans la scène !", this);
            return;
        }

        // Clic droit = ordre de mouvement, d'attaque ou de chase selon la cible
        inputManager.OnRightClickDown.AddListener(HandleRightClickOrder);
    }

    // Traite les ordres de mouvement, chase ou attaque via clic droit
    // Reçoit la position monde depuis InputManager (Z=0 pour la grille 2D)
    // Détecte automatiquement : ennemi = attaque, allié/neutre = chase, case vide = mouvement
    private void HandleRightClickOrder(Vector3 worldPosition)
    {
        // Vérifie qu'il y a des unités sélectionnées
        if (selectionManager == null || !selectionManager.HasUnitsSelected())
        {
            GameLogger.Log(GameLogger.Category.Selection, "OrderManager: Aucune unité sélectionnée pour ordre", this);
            return;
        }

        // Vérifie s'il y a une entité ciblable sous le curseur (unité ou bâtiment) via overlap 2D
        Collider2D targetCollider = Physics2D.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y), targetableLayer);
        Vector2Int debugCell = GridManager.Instance.WorldToGrid(worldPosition);
        GameLogger.Log(GameLogger.Category.Selection, $"🎯 OrderManager: clic monde {worldPosition} → case {debugCell}, collider détecté: {(targetCollider != null ? targetCollider.gameObject.name : "aucun")}", this);

        if (targetCollider != null)
        {
            // Entité détectée → Vérifie si ennemie ou alliée
            HandleUnitTargetOrder(targetCollider.gameObject);
        }
        else
        {
            // Pas d'unité → Ordre de mouvement classique
            HandleSimpleMovementOrder(worldPosition);
        }
    }

    // Gère l'ordre sur une entité cible (unité ou bâtiment)
    // Attaque si ennemi (via IDamageable + ITeamMember), chase si allié/neutre
    // Détermine automatiquement le comportement selon la relation d'équipe
    private void HandleUnitTargetOrder(GameObject targetUnit)
    {
        // Anti-spam : ignore si même cible que le dernier ordre
        if (targetUnit == lastTargetUnit)
        {
            return;
        }

        // Mémorise la nouvelle cible
        lastTargetUnit = targetUnit;

        var selectedUnits = selectionManager.GetSelectedUnits();

        // Filtre équipe : en mode multijoueur, ne traite que les unités du joueur local
        if (GameManager.Instance != null && !GameManager.Instance.DevModeAllTeams && PlayerManager.Instance != null)
            selectedUnits = selectedUnits.FindAll(u => {
                ITeamMember tm = u.GetComponent<ITeamMember>();
                return tm != null && PlayerManager.Instance.IsLocalPlayer(tm.GetTeamID());
            });

        int attackOrders = 0;
        int chaseOrders = 0;

        Vector2Int targetGridPos = GridManager.Instance.WorldToGrid(targetUnit.transform.position);
        ITeamMember targetTM = targetUnit.GetComponent<ITeamMember>();

        // Multi-target dispatch range-aware : groupage par range effectif.
        // Attaque (ennemi) -> range r�el de l'unit�. Chase (alli�/neutre) -> range=1 forc� (adjacent).
        Dictionary<GameObject, Vector2Int> approachAssignment = new Dictionary<GameObject, Vector2Int>();
        if (selectedUnits.Count > 1)
        {
            Dictionary<int, List<GameObject>> byRange = new Dictionary<int, List<GameObject>>();
            foreach (GameObject u in selectedUnits)
            {
                ITeamMember utm = u.GetComponent<ITeamMember>();
                bool isEnemyTarget = utm != null && targetTM != null && utm.IsEnemy(targetTM);

                int effectiveRange;
                if (isEnemyTarget)
                {
                    UnitStatsComponent stats = u.GetComponent<UnitStatsComponent>();
                    effectiveRange = stats != null ? stats.GetAttackRange() : 1;
                }
                else
                {
                    // Chase : adjacent, peu importe le range d'attaque
                    effectiveRange = 1;
                }

                if (!byRange.ContainsKey(effectiveRange)) byRange[effectiveRange] = new List<GameObject>();
                byRange[effectiveRange].Add(u);
            }

            HashSet<Vector2Int> globallyTaken = new HashSet<Vector2Int>();
            foreach (var kvp in byRange)
            {
                int range = kvp.Key;
                List<GameObject> unitsInGroup = kvp.Value;

                List<Vector2Int> candidateCells = DispatchHelper.ComputeRangeApproachCells(
                    targetGridPos, range, unitsInGroup.Count * 2 + 4, unitsInGroup[0]);

                candidateCells.Remove(targetGridPos);
                candidateCells.RemoveAll(c => globallyTaken.Contains(c));

                Dictionary<GameObject, Vector2Int> groupAssignment = DispatchHelper.AssignCellsGreedy(unitsInGroup, candidateCells, targetGridPos);
                foreach (var pair in groupAssignment)
                {
                    approachAssignment[pair.Key] = pair.Value;
                    globallyTaken.Add(pair.Value);
                }
            }
        }

        foreach (GameObject unit in selectedUnits)
        {
            MovementController movementController = unit.GetComponent<MovementController>();
            if (movementController != null && !movementController.enabled) continue;

            // Ne peut pas se cibler soi-même
            if (unit == targetUnit)
            {
                continue;
            }

            // Récupère l'appartenance d'équipe via ITeamMember (unités ET bâtiments futurs)
            ITeamMember unitTeamMember = unit.GetComponent<ITeamMember>();
            if (unitTeamMember == null)
            {
                GameLogger.Warn(GameLogger.Category.Selection, $"OrderManager: {unit.name} n'a pas d'ITeamMember (TeamComponent manquant ?)", this);
                continue;
            }

            // Vérifie si la cible est ennemie via ITeamMember
            ITeamMember targetTeamMember = targetUnit.GetComponent<ITeamMember>();
            if (unitTeamMember.IsEnemy(targetTeamMember))
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

                    if (approachAssignment.TryGetValue(unit, out Vector2Int assignedAttackCell) && movementController != null)
                    {
                        if (movementController.GetCurrentGridPosition() != assignedAttackCell)
                        {
                            UnityEngine.Events.UnityAction startAttackOnArrive = null;
                            startAttackOnArrive = () =>
                            {
                                movementController.OnArrived.RemoveListener(startAttackOnArrive);
                                if (movementController.GetCurrentGridPosition() == assignedAttackCell && targetUnit != null && attackController != null) attackController.StartAttack(targetUnit);
                            };
                            movementController.OnArrived.AddListener(startAttackOnArrive);

                            if (movementController.MoveTo(assignedAttackCell))
                            {
                                attackOrders++;
                                continue;
                            }

                            movementController.OnArrived.RemoveListener(startAttackOnArrive);
                        }
                    }

                    attackController.StartAttack(targetUnit);
                    attackOrders++;
                }
                else
                {
                    GameLogger.Warn(GameLogger.Category.Selection, $"OrderManager: {unit.name} n'a pas de AttackController", this);
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

                    if (approachAssignment.TryGetValue(unit, out Vector2Int assignedChaseCell) && movementController != null)
                    {
                        if (movementController.GetCurrentGridPosition() != assignedChaseCell)
                        {
                            UnityEngine.Events.UnityAction startChaseOnArrive = null;
                            startChaseOnArrive = () =>
                            {
                                movementController.OnArrived.RemoveListener(startChaseOnArrive);
                                if (movementController.GetCurrentGridPosition() == assignedChaseCell && targetUnit != null && chaseController != null) chaseController.StartChase(targetUnit);
                            };
                            movementController.OnArrived.AddListener(startChaseOnArrive);

                            if (movementController.MoveTo(assignedChaseCell))
                            {
                                chaseOrders++;
                                continue;
                            }

                            movementController.OnArrived.RemoveListener(startChaseOnArrive);
                        }
                    }

                    chaseController.StartChase(targetUnit);
                    chaseOrders++;
                }
                else
                {
                    GameLogger.Warn(GameLogger.Category.Selection, $"OrderManager: {unit.name} n'a pas de ChaseController", this);
                }
            }
        }

        if (attackOrders > 0)
        {
            GameLogger.Log(GameLogger.Category.Selection, $"OrderManager: Ordre d'attaque sur {targetUnit.name} : {attackOrders}/{selectedUnits.Count} unités", this);
        }
        if (chaseOrders > 0)
        {
            GameLogger.Log(GameLogger.Category.Selection, $"OrderManager: Ordre de chase sur {targetUnit.name} : {chaseOrders}/{selectedUnits.Count} unités", this);
        }
    }

    // Gère l'ordre de mouvement simple vers une position monde
    // Reçoit directement la position monde depuis HandleRightClickOrder
    private void HandleSimpleMovementOrder(Vector3 worldPosition)
    {
        // Convertit la position monde en coordonnées de grille
        Vector2Int targetGridPosition = GridManager.Instance.WorldToGrid(worldPosition);

        // Vérifie que la destination est dans les limites
        if (!GridManager.Instance.IsInBounds(targetGridPosition))
        {
            GameLogger.Warn(GameLogger.Category.Selection, $"OrderManager: Ordre de mouvement hors limites : {targetGridPosition}", this);
            return;
        }

        // Invalide le cache cible unité
        lastTargetUnit = null;

        // Envoie l'ordre de mouvement à toutes les unités sélectionnées
        var selectedUnits = selectionManager.GetSelectedUnits();

        // Filtre équipe : en mode multijoueur, ne traite que les unités du joueur local
        if (GameManager.Instance != null && !GameManager.Instance.DevModeAllTeams && PlayerManager.Instance != null)
            selectedUnits = selectedUnits.FindAll(u => {
                ITeamMember tm = u.GetComponent<ITeamMember>();
                return tm != null && PlayerManager.Instance.IsLocalPlayer(tm.GetTeamID());
            });

        int successfulOrders = 0;

        // Multi-target dispatch : si plusieurs unit�s, chacune re�oit une case distincte en spirale autour de la cible
        if (selectedUnits.Count > 1)
        {
            List<Vector2Int> dispatchCells = DispatchHelper.ComputeSpiral(targetGridPosition, selectedUnits.Count * 2 + 4, selectedUnits[0]);
            Dictionary<GameObject, Vector2Int> assignment = DispatchHelper.AssignCellsGreedy(selectedUnits, dispatchCells, targetGridPosition);

            foreach (GameObject unit in selectedUnits)
            {
                MovementController movementController = unit.GetComponent<MovementController>();
                if (movementController == null || !movementController.enabled) continue;

                // Annule la chase
                ChaseController chaseController = unit.GetComponent<ChaseController>();
                if (chaseController != null && chaseController.IsChasing())
                {
                    chaseController.StopChase();
                }

                // Annule l'attaque + flag manuel
                AttackController attackController = unit.GetComponent<AttackController>();
                if (attackController != null)
                {
                    if (attackController.IsAttacking()) attackController.StopAttack();
                    attackController.SetManualOrder(true);
                }

                // R�cup�re la case assign�e (fallback : case cible si dispatch a �chou� pour cette unit�)
                Vector2Int targetForThisUnit = assignment.TryGetValue(unit, out Vector2Int assignedCell)
                    ? assignedCell
                    : targetGridPosition;

                if (movementController.MoveTo(targetForThisUnit))
                {
                    successfulOrders++;
                }
            }

            GameLogger.Log(GameLogger.Category.Selection, $"OrderManager: Ordre group� vers {targetGridPosition} : {successfulOrders}/{selectedUnits.Count} unit�s dispatch�es sur {dispatchCells.Count} cases", this);
        }
        else
        {
            // Unit� unique : ordre direct sans dispatch
            foreach (GameObject unit in selectedUnits)
            {
                MovementController movementController = unit.GetComponent<MovementController>();
                if (movementController == null || !movementController.enabled) continue;

                ChaseController chaseController = unit.GetComponent<ChaseController>();
                if (chaseController != null && chaseController.IsChasing())
                {
                    chaseController.StopChase();
                }

                AttackController attackController = unit.GetComponent<AttackController>();
                if (attackController != null)
                {
                    if (attackController.IsAttacking()) attackController.StopAttack();
                    attackController.SetManualOrder(true);
                }

                if (movementController.MoveTo(targetGridPosition))
                {
                    successfulOrders++;
                }
            }

            GameLogger.Log(GameLogger.Category.Selection, $"OrderManager: Ordre de mouvement vers {targetGridPosition} : {successfulOrders}/{selectedUnits.Count} unit�s", this);
        }
    }

    // Nettoie les listeners lors de la destruction pour éviter les fuites mémoire
    private void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnRightClickDown.RemoveListener(HandleRightClickOrder);
        }
    }
}
