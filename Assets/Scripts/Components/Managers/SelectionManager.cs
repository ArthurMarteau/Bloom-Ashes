using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// Gère la sélection d'objets avec clic simple et rectangle de sélection
// Glue MonoBehaviour : input, raycasts, carrés visuels, events UI et tracking destruction
public class SelectionManager : MonoBehaviour
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
    private readonly Dictionary<GameObject, UnityAction> deathHandlers = new Dictionary<GameObject, UnityAction>();
    private SelectionSquare groundSquare;
    private LayerMask combinedSelectableMask;

    private bool isDragging;
    private Vector3 dragStartPosition;
    private Vector3 lastDragScreenPosition;
    private LineRenderer rectangleRenderer;

    private Camera mainCamera;
    private InputManager inputManager;

    [Header("Events — UI")]
    public UnityEvent<List<GameObject>>    OnUnitsSelected      = new UnityEvent<List<GameObject>>();
    public UnityEvent<Vector2Int>          OnGroundSelected     = new UnityEvent<Vector2Int>();
    public UnityEvent<GameObject>          OnObstacleSelected   = new UnityEvent<GameObject>();
    public UnityEvent<GameObject>          OnBuildingSelected   = new UnityEvent<GameObject>();
    public UnityEvent                      OnSelectionCleared   = new UnityEvent();

    // Initialise les références, masks et composants visuels
    private void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("❌ SelectionManager nécessite une caméra principale dans la scène !");
        }

        if (selectionSquarePrefab == null)
        {
            Debug.LogError("❌ SelectionManager nécessite un prefab SelectionSquare assigné !");
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
            Debug.LogError("❌ SelectionManager: InputManager introuvable dans la scène !");
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
            EntitySpawnManager.Instance.OnEntityReplaced.AddListener(HandleEntityReplaced);
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
        Debug.Log($"🎯 START Selection — monde {worldPosition}");
    }

    // Début du drag — enregistre la position initiale et active le rectangle
    private void OnDragStartHandler(Vector3 screenPosition)
    {
        if (IsPointerOverUserInterface(screenPosition))
        {
            isDragging = false;
            Debug.Log("🧱 SelectionManager : drag ignoré car démarré sur l'UI");
            return;
        }

        dragStartPosition = screenPosition;
        lastDragScreenPosition = screenPosition;
        isDragging = true;
        rectangleRenderer.enabled = true;

        Debug.Log("🟢 Mode rectangle ACTIVÉ !");
    }

    // Drag en cours — met à jour la position courante et redessine le rectangle
    private void OnDragUpdateHandler(Vector3 screenPosition)
    {
        if (!isDragging)
        {
            return;
        }

        lastDragScreenPosition = screenPosition;
        Debug.Log("🎨 Dessin du rectangle...");
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

        Collider2D[] hits = Physics2D.OverlapPointAll(clickWorldPosition, combinedSelectableMask);
        GameObject bestHit = SelectionResolver.ResolveBestHit(hits);

        if (bestHit != null)
        {
            SelectionType selectionType = SelectionResolver.Classify(bestHit, out GameObject entity);
            ApplySelection(selectionType, entity, Vector2Int.zero, clickWorldPosition, inputManager != null && inputManager.IsShiftHeld);
            return;
        }

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
            Debug.Log($"⚠️ Drag-select : {selectableUnits.Count} unités valides dans la zone → cap à {MAX_SELECTED_UNITS} (plus proches du centre)");
            selectableUnits.RemoveRange(MAX_SELECTED_UNITS, selectableUnits.Count - MAX_SELECTED_UNITS);
        }

        ApplyUnitListSelection(selectableUnits);
        Debug.Log($"📦 Rectangle sélection : {state.Units.Count} unités sélectionnées (zone contenait {unitsInArea.Length})");
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
        SynchronizeDeathHandlers();
        FireSelectionEvent();
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
                Debug.Log($"➖ Unité désélectionnée : {unit.name} (Total: {state.Units.Count})");
                return;
            }

            bool added = state.AddUnit(unit, MAX_SELECTED_UNITS);
            if (!added)
            {
                Debug.Log($"⚠️ Sélection plafonnée à {MAX_SELECTED_UNITS} unités — {unit.name} ignorée");
                return;
            }

            Debug.Log($"➕ Unité sélectionnée : {unit.name} (Total: {state.Units.Count})");
            return;
        }

        state.SetUnits(new[] { unit });
        Debug.Log($"✅ Unité sélectionnée : {unit.name} (Total: {state.Units.Count})");
    }

    // Remplace la sélection par une liste d'unités issue du rectangle
    private void ApplyUnitListSelection(List<GameObject> units)
    {
        state.SetUnits(units);
        SynchronizeSelectionVisuals(null);
        SynchronizeDeathHandlers();
        FireSelectionEvent();
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
        else if (state.Type == SelectionType.Building || state.Type == SelectionType.Obstacle)
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

        if (state.Type == SelectionType.Building && GridManager.Instance != null && worldClickPosition.HasValue)
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

    // Synchronise les handlers de mort avec les entités sélectionnées
    private void SynchronizeDeathHandlers()
    {
        List<GameObject> desiredTrackedEntities = GetTrackableSelectedEntities();
        List<GameObject> trackedEntities = new List<GameObject>(deathHandlers.Keys);

        foreach (GameObject trackedEntity in trackedEntities)
        {
            if (trackedEntity == null || !desiredTrackedEntities.Contains(trackedEntity))
            {
                UnsubscribeDeathHandler(trackedEntity);
            }
        }

        foreach (GameObject desiredEntity in desiredTrackedEntities)
        {
            if (desiredEntity != null && !deathHandlers.ContainsKey(desiredEntity))
            {
                SubscribeDeathHandler(desiredEntity);
            }
        }
    }

    // Retourne les entités sélectionnées qui exposent un event de destruction
    private List<GameObject> GetTrackableSelectedEntities()
    {
        List<GameObject> trackableEntities = new List<GameObject>();

        if (state.Type == SelectionType.Units)
        {
            foreach (GameObject unit in state.Units)
            {
                if (unit != null)
                {
                    trackableEntities.Add(unit);
                }
            }
        }
        else if (state.Type == SelectionType.Building && state.Single != null)
        {
            trackableEntities.Add(state.Single);
        }

        return trackableEntities;
    }

    // Abonne un handler de destruction pour une entité sélectionnée
    private void SubscribeDeathHandler(GameObject entity)
    {
        UnityAction handler = () => HandleTrackedEntityDestroyed(entity);
        BuildingStatsComponent buildingStats = entity.GetComponent<BuildingStatsComponent>();
        UnitStatsComponent unitStats = entity.GetComponent<UnitStatsComponent>();

        if (buildingStats != null)
        {
            buildingStats.OnBuildingDestroyed.AddListener(handler);
            deathHandlers[entity] = handler;
            return;
        }

        if (unitStats != null)
        {
            unitStats.OnDeath.AddListener(handler);
            deathHandlers[entity] = handler;
        }
    }

    // Désabonne le handler de destruction stocké pour une entité
    private void UnsubscribeDeathHandler(GameObject entity)
    {
        if (entity == null || !deathHandlers.TryGetValue(entity, out UnityAction handler))
        {
            deathHandlers.Remove(entity);
            return;
        }

        BuildingStatsComponent buildingStats = entity.GetComponent<BuildingStatsComponent>();
        UnitStatsComponent unitStats = entity.GetComponent<UnitStatsComponent>();

        if (buildingStats != null)
        {
            buildingStats.OnBuildingDestroyed.RemoveListener(handler);
        }

        if (unitStats != null)
        {
            unitStats.OnDeath.RemoveListener(handler);
        }

        deathHandlers.Remove(entity);
    }

    // Réagit à la destruction d'une entité sélectionnée
    private void HandleTrackedEntityDestroyed(GameObject destroyedEntity)
    {
        if (destroyedEntity == null)
        {
            return;
        }

        Vector3 lastPosition = destroyedEntity.transform.position;
        UnsubscribeDeathHandler(destroyedEntity);
        RemoveSelectionSquare(destroyedEntity);

        if (state.Type == SelectionType.Units)
        {
            state.RemoveUnit(destroyedEntity);
            if (state.Units.Count > 0)
            {
                SynchronizeSelectionVisuals(null);
                SynchronizeDeathHandlers();
                OnUnitsSelected.Invoke(GetSelectedUnits());
                Debug.Log($"💀 SelectionManager : entité retirée de la sélection, {state.Units.Count} restantes");
                return;
            }
        }
        else if (state.Single == destroyedEntity)
        {
            state.Clear();
        }

        FallbackToGroundAfterDestruction(lastPosition);
    }

    // Fallback vers le terrain sous la dernière position connue
    private void FallbackToGroundAfterDestruction(Vector3 lastPosition)
    {
        if (GridManager.Instance != null)
        {
            Vector2Int gridPosition = GridManager.Instance.WorldToGrid(lastPosition);
            if (GridManager.Instance.IsInBounds(gridPosition))
            {
                ApplySelection(SelectionType.Ground, null, gridPosition, lastPosition, false);
                Debug.Log($"🔄 SelectionManager : Fallback vers terrain {gridPosition} après destruction d'entité");
                return;
            }
        }

        ClearAllSelection();
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
                Debug.Log($"🏛️ Bâtiment sélectionné : {state.Single.name}");
                break;
            case SelectionType.Obstacle:
                OnObstacleSelected.Invoke(state.Single);
                Debug.Log($"🪨 Obstacle sélectionné : {state.Single.name}");
                break;
            case SelectionType.Ground:
                OnGroundSelected.Invoke(state.Ground);
                Debug.Log($"🌿 Terrain sélectionné : cellule {state.Ground}");
                break;
            default:
                OnSelectionCleared.Invoke();
                break;
        }
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
            Debug.LogError("❌ Impossible de créer SelectionSquare : prefab non assigné !");
            return null;
        }

        GameObject squareInstance = Instantiate(selectionSquarePrefab);
        SelectionSquare selectionSquare = squareInstance.GetComponent<SelectionSquare>();

        if (selectionSquare == null)
        {
            Debug.LogError("❌ Le prefab SelectionSquare ne contient pas le composant SelectionSquare !");
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
            SelectGameObject(spawnedEntity);
            Debug.Log($"🔄 SelectionManager : Sélection auto {spawnedEntity.name} (spawné par {spawner.name})");
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
            SelectGameObject(newEntity);
            Debug.Log($"🔄 SelectionManager : Sélection auto {newEntity.name} (remplace {oldEntity.name})");
        }
    }

    // Efface l'état, les carrés et les handlers sans émettre d'event
    private void ClearAllSelectionSilent()
    {
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
        List<GameObject> trackedEntities = new List<GameObject>(deathHandlers.Keys);
        foreach (GameObject trackedEntity in trackedEntities)
        {
            UnsubscribeDeathHandler(trackedEntity);
        }
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
            EntitySpawnManager.Instance.OnEntityReplaced.RemoveListener(HandleEntityReplaced);
        }

        UnsubscribeAllDeathHandlers();
    }

    // Efface complètement toute sélection et notifie l'UI
    public void ClearAllSelection()
    {
        ClearAllSelectionSilent();
        OnSelectionCleared.Invoke();
        Debug.Log("🧹 Toute sélection effacée");
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
