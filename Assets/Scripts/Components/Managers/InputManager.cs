using UnityEngine;
using UnityEngine.Events;

// Centralise tous les inputs souris et clavier
// Émet des UnityEvents découplés consommés par SelectionManager et OrderManager
// Isolation de l'ancien Input System : migrer vers New Input System = modifier ce seul fichier
// MonoBehaviour singleton (compatible netcode, pas static)
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float dragThreshold = 5f; // Distance en pixels avant de déclencher un drag

    [Header("Events — Clic gauche")]
    public UnityEvent<Vector3> OnLeftClickDown = new UnityEvent<Vector3>(); // Position monde
    public UnityEvent<Vector3> OnLeftClickUp   = new UnityEvent<Vector3>(); // Position écran
    public UnityEvent<Vector3> OnDragStart     = new UnityEvent<Vector3>(); // Position écran au début du drag
    public UnityEvent<Vector3> OnDragUpdate    = new UnityEvent<Vector3>(); // Position écran pendant le drag
    public UnityEvent          OnDragEnd       = new UnityEvent();          // Fin du drag (relâche après drag)

    [Header("Events — Clic droit")]
    public UnityEvent<Vector3> OnRightClickDown = new UnityEvent<Vector3>(); // Position monde

    [Header("Debug")]
    [SerializeField] private bool debugInput = false;

    // État interne du drag
    private bool isDragging = false;
    private Vector3 mouseDownScreenPosition; // Position écran au moment du MouseButtonDown(0)

    // Cache caméra pour éviter Camera.main à chaque frame
    private Camera mainCamera;

    // Retourne si Shift gauche ou droit est maintenu
    public bool IsShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    // Initialise le singleton et cache la caméra principale
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

        if (debugInput)
        {
            Debug.Log($"InputManager: Initialisé sur {gameObject.name}");
        }
    }

    // Lit tous les inputs souris chaque frame et invoque les events correspondants
    private void Update()
    {
        // --- Clic gauche enfoncé ---
        if (Input.GetMouseButtonDown(0))
        {
            // Sauvegarde la position pour la détection du drag
            mouseDownScreenPosition = Input.mousePosition;
            isDragging = false;

            // Émet la position monde (utile pour SelectionManager)
            Vector3 worldPosition = ScreenToWorld(Input.mousePosition);
            OnLeftClickDown.Invoke(worldPosition);

            if (debugInput)
            {
                Debug.Log($"🖱️ InputManager: LeftClickDown — monde {worldPosition}, Shift: {IsShiftHeld}");
            }
        }

        // --- Clic gauche maintenu — détection et mise à jour du drag ---
        if (Input.GetMouseButton(0))
        {
            float dragDistance = Vector3.Distance(Input.mousePosition, mouseDownScreenPosition);

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
                OnDragUpdate.Invoke(Input.mousePosition);
            }
        }

        // --- Clic gauche relâché ---
        if (Input.GetMouseButtonUp(0))
        {
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
                OnLeftClickUp.Invoke(Input.mousePosition);

                if (debugInput)
                {
                    Debug.Log($"🖱️ InputManager: LeftClickUp — écran {Input.mousePosition}");
                }
            }
        }

        // --- Clic droit ---
        if (Input.GetMouseButtonDown(1))
        {
            Vector3 worldPosition = ScreenToWorld(Input.mousePosition);
            OnRightClickDown.Invoke(worldPosition);

            if (debugInput)
            {
                Debug.Log($"🖱️ InputManager: RightClickDown — monde {worldPosition}, Shift: {IsShiftHeld}");
            }
        }
    }

    // Convertit une position écran en position monde avec Z=0 pour la grille 2D
    // Utilise la caméra cachée pour ne pas appeler Camera.main à chaque frame
    private Vector3 ScreenToWorld(Vector3 screenPosition)
    {
        if (mainCamera == null) return Vector3.zero;

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        worldPosition.z = 0f; // Force Z=0 — la grille 2D est dans le plan XY
        return worldPosition;
    }
}
