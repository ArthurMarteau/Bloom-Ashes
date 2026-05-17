using System.Collections;
using UnityEngine;

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
            GameLogger.Warn(GameLogger.Category.UI, "✨ VFXPlayer.PlayAt : VFXData null, aucun effet joué", null);
            return;
        }

        GameObject vfxRoot = new GameObject($"VFX_{data.name}");
        vfxRoot.transform.position = position;

        if (attachTarget != null)
            vfxRoot.transform.SetParent(attachTarget);

        VFXPlayer player = vfxRoot.AddComponent<VFXPlayer>();
        player.StartCoroutine(player.PlayAllEffects(data));

        GameLogger.Log(GameLogger.Category.UI, $"✨ VFXPlayer : lecture de '{data.name}' à {position}", null);
    }

    // Coroutine principale : lance tous les effets en parallèle et détruit le root quand tout est terminé
    private IEnumerator PlayAllEffects(VFXData data)
    {
        float maxDuration = 0f;

        // Particules
        if (data.particlesPrefab != null)
        {
            GameObject particles = Instantiate(data.particlesPrefab, transform.position, Quaternion.identity, transform);
            maxDuration = Mathf.Max(maxDuration, GetParticleSystemDuration(particles));
        }

        // Son
        if (data.soundEffect != null)
            AudioSource.PlayClipAtPoint(data.soundEffect, transform.position);

        yield return new WaitForSeconds(maxDuration);

        Destroy(gameObject);
    }

    // Calcule la durée de vie attendue du ParticleSystem instancié
    private float GetParticleSystemDuration(GameObject particles)
    {
        ParticleSystem particleSystem = particles.GetComponentInChildren<ParticleSystem>();
        if (particleSystem == null) return 3f;

        ParticleSystem.MainModule main = particleSystem.main;
        return main.duration + main.startLifetime.constantMax;
    }
}
