using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Composant utilitaire qui joue un VFXData à une position donnée
// Gère le cycle de vie complet : instanciation, animation, nettoyage automatique
public class VFXPlayer : MonoBehaviour
{
    // Méthode factory statique : crée un GameObject temporaire et joue tous les effets du VFXData
    // attachTarget : si fourni, le VFX suit cette transform (ex : l'unité spawnée)
    public static void PlayAt(VFXData data, Vector3 position, Transform attachTarget = null)
    {
        if (data == null)
        {
            Debug.LogWarning("✨ VFXPlayer.PlayAt : VFXData null, aucun effet joué");
            return;
        }

        GameObject vfxRoot = new GameObject($"VFX_{data.name}");
        vfxRoot.transform.position = position;

        if (attachTarget != null)
            vfxRoot.transform.SetParent(attachTarget);

        VFXPlayer player = vfxRoot.AddComponent<VFXPlayer>();
        player.StartCoroutine(player.PlayAllEffects(data));

        Debug.Log($"✨ VFXPlayer : lecture de '{data.name}' à {position}");
    }

    // Coroutine principale : lance tous les effets en parallèle et détruit le root quand tout est terminé
    private IEnumerator PlayAllEffects(VFXData data)
    {
        float maxDuration = 0f;

        // Particules
        if (data.particlesPrefab != null)
        {
            GameObject particles = Instantiate(data.particlesPrefab, transform.position, Quaternion.identity, transform);
            Destroy(particles, data.particlesLifetime);
            maxDuration = Mathf.Max(maxDuration, data.particlesLifetime);
        }

        // Light 2D pulse
        if (data.useLight2D)
        {
            StartCoroutine(PlayLightPulse(data));
            maxDuration = Mathf.Max(maxDuration, data.lightPulseDuration);
        }

        // Son
        if (data.soundEffect != null)
            AudioSource.PlayClipAtPoint(data.soundEffect, transform.position);

        yield return new WaitForSeconds(maxDuration);

        Destroy(gameObject);
    }

    // Crée une Light 2D enfant et anime son intensité en pulse (0 → max → 0)
    private IEnumerator PlayLightPulse(VFXData data)
    {
        GameObject lightObject = new GameObject("VFX_Light2D");
        lightObject.transform.SetParent(transform);
        lightObject.transform.localPosition = Vector3.zero;

        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = data.lightColor;
        light.intensity = 0f;
        light.pointLightInnerRadius = data.lightInnerRadius;
        light.pointLightOuterRadius = data.lightOuterRadius;

        float halfDuration = data.lightPulseDuration * 0.5f;
        float elapsed = 0f;

        // Montée : 0 → intensité max
        while (elapsed < halfDuration)
        {
            if (lightObject == null) yield break;
            light.intensity = Mathf.Lerp(0f, data.lightIntensity, elapsed / halfDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        light.intensity = data.lightIntensity;
        elapsed = 0f;

        // Descente : intensité max → 0
        while (elapsed < halfDuration)
        {
            if (lightObject == null) yield break;
            light.intensity = Mathf.Lerp(data.lightIntensity, 0f, elapsed / halfDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (lightObject != null)
            Destroy(lightObject);
    }
}
