using UnityEngine;

// Gère le curseur hardware Unity à partir d'un CursorData assigné dans la scène bootstrap
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Cursor")]
    [SerializeField] private CursorData cursorData;

    // Initialise le singleton MonoBehaviour et applique le curseur au démarrage
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ApplyCursor();
    }

    // Réinitialise le curseur Unity quand ce manager est détruit
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    // Applique le curseur configuré depuis l'Inspector ou le menu contextuel éditeur
    [ContextMenu("Apply Cursor")]
    public void ApplyCursor()
    {
        if (cursorData == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, "🖱️ CursorManager : CursorData non assigné, curseur par défaut conservé", this);
            return;
        }

        if (cursorData.cursorTexture == null)
        {
            GameLogger.Warn(GameLogger.Category.UI, "🖱️ CursorManager : texture de curseur non assignée. Importer la Texture2D en mode Cursor dans Unity.", this);
            return;
        }

        Cursor.SetCursor(cursorData.cursorTexture, cursorData.hotspot, cursorData.cursorMode);
        GameLogger.Log(GameLogger.Category.UI, "🖱️ CursorManager : curseur personnalisé appliqué. Vérifier que la texture est importée en mode Cursor.", this);
    }
}
