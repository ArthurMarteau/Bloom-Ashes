using System.Collections;
using UnityEngine;

// Anime linéairement le localScale d'un transform de startScale vers endScale sur une durée donnée
// Component utilitaire utilisé par VFXApplier pour l'effet "pop" au spawn d'une unité
public class ScaleUpController : MonoBehaviour
{
    [SerializeField] private Transform targetTransform;

    private Coroutine activeCoroutine;

    // Auto-assigne le transform courant si aucun n'est défini en inspecteur
    private void Awake()
    {
        if (targetTransform == null)
            targetTransform = transform;
    }

    // Lance l'animation de scale up, en stoppant toute coroutine en cours
    public void StartScaleUp(float startScale, float endScale, float duration)
    {
        if (activeCoroutine != null)
            StopCoroutine(activeCoroutine);

        targetTransform.localScale = Vector3.one * startScale;
        activeCoroutine = StartCoroutine(ScaleUpRoutine(startScale, endScale, duration));
    }

    // Arrête la coroutine en cours sans modifier le scale actuel
    public void StopScaleUp()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
    }

    // Interpolation linéaire du scale sur la durée donnée
    private IEnumerator ScaleUpRoutine(float startScale, float endScale, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (this == null || gameObject == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            targetTransform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            yield return null;
        }

        if (this != null && gameObject != null)
            targetTransform.localScale = Vector3.one * endScale;

        activeCoroutine = null;
    }

    [ContextMenu("Debug: Test Scale 0.3 → 1.0 sur 2s")]
    private void DebugTestScaleUp()
    {
        StartScaleUp(0.3f, 1f, 2f);
    }
}
