using System.Collections;
using UnityEngine;

// Pilote le shader UnitShader (properties _WatersapFill, _WatersapColor, _WatersapEdgeWidth)
// pour animer la vague Wave Fill qui remonte le sprite du bas vers le haut
// Utilise MaterialPropertyBlock pour éviter toute instanciation de material (perf)
[RequireComponent(typeof(SpriteRenderer))]
public class WaveFillController : MonoBehaviour
{
    [Tooltip("SpriteRenderer cible — auto-assigné via GetComponent si non renseigné")]
    [SerializeField] private SpriteRenderer targetRenderer;

    private MaterialPropertyBlock propertyBlock;
    private Coroutine activeCoroutine;

    private static readonly int FillProperty      = Shader.PropertyToID("_WatersapFill");
    private static readonly int ColorProperty     = Shader.PropertyToID("_WatersapColor");
    private static readonly int EdgeWidthProperty = Shader.PropertyToID("_WatersapEdgeWidth");

    // Initialise le MaterialPropertyBlock et récupère le SpriteRenderer si absent
    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        propertyBlock = new MaterialPropertyBlock();
    }

    // Configure la couleur et la largeur de transition (à appeler une fois avant PlayFillAnimation)
    public void Configure(Color color, float edgeWidth)
    {
        if (targetRenderer == null) return;

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(ColorProperty, color);
        propertyBlock.SetFloat(EdgeWidthProperty, edgeWidth);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    // Set instantané du fill (0 = aucun effet, 1 = sprite entièrement teinté)
    public void SetFill(float value)
    {
        if (targetRenderer == null) return;

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FillProperty, Mathf.Clamp01(value));
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    // Lance l'animation complète : 0 → 1 sur fillDuration, hold pendant holdDuration, puis reset à 0
    public void PlayFillAnimation(float fillDuration, float holdDuration, AnimationCurve curve)
    {
        StopAnimation();
        activeCoroutine = StartCoroutine(FillRoutine(fillDuration, holdDuration, curve));
    }

    // Arrête l'animation en cours et reset le fill à 0
    public void StopAnimation()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
        SetFill(0f);
    }

    // Coroutine qui anime le fill 0 → 1, attend le hold, puis reset à 0
    private IEnumerator FillRoutine(float fillDuration, float holdDuration, AnimationCurve curve)
    {
        float elapsed = 0f;

        // Phase 1 : montée 0 → 1
        while (elapsed < fillDuration)
        {
            if (this == null || gameObject == null) yield break;

            float t = elapsed / fillDuration;
            SetFill(Mathf.Clamp01(curve.Evaluate(t)));

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetFill(1f);

        // Phase 2 : maintien au sommet
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        // Phase 3 : reset propre
        if (this != null && gameObject != null)
            SetFill(0f);

        activeCoroutine = null;
    }

    // Force le fill à 100% depuis le menu de contexte
    [ContextMenu("Debug: Fill 100%")]
    private void DebugFillFull()
    {
        SetFill(1f);
    }

    // Force le fill à 0% depuis le menu de contexte
    [ContextMenu("Debug: Fill 0%")]
    private void DebugFillZero()
    {
        SetFill(0f);
    }

    // Lance une animation de test depuis le menu de contexte
    [ContextMenu("Debug: Test Animation 1s + 0.2s hold")]
    private void DebugTestAnimation()
    {
        Configure(new Color(0.2f, 0.7f, 1f, 1f), 0.1f);
        PlayFillAnimation(1f, 0.2f, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
    }
}
