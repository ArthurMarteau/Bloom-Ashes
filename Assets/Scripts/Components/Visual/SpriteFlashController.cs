using System.Collections;
using UnityEngine;

// Applique le paramètre _FlashAmount du shader SpriteFlash sur un SpriteRenderer cible
// Utilise MaterialPropertyBlock pour éviter toute instanciation de material (perf)
// Dépend de FlashCurveSystem pour les calculs de courbe
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlashController : MonoBehaviour
{
    [Tooltip("SpriteRenderer cible — auto-assigné via GetComponent si non renseigné")]
    [SerializeField] private SpriteRenderer targetRenderer;

    private MaterialPropertyBlock propertyBlock;
    private FlashCurveSystem flashCurveSystem;
    private Coroutine activeCoroutine;

    private static readonly int FlashAmountProperty = Shader.PropertyToID("_FlashAmount");
    private const string RequiredShaderName = "UnitShader";

    // Initialise le MaterialPropertyBlock et vérifie la présence du shader SpriteFlash
    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        propertyBlock = new MaterialPropertyBlock();
        flashCurveSystem = new FlashCurveSystem();

        ValidateShader();
    }

    // Vérifie que le SpriteRenderer utilise bien le material SpriteFlash
    private void ValidateShader()
    {
        if (targetRenderer == null || targetRenderer.sharedMaterial == null)
        {
            Debug.LogWarning($"✨ SpriteFlashController {gameObject.name} : SpriteRenderer ou material absent");
            return;
        }

        if (!targetRenderer.sharedMaterial.shader.name.Contains(RequiredShaderName))
        {
            Debug.LogWarning($"✨ SpriteFlashController {gameObject.name} : le material '{targetRenderer.sharedMaterial.name}' n'utilise pas le shader SpriteFlash — les effets de flash ne seront pas visibles");
        }
    }

    // Applique instantanément la valeur de flash (0 = normal, 1 = blanc total)
    public void SetFlashAmount(float value)
    {
        if (targetRenderer == null) return;

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FlashAmountProperty, Mathf.Clamp01(value));
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    // Lance une coroutine de décroissance du flash (1 → 0) selon la courbe fournie
    public void FadeOut(float duration, AnimationCurve curve)
    {
        StopFade();
        activeCoroutine = StartCoroutine(FadeRoutine(duration, curve, isFadeIn: false));
    }

    // Lance une coroutine de montée en flash (0 → 1) selon la courbe fournie
    public void FadeIn(float duration, AnimationCurve curve)
    {
        StopFade();
        activeCoroutine = StartCoroutine(FadeRoutine(duration, curve, isFadeIn: true));
    }

    // Arrête la coroutine de fade en cours
    public void StopFade()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
    }

    // Coroutine générique pour fade-in et fade-out via FlashCurveSystem
    private IEnumerator FadeRoutine(float duration, AnimationCurve curve, bool isFadeIn)
    {
        float elapsed = 0f;

        while (!flashCurveSystem.IsComplete(elapsed, duration))
        {
            if (this == null || gameObject == null) yield break;

            float flashAmount = isFadeIn
                ? flashCurveSystem.EvaluateFadeIn(elapsed, duration, curve)
                : flashCurveSystem.EvaluateFadeOut(elapsed, duration, curve);

            SetFlashAmount(flashAmount);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Valeur finale garantie
        SetFlashAmount(isFadeIn ? 1f : 0f);
        activeCoroutine = null;
    }

    [ContextMenu("Debug: Flash 100%")]
    private void DebugFlashFull()
    {
        SetFlashAmount(1f);
        Debug.Log($"✨ SpriteFlashController {gameObject.name} : Flash 100%");
    }

    [ContextMenu("Debug: Flash 0%")]
    private void DebugFlashZero()
    {
        SetFlashAmount(0f);
        Debug.Log($"✨ SpriteFlashController {gameObject.name} : Flash 0%");
    }

    [ContextMenu("Debug: Test FadeOut 2s")]
    private void DebugFadeOut2s()
    {
        AnimationCurve easeOut = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        SetFlashAmount(1f);
        FadeOut(2f, easeOut);
        Debug.Log($"✨ SpriteFlashController {gameObject.name} : FadeOut 2s lancé");
    }
}
