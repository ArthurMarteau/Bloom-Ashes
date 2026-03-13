using UnityEngine;

// Attaché à la caméra principale
// Détecte le sprite sous le curseur via OverlapPoint et active son outline coloré selon les HP
public class OutlineController : MonoBehaviour
{
    private static readonly Color ColorHealthFull     = new Color(0.2f, 0.9f, 0.2f, 1f); // > 75% : vert
    private static readonly Color ColorHealthMid      = new Color(0.9f, 0.9f, 0.1f, 1f); // > 50% : jaune
    private static readonly Color ColorHealthLow      = new Color(1.0f, 0.5f, 0.0f, 1f); // > 25% : orange
    private static readonly Color ColorHealthCritical = new Color(0.9f, 0.1f, 0.1f, 1f); // ≤ 25% : rouge

    private Camera mainCamera;
    private HighlightTarget currentTarget;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("OutlineController: Camera.main introuvable !");
            enabled = false;
        }
    }

    // Détecte l'objet sous le curseur via OverlapPoint (fiable en 2D top-down)
    private void Update()
    {
        // nearClipPlane corrige le Z pour une caméra orthographique 2D centrée sur une grille
        Vector3 screenPos  = new Vector3(Input.mousePosition.x, Input.mousePosition.y, mainCamera.nearClipPlane);
        Vector3 worldPos   = mainCamera.ScreenToWorldPoint(screenPos);
        Vector2 worldPos2D = new Vector2(worldPos.x, worldPos.y);

        // OverlapPointAll : cherche un HighlightTarget parmi tous les colliders (ignore Tilemap etc.)
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos2D);
        HighlightTarget target = null;
        foreach (Collider2D col in hits)
        {
            HighlightTarget ht = col.GetComponent<HighlightTarget>();
            if (ht != null) { target = ht; break; }
        }

        if (target == currentTarget) return;

        // Change de cible
        currentTarget?.HideOutline();
        currentTarget = target;
        currentTarget?.ShowOutline(GetHealthColor(currentTarget));
    }

    // Calcule la couleur selon le ratio HP (blanc si pas de stats)
    private Color GetHealthColor(HighlightTarget target)
    {
        UnitStatsComponent stats = target.GetComponent<UnitStatsComponent>();
        if (stats == null) return Color.white;

        float ratio = stats.GetHealthPercentage();
        if (ratio > 0.75f) return ColorHealthFull;
        if (ratio > 0.50f) return ColorHealthMid;
        if (ratio > 0.25f) return ColorHealthLow;
        return ColorHealthCritical;
    }

    private void OnDisable()
    {
        currentTarget?.HideOutline();
        currentTarget = null;
    }
}
