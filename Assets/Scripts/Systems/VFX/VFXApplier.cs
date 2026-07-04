using UnityEngine;

// Applique les effets d'un VFXData sur une cible sans dépendre d'un système métier
public static class VFXApplier
{
    // Applique tous les effets d'un VFXData sur le GameObject cible
    public static void Apply(VFXData data, GameObject target)
    {
        if (data == null || target == null) return;

        Vector3 center = GetCenter(target);
        VFXPlayer.PlayAt(data, center);
        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXApplier : VFX '{data.name}' joué sur {target.name} en {center}", null);

        if (data.useSpriteFlash)
            ApplySpriteFlash(data, target);

        if (data.useScaleUp)
            ApplyScaleUp(data, target);

        if (data.useWaveFill)
            ApplyWaveFill(data, target);
    }

    // Calcule le centre world-space d'un GameObject cible
    private static Vector3 GetCenter(GameObject go)
    {
        Renderer renderer = go.GetComponentInChildren<Renderer>();
        if (renderer != null)
            return renderer.bounds.center;

        Collider2D collider = go.GetComponent<Collider2D>();
        if (collider != null)
            return collider.bounds.center;

        return go.transform.position;
    }

    // Retourne un composant existant ou l'ajoute si absent
    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component != null)
            return component;

        return go.AddComponent<T>();
    }

    // Applique le flash sprite selon la direction configurée
    private static void ApplySpriteFlash(VFXData data, GameObject target)
    {
        SpriteFlashController flashController = GetOrAdd<SpriteFlashController>(target);
        flashController.SetFlashColor(data.flashColor);

        if (data.flashDirection == VFXData.FlashDirection.FadeIn)
        {
            flashController.SetFlashAmount(0f);
            flashController.FadeIn(data.flashFadeDuration, data.flashFadeCurve);
            GameLogger.Log(GameLogger.Category.UI, $"✨ VFXApplier : flash FadeIn sur {target.name} ({data.flashFadeDuration}s)", null);
            return;
        }

        flashController.SetFlashAmount(1f);
        flashController.FadeOut(data.flashFadeDuration, data.flashFadeCurve);
        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXApplier : flash FadeOut sur {target.name} ({data.flashFadeDuration}s)", null);
    }

    // Applique l'effet de scale up configuré
    private static void ApplyScaleUp(VFXData data, GameObject target)
    {
        ScaleUpController scaleController = GetOrAdd<ScaleUpController>(target);
        float duration = data.scaleUpUseFlashDuration
            ? data.flashFadeDuration
            : data.scaleUpCustomDuration;

        scaleController.StartScaleUp(
            data.scaleUpStartScale,
            data.scaleUpEndScale,
            duration
        );

        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXApplier : scale up sur {target.name} ({data.scaleUpStartScale} → {data.scaleUpEndScale} sur {duration}s)", null);
    }

    // Applique l'effet Wave Fill configuré
    private static void ApplyWaveFill(VFXData data, GameObject target)
    {
        WaveFillController fillController = GetOrAdd<WaveFillController>(target);
        fillController.Configure(data.waveFillColor, data.waveFillEdgeWidth);
        fillController.PlayFillAnimation(
            data.waveFillDuration,
            data.waveFillHoldDuration,
            data.waveFillCurve
        );

        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXApplier : wave fill sur {target.name} ({data.waveFillDuration}s + hold {data.waveFillHoldDuration}s)", null);
    }
}
