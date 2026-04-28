using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// Gère la sélection d'objets avec clic simple et rectangle de sélection
// Système hybride : raycast direct + Physics2D pour rectangle
// Priorité : Unités (multiple) > Obstacles/Ground (unique, remplace unités)
// Consomme les events de InputManager — aucun accès direct à Input.*
public class SelectionManager : MonoBehaviour
{
    [Header("Selection Settings")]
    // Filtre pour détecter uniquement les objets sur le layer "Units"
    [SerializeField] private LayerMask unitLayer = 1 << 9;
    [SerializeField] private LayerMask buildingLayer = 1 << 8;
    [SerializeField] private LayerMask environmentWalkableLayer = 1 << 7;
    [SerializeField] private LayerMask obstacleLayer = 1 << 6;
    [SerializeField] private LayerMask groundLayer = 1 << 3;

    [Header("Selection Square")]
    // Prefab du carré de sélection à instancier pour chaque objet sélectionné
    [SerializeField] private GameObject selectionSquarePrefab;

    // === SELECTION MULTIPLE UNITES ===
    // Stocke toutes les unités actuellement sélectionnées avec leurs carrés
    private Dictionary<GameObject, SelectionSquare> selectedUnits = new Dictionary<GameObject, SelectionSquare>();

    // === SELECTION UNIQUE OBSTACLE/GROUND ===
    // Stocke la sélection actuelle d'obstacle ou ground (exclusif avec unités)
    private GameObject currentSelectedObject;
    private SelectionSquare currentObjectSelectionSquare;
    private Vector2Int currentGroundSelection;
    private bool hasGroundSelection;

    // === RECTANGLE SELECTION ===
    // Variables pour gérer la sélection par drag rectangle
    private bool isDragging;
    private Vector3 dragStartPosition;       // Position écran au début du drag (fournie par InputManager)
    private Vector3 lastDragScreenPosition;  // Dernière position écran reçue pendant le drag
    private LineRenderer rectangleRenderer;

    // Référence à la caméra pour convertir les clics souris en rayons 3D
    private Camera mainCamera;

    // Référence à l'InputManager (abonnement aux events, accès à IsShiftHeld)
    private InputManager inputManager;

    // Composants trackés pour détecter la destruction de l'entité sélectionnée
    private UnitStatsComponent trackedUnitStats;
    private BuildingStatsComponent trackedBuildingStats;

    [Header("Events — UI")]
    // Notifie l'UI lors de tout changement de sélection (abonnés par UnitPanelUI)
    public UnityEvent<List<GameObject>> OnUnitsSelected    = new UnityEvent<List<GameObject>>(); // Unités sélectionnées (liste vide si désélection)
    public UnityEvent<Vector2Int>       OnGroundSelected   = new UnityEvent<Vector2Int>();        // Coordonnées grille du terrain sélectionné
    public UnityEvent<GameObject>       OnObstacleSelected = new UnityEvent<GameObject>();        // Obstacle sélectionné
    public UnityEvent<GameObject>       OnBuildingSelected = new UnityEvent<GameObject>();        // Bâtiment sélectionné
    public UnityEvent                   OnSelectionCleared = new UnityEvent();                    // Toute sélection effacée

    // Méthode appelée au démarrage pour initialiser les références et composants
    private void Awake()
    {
        // Trouve la caméra principale automatiquement
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("SelectionManager nécessite une caméra principale dans la scène !");
        }

        // Vérifie que le prefab est assigné
        if (selectionSquarePrefab == null)
        {
            Debug.LogError("SelectionManager nécessite un prefab SelectionSquare assigné !");
        }

