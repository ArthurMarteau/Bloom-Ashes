using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// Gère la sélection d'objets avec clic simple et rectangle de sélection
// Système hybride : registry logique (unités) + Physics2D (bâtiments/env)
// Priorité unique via SelectAtCell : Unit > Building > Environment > Ground
// Tracking destruction symétrique par entité via trackedDestroyHandlers
public class SelectionManager : MonoBehaviour
{
    [Header("Selection Settings")]
    [SerializeField] private LayerMask unitLayer = 1 << 9;
    [SerializeField] private LayerMask buildingLayer = 1 << 8;
    [SerializeField] private LayerMask environmentWalkableLayer = 1 << 7;
    [SerializeField] private LayerMask obstacleLayer = 1 << 6;
    [SerializeField] private LayerMask groundLayer = 1 << 3;

    [Header("Selection Square")]
    [SerializeField] private GameObject selectionSquarePrefab;

    // === SELECTION MULTIPLE UNITES ===
    private Dictionary<GameObject, SelectionSquare> selectedUnits = new Dictionary<GameObject, SelectionSquare>();

    // === SELECTION UNIQUE OBSTACLE/GROUND ===
    private GameObject currentSelectedObject;
    private SelectionSquare currentObjectSelectionSquare;
    private Vector2Int currentGroundSelection;
    private bool hasGroundSelection;

    // === RECTANGLE SELECTION ===
    private bool isDragging;
    private Vector3 dragStartPosition;
    private Vector3 lastDragScreenPosition;
    private LineRenderer rectangleRenderer;

    private Camera mainCamera;
    private InputManager inputManager;

    // Handlers de destruction indexés par entité — permet RemoveListener avec callback exact
    private Dictionary<GameObject, UnityAction> trackedDestroyHandlers = new Dictionary<GameObject, UnityAction>();

    [Header("Events — UI")]
    public UnityEvent<List<GameObject>>    OnUnitsSelected    = new UnityEvent<List<GameObject>>();
    public UnityEvent<Vector2Int>          OnGroundSelected   = new UnityEvent<Vector2Int>();
    public UnityEvent<GameObject>          OnObstacleSelected = new UnityEvent<GameObject>();
    public UnityEvent<GameObject>          OnBuildingSelected = new UnityEvent<GameObject>();
    public UnityEvent                      OnSelectionCleared = new UnityEvent();

    // Méthode appelée au démarrage pour initialiser les références et composants
    private void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            Debug.LogError("SelectionManager nécessite une caméra principale dans la scène !");

        if (selectionSquarePrefab == null)
            Debug.LogError("SelectionManager nécessite un prefab SelectionSquare assigné !");

