using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// Gère la sélection d'objets avec clic simple et rectangle de sélection
// Glue MonoBehaviour : input, raycasts, carrés visuels, events UI et tracking destruction
public class SelectionManager : MonoBehaviour, ICellListener
{
    [Header("Selection Settings")]
    [SerializeField] private LayerMask unitLayer = 1 << 9;
    [SerializeField] private LayerMask buildingLayer = 1 << 8;
    [SerializeField] private LayerMask environmentWalkableLayer = 1 << 7;
    [SerializeField] private LayerMask obstacleLayer = 1 << 6;
    [SerializeField] private LayerMask groundLayer = 1 << 3;
    private const int MAX_SELECTED_UNITS = 10;

    [Header("Selection Square")]
    [SerializeField] private GameObject selectionSquarePrefab;

    private readonly SelectionState state = new SelectionState();
    private readonly Dictionary<GameObject, SelectionSquare> squares = new Dictionary<GameObject, SelectionSquare>();
    private SelectionSquare groundSquare;
    private LayerMask combinedSelectableMask;

    private bool isDragging;
    private Vector3 dragStartPosition;
    private Vector3 lastDragScreenPosition;
    private LineRenderer rectangleRenderer;

    private Camera mainCamera;
    private InputManager inputManager;
    private readonly List<Vector2Int> watchedSelectionCells = new List<Vector2Int>();
    private Vector2Int? selectionAnchorCell;
    private bool preserveSelectionAnchor;
    private bool watchedSelectionFromAutoSwitch;

    [Header("Events — UI")]
    public UnityEvent<List<GameObject>>    OnUnitsSelected      = new UnityEvent<List<GameObject>>();
    public UnityEvent<Vector2Int>          OnGroundSelected     = new UnityEvent<Vector2Int>();
    public UnityEvent<GameObject>          OnObstacleSelected   = new UnityEvent<GameObject>();
    public UnityEvent<GameObject>          OnBuildingSelected   = new UnityEvent<GameObject>();
    public UnityEvent<GameObject>          OnEnvironmentSelected = new UnityEvent<GameObject>();
    public UnityEvent                      OnSelectionCleared   = new UnityEvent();