        // Crée le LineRenderer pour le rectangle de sélection
        SetupRectangleRenderer();
    }

    // S'abonne aux events de InputManager pour découpler la lecture d'input
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

        // Abonnement aux events de spawn/remplacement d'entités
        if (EntitySpawnManager.Instance != null)
        {
            EntitySpawnManager.Instance.OnEntitySpawned.AddListener(HandleEntitySpawned);
            EntitySpawnManager.Instance.OnEntityReplaced.AddListener(HandleEntityReplaced);
        }
    }

    // Configure le LineRenderer pour dessiner le rectangle de sélection
    // Utilisé une seule fois à l'initialisation pour optimiser les performances
    private void SetupRectangleRenderer()
    {
        // Crée un GameObject enfant pour le LineRenderer
        GameObject rectangleObject = new GameObject("SelectionRectangle");
        rectangleObject.transform.SetParent(transform);

        // Configure le LineRenderer pour dessiner un rectangle
        rectangleRenderer = rectangleObject.AddComponent<LineRenderer>();
        rectangleRenderer.material = new Material(Shader.Find("Sprites/Default"));
        rectangleRenderer.startColor = Color.white;
        rectangleRenderer.startWidth = 0.05f;
        rectangleRenderer.endWidth = 0.05f;
        rectangleRenderer.positionCount = 5; // 4 coins + retour au début
        rectangleRenderer.useWorldSpace = true;
        rectangleRenderer.sortingOrder = 100; // Au-dessus de tout
        rectangleRenderer.enabled = false; // Masqué par défaut
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
        if (!isDragging)
        {
            return;
        }

        lastDragScreenPosition = screenPosition;

        Debug.Log("🎨 Dessin du rectangle...");
        DrawSelectionRectangle();
    }

    // Clic simple relâché (pas de drag) — sélectionne l'objet sous le curseur
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
        if (!isDragging)
        {
            return;
        }

        CompleteRectangleSelection();
        isDragging = false;
        rectangleRenderer.enabled = false;
    }

    // --- Logique de sélection ---

    // Traite la sélection par clic simple avec système de priorité
    // Priorité : unitLayer > buildingLayer > obstacleLayer > groundLayer
    private void HandleSimpleClick(Vector3 screenPosition)
    {
        // Convertit la position écran en rayon 3D dans le monde
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        Vector3 clickWorldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        clickWorldPosition.z = 0;

        // 1. Vérifie d'abord les unités (priorité la plus haute)
        RaycastHit2D unitHit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, unitLayer);
        if (unitHit.collider != null)
        {
            HandleUnitSelection(unitHit.collider.gameObject, clickWorldPosition);
            return;
        }

        // 2. Vérifie les bâtiments (layer Buildings)
        RaycastHit2D buildingHit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, buildingLayer);
        if (buildingHit.collider != null)
        {
            HandleUnitSelection(buildingHit.collider.gameObject, clickWorldPosition);
            return;
        }

        // 2b. Vérifie les bâtiments sur Environment_Walkable (ex: Nexus)
        RaycastHit2D envBuildingHit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, environmentWalkableLayer);
        if (envBuildingHit.collider != null)
        {
            HandleUnitSelection(envBuildingHit.collider.gameObject, clickWorldPosition);
            return;
        }

        // 3. Si pas d'unité ni bâtiment, vérifie les obstacles
        RaycastHit2D obstacleHit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, obstacleLayer);
        if (obstacleHit.collider != null)
        {
            SelectObstacle(obstacleHit.collider.gameObject);
            return;
        }

        // 3. Si ni unité ni obstacle, sélection ground
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        worldPosition.z = 0;
        HandleGroundSelection(worldPosition);
    }

    // Gère la sélection d'unité avec support Shift pour sélection multiple
    // Détecte si l'objet est un bâtiment (BuildingStatsComponent) → déclenche OnBuildingSelected
    // worldPosition : position monde du clic, utilisée pour placer le carré sur la tile cl iquée
    private void HandleUnitSelection(GameObject unit, Vector3 worldPosition = default)
    {
        // Détecte si c'est un bâtiment → event dédié, carré sur la tile cliquée
        BuildingStatsComponent building = unit.GetComponent<BuildingStatsComponent>();
        if (building != null)
        {
            ClearAllSelectionSilent();
            currentSelectedObject = unit;
            hasGroundSelection = false;

            // Place le carré sur la tile cliquée (pas sur le pivot du bâtiment)
            currentObjectSelectionSquare = CreateSelectionSquare();
            if (currentObjectSelectionSquare != null)
            {
                Vector2Int clickedTile = GridManager.Instance.WorldToGrid(worldPosition);
                currentObjectSelectionSquare.AttachToGridPosition(clickedTile);
            }

            OnBuildingSelected.Invoke(unit);
            UnsubscribeEntityDestroyedEvents();
            BuildingStatsComponent buildingStats = unit.GetComponent<BuildingStatsComponent>();
            if (buildingStats != null)
            {
                trackedBuildingStats = buildingStats;
                trackedBuildingStats.OnBuildingDestroyed.AddListener(OnSelectedEntityDestroyed);
            }
            Debug.Log($"Bâtiment sélectionné : {unit.name}");
            return;
        }

        // Efface les sélections obstacle/ground (unités prioritaires)
        ClearNonUnitSelection();

        if (InputManager.Instance.IsShiftHeld)
        {
            // Mode sélection multiple - ajoute ou retire l'unité
            if (IsUnitSelected(unit))
            {
                DeselectUnit(unit);
            }
            else
            {
                SelectUnit(unit);
            }
        }
        else
        {
            // Mode sélection simple - remplace toute la sélection
            ClearUnitSelection();
            SelectUnit(unit);
        }

        // Notifie l'UI avec la liste courante (peut être vide si shift-désélection du dernier)
        List<GameObject> currentUnits = GetSelectedUnits();
        if (currentUnits.Count > 0)
        {
            OnUnitsSelected.Invoke(currentUnits);
        }
        else
        {
            OnSelectionCleared.Invoke();
        }
    }

    // Traite la sélection de terrain à partir d'une position mondiale
    // Convertit en coordonnées grille et crée la sélection ground
    private void HandleGroundSelection(Vector3 worldPosition)
    {
        Vector2Int gridCoordinates = GridManager.Instance.WorldToGrid(worldPosition);

        if (GridManager.Instance.IsInBounds(gridCoordinates))
        {
            SelectGround(gridCoordinates);
        }
        else
        {
            // Position hors grille - efface toute sélection
            ClearAllSelection();
        }
    }

    // Finalise la sélection rectangle en récupérant toutes les unités dans la zone
    // Utilise Physics2D.OverlapArea pour détecter les unités en temps réel
    private void CompleteRectangleSelection()
    {
        // Convertit les positions écran en positions monde
        Vector3 startWorld = mainCamera.ScreenToWorldPoint(dragStartPosition);
        Vector3 endWorld = mainCamera.ScreenToWorldPoint(lastDragScreenPosition);
        startWorld.z = endWorld.z = 0;

        // Calcule les coins de la zone de sélection
        Vector2 bottomLeft = new Vector2(
            Mathf.Min(startWorld.x, endWorld.x),
            Mathf.Min(startWorld.y, endWorld.y)
        );
        Vector2 topRight = new Vector2(
            Mathf.Max(startWorld.x, endWorld.x),
            Mathf.Max(startWorld.y, endWorld.y)
        );

        // Trouve toutes les unités dans la zone rectangle
        Collider2D[] unitsInArea = Physics2D.OverlapAreaAll(bottomLeft, topRight, unitLayer);

        // Efface sélections précédentes sans émettre d'event (remplacé par OnUnitsSelected)
        ClearAllSelectionSilent();

        // Sélectionne toutes les unités trouvées
        foreach (Collider2D unitCollider in unitsInArea)
        {
            SelectUnit(unitCollider.gameObject);
        }

        // Notifie l'UI avec la nouvelle liste d'unités
        OnUnitsSelected.Invoke(GetSelectedUnits());
        Debug.Log($"Rectangle sélection : {unitsInArea.Length} unités sélectionnées");
    }

    // Dessine le rectangle de sélection en temps réel pendant le drag
    // Met à jour les positions du LineRenderer pour afficher la zone
    private void DrawSelectionRectangle()
    {
        // Convertit les positions écran en positions monde
        Vector3 startWorld = mainCamera.ScreenToWorldPoint(dragStartPosition);
        Vector3 endWorld = mainCamera.ScreenToWorldPoint(lastDragScreenPosition);
        startWorld.z = endWorld.z = 0;

        // Définit les 4 coins du rectangle + retour au début
        Vector3[] corners = new Vector3[5];
        corners[0] = new Vector3(startWorld.x, startWorld.y, 0); // Bas-gauche
        corners[1] = new Vector3(endWorld.x, startWorld.y, 0);   // Bas-droite
        corners[2] = new Vector3(endWorld.x, endWorld.y, 0);     // Haut-droite
        corners[3] = new Vector3(startWorld.x, endWorld.y, 0);   // Haut-gauche
        corners[4] = corners[0]; // Retour au début pour fermer

        // Met à jour le LineRenderer
        rectangleRenderer.SetPositions(corners);
    }

    // Ajoute une unité à la sélection multiple et crée son carré visuel
    // Maintient le dictionnaire unité→carré pour la gestion de sélection
    // Toute unité est sélectionnable — la restriction d'équipe appartient à OrderManager
    private void SelectUnit(GameObject unit)
    {
        if (unit == null || IsUnitSelected(unit)) return;

        // Crée le carré de sélection pour cette unité
        SelectionSquare selectionSquare = CreateSelectionSquare();
        if (selectionSquare != null)
        {
            selectionSquare.AttachToGameObject(unit);

            // Ajoute au dictionnaire de sélection
            selectedUnits.Add(unit, selectionSquare);

            UnsubscribeEntityDestroyedEvents();
            UnitStatsComponent unitStats = unit.GetComponent<UnitStatsComponent>();
            if (unitStats != null)
            {
                trackedUnitStats = unitStats;
                trackedUnitStats.OnDeath.AddListener(OnSelectedEntityDestroyed);
            }

            Debug.Log($"Unité sélectionnée : {unit.name} (Total: {selectedUnits.Count})");
        }
    }

    // Retire une unité de la sélection multiple et détruit son carré
    // Maintient la cohérence du dictionnaire unité→carré
    private void DeselectUnit(GameObject unit)
    {
        if (unit == null || !IsUnitSelected(unit)) return;

        // Récupère et détruit le carré de sélection
        if (selectedUnits.TryGetValue(unit, out SelectionSquare selectionSquare))
        {
            if (selectionSquare != null)
            {
                selectionSquare.DestroySelectionSquare();
            }

            // Retire du dictionnaire
            selectedUnits.Remove(unit);

            Debug.Log($"Unité désélectionnée : {unit.name} (Total: {selectedUnits.Count})");
        }
    }

    // Sélectionne un obstacle (unique, remplace toute sélection unité)
    // Obstacle sélectionné en priorité si aucune unité sur la cellule
    private void SelectObstacle(GameObject obstacle)
    {
        if (obstacle == null) return;

        // Efface toutes les sélections précédentes sans émettre d'event (remplacé par OnObstacleSelected)
        ClearAllSelectionSilent();

        // Crée le carré de sélection pour l'obstacle
        currentObjectSelectionSquare = CreateSelectionSquare();
        if (currentObjectSelectionSquare != null)
        {
            currentObjectSelectionSquare.AttachToGameObject(obstacle);

            currentSelectedObject = obstacle;
            hasGroundSelection = false;

            OnObstacleSelected.Invoke(obstacle);
            Debug.Log($"Obstacle sélectionné : {obstacle.name}");
        }
    }

    // Sélectionne une cellule de terrain (unique, remplace toute sélection)
    // Ground sélectionné seulement si aucune unité ni obstacle sur la cellule
    private void SelectGround(Vector2Int gridCoordinates)
    {
        // Efface toutes les sélections précédentes sans émettre d'event (remplacé par OnGroundSelected)
        ClearAllSelectionSilent();

        // Crée le carré de sélection pour le terrain
        currentObjectSelectionSquare = CreateSelectionSquare();
        if (currentObjectSelectionSquare != null)
        {
            currentObjectSelectionSquare.AttachToGridPosition(gridCoordinates);

            currentSelectedObject = null;
            currentGroundSelection = gridCoordinates;
            hasGroundSelection = true;

            OnGroundSelected.Invoke(gridCoordinates);
            Debug.Log($"Terrain sélectionné : cellule {gridCoordinates}");
        }
    }

    // Crée une nouvelle instance du carré de sélection
    // Factory method pour instancier et configurer les carrés
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

    // Efface uniquement la sélection des unités (garde obstacle/ground)
    // Utilisé lors de sélection obstacle/ground pour les remplacer
    private void ClearUnitSelection()
    {
        UnsubscribeEntityDestroyedEvents();
        foreach (var kvp in selectedUnits)
        {
            if (kvp.Value != null)
            {
                kvp.Value.DestroySelectionSquare();
            }
        }
        selectedUnits.Clear();
        Debug.Log("Sélection unités effacée");
    }

    // Efface uniquement la sélection obstacle/ground (garde unités)
    // Utilisé lors de sélection unité pour les remplacer
    private void ClearNonUnitSelection()
    {
        if (currentObjectSelectionSquare != null)
        {
            currentObjectSelectionSquare.DestroySelectionSquare();
            currentObjectSelectionSquare = null;
        }

        currentSelectedObject = null;
        hasGroundSelection = false;
        Debug.Log("Sélection obstacle/ground effacée");
    }

    // Appelé quand l'entité sélectionnée est détruite — fallback vers terrain ou clear
    private void OnSelectedEntityDestroyed()
    {
        // Récupère la position de l'entité détruite
        Vector3 lastPosition = Vector3.zero;

        if (trackedUnitStats != null)
        {
            lastPosition = trackedUnitStats.transform.position;
        }
        else if (trackedBuildingStats != null)
        {
            lastPosition = trackedBuildingStats.transform.position;
        }

        // Fallback : sélectionne le terrain sous l'entité détruite
        if (lastPosition != Vector3.zero)
        {
            Vector2Int gridPosition = GridManager.Instance.WorldToGrid(lastPosition);
            if (GridManager.Instance.IsInBounds(gridPosition))
            {
                SelectGround(gridPosition);
                Debug.Log($"🔄 SelectionManager : Fallback vers terrain {gridPosition} après destruction d'entité");
                return;
            }
        }

        // Si pas de position valide, clear total
        ClearAllSelection();
    }

    // === CALLBACKS ENTITY SPAWN EVENTS ===

    // Appelé quand une entité spawn (sans remplacer le spawner)
    // Si le spawner était sélectionné → sélectionne l'entité spawnée
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

    // Appelé quand une entité est remplacée par une autre
    // Si l'ancienne entité était sélectionnée → sélectionne la nouvelle
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

    // Désabonne les events de mort/destruction de l'entité trackée
    private void UnsubscribeEntityDestroyedEvents()
    {
        if (trackedUnitStats != null)
        {
            trackedUnitStats.OnDeath.RemoveListener(OnSelectedEntityDestroyed);
            trackedUnitStats = null;
        }
        if (trackedBuildingStats != null)
        {
            trackedBuildingStats.OnBuildingDestroyed.RemoveListener(OnSelectedEntityDestroyed);
            trackedBuildingStats = null;
        }
    }

    // Efface complètement toute sélection sans émettre d'event (usage interne uniquement)
    // Appelé par SelectObstacle/SelectGround/CompleteRectangleSelection avant de définir une nouvelle sélection
    private void ClearAllSelectionSilent()
    {
        ClearUnitSelection();
        ClearNonUnitSelection();
    }

    // Efface complètement toute sélection (unités + obstacle/ground) et notifie l'UI
    // Méthode publique pour reset total depuis l'extérieur
    public void ClearAllSelection()
    {
        ClearAllSelectionSilent();
        OnSelectionCleared.Invoke();
        Debug.Log("Toute sélection effacée");
    }

    // Vérifie si un GameObject est sur un layer spécifique
    // Utilitaire pour tester l'appartenance aux layers de sélection
    private bool IsOnLayer(GameObject obj, LayerMask layer)
    {
        return (layer & (1 << obj.layer)) != 0;
    }

    // Vérifie si une unité spécifique est actuellement sélectionnée
    // Utilisé pour la logique Shift+clic (ajout/retrait)
    private bool IsUnitSelected(GameObject unit)
    {
        return selectedUnits.ContainsKey(unit);
    }

    // Nettoie les listeners lors de la destruction pour éviter les fuites mémoire
    private void OnDestroy()
    {
        UnsubscribeEntityDestroyedEvents();

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

    // === METHODES PUBLIQUES POUR ACCES EXTERNE ===

    // Retourne la liste de toutes les unités sélectionnées
    // Interface principale pour les autres systèmes (mouvement, combat)
    public List<GameObject> GetSelectedUnits()
    {
        return new List<GameObject>(selectedUnits.Keys);
    }

    // Retourne l'obstacle actuellement sélectionné (null si aucun)
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
    public bool HasUnitsSelected()
    {
        return selectedUnits.Count > 0;
    }

    public bool HasObstacleSelected()
    {
        return currentSelectedObject != null && !hasGroundSelection;
    }

    public bool HasGroundSelected()
    {
        return hasGroundSelection;
    }

    public bool HasAnySelection()
    {
        return HasUnitsSelected() || HasObstacleSelected() || HasGroundSelected();
    }

    // Retourne le nombre d'unités sélectionnées
    public int GetSelectedUnitCount()
    {
        return selectedUnits.Count;
    }

    // Sélectionne un GameObject externe (ex: Bud après transformation Rootborn)
    // Efface la sélection courante et sélectionne l'unité donnée
    public void SelectGameObject(GameObject unit)
    {
        if (unit == null) return;
        ClearAllSelectionSilent();

        BuildingStatsComponent building = unit.GetComponent<BuildingStatsComponent>();
        if (building != null)
        {
            currentSelectedObject = unit;
            hasGroundSelection = false;
            currentObjectSelectionSquare = CreateSelectionSquare();
            if (currentObjectSelectionSquare != null)
                currentObjectSelectionSquare.AttachToGameObject(unit);
            OnBuildingSelected.Invoke(unit);
        }
        else
        {
            SelectUnit(unit);
            OnUnitsSelected.Invoke(GetSelectedUnits());
        }
    }
}
