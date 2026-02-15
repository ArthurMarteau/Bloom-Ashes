using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Gère la sélection d'objets avec clic simple et rectangle de sélection
// Système hybride : raycast direct + Physics2D pour rectangle
// Priorité : Unités (multiple) > Obstacles/Ground (unique, remplace unités)
public class SelectionManager : MonoBehaviour
{
    [Header("Selection Settings")]
    // Filtre pour détecter uniquement les objets sur le layer "Units" (Layer 8)
    [SerializeField] private LayerMask unitLayer = 1 << 8;
    [SerializeField] private LayerMask obstacleLayer = 1 << 6;
    [SerializeField] private LayerMask groundLayer = 1 << 3;

    [Header("Selection Square")]
    // Prefab du carré de sélection à instancier pour chaque objet sélectionné
    [SerializeField] private GameObject selectionSquarePrefab;

    [Header("Rectangle Selection")]
    // Délai minimum pour déclencher la sélection rectangle (évite clic accidentel)
    [SerializeField] private float rectangleSelectionDelay = 0.1f;

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
    private Vector3 dragStartPosition;
    private float dragStartTime;
    private LineRenderer rectangleRenderer;

    // Référence à la caméra pour convertir les clics souris en rayons 3D
    private Camera mainCamera;

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

    // Méthode appelée chaque frame pour gérer les interactions de sélection
    // Gère le clic simple, le début/fin du drag et la mise à jour du rectangle
    private void Update()
    {
        HandleSelectionInput();
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

    // Point d'entrée principal pour gérer tous les types d'input de sélection et mouvement
    // Détermine s'il s'agit d'un clic simple, sélection rectangle ou ordre de mouvement
    private void HandleSelectionInput()
    {
        // Ignore les clics sur l'interface utilisateur (boutons, menus)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Début du clic gauche - démarre potentiellement une sélection
        if (Input.GetMouseButtonDown(0))
        {
            StartPotentialSelection();
        }
        // Pendant le maintien du clic - met à jour le rectangle si nécessaire
        else if (Input.GetMouseButton(0))
        {
            // Vérifie si la souris a bougé pour déclencher le rectangle
            if (Vector3.Distance(Input.mousePosition, dragStartPosition) > 5f || isDragging)
            {
                UpdateRectangleSelection();
            }
        }
        // Fin du clic gauche - finalise la sélection (simple ou rectangle)
        else if (Input.GetMouseButtonUp(0))
        {
            EndSelection();
        }
    }

    // Démarre une sélection potentielle et détermine le type (simple/rectangle)
    // Enregistre la position et le temps pour décider ultérieurement du type
    private void StartPotentialSelection()
    {
        Debug.Log($"🎯 START Selection - MousePos: {Input.mousePosition}");
        // Enregistre la position de début et le temps pour le rectangle
        dragStartPosition = Input.mousePosition;
        dragStartTime = Time.time;
        isDragging = false; // Pas encore en mode drag
    }

    // Met à jour le rectangle de sélection pendant le drag
    // Active le mode rectangle si délai écoulé et dessine le rectangle en temps réel
    private void UpdateRectangleSelection()
    {
        float timeSinceStart = Time.time - dragStartTime;
        Debug.Log($"Update Rectangle: Time={timeSinceStart}, IsDragging={isDragging}, Delay={rectangleSelectionDelay}");

        // Vérifie si le délai minimum est écoulé pour activer le rectangle
        if (!isDragging && timeSinceStart > rectangleSelectionDelay)
        {
            isDragging = true;
            rectangleRenderer.enabled = true;
            Debug.Log("🟢 Mode rectangle ACTIVÉ !");
        }

        // Si en mode rectangle, met à jour le dessin
        if (isDragging)
        {
            Debug.Log("🎨 Dessin du rectangle...");
            DrawSelectionRectangle();
        }
    }

    // Finalise la sélection selon le mode actuel (simple ou rectangle)
    // Point de décision final pour traiter le résultat de l'interaction
    private void EndSelection()
    {
        if (isDragging)
        {
            // Mode rectangle - sélectionne toutes les unités dans la zone
            CompleteRectangleSelection();
            isDragging = false;
            rectangleRenderer.enabled = false;
        }
        else
        {
            // Mode clic simple - sélectionne l'objet sous le curseur
            HandleSimpleClick();
        }
    }

    // Traite la sélection par clic simple avec système de priorité
    // Priorité : unitLayer > obstacleLayer > groundLayer (pas de replacement)
    private void HandleSimpleClick()
    {
        // Convertit la position souris en rayon 3D dans le monde
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // 1. Vérifie d'abord les unités (priorité la plus haute)
        RaycastHit2D unitHit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, unitLayer);
        if (unitHit.collider != null)
        {
            // Unité détectée - gestion sélection multiple avec Shift
            HandleUnitSelection(unitHit.collider.gameObject);
            return;
        }

        // 2. Si pas d'unité, vérifie les obstacles
        RaycastHit2D obstacleHit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, obstacleLayer);
        if (obstacleHit.collider != null)
        {
            // Obstacle détecté - sélection unique (remplace unités)
            SelectObstacle(obstacleHit.collider.gameObject);
            return;
        }

