using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Centralise tous les inputs souris et clavier via le New Input System
// Émet des UnityEvents découplés consommés par SelectionManager et OrderManager
// Isolation du système d'input : modifier ce seul fichier pour changer d'input system
// MonoBehaviour singleton (compatible netcode, pas static)
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float dragThreshold = 5f; // Distance en pixels avant de déclencher un drag
    [SerializeField] private InputActionAsset inputActions; // Référence à BloomAndAshesInputActions.inputactions

    [Header("Events — Clic gauche")]
    public UnityEvent<Vector3> OnLeftClickDown = new UnityEvent<Vector3>(); // Position monde
    public UnityEvent<Vector3> OnLeftClickUp   = new UnityEvent<Vector3>(); // Position écran
    public UnityEvent<Vector3> OnDragStart     = new UnityEvent<Vector3>(); // Position écran au début du drag
    public UnityEvent<Vector3> OnDragUpdate    = new UnityEvent<Vector3>(); // Position écran pendant le drag
    public UnityEvent          OnDragEnd       = new UnityEvent();          // Fin du drag (relâche après drag)

    [Header("Events — Clic droit")]
    public UnityEvent<Vector3> OnRightClickDown = new UnityEvent<Vector3>(); // Position monde

    [Header("Events — Clavier")]
    public UnityEvent OnAbilityKeyPressed = new UnityEvent();

    [Header("Debug")]
    [SerializeField] private bool debugInput = false;

    // Actions extraites du map "Gameplay"
    private InputAction leftClickAction;
    private InputAction rightClickAction;
    private InputAction mousePositionAction;
    private InputAction shiftAction;
    private InputAction abilityKeyAction;

    // État interne du drag
    private bool isDragging = false;
    private Vector3 mouseDownScreenPosition; // Position écran au moment du LeftClick.started
    private bool isLeftClickHeld = false;    // Vrai tant que le bouton gauche est maintenu

    // Cache de la position souris courante (mis à jour via callback MousePosition)
    private Vector2 currentMousePosition;

    // Cache caméra pour éviter Camera.main à chaque frame
    private Camera mainCamera;

    // Retourne si Shift gauche ou droit est maintenu — utilise l'InputAction Shift
    public bool IsShiftHeld => shiftAction != null && shiftAction.IsPressed();

    // Initialise le singleton, cache la caméra et récupère les InputActions
    private void Awake()
    {
        // Singleton MonoBehaviour (non-static, compatible netcode)
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"InputManager: Instance dupliquée sur {gameObject.name}, destruction.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Cache la caméra principale une seule fois
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("InputManager: Camera.main introuvable !");
        }

        // Vérifie que l'asset d'actions est bien assigné
        if (inputActions == null)
        {
            Debug.LogError("InputManager: InputActionAsset non assigné ! Assigner BloomAndAshesInputActions dans l'Inspector.");
            return;
        }

        // Récupère les actions depuis le map "Gameplay"
        InputActionMap gameplayMap = inputActions.FindActionMap("Gameplay", throwIfNotFound: true);
        leftClickAction      = gameplayMap.FindAction("LeftClick",     throwIfNotFound: true);
        rightClickAction     = gameplayMap.FindAction("RightClick",    throwIfNotFound: true);
        mousePositionAction  = gameplayMap.FindAction("MousePosition", throwIfNotFound: true);
        shiftAction          = gameplayMap.FindAction("Shift",         throwIfNotFound: true);
        abilityKeyAction     = gameplayMap.FindAction("AbilityKey",    throwIfNotFound: true);

        if (debugInput)
        {
            Debug.Log($"✅ InputManager: Initialisé sur {gameObject.name} avec New Input System");
        }
    }

    // Abonne les callbacks et active les actions quand le composant s'active
    private void OnEnable()
    {
        if (inputActions == null) return;

        // Abonnements : LeftClick
        leftClickAction.started  += OnLeftClickStarted;   // Équivalent GetMouseButtonDown(0)
        leftClickAction.canceled += OnLeftClickCanceled;  // Équivalent GetMouseButtonUp(0)

        // Abonnements : RightClick
        rightClickAction.started += OnRightClickStarted; // Équivalent GetMouseButtonDown(1)

        // Abonnements : MousePosition (mis à jour en continu)
        mousePositionAction.performed += OnMousePositionPerformed;

        // Abonnements : clavier
        abilityKeyAction.started += OnAbilityKeyStarted;

        // Active le map entier
        inputActions.FindActionMap("Gameplay").Enable();
    }

    // Désabonne les callbacks et désactive les actions quand le composant se désactive
    private void OnDisable()
    {
        if (inputActions == null) return;

        leftClickAction.started  -= OnLeftClickStarted;
        leftClickAction.canceled -= OnLeftClickCanceled;
        rightClickAction.started -= OnRightClickStarted;
        mousePositionAction.performed -= OnMousePositionPerformed;
        abilityKeyAction.started -= OnAbilityKeyStarted;

        inputActions.FindActionMap("Gameplay").Disable();
    }

    // Libère les ressources natives de l'InputActionAsset
    private void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.FindActionMap("Gameplay")?.Disable();
        }
    }

    // Callback : bouton gauche enfoncé — équivalent GetMouseButtonDown(0)
    private void OnLeftClickStarted(InputAction.CallbackContext ctx)
    {
        // Sauvegarde la position pour la détection du drag
        mouseDownScreenPosition = currentMousePosition;
        isDragging = false;
        isLeftClickHeld = true;

        // Émet la position monde (utile pour SelectionManager)
        Vector3 worldPosition = ScreenToWorld(currentMousePosition);
        OnLeftClickDown.Invoke(worldPosition);

        if (debugInput)
        {
            Debug.Log($"🖱️ InputManager: LeftClickDown — monde {worldPosition}, Shift: {IsShiftHeld}");
        }
    }

    // Callback : bouton gauche relâché — équivalent GetMouseButtonUp(0)
    private void OnLeftClickCanceled(InputAction.CallbackContext ctx)
    {
        isLeftClickHeld = false;

        if (isDragging)
        {
            // Fin du drag : le relâché n'est pas un clic simple
            isDragging = false;
            OnDragEnd.Invoke();

            if (debugInput)
            {
                Debug.Log($"🖱️ InputManager: DragEnd");
            }
        }
        else
        {
            // Pas de drag : c'est un vrai clic simple
            OnLeftClickUp.Invoke(currentMousePosition);

            if (debugInput)
            {
                Debug.Log($"🖱️ InputManager: LeftClickUp — écran {currentMousePosition}");
            }
        }
    }

    // Callback : bouton droit enfoncé — équivalent GetMouseButtonDown(1)
    private void OnRightClickStarted(InputAction.CallbackContext ctx)
    {
        Vector3 worldPosition = ScreenToWorld(currentMousePosition);
        OnRightClickDown.Invoke(worldPosition);

        if (debugInput)
        {
            Debug.Log($"🖱️ InputManager: RightClickDown — monde {worldPosition}, Shift: {IsShiftHeld}");
        }
    }

    // Callback : position souris mise à jour — met à jour le cache de position
    private void OnMousePositionPerformed(InputAction.CallbackContext ctx)
    {
        currentMousePosition = ctx.ReadValue<Vector2>();
    }

    // Callback : touche d'ability enfoncée — émet l'event découplé pour les handlers gameplay
    private void OnAbilityKeyStarted(InputAction.CallbackContext ctx)
    {
        OnAbilityKeyPressed.Invoke();

        if (debugInput)
        {
            Debug.Log("⌨️ InputManager: AbilityKey pressée");
        }
    }

    // Gère la détection et la mise à jour du drag chaque frame
    // La logique drag reste dans Update() car elle nécessite une évaluation continue
    private void Update()
    {
        if (!isLeftClickHeld) return;

        float dragDistance = Vector3.Distance(currentMousePosition, mouseDownScreenPosition);

        if (!isDragging && dragDistance >= dragThreshold)
        {
            // Seuil franchi : démarre le drag depuis la position initiale du clic
            isDragging = true;
            OnDragStart.Invoke(mouseDownScreenPosition);

            if (debugInput)
            {
                Debug.Log($"🖱️ InputManager: DragStart — écran {mouseDownScreenPosition}");
            }
        }

        if (isDragging)
        {
            // Met à jour la position courante du drag chaque frame
            OnDragUpdate.Invoke(currentMousePosition);
        }
    }

    // Convertit une position écran en position monde avec Z=0 pour la grille 2D
    // Utilise la caméra cachée pour ne pas appeler Camera.main à chaque frame
    private Vector3 ScreenToWorld(Vector2 screenPosition)
    {
        if (mainCamera == null) return Vector3.zero;

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        worldPosition.z = 0f; // Force Z=0 — la grille 2D est dans le plan XY
        return worldPosition;
    }
}
