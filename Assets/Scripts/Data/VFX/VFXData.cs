using UnityEngine;

// ScriptableObject de configuration pour un effet VFX réutilisable (particules, light, flash, son)
// Utilisé par VFXPlayer et VFXManager pour piloter les effets de manière data-driven
[CreateAssetMenu(fileName = "New VFX", menuName = "Bloom and Ashes/VFX/VFX Data")]
public class VFXData : ScriptableObject
{
    [Header("Particules")]
    [Tooltip("Prefab contenant un ParticleSystem à instancier au point de spawn")]
    [SerializeField] public GameObject particlesPrefab;

    [Tooltip("Durée de vie des particules avant destruction automatique (secondes)")]
    [SerializeField] public float particlesLifetime = 2f;

    [Header("Light 2D Pulse")]
    [Tooltip("Active l'effet de halo lumineux au moment du spawn")]
    [SerializeField] public bool useLight2D = true;

    [Tooltip("Couleur du halo lumineux")]
    [SerializeField] public Color lightColor = Color.white;

    [Tooltip("Intensité maximale atteinte pendant le pulse")]
    [SerializeField] public float lightIntensity = 1.5f;

    [Tooltip("Rayon intérieur de la Light 2D")]
    [SerializeField] public float lightInnerRadius = 0.3f;

    [Tooltip("Rayon extérieur de la Light 2D")]
    [SerializeField] public float lightOuterRadius = 1.2f;

    [Tooltip("Durée du pulse lumineux (0 → intensité → 0), en secondes")]
    [SerializeField] public float lightPulseDuration = 0.5f;

    [Header("Flash Sprite (saturation blanche)")]
    [Tooltip("Active l'effet de flash blanc sur le SpriteRenderer de l'entité spawnée")]
    [SerializeField] public bool useSpriteFlash = true;

    [Tooltip("Durée de la décroissance du flash (1 → 0), en secondes")]
    [SerializeField] public float flashFadeDuration = 2f;

    [Tooltip("Courbe d'animation du fade-out (doit aller de 1 à 0)")]
    [SerializeField] public AnimationCurve flashFadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Scale Up (effet pop sur unité spawnée)")]
    [Tooltip("Active l'effet de scale up sur l'unité au moment du spawn")]
    public bool useScaleUp = true;

    [Tooltip("Scale de départ de l'unité (1 = taille normale, 0.3 = très petit)")]
    [Range(0.05f, 1f)]
    public float scaleUpStartScale = 0.3f;

    [Tooltip("Scale final cible (devrait rester à 1.0 sauf cas particulier)")]
    [Range(0.5f, 2f)]
    public float scaleUpEndScale = 1f;

    [Tooltip("Si true, utilise la même durée que le flash fade. Sinon, durée custom ci-dessous.")]
    public bool scaleUpUseFlashDuration = true;

    [Tooltip("Durée custom du scale up (utilisé uniquement si scaleUpUseFlashDuration = false)")]
    public float scaleUpCustomDuration = 2f;

    [Header("Audio")]
    [Tooltip("Son joué au moment du spawn (optionnel)")]
    [SerializeField] public AudioClip soundEffect;
}
