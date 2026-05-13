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

    // Payload dégâts (optionnel, null si projectile purement visuel)
    private Transform targetTransform;
    private IDamageable targetDamageable;
    private GameObject attackerObject;
    private int damageAmount;

    // Initialise le projectile sans payload de dégâts (purement visuel)
    // À appeler immédiatement après Instantiate
    public void Initialize(Vector3 start, Vector3 end, float duration)
    {
        startPosition = start;
        endPosition = end;
        travelDuration = duration;
        transform.position = startPosition;
    }

    // Initialise le projectile avec homing et payload de dégâts autonomes
    // Le projectile suit la cible et applique les dégâts à l'impact
    public void Initialize(Vector3 start, Transform target, float duration, IDamageable damageable, GameObject attacker, int damage)
    {
        startPosition = start;
        targetTransform = target;
        endPosition = target != null ? target.position : start;
        travelDuration = duration;
        targetDamageable = damageable;
        attackerObject = attacker;
        damageAmount = damage;
        transform.position = startPosition;
    }

    // Oriente le sprite vers la direction de déplacement courante
    // Utilise Atan2 car le sprite de base pointe vers l'Est (0°)
    private void UpdateRotation()
    {
        Vector3 direction = endPosition - startPosition;
        if (direction.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        // Homing : met à jour la destination si la cible est encore présente
        if (targetTransform != null)
        {
            endPosition = targetTransform.position;
        }

        UpdateRotation();

        elapsedTime += Time.deltaTime;

        // Calcule la progression (0 à 1)
        float progress = Mathf.Clamp01(elapsedTime / travelDuration);

        // Interpolation linéaire
        transform.position = Vector3.Lerp(startPosition, endPosition, progress);

        // Impact à l'arrivée
        if (progress >= 1f)
        {
            // Applique les dégâts si payload présent et cible encore vivante
            if (targetDamageable != null && !targetDamageable.IsDead())
            {
                targetDamageable.TakeDamage(damageAmount, attackerObject);
            }

            Destroy(gameObject);
        }
    }
}