        // 3. Si ni unité ni obstacle, sélection ground
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        worldPosition.z = 0;
        HandleGroundSelection(worldPosition);
    }

    // Gère la sélection d'unité avec support Shift pour sélection multiple
    // Lien avec SelectUnit/DeselectUnit selon l'état actuel
    private void HandleUnitSelection(GameObject unit)
    {
        // Efface les sélections obstacle/ground (unités prioritaires)
        ClearNonUnitSelection();

        if (Input.GetKey(KeyCode.LeftShift))
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
        Vector3 endWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
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

        // Efface sélections précédentes
        ClearAllSelection();

        // Sélectionne toutes les unités trouvées
        foreach (Collider2D unitCollider in unitsInArea)
        {
            SelectUnit(unitCollider.gameObject);
        }

        Debug.Log($"Rectangle sélection : {unitsInArea.Length} unités sélectionnées");
    }

    // Dessine le rectangle de sélection en temps réel pendant le drag
    // Met à jour les positions du LineRenderer pour afficher la zone
    private void DrawSelectionRectangle()
    {
        // Convertit les positions écran en positions monde
        Vector3 startWorld = mainCamera.ScreenToWorldPoint(dragStartPosition);
        Vector3 endWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
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

        // Efface toutes les sélections précédentes
        ClearAllSelection();

        // Crée le carré de sélection pour l'obstacle
        currentObjectSelectionSquare = CreateSelectionSquare();
        if (currentObjectSelectionSquare != null)
        {
            currentObjectSelectionSquare.AttachToGameObject(obstacle);

            currentSelectedObject = obstacle;
            hasGroundSelection = false;

            Debug.Log($"Obstacle sélectionné : {obstacle.name}");
        }
    }

    // Sélectionne une cellule de terrain (unique, remplace toute sélection)
    // Ground sélectionné seulement si aucune unité ni obstacle sur la cellule
    private void SelectGround(Vector2Int gridCoordinates)
    {
        // Efface toutes les sélections précédentes
        ClearAllSelection();

        // Crée le carré de sélection pour le terrain
        currentObjectSelectionSquare = CreateSelectionSquare();
        if (currentObjectSelectionSquare != null)
        {
            currentObjectSelectionSquare.AttachToGridPosition(gridCoordinates);

            currentSelectedObject = null;
            currentGroundSelection = gridCoordinates;
            hasGroundSelection = true;

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

    // Efface complètement toute sélection (unités + obstacle/ground)
    // Méthode publique pour reset total depuis l'extérieur
    public void ClearAllSelection()
    {
        ClearUnitSelection();
        ClearNonUnitSelection();
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
}