    // Initialise les références, masks et composants visuels
    private void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameLogger.Error("❌ SelectionManager nécessite une caméra principale dans la scène !", this);
        }

        if (selectionSquarePrefab == null)
        {
            GameLogger.Error("❌ SelectionManager nécessite un prefab SelectionSquare assigné !", this);
        }

        combinedSelectableMask = unitLayer | buildingLayer | environmentWalkableLayer | obstacleLayer;
        SetupRectangleRenderer();
    }

    // S'abonne aux events de InputManager et EntitySpawnManager
    private void Start()
    {
        inputManager = InputManager.Instance;
        if (inputManager == null)
        {
            GameLogger.Error("❌ SelectionManager: InputManager introuvable dans la scène !", this);
            return;
        }

        inputManager.OnLeftClickDown.AddListener(OnLeftClickDownHandler);
        inputManager.OnDragStart.AddListener(OnDragStartHandler);
        inputManager.OnDragUpdate.AddListener(OnDragUpdateHandler);
        inputManager.OnLeftClickUp.AddListener(OnLeftClickUpHandler);
        inputManager.OnDragEnd.AddListener(OnDragEndHandler);

        if (EntitySpawnManager.Instance != null)
        {
            EntitySpawnManager.Instance.OnEntitySpawned.AddListener(HandleEntitySpawned);
            EntitySpawnManager.Instance.OnEntitySpawnCompleted.AddListener(HandleEntitySpawnCompleted);
            EntitySpawnManager.Instance.OnEntityReplaced.AddListener(HandleEntityReplaced);
            EntitySpawnManager.Instance.OnEntityDespawned.AddListener(HandleEntityDespawned);
            EntitySpawnManager.Instance.OnEntityDestroyed.AddListener(HandleEntityDestroyed);
        }
    }

    // Configure le LineRenderer pour dessiner le rectangle de sélection
    private void SetupRectangleRenderer()
    {
        GameObject rectangleObject = new GameObject("SelectionRectangle");
        rectangleObject.transform.SetParent(transform);

        rectangleRenderer = rectangleObject.AddComponent<LineRenderer>();
        rectangleRenderer.material = new Material(Shader.Find("Sprites/Default"));
        rectangleRenderer.startColor = Color.white;
        rectangleRenderer.startWidth = 0.05f;
        rectangleRenderer.endWidth = 0.05f;
        rectangleRenderer.positionCount = 5;
        rectangleRenderer.useWorldSpace = true;
        rectangleRenderer.sortingOrder = 100;
        rectangleRenderer.enabled = false;
    }

    // Clic gauche enfoncé — réinitialise l'état du drag
    private void OnLeftClickDownHandler(Vector3 worldPosition)
    {
        isDragging = false;
        if (GameLogger.IsEnabled(GameLogger.Category.Selection))
            GameLogger.Log(GameLogger.Category.Selection, $"🎯 START Selection — monde {worldPosition}", this);
    }

    // Début du drag — enregistre la position initiale et active le rectangle
    private void OnDragStartHandler(Vector3 screenPosition)
    {
        if (IsPointerOverUserInterface(screenPosition))
        {
            isDragging = false;
            GameLogger.Log(GameLogger.Category.Selection, "🧱 SelectionManager : drag ignoré car démarré sur l'UI", this);
            return;
        }

        dragStartPosition = screenPosition;
        lastDragScreenPosition = screenPosition;
        isDragging = true;
        rectangleRenderer.enabled = true;

        GameLogger.Log(GameLogger.Category.Selection, "🟢 Mode rectangle ACTIVÉ !", this);
    }

    // Drag en cours — met à jour la position courante et redessine le rectangle
    private void OnDragUpdateHandler(Vector3 screenPosition)
    {
        if (!isDragging)
        {
            return;
        }

        lastDragScreenPosition = screenPosition;
        GameLogger.Log(GameLogger.Category.Selection, "🎨 Dessin du rectangle...", this);
        DrawSelectionRectangle();
    }

    // Clic simple relâché — sélectionne l'objet sous le curseur
    private void OnLeftClickUpHandler(Vector3 screenPosition)
    {
        if (IsPointerOverUserInterface(screenPosition))
        {
            return;
        }

        HandleSimpleClick(screenPosition);
    }

    // Fin du drag — complète la sélection rectangle et masque le rectangle
    private void OnDragEndHandler()
    {
        if (!isDragging)
        {
            return;
        }

        CompleteRectangleSelection();
        isDragging = false;
        rectangleRenderer.enabled = false;
    }

    // Traite la sélection par clic simple avec priorité Unit > Building > Obstacle > Ground
    private void HandleSimpleClick(Vector3 screenPosition)
    {
        if (mainCamera == null)
        {
            return;
        }

        Vector3 clickWorldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        clickWorldPosition.z = 0f;
        Vector2Int? clickedCell = TryWorldToGridCell(clickWorldPosition);

        Collider2D[] hits = Physics2D.OverlapPointAll(clickWorldPosition, combinedSelectableMask);
        GameObject bestHit = SelectionResolver.ResolveBestHit(hits);

        if (bestHit != null)
        {
            SelectionType selectionType = SelectionResolver.Classify(bestHit, out GameObject entity);
            SetManualSelectionAnchor(clickedCell);
            ApplySelection(selectionType, entity, Vector2Int.zero, clickWorldPosition, inputManager != null && inputManager.IsShiftHeld);
            return;
        }

        SetManualSelectionAnchor(clickedCell);
        HandleGroundSelection(clickWorldPosition);
    }

    // Traite la sélection de terrain à partir d'une position mondiale
    private void HandleGroundSelection(Vector3 worldPosition)
    {
        if (GridManager.Instance == null)
        {
            ClearAllSelection();
            return;
        }

        Vector2Int gridCoordinates = GridManager.Instance.WorldToGrid(worldPosition);

        if (GridManager.Instance.IsInBounds(gridCoordinates))
        {
            ApplySelection(SelectionType.Ground, null, gridCoordinates, worldPosition, false);
        }
        else
        {
            ClearAllSelection();
        }
    }

    // Finalise la sélection rectangle en récupérant uniquement les vraies unités dans la zone
    private void CompleteRectangleSelection()
    {
        if (mainCamera == null)
        {
            return;
        }

        Vector3 startWorld = mainCamera.ScreenToWorldPoint(dragStartPosition);
        Vector3 endWorld = mainCamera.ScreenToWorldPoint(lastDragScreenPosition);
        startWorld.z = endWorld.z = 0f;

        Vector2 bottomLeft = new Vector2(Mathf.Min(startWorld.x, endWorld.x), Mathf.Min(startWorld.y, endWorld.y));
        Vector2 topRight = new Vector2(Mathf.Max(startWorld.x, endWorld.x), Mathf.Max(startWorld.y, endWorld.y));
        Collider2D[] unitsInArea = Physics2D.OverlapAreaAll(bottomLeft, topRight, unitLayer);

        Vector2 dragCenter = (bottomLeft + topRight) * 0.5f;
        List<GameObject> selectableUnits = new List<GameObject>();
        foreach (Collider2D unitCollider in unitsInArea)
        {
            if (unitCollider != null && SelectionResolver.IsSelectableUnit(unitCollider.gameObject) && !selectableUnits.Contains(unitCollider.gameObject))
            {
                selectableUnits.Add(unitCollider.gameObject);
            }
        }

        selectableUnits.Sort((a, b) =>
        {
            float distanceA = ((Vector2)a.transform.position - dragCenter).sqrMagnitude;
            float distanceB = ((Vector2)b.transform.position - dragCenter).sqrMagnitude;
            return distanceA.CompareTo(distanceB);
        });

        if (selectableUnits.Count > MAX_SELECTED_UNITS)
        {
            if (GameLogger.IsEnabled(GameLogger.Category.Selection))
                GameLogger.Log(GameLogger.Category.Selection, $"⚠️ Drag-select : {selectableUnits.Count} unités valides dans la zone → cap à {MAX_SELECTED_UNITS} (plus proches du centre)", this);
            selectableUnits.RemoveRange(MAX_SELECTED_UNITS, selectableUnits.Count - MAX_SELECTED_UNITS);
        }

        ApplyUnitListSelection(selectableUnits);
        if (GameLogger.IsEnabled(GameLogger.Category.Selection))
            GameLogger.Log(GameLogger.Category.Selection, $"📦 Rectangle sélection : {state.Units.Count} unités sélectionnées (zone contenait {unitsInArea.Length})", this);
    }

    // Applique une transition de sélection unique ou unité depuis clic ou API externe
    private void ApplySelection(SelectionType selectionType, GameObject entity, Vector2Int gridPosition, Vector3? worldClickPosition, bool additive)
    {
        switch (selectionType)
        {
            case SelectionType.Units:
                ApplyUnitSelection(entity, additive);
                break;
            case SelectionType.Building:
                state.SetSingle(SelectionType.Building, entity);
                break;
            case SelectionType.Environment:
                state.SetSingle(SelectionType.Environment, entity);
                break;
            case SelectionType.Obstacle:
                state.SetSingle(SelectionType.Obstacle, entity);
                break;
            case SelectionType.Ground:
                state.SetGround(gridPosition);
                break;
            default:
                state.Clear();
                break;
        }

        SynchronizeSelectionVisuals(worldClickPosition);
        FireSelectionEvent();
        if (state.Type == SelectionType.Units && state.Units.Count != 1)
            ClearSelectionAnchor();
        UpdateWatchedSelectionCell(worldClickPosition);
    }

    // Applique la règle Shift+clic pour une unité
    private void ApplyUnitSelection(GameObject unit, bool additive)
    {
        if (unit == null || !SelectionResolver.IsSelectableUnit(unit))
        {
            state.Clear();
            return;
        }

        if (additive)
        {
            if (state.Type == SelectionType.Units && state.Contains(unit))
            {
                state.RemoveUnit(unit);
                GameLogger.Log(GameLogger.Category.Selection, $"➖ Unité désélectionnée : {unit.name} (Total: {state.Units.Count})", this);
                return;
            }

            bool added = state.AddUnit(unit, MAX_SELECTED_UNITS);
            if (!added)
            {
                GameLogger.Log(GameLogger.Category.Selection, $"⚠️ Sélection plafonnée à {MAX_SELECTED_UNITS} unités — {unit.name} ignorée", this);
                return;
            }

            GameLogger.Log(GameLogger.Category.Selection, $"➕ Unité sélectionnée : {unit.name} (Total: {state.Units.Count})", this);
            return;
        }

        state.SetUnits(new[] { unit });
        GameLogger.Log(GameLogger.Category.Selection, $"✅ Unité sélectionnée : {unit.name} (Total: {state.Units.Count})", this);
    }

    // Remplace la sélection par une liste d'unités issue du rectangle
    private void ApplyUnitListSelection(List<GameObject> units)
    {
        ClearSelectionAnchor();
        state.SetUnits(units);
        SynchronizeSelectionVisuals(null);
        FireSelectionEvent();
        UpdateWatchedSelectionCell(null);
    }

    // Synchronise les carrés de sélection avec l'état courant
    private void SynchronizeSelectionVisuals(Vector3? worldClickPosition)
    {
        List<GameObject> desiredEntities = GetSelectedEntitiesForSquares();

        List<GameObject> existingEntities = new List<GameObject>(squares.Keys);
        foreach (GameObject existingEntity in existingEntities)
        {
            if (existingEntity == null || !desiredEntities.Contains(existingEntity))
            {
                RemoveSelectionSquare(existingEntity);
            }
        }

        if (worldClickPosition.HasValue && state.Single != null)
        {
            IMultiTile multiTile = state.Single.GetComponent<IMultiTile>();
            if (multiTile != null && squares.TryGetValue(state.Single, out SelectionSquare existingSquare))
            {
                Vector2Int clickedTile = GridManager.Instance.WorldToGrid(worldClickPosition.Value);
                existingSquare.AttachToGridPosition(clickedTile);
            }
        }

        foreach (GameObject selectedEntity in desiredEntities)
        {
            if (selectedEntity != null && !squares.ContainsKey(selectedEntity))
            {
                CreateEntitySelectionSquare(selectedEntity, worldClickPosition);
            }
        }

        if (state.Type == SelectionType.Ground)
        {
            EnsureGroundSelectionSquare(state.Ground);
        }
        else
        {
            RemoveGroundSelectionSquare();
        }
    }

    // Retourne les entités devant avoir un carré de sélection
    private List<GameObject> GetSelectedEntitiesForSquares()
    {
        List<GameObject> desiredEntities = new List<GameObject>();

        if (state.Type == SelectionType.Units)
        {
            foreach (GameObject unit in state.Units)
            {
                desiredEntities.Add(unit);
            }
        }
        else if (state.Type == SelectionType.Building || state.Type == SelectionType.Environment || state.Type == SelectionType.Obstacle)
        {
            desiredEntities.Add(state.Single);
        }

        return desiredEntities;
    }

    // Crée un carré de sélection attaché à une entité
    private void CreateEntitySelectionSquare(GameObject entity, Vector3? worldClickPosition)
    {
        SelectionSquare selectionSquare = CreateSelectionSquare();
        if (selectionSquare == null)
        {
            return;
        }

        IMultiTile multiTile = entity.GetComponent<IMultiTile>();
        if (multiTile != null && worldClickPosition.HasValue)
        {
            Vector2Int clickedTile = GridManager.Instance.WorldToGrid(worldClickPosition.Value);
            selectionSquare.AttachToGridPosition(clickedTile);
        }
        else
        {
            selectionSquare.AttachToGameObject(entity);
        }

        squares[entity] = selectionSquare;
    }

    // Crée ou remplace le carré de sélection de terrain
    private void EnsureGroundSelectionSquare(Vector2Int gridPosition)
    {
        RemoveGroundSelectionSquare();

        groundSquare = CreateSelectionSquare();
        if (groundSquare != null)
        {
            groundSquare.AttachToGridPosition(gridPosition);
        }
    }

    // Détruit le carré de sélection d'une entité
    private void RemoveSelectionSquare(GameObject entity)
    {
        if (squares.TryGetValue(entity, out SelectionSquare selectionSquare) && selectionSquare != null)
        {
            selectionSquare.DestroySelectionSquare();
        }

        squares.Remove(entity);
    }

    // Détruit le carré de sélection de terrain
    private void RemoveGroundSelectionSquare()
    {
        if (groundSquare != null)
        {
            groundSquare.DestroySelectionSquare();
            groundSquare = null;
        }
    }

    // Réagit à la destruction d'une entité sélectionnée
    private void HandleTrackedEntityDestroyed(GameObject destroyedEntity)
    {
        if (destroyedEntity == null)
        {
            return;
        }

        Vector3 lastPosition = destroyedEntity.transform.position;
        RemoveSelectionSquare(destroyedEntity);

        if (state.Type == SelectionType.Units)
        {
            state.RemoveUnit(destroyedEntity);
            if (state.Units.Count > 0)
            {
                SynchronizeSelectionVisuals(null);
                OnUnitsSelected.Invoke(GetSelectedUnits());
                GameLogger.Log(GameLogger.Category.Selection, $"💀 SelectionManager : entité retirée de la sélection, {state.Units.Count} restantes", this);
                return;
            }
        }
        else if (state.Single == destroyedEntity)
        {
            state.Clear();
        }

        FallbackToGroundAfterDestruction(lastPosition, destroyedEntity);
    }

    private void HandleEntityDestroyed(GameObject destroyedEntity)
    {
        if (destroyedEntity == null || !state.Contains(destroyedEntity)) return;
        HandleTrackedEntityDestroyed(destroyedEntity);
    }

    // Fallback vers le terrain sous la dernière position connue
    private void FallbackToGroundAfterDestruction(Vector3 lastPosition, GameObject excludeEntity = null)
    {
        if (mainCamera == null) return;

        // Raycast sur la position — sélectionne ce qui occupe réellement la tile
        Collider2D[] hits = Physics2D.OverlapPointAll(lastPosition, combinedSelectableMask);

        // Filtre l'entité détruite — son collider est encore actif au moment du raycast
        List<Collider2D> filteredHits = new List<Collider2D>();
        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject != excludeEntity)
                filteredHits.Add(hit);
        }

        GameObject bestHit = SelectionResolver.ResolveBestHit(filteredHits.ToArray());

        if (bestHit != null)
        {
            SelectionType selectionType = SelectionResolver.Classify(bestHit, out GameObject entity);
            ApplySelection(selectionType, entity, Vector2Int.zero, lastPosition, false);
            GameLogger.Log(GameLogger.Category.Selection, $" SelectionManager : Fallback vers {bestHit.name} après destruction", this);
            return;
        }

        HandleGroundSelection(lastPosition);
    }

    // Déclenche l'event public correspondant au type final de sélection
    private void FireSelectionEvent()
    {
        switch (state.Type)
        {
            case SelectionType.Units:
                OnUnitsSelected.Invoke(GetSelectedUnits());
                break;
            case SelectionType.Building:
                OnBuildingSelected.Invoke(state.Single);
                GameLogger.Log(GameLogger.Category.Selection, $"🏛️ Bâtiment sélectionné : {state.Single.name}", this);
                break;
            case SelectionType.Environment:
                OnEnvironmentSelected.Invoke(state.Single);
                GameLogger.Log(GameLogger.Category.Selection, $"🌿 Environnement sélectionné : {state.Single.name}", this);
                break;
            case SelectionType.Obstacle:
                OnObstacleSelected.Invoke(state.Single);
                GameLogger.Log(GameLogger.Category.Selection, $"🪨 Obstacle sélectionné : {state.Single.name}", this);
                break;
            case SelectionType.Ground:
                OnGroundSelected.Invoke(state.Ground);
                GameLogger.Log(GameLogger.Category.Selection, $"🌿 Terrain sélectionné : cellule {state.Ground}", this);
                break;
            default:
                OnSelectionCleared.Invoke();
                break;
        }
    }

    // Suit les cellules associées à une sélection simple pour réagir aux entrées/sorties d'unités.
    private void UpdateWatchedSelectionCell(Vector3? worldClickPosition)
    {
        UnregisterWatchedSelectionCell();

        if (EntityRegistry.Instance == null)
            return;

        List<Vector2Int> cells;
        if (preserveSelectionAnchor)
        {
            preserveSelectionAnchor = false;
            watchedSelectionFromAutoSwitch = true;
            cells = GetAnchorWatchCells();
        }
        else
        {
            cells = GetSelectionWatchCells(worldClickPosition);
            watchedSelectionFromAutoSwitch = false;
        }

        if (cells.Count == 0)
            return;

        RegisterWatchedSelectionCells(cells);
    }

    // Retire tous les listeners de cellules courants.
    private void UnregisterWatchedSelectionCell()
    {
        EntityRegistry.Instance?.UnregisterAllCellListeners(this);
        watchedSelectionCells.Clear();
    }

    // Enregistre une liste de cellules sans doublons.
    private void RegisterWatchedSelectionCells(List<Vector2Int> cells)
    {
        foreach (Vector2Int cell in cells)
        {
            if (watchedSelectionCells.Contains(cell))
                continue;

            watchedSelectionCells.Add(cell);
            EntityRegistry.Instance.RegisterCellListener(cell, this);
        }
    }

    // Convertit une position monde en cellule si elle est dans la grille.
    private Vector2Int? TryWorldToGridCell(Vector3 worldPosition)
    {
        if (GridManager.Instance == null)
            return null;

        Vector2Int cell = GridManager.Instance.WorldToGrid(worldPosition);
        return GridManager.Instance.IsInBounds(cell) ? cell : (Vector2Int?)null;
    }

    // Pose l'ancre de sélection depuis une action manuelle du joueur.
    private void SetManualSelectionAnchor(Vector2Int? cell)
    {
        selectionAnchorCell = cell;
        preserveSelectionAnchor = false;
        watchedSelectionFromAutoSwitch = false;
    }

    // Efface l'ancre de sélection.
    private void ClearSelectionAnchor()
    {
        selectionAnchorCell = null;
        preserveSelectionAnchor = false;
        watchedSelectionFromAutoSwitch = false;
    }

    // Retourne les cellules à surveiller pour le contenu prioritaire de l'ancre.
    private List<Vector2Int> GetAnchorWatchCells()
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        if (!selectionAnchorCell.HasValue)
            return cells;

        GameObject anchoredEntity = SelectionResolver.ResolveAtCell(selectionAnchorCell.Value);
        if (anchoredEntity != null)
        {
            IMultiTile multiTile = anchoredEntity.GetComponent<IMultiTile>();
            if (multiTile != null)
            {
                foreach (Vector2Int occupiedCell in multiTile.GetOccupiedTiles())
                {
                    if (!cells.Contains(occupiedCell))
                        cells.Add(occupiedCell);
                }
            }
        }

        if (!cells.Contains(selectionAnchorCell.Value))
            cells.Add(selectionAnchorCell.Value);

        return cells;
    }

    // Détermine les cellules à suivre pour la sélection courante.
    private List<Vector2Int> GetSelectionWatchCells(Vector3? worldClickPosition)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        if (state.Type == SelectionType.Units)
        {
            if (state.Units.Count != 1)
                return cells;

            if (TryGetEntityCell(state.Units[0], out Vector2Int unitCell))
                cells.Add(unitCell);
            return cells;
        }

        if (state.Type == SelectionType.Ground)
        {
            cells.Add(state.Ground);
            return cells;
        }

        if (state.Single != null)
        {
            IMultiTile multiTile = state.Single.GetComponent<IMultiTile>();
            if (multiTile != null)
            {
                foreach (Vector2Int occupiedCell in multiTile.GetOccupiedTiles())
                {
                    if (!cells.Contains(occupiedCell))
                        cells.Add(occupiedCell);
                }

                if (cells.Count > 0)
                    return cells;
            }

            if (TryGetEntityCell(state.Single, out Vector2Int entityCell))
            {
                cells.Add(entityCell);
                return cells;
            }
        }

        if (worldClickPosition.HasValue && GridManager.Instance != null)
        {
            Vector2Int clickCell = GridManager.Instance.WorldToGrid(worldClickPosition.Value);
            if (GridManager.Instance.IsInBounds(clickCell))
                cells.Add(clickCell);
        }

        return cells;
    }

    // Retourne la cellule logique connue d'une entité sélectionnée.
    private bool TryGetEntityCell(GameObject entity, out Vector2Int cell)
    {
        if (entity != null && EntityRegistry.Instance != null && EntityRegistry.Instance.TryGetLogicalPosition(entity, out cell))
            return true;

        MovementController movementController = entity != null ? entity.GetComponent<MovementController>() : null;
        if (movementController != null)
        {
            cell = movementController.GetCurrentGridPosition();
            return true;
        }

        cell = default(Vector2Int);
        return false;
    }

    // Réagit à l'arrivée d'une unité sur la cellule suivie.
    public void OnUnitEntered(Vector2Int cell, GameObject unit)
    {
        if (EntitySpawnManager.Instance != null && EntitySpawnManager.Instance.IsBeingReplaced(unit))
            return;
        if (!watchedSelectionCells.Contains(cell))
            return;
        if (watchedSelectionFromAutoSwitch && selectionAnchorCell.HasValue && cell != selectionAnchorCell.Value)
            return;

        ResolveWatchedCellSelection(cell);
    }

    // Réagit au départ d'une unité depuis la cellule suivie.
    public void OnUnitLeft(Vector2Int cell, GameObject unit)
    {
        if (EntitySpawnManager.Instance != null && EntitySpawnManager.Instance.IsBeingReplaced(unit))
            return;
        if (!watchedSelectionCells.Contains(cell))
            return;

        if (IsSingleSelectedUnit(unit) && !watchedSelectionFromAutoSwitch)
        {
            if (TryGetEntityCell(unit, out Vector2Int unitCell))
                selectionAnchorCell = unitCell;
            UpdateWatchedSelectionCell(null);
            return;
        }

        ResolveWatchedCellSelection(selectionAnchorCell.HasValue ? selectionAnchorCell.Value : cell);
    }

    // Indique si l'unité sortante est l'unique unité sélectionnée, auquel cas la sélection la suit.
    private bool IsSingleSelectedUnit(GameObject unit)
    {
        return unit != null
            && state.Type == SelectionType.Units
            && state.Units.Count == 1
            && state.Units[0] == unit;
    }

    // Re-résout la cellule d'événement et bascule la sélection si la priorité effective a changé.
    private void ResolveWatchedCellSelection(Vector2Int eventCell)
    {
        if (watchedSelectionCells.Count == 0)
            return;

        Vector2Int cell = eventCell;
        GameObject resolved = SelectionResolver.ResolveAtCell(cell);

        if (IsCurrentSelectionForCellResult(cell, resolved))
            return;

        Vector3? worldPosition = null;
        if (GridManager.Instance != null)
            worldPosition = GridManager.Instance.GridToWorld(cell);
        if (resolved == null)
        {
            ApplySelection(SelectionType.Ground, null, cell, worldPosition, false);
            return;
        }

        SelectionType selectionType = SelectionResolver.Classify(resolved, out GameObject entity);
        PreserveAnchorIfSelectingUnit(selectionType);
        ApplySelection(selectionType, entity, cell, worldPosition, false);
    }

    // Garde l'ancre manuelle quand une unité est sélectionnée automatiquement par arrivée.
    private void PreserveAnchorIfSelectingUnit(SelectionType selectionType)
    {
        if (selectionType != SelectionType.Units)
            return;

        preserveSelectionAnchor = selectionAnchorCell.HasValue;
    }

    // Compare la sélection courante avec le résultat prioritaire d'une cellule.
    private bool IsCurrentSelectionForCellResult(Vector2Int cell, GameObject resolved)
    {
        if (resolved == null)
            return state.Type == SelectionType.Ground && state.Ground == cell;

        if (state.Type == SelectionType.Units)
            return state.Units.Count == 1 && state.Units[0] == resolved;

        return state.Single == resolved;
    }

    // Dessine le rectangle de sélection en temps réel pendant le drag
    private void DrawSelectionRectangle()
    {
        Vector3 startWorld = mainCamera.ScreenToWorldPoint(dragStartPosition);
        Vector3 endWorld = mainCamera.ScreenToWorldPoint(lastDragScreenPosition);
        startWorld.z = endWorld.z = 0f;

        Vector3[] corners = new Vector3[5];
        corners[0] = new Vector3(startWorld.x, startWorld.y, 0f);
        corners[1] = new Vector3(endWorld.x, startWorld.y, 0f);
        corners[2] = new Vector3(endWorld.x, endWorld.y, 0f);
        corners[3] = new Vector3(startWorld.x, endWorld.y, 0f);
        corners[4] = corners[0];

        rectangleRenderer.SetPositions(corners);
    }

    // Indique si la position écran touche une UI Unity
    private bool IsPointerOverUserInterface(Vector3 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = new Vector2(screenPosition.x, screenPosition.y);
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    // Crée une nouvelle instance du carré de sélection
    private SelectionSquare CreateSelectionSquare()
    {
        if (selectionSquarePrefab == null)
        {
            GameLogger.Error("❌ Impossible de créer SelectionSquare : prefab non assigné !", this);
            return null;
        }

        GameObject squareInstance = Instantiate(selectionSquarePrefab);
        SelectionSquare selectionSquare = squareInstance.GetComponent<SelectionSquare>();

        if (selectionSquare == null)
        {
            GameLogger.Error("❌ Le prefab SelectionSquare ne contient pas le composant SelectionSquare !", this);
            Destroy(squareInstance);
            return null;
        }

        return selectionSquare;
    }

    // Appelé quand une entité spawn sans remplacer le spawner
    private void HandleEntitySpawned(GameObject spawner, GameObject spawnedEntity)
    {
        if (spawner == null || spawnedEntity == null)
        {
            return;
        }

        if (state.Contains(spawner))
        {
            SetManualSelectionAnchor(TryGetEntityCell(spawnedEntity, out Vector2Int spawnedCell) ? spawnedCell : (Vector2Int?)null);
            SelectGameObject(spawnedEntity);
            GameLogger.Log(GameLogger.Category.Selection, $"🔄 SelectionManager : Sélection auto {spawnedEntity.name} (spawné par {spawner.name})", this);
        }
    }

    // Appelé quand une entité est remplacée par une autre
    private void HandleEntityReplaced(GameObject oldEntity, GameObject newEntity)
    {
        if (oldEntity == null || newEntity == null)
        {
            return;
        }

        if (state.Contains(oldEntity))
        {
            SetManualSelectionAnchor(TryGetEntityCell(newEntity, out Vector2Int newEntityCell) ? newEntityCell : (Vector2Int?)null);
            SelectGameObject(newEntity);
            GameLogger.Log(GameLogger.Category.Selection, $"🔄 SelectionManager : Sélection auto {newEntity.name} (remplace {oldEntity.name})", this);
        }
    }

    // Efface l'état, les carrés et les handlers sans émettre d'event
    private void ClearAllSelectionSilent()
    {
        UnregisterWatchedSelectionCell();
        ClearSelectionAnchor();
        state.Clear();

        List<GameObject> selectedEntities = new List<GameObject>(squares.Keys);
        foreach (GameObject selectedEntity in selectedEntities)
        {
            RemoveSelectionSquare(selectedEntity);
        }

        RemoveGroundSelectionSquare();
        UnsubscribeAllDeathHandlers();
    }

    // Désabonne tous les handlers de destruction trackés
    private void UnsubscribeAllDeathHandlers()
    {
        // No-op : la destruction est suivie centralement via EntitySpawnManager.OnEntityDestroyed.
    }

    // Réagit à tout spawn sur l'ancre suivie ou dans le voisinage de la sélection.
    private void HandleEntitySpawnCompleted(GameObject spawnedEntity)
    {
        if (EntitySpawnManager.Instance != null && EntitySpawnManager.Instance.IsBeingReplaced(spawnedEntity))
            return;
        if (spawnedEntity == null || !TryGetEntityCell(spawnedEntity, out Vector2Int spawnedCell))
            return;

        bool isWatchedCell = watchedSelectionCells.Contains(spawnedCell)
            || (selectionAnchorCell.HasValue && selectionAnchorCell.Value == spawnedCell);
        if (isWatchedCell)
        {
            ResolveWatchedCellSelection(selectionAnchorCell.HasValue ? selectionAnchorCell.Value : spawnedCell);
            return;
        }

        if (spawnedEntity.GetComponent<BuildingStatsComponent>() == null
            || state.Type != SelectionType.Units
            || state.Units.Count != 1
            || !TryGetEntityCell(state.Units[0], out Vector2Int selectedUnitCell))
        {
            return;
        }

        Vector2Int distance = spawnedCell - selectedUnitCell;
        if (Mathf.Abs(distance.x) <= 1 && Mathf.Abs(distance.y) <= 1)
        {
            // Republie la sélection sans la modifier pour réévaluer le contexte du panneau d'actions.
            FireSelectionEvent();
        }
    }

    // Re-résout l'ancre quand l'entité retirée était la sélection courante.
    private void HandleEntityDespawned(GameObject despawnedEntity)
    {
        if (EntitySpawnManager.Instance != null && EntitySpawnManager.Instance.IsBeingReplaced(despawnedEntity))
            return;
        if (despawnedEntity == null || !state.Contains(despawnedEntity))
            return;

        if (selectionAnchorCell.HasValue)
        {
            ResolveWatchedCellSelection(selectionAnchorCell.Value);
            return;
        }

        HandleTrackedEntityDestroyed(despawnedEntity);
    }

    // Nettoie les listeners lors de la destruction pour éviter les fuites mémoire
    private void OnDestroy()
    {
        if (inputManager != null)
        {
            inputManager.OnLeftClickDown.RemoveListener(OnLeftClickDownHandler);
            inputManager.OnDragStart.RemoveListener(OnDragStartHandler);
            inputManager.OnDragUpdate.RemoveListener(OnDragUpdateHandler);
            inputManager.OnLeftClickUp.RemoveListener(OnLeftClickUpHandler);
            inputManager.OnDragEnd.RemoveListener(OnDragEndHandler);
        }

        if (EntitySpawnManager.Instance != null)
        {
            EntitySpawnManager.Instance.OnEntitySpawned.RemoveListener(HandleEntitySpawned);
            EntitySpawnManager.Instance.OnEntitySpawnCompleted.RemoveListener(HandleEntitySpawnCompleted);
            EntitySpawnManager.Instance.OnEntityReplaced.RemoveListener(HandleEntityReplaced);
            EntitySpawnManager.Instance.OnEntityDespawned.RemoveListener(HandleEntityDespawned);
            EntitySpawnManager.Instance.OnEntityDestroyed.RemoveListener(HandleEntityDestroyed);
        }

        UnregisterWatchedSelectionCell();
        UnsubscribeAllDeathHandlers();
    }

    // Efface complètement toute sélection et notifie l'UI
    public void ClearAllSelection()
    {
        ClearAllSelectionSilent();
        OnSelectionCleared.Invoke();
        GameLogger.Log(GameLogger.Category.Selection, "🧹 Toute sélection effacée", this);
    }

    // Retourne la liste de toutes les unités sélectionnées
    public List<GameObject> GetSelectedUnits()
    {
        return new List<GameObject>(state.Units);
    }

    // Retourne l'obstacle actuellement sélectionné
    public GameObject GetSelectedObstacle()
    {
        return state.Type == SelectionType.Obstacle ? state.Single : null;
    }

    // Retourne le bâtiment actuellement sélectionné
    public GameObject GetSelectedBuilding()
    {
        return state.Type == SelectionType.Building ? state.Single : null;
    }

    // Retourne les coordonnées du terrain sélectionné
    public Vector2Int GetSelectedGroundPosition()
    {
        return state.Type == SelectionType.Ground ? state.Ground : Vector2Int.zero;
    }

    // Vérifie si des unités sont sélectionnées
    public bool HasUnitsSelected()
    {
        return state.Type == SelectionType.Units && state.Units.Count > 0;
    }

    // Vérifie si un obstacle est sélectionné
    public bool HasObstacleSelected()
    {
        return state.Type == SelectionType.Obstacle;
    }

    // Vérifie si un terrain est sélectionné
    public bool HasGroundSelected()
    {
        return state.Type == SelectionType.Ground;
    }

    // Vérifie si une sélection quelconque existe
    public bool HasAnySelection()
    {
        return !state.IsEmpty;
    }

    // Retourne le nombre d'unités sélectionnées
    public int GetSelectedUnitCount()
    {
        return state.Type == SelectionType.Units ? state.Units.Count : 0;
    }

    // Sélectionne un GameObject externe via la logique centrale
    public void SelectGameObject(GameObject unit)
    {
        if (unit == null)
        {
            return;
        }

        SelectionType selectionType = SelectionResolver.Classify(unit, out GameObject entity);
        ApplySelection(selectionType, entity, Vector2Int.zero, unit.transform.position, false);
    }
}