        SetupRectangleRenderer();
    }

    // S'abonne aux events de InputManager et EntitySpawnManager
    private void Start()
    {
        inputManager = InputManager.Instance;
        if (inputManager == null)
        {
            Debug.LogError("SelectionManager: InputManager introuvable dans la scène !");
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

    // --- Handlers InputManager ---

    // Clic gauche enfoncé — réinitialise l'état du drag
    private void OnLeftClickDownHandler(Vector3 worldPosition)
    {
        isDragging = false;
        Debug.Log($"🎯 START Selection — monde {worldPosition}");
    }

    // Début du drag — enregistre la position initiale et active le rectangle
    private void OnDragStartHandler(Vector3 screenPosition)
    {
        dragStartPosition = screenPosition;
        lastDragScreenPosition = screenPosition;
        isDragging = true;
        rectangleRenderer.enabled = true;
        Debug.Log("🟢 Mode rectangle ACTIVÉ !");
    }

    // Drag en cours — met à jour la position courante et redessine le rectangle
    private void OnDragUpdateHandler(Vector3 screenPosition)
    {
        if (!isDragging) return;
        lastDragScreenPosition = screenPosition;
        Debug.Log("🎨 Dessin du rectangle...");
        DrawSelectionRectangle();
    }

    // Clic simple relâché — filtre UI puis délègue à SelectAtCell
    private void OnLeftClickUpHandler(Vector3 screenPosition)
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = new Vector2(screenPosition.x, screenPosition.y);
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        if (results.Count > 0) return;

        HandleSimpleClick(screenPosition);
    }

    // Fin du drag — complète la sélection rectangle et masque le rectangle
    private void OnDragEndHandler()
    {
        if (!isDragging) return;
        CompleteRectangleSelection();
        isDragging = false;
        rectangleRenderer.enabled = false;
    }

    // --- Logique de sélection ---

    // Traite la sélection par clic simple — convertit screen→world puis délègue à SelectAtCell
    private void HandleSimpleClick(Vector3 screenPosition)
    {
        Vector3 clickWorldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        clickWorldPosition.z = 0;
        Vector2Int cell = GridManager.Instance.WorldToGrid(clickWorldPosition);
        SelectAtCell(cell);
    }

    // Point d'entrée unique pour la logique de priorité de sélection par cellule
    // Priorité : Unit (registry) > Building (Physics2D + BuildingStatsComponent) > Environment > Ground
    public void SelectAtCell(Vector2Int cell)
    {
        Vector3 worldPos = GridManager.Instance.GridToWorld(cell);

        // Priorité 1 : unité via registry logique (fiable pendant le Lerp visuel)
        if (EntityRegistry.Instance != null)
        {
            GameObject unit = EntityRegistry.Instance.GetUnitAtLogicalPosition(cell);
            if (unit != null)
            {
                UnitStatsComponent stats = unit.GetComponent<UnitStatsComponent>();
                if (stats == null || !stats.IsDead())
                {
                    HandleUnitSelection(unit, worldPos);
                    return;
                }
            }
        }

        // Priorité 2 : bâtiment (BuildingStatsComponent présent sur layer 8 ou 7)
        Collider2D buildingCollider = Physics2D.OverlapPoint(worldPos, buildingLayer | environmentWalkableLayer);
        if (buildingCollider != null && SelectableResolver.IsBuilding(buildingCollider.gameObject))
        {
            HandleUnitSelection(buildingCollider.gameObject, worldPos);
            return;
        }

        // Priorité 3 : environnement (obstacle ou env walkable sans BuildingStats)
        Collider2D envCollider = Physics2D.OverlapPoint(worldPos, obstacleLayer | environmentWalkableLayer);
        if (envCollider != null && !SelectableResolver.IsBuilding(envCollider.gameObject))
        {
            SelectObstacle(envCollider.gameObject);
            return;
        }

        // Fallback : terrain
        if (GridManager.Instance.IsInBounds(cell))
            SelectGround(cell);
        else
            ClearAllSelection();
    }

    // Retourne la cellule de la première entité sélectionnée (unité ou objet courant)
    // Utile comme fallback quand le transform peut déjà être détruit à l'appel
    public bool TryGetSelectedEntityCell(out Vector2Int cell)
    {
        foreach (var kvp in selectedUnits)
        {
            if (kvp.Key != null)
            {
                cell = GridManager.Instance.WorldToGrid(kvp.Key.transform.position);
                return true;
            }
        }
        if (currentSelectedObject != null)
        {
            cell = GridManager.Instance.WorldToGrid(currentSelectedObject.transform.position);
            return true;
        }
        cell = Vector2Int.zero;
        return false;
    }

    // Gère la sélection d'une entité (unit ou building) avec support Shift pour sélection multiple
    // Utilise SelectableResolver pour classifier et router vers la bonne branche
    private void HandleUnitSelection(GameObject unit, Vector3 worldPosition = default)
    {
        SelectableType type = SelectableResolver.ResolveType(unit);

        if (type == SelectableType.Building)
        {
            ClearAllSelectionSilent();
            currentSelectedObject = unit;
            hasGroundSelection = false;

            currentObjectSelectionSquare = CreateSelectionSquare();
            if (currentObjectSelectionSquare != null)
            {
                Vector2Int clickedTile = GridManager.Instance.WorldToGrid(worldPosition);
                currentObjectSelectionSquare.AttachToGridPosition(clickedTile);
            }

            TrackEntityDestruction(unit);
            OnBuildingSelected.Invoke(unit);
            Debug.Log($"🏗️ Bâtiment sélectionné : {unit.name}");
            return;
        }

        // Branche unité — efface obstacle/ground, conserve les autres unités (shift)
        ClearNonUnitSelection();

        if (InputManager.Instance.IsShiftHeld)
        {
            if (IsUnitSelected(unit)) DeselectUnit(unit);
            else SelectUnit(unit);
        }
        else
        {
            ClearUnitSelection();
            SelectUnit(unit);
        }

        List<GameObject> currentUnits = GetSelectedUnits();
        if (currentUnits.Count > 0)
            OnUnitsSelected.Invoke(currentUnits);
        else
            OnSelectionCleared.Invoke();
    }

    // Finalise la sélection rectangle en récupérant toutes les unités dans la zone
    private void CompleteRectangleSelection()
    {
        Vector3 startWorld = mainCamera.ScreenToWorldPoint(dragStartPosition);
        Vector3 endWorld   = mainCamera.ScreenToWorldPoint(lastDragScreenPosition);
        startWorld.z = endWorld.z = 0;

        Vector2 bottomLeft = new Vector2(Mathf.Min(startWorld.x, endWorld.x), Mathf.Min(startWorld.y, endWorld.y));
        Vector2 topRight   = new Vector2(Mathf.Max(startWorld.x, endWorld.x), Mathf.Max(startWorld.y, endWorld.y));

        Collider2D[] unitsInArea = Physics2D.OverlapAreaAll(bottomLeft, topRight, unitLayer);

        ClearAllSelectionSilent();

        foreach (Collider2D unitCollider in unitsInArea)
            SelectUnit(unitCollider.gameObject);

        List<GameObject> rectangleUnits = GetSelectedUnits();
        OnUnitsSelected.Invoke(rectangleUnits);

        Debug.Log($"🔲 Rectangle sélection : {unitsInArea.Length} unités sélectionnées");
    }

    // Dessine le rectangle de sélection en temps réel pendant le drag
    private void DrawSelectionRectangle()
    {
        Vector3 startWorld = mainCamera.ScreenToWorldPoint(dragStartPosition);
        Vector3 endWorld   = mainCamera.ScreenToWorldPoint(lastDragScreenPosition);
        startWorld.z = endWorld.z = 0;

        Vector3[] corners = new Vector3[5];
        corners[0] = new Vector3(startWorld.x, startWorld.y, 0);
        corners[1] = new Vector3(endWorld.x,   startWorld.y, 0);
        corners[2] = new Vector3(endWorld.x,   endWorld.y,   0);
        corners[3] = new Vector3(startWorld.x, endWorld.y,   0);
        corners[4] = corners[0];

        rectangleRenderer.SetPositions(corners);
    }

    // Ajoute une unité à la sélection multiple — track sa destruction individuellement
    private void SelectUnit(GameObject unit)
    {
        if (unit == null || IsUnitSelected(unit)) return;

        SelectionSquare selectionSquare = CreateSelectionSquare();
        if (selectionSquare != null)
        {
            selectionSquare.AttachToGameObject(unit);
            selectedUnits.Add(unit, selectionSquare);
            TrackEntityDestruction(unit);
            Debug.Log($"✅ Unité sélectionnée : {unit.name} (Total: {selectedUnits.Count})");
        }
    }

    // Retire une unité de la sélection multiple et annule le tracking de destruction
    private void DeselectUnit(GameObject unit)
    {
        if (unit == null || !IsUnitSelected(unit)) return;

        if (selectedUnits.TryGetValue(unit, out SelectionSquare selectionSquare))
        {
            if (selectionSquare != null) selectionSquare.DestroySelectionSquare();
            selectedUnits.Remove(unit);
            UntrackEntityDestruction(unit);
            Debug.Log($"❌ Unité désélectionnée : {unit.name} (Total: {selectedUnits.Count})");
        }
    }

    // Sélectionne un obstacle/environnement (unique, remplace toute sélection)
    private void SelectObstacle(GameObject obstacle)
    {
        if (obstacle == null) return;

        ClearAllSelectionSilent();

        currentObjectSelectionSquare = CreateSelectionSquare();
        if (currentObjectSelectionSquare != null)
        {
            currentObjectSelectionSquare.AttachToGameObject(obstacle);
            currentSelectedObject = obstacle;
            hasGroundSelection = false;
            OnObstacleSelected.Invoke(obstacle);
            Debug.Log($"🌳 Obstacle sélectionné : {obstacle.name}");
        }
    }

    // Sélectionne une cellule de terrain (unique, remplace toute sélection)
    private void SelectGround(Vector2Int gridCoordinates)
    {
        ClearAllSelectionSilent();

        currentObjectSelectionSquare = CreateSelectionSquare();
        if (currentObjectSelectionSquare != null)
        {
            currentObjectSelectionSquare.AttachToGridPosition(gridCoordinates);
            currentSelectedObject = null;
            currentGroundSelection = gridCoordinates;
            hasGroundSelection = true;
            OnGroundSelected.Invoke(gridCoordinates);
            Debug.Log($"🌱 Terrain sélectionné : cellule {gridCoordinates}");
        }
    }

    // Crée une nouvelle instance du carré de sélection
    private SelectionSquare CreateSelectionSquare()
    {
        if (selectionSquarePrefab == null)
        {
            Debug.LogError("Impossible de créer SelectionSquare : prefab non assigné !");
            return null;
        }

        GameObject squareInstance = Instantiate(selectionSquarePrefab);
        SelectionSquare selectionSquare = squareInstance.GetComponent<SelectionSquare>();

        if (selectionSquare == null)
        {
            Debug.LogError("Le prefab SelectionSquare ne contient pas le composant SelectionSquare !");
            Destroy(squareInstance);
            return null;
        }

        return selectionSquare;
    }

    // Efface uniquement la sélection des unités, annule leur tracking de destruction
    private void ClearUnitSelection()
    {
        foreach (var kvp in selectedUnits)
        {
            if (kvp.Value != null) kvp.Value.DestroySelectionSquare();
            UntrackEntityDestruction(kvp.Key);
        }
        selectedUnits.Clear();
        Debug.Log("🗑️ Sélection unités effacée");
    }

    // Efface uniquement la sélection obstacle/ground, annule le tracking de l'objet courant
    private void ClearNonUnitSelection()
    {
        if (currentObjectSelectionSquare != null)
        {
            currentObjectSelectionSquare.DestroySelectionSquare();
            currentObjectSelectionSquare = null;
        }

        if (currentSelectedObject != null)
            UntrackEntityDestruction(currentSelectedObject);

        currentSelectedObject = null;
        hasGroundSelection = false;
    }

    // === TRACKING DESTRUCTION ===

    // S'abonne à l'event de mort/destruction d'une entité — stocke le handler pour RemoveListener exact
    private void TrackEntityDestruction(GameObject entity)
    {
        if (entity == null || trackedDestroyHandlers.ContainsKey(entity)) return;

        UnitStatsComponent unitStats = entity.GetComponent<UnitStatsComponent>();
        if (unitStats != null)
        {
            UnityAction handler = () => HandleEntityDestroyed(entity);
            trackedDestroyHandlers[entity] = handler;
            unitStats.OnDeath.AddListener(handler);
            return;
        }

        BuildingStatsComponent buildingStats = entity.GetComponent<BuildingStatsComponent>();
        if (buildingStats != null)
        {
            UnityAction handler = () => HandleEntityDestroyed(entity);
            trackedDestroyHandlers[entity] = handler;
            buildingStats.OnBuildingDestroyed.AddListener(handler);
        }
    }

    // Désabonne l'event de mort/destruction d'une entité vivante — symétrique à TrackEntityDestruction
    private void UntrackEntityDestruction(GameObject entity)
    {
        if (entity == null || !trackedDestroyHandlers.TryGetValue(entity, out UnityAction handler)) return;

        UnitStatsComponent unitStats = entity.GetComponent<UnitStatsComponent>();
        if (unitStats != null) unitStats.OnDeath.RemoveListener(handler);

        BuildingStatsComponent buildingStats = entity.GetComponent<BuildingStatsComponent>();
        if (buildingStats != null) buildingStats.OnBuildingDestroyed.RemoveListener(handler);

        trackedDestroyHandlers.Remove(entity);
    }

    // Désabonne tous les handlers de destruction — utilisé par ClearAllSelectionSilent et OnDestroy
    private void UntrackAllEntityDestruction()
    {
        foreach (var kvp in trackedDestroyHandlers)
        {
            if (kvp.Key == null) continue;

            UnitStatsComponent unitStats = kvp.Key.GetComponent<UnitStatsComponent>();
            if (unitStats != null) unitStats.OnDeath.RemoveListener(kvp.Value);

            BuildingStatsComponent buildingStats = kvp.Key.GetComponent<BuildingStatsComponent>();
            if (buildingStats != null) buildingStats.OnBuildingDestroyed.RemoveListener(kvp.Value);
        }
        trackedDestroyHandlers.Clear();
    }

    // Appelé quand une entité sélectionnée est détruite
    // Capture la position AVANT toute opération (transform encore valide à cet instant)
    // Si d'autres unités restent sélectionnées → met à jour l'UI ; sinon → fallback SelectAtCell
    private void HandleEntityDestroyed(GameObject destroyed)
    {
        if (destroyed == null) return;

        // Capture position monde avant tout traitement — le transform est encore valide ici
        Vector3 worldPos     = destroyed.transform.position;
        Vector2Int cellDestroyed = GridManager.Instance.WorldToGrid(worldPos);

        // Retire du dict sans appeler RemoveListener (entité en cours de destruction)
        trackedDestroyHandlers.Remove(destroyed);

        if (selectedUnits.ContainsKey(destroyed))
        {
            if (selectedUnits.TryGetValue(destroyed, out SelectionSquare sq) && sq != null)
                sq.DestroySelectionSquare();
            selectedUnits.Remove(destroyed);

            if (selectedUnits.Count > 0)
            {
                OnUnitsSelected.Invoke(GetSelectedUnits());
                Debug.Log($"💀 {destroyed.name} détruit — {selectedUnits.Count} unité(s) encore sélectionnée(s)");
                return;
            }
        }
        else if (currentSelectedObject == destroyed)
        {
            if (currentObjectSelectionSquare != null)
            {
                currentObjectSelectionSquare.DestroySelectionSquare();
                currentObjectSelectionSquare = null;
            }
            currentSelectedObject = null;
        }
        else
        {
            return; // Entité détruite mais non sélectionnée — rien à faire
        }

        Debug.Log($"💀 {destroyed.name} détruit — fallback SelectAtCell({cellDestroyed})");
        SelectAtCell(cellDestroyed);
    }

    // === CALLBACKS ENTITY SPAWN EVENTS ===

    // Appelé quand une entité spawn — si le spawner était sélectionné, sélectionne le spawné
    private void HandleEntitySpawned(GameObject spawner, GameObject spawnedEntity)
    {
        if (spawner == null || spawnedEntity == null) return;

        bool spawnerWasSelected = (currentSelectedObject == spawner) || IsUnitSelected(spawner);
        if (spawnerWasSelected)
        {
            SelectGameObject(spawnedEntity);
            Debug.Log($"🔄 SelectionManager : Sélection auto {spawnedEntity.name} (spawné par {spawner.name})");
        }
    }

    // Appelé quand une entité est remplacée — si l'ancienne était sélectionnée, sélectionne la nouvelle
    private void HandleEntityReplaced(GameObject oldEntity, GameObject newEntity)
    {
        if (oldEntity == null || newEntity == null) return;

        bool oldWasSelected = (currentSelectedObject == oldEntity) || IsUnitSelected(oldEntity);
        if (oldWasSelected)
        {
            SelectGameObject(newEntity);
            Debug.Log($"🔄 SelectionManager : Sélection auto {newEntity.name} (remplace {oldEntity.name})");
        }
    }

    // Efface complètement toute sélection sans émettre d'event (usage interne)
    private void ClearAllSelectionSilent()
    {
        ClearUnitSelection();
        ClearNonUnitSelection();
    }

    // Efface complètement toute sélection et notifie l'UI
    public void ClearAllSelection()
    {
        ClearAllSelectionSilent();
        OnSelectionCleared.Invoke();
        Debug.Log("🗑️ Toute sélection effacée");
    }

    // Vérifie si un GameObject est sur un layer spécifique
    private bool IsOnLayer(GameObject obj, LayerMask layer)
    {
        return (layer & (1 << obj.layer)) != 0;
    }

    // Vérifie si une unité spécifique est actuellement sélectionnée
    private bool IsUnitSelected(GameObject unit)
    {
        return selectedUnits.ContainsKey(unit);
    }

    // Nettoie les listeners lors de la destruction pour éviter les fuites mémoire
    private void OnDestroy()
    {
        UntrackAllEntityDestruction();

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
    }

    // === METHODES PUBLIQUES ===

    // Retourne la liste de toutes les unités sélectionnées
    public List<GameObject> GetSelectedUnits()
    {
        return new List<GameObject>(selectedUnits.Keys);
    }

    // Retourne l'obstacle actuellement sélectionné (null si aucun ou si ground)
    public GameObject GetSelectedObstacle()
    {
        return !hasGroundSelection ? currentSelectedObject : null;
    }

    // Retourne les coordonnées du terrain sélectionné
    public Vector2Int GetSelectedGroundPosition()
    {
        return hasGroundSelection ? currentGroundSelection : Vector2Int.zero;
    }

    // Vérifie les types de sélection actuelle
    public bool HasUnitsSelected()    => selectedUnits.Count > 0;
    public bool HasObstacleSelected() => currentSelectedObject != null && !hasGroundSelection;
    public bool HasGroundSelected()   => hasGroundSelection;
    public bool HasAnySelection()     => HasUnitsSelected() || HasObstacleSelected() || HasGroundSelected();
    public int  GetSelectedUnitCount() => selectedUnits.Count;

    // Sélectionne un GameObject externe en le classifiant via SelectableResolver
    // Utilisé par HandleEntitySpawned/HandleEntityReplaced — efface la sélection courante
    public void SelectGameObject(GameObject unit)
    {
        if (unit == null) return;
        ClearAllSelectionSilent();

        SelectableType type = SelectableResolver.ResolveType(unit);
        switch (type)
        {
            case SelectableType.Building:
                currentSelectedObject = unit;
                hasGroundSelection = false;
                currentObjectSelectionSquare = CreateSelectionSquare();
                if (currentObjectSelectionSquare != null)
                    currentObjectSelectionSquare.AttachToGameObject(unit);
                TrackEntityDestruction(unit);
                OnBuildingSelected.Invoke(unit);
                break;

            case SelectableType.Environment:
                SelectObstacle(unit);
                break;

            default: // Unit ou None
                SelectUnit(unit);
                OnUnitsSelected.Invoke(GetSelectedUnits());
                break;
        }
    }
}
