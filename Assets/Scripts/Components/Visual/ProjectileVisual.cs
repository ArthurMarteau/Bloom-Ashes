using UnityEngine;

// Projectile visuel simple pour attaques à distance
// S'autodétruit après avoir atteint sa destination
// Interpolation linéaire pendant la durée spécifiée
public class ProjectileVisual : MonoBehaviour
{
    private Vector3 startPosition;
    private Vector3 endPosition;
    private float travelDuration;
    private float elapsedTime = 0f;

    // Initialise le projectile avec ses paramètres
    // À appeler immédiatement après Instantiate
    public void Initialize(Vector3 start, Vector3 end, float duration)
    {
        startPosition = start;
        endPosition = end;
        travelDuration = duration;
        transform.position = startPosition;
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        // Calcule la progression (0 à 1)
        float progress = Mathf.Clamp01(elapsedTime / travelDuration);

        // Interpolation linéaire
        transform.position = Vector3.Lerp(startPosition, endPosition, progress);

        // Autodestruction à l'arrivée
        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }
}