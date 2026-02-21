using UnityEngine;
using UnityEngine.UI;

// Affiche une barre de vie au-dessus d'une unité
// Se met à jour automatiquement selon les HP de UnitStatsComponent
// Suit l'unité en mouvement et s'adapte à la caméra
public class HealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UnitStatsComponent unitStatsComponent; // Stats de l'unité
    [SerializeField] private Image fillImage;                       // Image de remplissage de la barre
    [SerializeField] private Canvas canvas;                         // Canvas pour orientation caméra

    [Header("Visual Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, 0); // Offset au-dessus de l'unité
    [SerializeField] private Color healthyColor = Color.green;          // Couleur > 65% HP
    [SerializeField] private Color damagedColor = Color.yellow;         // Couleur 25-65% HP
    [SerializeField] private Color criticalColor = Color.red;           // Couleur < 25% HP
    [SerializeField] private bool hideWhenFull = true;                  // Cacher si HP max

    [Header("Debug")]
    [SerializeField] private bool debugHealthBar = false;

    private Transform unitTransform;
    private Camera mainCamera;

    // Initialise les références et s'abonne aux événements de UnitStatsComponent
    private void Awake()
    {
        // Cherche UnitStatsComponent dans le parent si non assigné en Inspector
        if (unitStatsComponent == null)
        {
            unitStatsComponent = GetComponentInParent<UnitStatsComponent>();
        }

        if (unitStatsComponent == null)
        {
            Debug.LogError($"HealthBar sur {gameObject.name}: UnitStatsComponent non trouvé !");
            enabled = false;
            return;
        }

        unitTransform = unitStatsComponent.transform;

        // S'abonne aux événements du système de stats
        unitStatsComponent.OnDamaged.AddListener(UpdateHealthBar);
        unitStatsComponent.OnHealed.AddListener(UpdateHealthBar);
        unitStatsComponent.OnDeath.AddListener(OnUnitDeath);

        if (debugHealthBar)
        {
            Debug.Log($"HealthBar {gameObject.name}: UnitStatsComponent trouvé sur {unitTransform.name}");
        }

        // Trouve le Canvas si non assigné en Inspector
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>();
        }

        mainCamera = Camera.main;

        // Configure le canvas pour toujours faire face à la caméra
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
        }

        // Initialise la barre au bon état dès le départ
        UpdateHealthBar();
    }

    // Force la mise à jour après l'initialisation complète de la scène
    private void Start()
    {
        UpdateHealthBar();
    }

    // Met à jour la position et l'orientation chaque frame
    private void LateUpdate()
    {
        if (unitTransform == null || mainCamera == null)
        {
            return;
        }

        // Suit l'unité avec offset
        transform.position = unitTransform.position + offset;

        // Fait toujours face à la caméra
        if (canvas != null)
        {
            canvas.transform.LookAt(canvas.transform.position + mainCamera.transform.forward);
        }
    }

    // Met à jour le remplissage et la couleur de la barre
    // Appelé automatiquement via événements OnDamaged/OnHealed
    private void UpdateHealthBar()
    {
        if (fillImage == null)
        {
            return;
        }

        // Récupère le pourcentage de vie depuis UnitStatsComponent
        float healthPercentage = GetHealthPercentage();

        // Met à jour le remplissage
        fillImage.fillAmount = healthPercentage;

        // Met à jour la couleur selon le pourcentage de vie
        if (healthPercentage > 0.65f)
        {
            fillImage.color = healthyColor;
        }
        else if (healthPercentage > 0.25f)
        {
            fillImage.color = damagedColor;
        }
        else
        {
            fillImage.color = criticalColor;
        }

        // Cache la barre si HP max
        if (canvas != null)
        {
            canvas.gameObject.SetActive(!(hideWhenFull && healthPercentage >= 1f));
        }

        if (debugHealthBar)
        {
            Debug.Log($"HealthBar {gameObject.name}: {healthPercentage * 100:F1}% HP");
        }
    }

    // Récupère le pourcentage de santé depuis UnitStatsComponent
    private float GetHealthPercentage()
    {
        if (unitStatsComponent != null)
        {
            return unitStatsComponent.GetHealthPercentage();
        }

        return 0f;
    }

    // Cache la barre de vie quand l'unité meurt
    private void OnUnitDeath()
    {
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }

        if (debugHealthBar)
        {
            Debug.Log($"HealthBar {gameObject.name}: Unité morte, barre cachée");
        }
    }

    // Nettoie les listeners lors de la destruction pour éviter les fuites mémoire
    private void OnDestroy()
    {
        if (unitStatsComponent != null)
        {
            unitStatsComponent.OnDamaged.RemoveListener(UpdateHealthBar);
            unitStatsComponent.OnHealed.RemoveListener(UpdateHealthBar);
            unitStatsComponent.OnDeath.RemoveListener(OnUnitDeath);
        }
    }
}
