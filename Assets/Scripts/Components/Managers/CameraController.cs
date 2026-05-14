using UnityEngine;
using UnityEngine.InputSystem;

// Contrôle la caméra 2D via clavier, edge scrolling et zoom molette
// Expose une API simple pour les futurs systèmes comme la minimap
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions; // Référence à BloomAndAshesInputActions

    [Header("Mouvement")]
    [SerializeField] private float keyboardSpeed = 15f;     // Unités/sec via clavier
    [SerializeField] private float edgeScrollSpeed = 12f;   // Unités/sec via edge scroll

    [Header("Edge Scrolling")]
    [SerializeField] private bool edgeScrollEnabled = true; // Toggleable via API publique
    [SerializeField] private float edgeMargin = 20f;        // Pixels depuis le bord écran

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;          // Unités d'orthographicSize/tick
    [SerializeField] private float zoomMin = 4f;            // Plus zoomé (in)
    [SerializeField] private float zoomMax = 12f;           // Moins zoomé (out)
    [SerializeField] private float zoomSmoothing = 8f;      // Lerp smoothing

    [Header("Bornes")]
    [SerializeField] private bool clampToGrid = true;       // Limite caméra aux bornes GridManager

    [Header("Debug")]
    [SerializeField] private bool debugCamera = false;

    private Camera cam;
    private InputAction cameraMoveAction;
    private InputAction cameraZoomAction;
    private float targetZoom;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameLogger.Warn(GameLogger.Category.Movement, $"CameraController: Instance dupliquée sur {gameObject.name}, destruction.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        cam = GetComponent<Camera>();
        if (cam == null)
        {
            GameLogger.Error("CameraController: Camera introuvable sur ce GameObject.", this);
            return;
        }

        targetZoom = cam.orthographicSize;
        CacheInputActions();

        if (debugCamera)
        {
            LogStartupState();
        }
    }

    private void OnEnable()
    {
        if (inputActions == null) return;

        InputActionMap gameplayMap = inputActions.FindActionMap("Gameplay", throwIfNotFound: false);
        gameplayMap?.Enable();
    }

    private void OnDisable()
    {
        // Le map Gameplay reste géré par InputManager.
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (cam == null) return;

        Vector2 keyboardInput = cameraMoveAction != null ? cameraMoveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector2 edgeInput = edgeScrollEnabled ? GetEdgeScrollDirection() : Vector2.zero;
        Vector2 moveDir = keyboardInput * keyboardSpeed + edgeInput * edgeScrollSpeed;

        if (moveDir != Vector2.zero)
        {
            transform.position += new Vector3(moveDir.x, moveDir.y, 0f) * Time.deltaTime;
        }

        Vector2 scroll = cameraZoomAction != null ? cameraZoomAction.ReadValue<Vector2>() : Vector2.zero;
        if (scroll.y != 0f)
        {
            targetZoom -= scroll.y * zoomSpeed * Time.deltaTime;
            targetZoom = Mathf.Clamp(targetZoom, zoomMin, zoomMax);
        }

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, zoomSmoothing * Time.deltaTime);

        if (clampToGrid)
        {
            ClampPositionToGrid();
        }
    }

    // Déplace directement la caméra vers une position monde
    public void MoveTo(Vector3 worldPosition)
    {
        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);

        if (clampToGrid)
        {
            ClampPositionToGrid();
        }

        if (debugCamera)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"CameraController: MoveTo {worldPosition}", this);
        }
    }

    // Centre la caméra sur une case de grille
    public void FocusOn(Vector2Int gridPosition)
    {
        if (GridManager.Instance == null) return;

        Vector3 world = GridManager.Instance.GridToWorld(gridPosition);
        MoveTo(world);

        if (debugCamera)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"CameraController: FocusOn {gridPosition} -> {world}", this);
        }
    }

    public void SetEdgeScrollEnabled(bool enabled) => edgeScrollEnabled = enabled;
    public bool GetEdgeScrollEnabled() => edgeScrollEnabled;

    private void CacheInputActions()
    {
        if (inputActions == null)
        {
            GameLogger.Error("CameraController: InputActionAsset non assigné ! Assigner BloomAndAshesInputActions dans l'Inspector.", this);
            return;
        }

        InputActionMap gameplayMap = inputActions.FindActionMap("Gameplay", throwIfNotFound: false);
        if (gameplayMap == null)
        {
            GameLogger.Error("CameraController: Map Gameplay introuvable dans l'InputActionAsset.", this);
            return;
        }

        cameraMoveAction = gameplayMap.FindAction("CameraMove", throwIfNotFound: false);
        cameraZoomAction = gameplayMap.FindAction("CameraZoom", throwIfNotFound: false);

        if (cameraMoveAction == null)
        {
            GameLogger.Error("CameraController: Action CameraMove introuvable dans le map Gameplay.", this);
        }

        if (cameraZoomAction == null)
        {
            GameLogger.Error("CameraController: Action CameraZoom introuvable dans le map Gameplay.", this);
        }
    }

    private Vector2 GetEdgeScrollDirection()
    {
        if (Mouse.current == null) return Vector2.zero;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        if (mousePosition.x < 0f || mousePosition.x > Screen.width ||
            mousePosition.y < 0f || mousePosition.y > Screen.height)
        {
            return Vector2.zero;
        }

        Vector2 direction = Vector2.zero;

        if (mousePosition.x <= edgeMargin)
        {
            direction.x = -1f;
        }
        else if (mousePosition.x >= Screen.width - edgeMargin)
        {
            direction.x = 1f;
        }

        if (mousePosition.y <= edgeMargin)
        {
            direction.y = -1f;
        }
        else if (mousePosition.y >= Screen.height - edgeMargin)
        {
            direction.y = 1f;
        }

        return direction;
    }

    private void ClampPositionToGrid()
    {
        if (cam == null || GridManager.Instance == null) return;

        Vector2Int gridSize = GridManager.Instance.GetGridSize();
        float cellSize = GridManager.Instance.GetCellSize();
        var (originX, originY) = GridSystem.CalculateOrigin(gridSize.x, gridSize.y, cellSize);

        float minX = originX;
        float minY = originY;
        float maxX = originX + gridSize.x * cellSize;
        float maxY = originY + gridSize.y * cellSize;

        float vertExtent = cam.orthographicSize;
        float horizExtent = vertExtent * cam.aspect;

        Vector3 position = transform.position;

        if (maxX - minX <= horizExtent * 2f)
        {
            position.x = (minX + maxX) * 0.5f;
        }
        else
        {
            position.x = Mathf.Clamp(position.x, minX + horizExtent, maxX - horizExtent);
        }

        if (maxY - minY <= vertExtent * 2f)
        {
            position.y = (minY + maxY) * 0.5f;
        }
        else
        {
            position.y = Mathf.Clamp(position.y, minY + vertExtent, maxY - vertExtent);
        }

        transform.position = position;
    }

    private void LogStartupState()
    {
        if (GridManager.Instance == null)
        {
            GameLogger.Log(GameLogger.Category.Movement, $"CameraController: Initialisé sans GridManager, position {transform.position}", this);
            return;
        }

        Vector2Int gridSize = GridManager.Instance.GetGridSize();
        float cellSize = GridManager.Instance.GetCellSize();
        GameLogger.Log(GameLogger.Category.Movement, $"CameraController: Grille {gridSize.x}x{gridSize.y}, cellSize {cellSize}, position initiale {transform.position}", this);
    }
}
