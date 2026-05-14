using UnityEngine;
using TMPro;

// Tooltip générique flottant affiché au survol d'éléments interactifs (ItemSlot, SacrificeButton)
// Singleton MonoBehaviour — accessible via TooltipUI.Instance
// Suit le curseur avec un offset fixe, s'affiche/masque via Show()/Hide()
//
// SETUP PREFAB — Hiérarchie attendue :
//   TooltipUI (Canvas, Screen Space Overlay, Sorting Order 200)
//   └── TooltipPanel (Image fond sombre, VerticalLayoutGroup)
//       ├── TitleText  (TextMeshProUGUI — bold, blanc)
//       └── BodyText   (TextMeshProUGUI — normal, gris clair)
public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    [Header("Références UI")]
    [SerializeField] private GameObject tooltipPanel;     // Panneau racine du tooltip (activé/désactivé)
    [SerializeField] private TextMeshProUGUI titleText;   // Texte titre (bold)
    [SerializeField] private TextMeshProUGUI bodyText;    // Texte description

    [Header("Paramètres")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(16f, -16f); // Décalage par rapport au curseur

    // Canvas racine pour la conversion de coordonnées écran → canvas
    private RectTransform tooltipRect;
    private Canvas rootCanvas;
    private bool isVisible = false;

    // Initialise le singleton et cache les références nécessaires
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            GameLogger.Warn(GameLogger.Category.UI, "TooltipUI: Instance dupliquée, destruction.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Récupère le RectTransform du panneau pour le positionnement
        if (tooltipPanel != null)
        {
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        }

        // Trouve le Canvas racine pour les conversions de coordonnées
        rootCanvas = GetComponentInParent<Canvas>();

        if (bodyText != null)
        {
            bodyText.richText = true;
        }

        // Masqué par défaut
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    // Met à jour la position du tooltip en suivant le curseur chaque frame
    private void Update()
    {
        if (!isVisible) return;

        UpdateTooltipPosition();
    }

    // Affiche le tooltip avec un titre et une description
    // Appelé au OnPointerEnter des éléments interactifs
    public void Show(string title, string description)
    {
        if (tooltipPanel == null) return;

        // Remplit les textes
        if (titleText != null)
        {
            titleText.text = title;
        }

        if (bodyText != null)
        {
            bodyText.text = description;
        }

        tooltipPanel.SetActive(true);
        isVisible = true;

        // Positionne immédiatement avant le premier Update pour éviter le flash
        UpdateTooltipPosition();

        GameLogger.Log(GameLogger.Category.UI, $"💬 TooltipUI: Affiché — \"{title}\"", this);
    }

    // Masque le tooltip
    // Appelé au OnPointerExit des éléments interactifs
    public void Hide()
    {
        if (tooltipPanel == null) return;

        tooltipPanel.SetActive(false);
        isVisible = false;
    }

    // Repositionne le panneau tooltip près du curseur avec offset
    // Utilise les coordonnées canvas pour un placement correct en Screen Space Overlay
    private void UpdateTooltipPosition()
    {
        if (tooltipRect == null) return;

        Vector2 mousePos = Input.mousePosition; // Compatible avec les deux Input Systems pour la position UI

        // Convertit la position souris en coordonnées locales du canvas racine
        if (rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            tooltipRect.position = new Vector3(
                mousePos.x + cursorOffset.x,
                mousePos.y + cursorOffset.y,
                0f
            );
        }
        else if (rootCanvas != null)
        {
            // Pour les autres modes de rendu (Camera, WorldSpace)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(),
                mousePos,
                rootCanvas.worldCamera,
                out Vector2 localPoint
            );
            tooltipRect.localPosition = localPoint + cursorOffset;
        }

        // Empêche le tooltip de sortir des bords de l'écran
        ClampToScreen();
    }

    // Ajuste la position pour garder le tooltip dans les limites de l'écran
    private void ClampToScreen()
    {
        if (tooltipRect == null) return;

        Vector3 pos = tooltipRect.position;
        float panelWidth  = tooltipRect.rect.width;
        float panelHeight = tooltipRect.rect.height;

        // Ajustement horizontal (débordement à droite)
        if (pos.x + panelWidth > Screen.width)
        {
            pos.x = Screen.width - panelWidth;
        }

        // Ajustement vertical (débordement en bas)
        if (pos.y - panelHeight < 0)
        {
            pos.y = panelHeight;
        }

        tooltipRect.position = pos;
    }
}
