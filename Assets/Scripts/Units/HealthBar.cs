using UnityEngine;
using UnityEngine.UI;

// Affiche une barre de vie au-dessus d'une unité
// Se met à jour automatiquement selon les HP de UnitStats
// Suit l'unité en mouvement et s'adapte à la caméra
public class HealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UnitStats unitStats;           // Stats de l'unité à suivre
    [SerializeField] private Image fillImage;               // Image de remplissage de la barre
    [SerializeField] private Canvas canvas;                 // Canvas pour orientation caméra

    [Header("Visual Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 1.5f, 0); // Offset au-dessus de l'unité
    [SerializeField] private Color healthyColor = Color.green;          // Couleur > 50% HP
    [SerializeField] private Color damagedColor = Color.yellow;         // Couleur 25-50% HP
    [SerializeField] private Color criticalColor = Color.red;           // Couleur < 25% HP
    [SerializeField] private bool hideWhenFull = true;                  // Cacher si HP max

    [Header("Debug")]
    [SerializeField] private bool debugHealthBar = false;

    private Transform unitTransform;
    private Camera mainCamera;

    // Initialise les références
    private void Awake()
    {
        // Trouve UnitStats si non assigné
        if (unitStats == null)
        {
            unitStats = GetComponentInParent<UnitStats>();
            if (unitStats == null)
            {
                Debug.LogError($"HealthBar sur {gameObject.name}: UnitStats non trouvé !");
                enabled = false;
                return;
            }
        }

        // Trouve le Canvas si non assigné
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>();
        }

        unitTransform = unitStats.transform;
        mainCamera = Camera.main;

        // Configure le canvas pour toujours faire face à la caméra
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
        }

        // S'abonne aux événements de dégâts/soins
        if (unitStats != null)
        {
            unitStats.OnDamaged.AddListener(UpdateHealthBar);
            unitStats.OnHealed.AddListener(UpdateHealthBar);
            unitStats.OnDeath.AddListener(OnUnitDeath);
        }

        // Initialise la barre
        UpdateHealthBar();
    }

    // Force la mise à jour après l'initialisation complète
    private void Start()
    {
        // Petit délai pour s'assurer que tout est initialisé
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
        if (unitStats == null || fillImage == null)
        {
            return;
        }

        float healthPercentage = unitStats.GetHealthPercentage();

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

        // Cache la barre si HP max (optionnel)
        if (hideWhenFull && healthPercentage >= 1f)
        {
            canvas.gameObject.SetActive(false);
        }
        else
        {
            canvas.gameObject.SetActive(true);
        }

        if (debugHealthBar)
        {
            Debug.Log($"HealthBar {unitStats.gameObject.name}: {healthPercentage * 100:F1}% HP");
        }
    }

    // Gère la mort de l'unité
    // Cache la barre de vie
    private void OnUnitDeath()
    {
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }

        if (debugHealthBar)
        {
            Debug.Log($"HealthBar {unitStats.gameObject.name}: Unité morte, barre cachée");
        }
    }

    // Nettoie les événements lors de la destruction
    private void OnDestroy()
    {
        if (unitStats != null)
        {
            unitStats.OnDamaged.RemoveListener(UpdateHealthBar);
            unitStats.OnHealed.RemoveListener(UpdateHealthBar);
            unitStats.OnDeath.RemoveListener(OnUnitDeath);
        }
    }
}