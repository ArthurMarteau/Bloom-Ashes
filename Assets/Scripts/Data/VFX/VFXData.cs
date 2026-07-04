using UnityEngine;

// ScriptableObject de configuration pour un effet VFX réutilisable (particules, flash, scale up, wave fill, son)
// Utilisé par VFXPlayer et VFXApplier pour piloter les effets de manière data-driven
[CreateAssetMenu(fileName = "New VFX", menuName = "Bloom and Ashes/Data/VFX")]
public class VFXData : ScriptableObject
{
    [Header("Audio")]
    [Tooltip("Son joué au moment du spawn (optionnel)")]
    [SerializeField] public AudioClip soundEffect;

    [Header("Particules")]
    [Tooltip("Prefab contenant un ParticleSystem à instancier au point de spawn")]
    [SerializeField] public GameObject particlesPrefab;

    [Header("Sprite")]
    [Tooltip("Prefab contenant un Animator à instancier au point de spawn")]
    [SerializeField] public GameObject spritePrefab;

    [Tooltip("Durée de vie override du sprite animé")]
    [SerializeField] public float spriteDuration = 1f;

    [Tooltip("Scale appliqué au sprite GO au spawn (Vector3.one = taille inchangée)")]
    public Vector3 spriteScale = Vector3.one;

    [Tooltip("Joue l'Animator du sprite en reverse")]
    [SerializeField] public bool spritePlayReverse = false;

    [Header("Flash")]
    [Tooltip("Active l'effet de flash blanc sur le SpriteRenderer de l'entité spawnée")]
    [SerializeField] public bool useSpriteFlash = false;

    public enum FlashDirection { FadeOut, FadeIn }

    [Tooltip("FadeOut = flash instantané qui disparaît (1→0). FadeIn = monte progressivement (0→1).")]
    public FlashDirection flashDirection = FlashDirection.FadeOut;

    [Tooltip("Durée de la décroissance du flash (1 → 0), en secondes")]
    [SerializeField] public float flashFadeDuration = 2f;

    [Tooltip("Courbe d'animation du fade-out (doit aller de 1 à 0)")]
    [SerializeField] public AnimationCurve flashFadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Tooltip("Couleur du flash (blanc par défaut, rouge pour sacrifice, etc.)")]
    [SerializeField] public Color flashColor = Color.white;

    [Header("Scale Up")]
    [Tooltip("Active l'effet de scale up sur l'unité au moment du spawn")]
    [SerializeField] public bool useScaleUp = false;

    [Tooltip("Scale de départ de l'unité (1 = taille normale, 0.3 = très petit)")]
    [Range(0.1f, 1f)]
    public float scaleUpStartScale = 0.1f;

    [Tooltip("Scale final cible (devrait rester à 1.0 sauf cas particulier)")]
    [Range(0.1f, 1f)]
    public float scaleUpEndScale = 1f;

    [Tooltip("Si true, utilise la même durée que le flash fade. Sinon, durée custom ci-dessous.")]
    [SerializeField] public bool scaleUpUseFlashDuration = true;

    [Tooltip("Durée custom du scale up (utilisé uniquement si scaleUpUseFlashDuration = false)")]
    public float scaleUpCustomDuration = 2f;

    [Header("Wave Fill")]
    [Tooltip("Active l'effet de vague Wave Fill qui remonte le sprite du bas vers le haut")]
    [SerializeField] public bool useWaveFill = false;

    [Tooltip("Durée de la montée (de 0 à 1) en secondes")]
    public float waveFillDuration = 1f;

    [Tooltip("Délai avant reset de _WatersapFill à 0 après avoir atteint 1 (pour laisser le sprite teinté un instant)")]
    public float waveFillHoldDuration = 0.2f;

    [Tooltip("Couleur de la vague (bleu cyan magique recommandé)")]
    public Color waveFillColor = new Color(0.2f, 0.7f, 1f, 1f);

    [Tooltip("Largeur de la zone de transition (effet vague lumineuse plutôt que ligne nette)")]
    [Range(0.01f, 0.5f)]
    public float waveFillEdgeWidth = 0.1f;

    [Tooltip("Courbe d'animation de la montée (0 → 1)")]
    public AnimationCurve waveFillCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}
