using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Composant responsable de la gestion des animations d'unités
// Attaché à chaque GameObject unité pour gérer ses animations selon les directions
// Interface entre MovementController et Animator Unity avec gestion miroir
public class AnimatorController : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float rootbornIdleReversedDuration = 1f;  // Durée IdleReversed Rootborn
    [SerializeField] private bool syncAnimationWithDuration = true;     // Synchronise animation avec durée

    [Header("Debug")]
    [SerializeField] private bool debugAnimations = true;

    // Énumération des directions d'animation disponibles
    public enum AnimationDirection
    {
        North,      // N
        NorthWest,  // NW
        West,       // W
        SouthWest,  // SW
        South,      // S
        NorthEast,  // NE (miroir de NW)
        East,       // E (miroir de W)
        SouthEast   // SE (miroir de SW)
    }

    // Variables d'état d'animation
    private Animator unitAnimator;
    private SpriteRenderer spriteRenderer;
    private bool isPlayingIdleReversed = false;
    private Coroutine idleReversedCoroutine;

    // Cache des noms d'animations universels
    private Dictionary<AnimationDirection, string> walkAnimations;
    private string idleAnimationName = "Idle";
    private string idleReversedAnimationName = "Idle_Reversed"; // Spécifique Rootborn
    private bool hasPlayedIdleReversedThisSession = false;  // Flag persistant jusqu'à arrêt complet

    // Initialise les références et configure les animations universelles
    private void Awake()
    {
        // Cache les composants requis
        unitAnimator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Valide que les composants existent
        if (unitAnimator == null)
        {
            Debug.LogError($"AnimatorController sur {gameObject.name}: Composant Animator manquant!");
            return;
        }

        if (spriteRenderer == null)
        {
            Debug.LogError($"AnimatorController sur {gameObject.name}: Composant SpriteRenderer manquant!");
            return;
        }

        // Configure les noms d'animations universels
        SetupAnimationNames();

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController initialisé pour {gameObject.name}, Rootborn: {IsRootborn()}");
        }
    }

    // Configure les noms d'animations universels pour toutes les unités
    // Utilise le même nommage pour tous types d'unités
    private void SetupAnimationNames()
    {
        walkAnimations = new Dictionary<AnimationDirection, string>
        {
            { AnimationDirection.North, "Walk_N" },
            { AnimationDirection.NorthWest, "Walk_NW" },
            { AnimationDirection.West, "Walk_W" },
            { AnimationDirection.SouthWest, "Walk_SW" },
            { AnimationDirection.South, "Walk_S" },
            // Les directions miroir utilisent les mêmes animations
            { AnimationDirection.NorthEast, "Walk_NW" },    // Miroir de NW
            { AnimationDirection.East, "Walk_W" },          // Miroir de W
            { AnimationDirection.SouthEast, "Walk_SW" }     // Miroir de SW
        };
    }

    // Interface principale pour déclencher animation de mouvement
    // Calcule la direction et gère le miroir si nécessaire
    // Appelée par MovementController lors du déplacement
    public void PlayWalkAnimation(Vector2Int fromPosition, Vector2Int toPosition)
    {
        if (unitAnimator == null) return;

        // Calcule la direction du mouvement
        AnimationDirection direction = CalculateDirection(fromPosition, toPosition);

        // Détermine si un miroir est requis
        bool requiresMirror = RequiresMirror(direction);
        SetMirror(requiresMirror);

        // Récupère le nom de l'animation correspondante
        if (walkAnimations.TryGetValue(direction, out string animationName))
        {
            unitAnimator.Play(animationName);

            if (debugAnimations)
            {
                Debug.Log($"AnimatorController {gameObject.name}: Animation {animationName}, Direction {direction}, Miroir: {requiresMirror}");
            }
        }
        else
        {
            Debug.LogWarning($"AnimatorController {gameObject.name}: Animation non trouvée pour direction {direction}");
        }
    }

    // Interface pour déclencher animation d'attaque
    // Ajuste automatiquement la vitesse selon attackInterval de l'unité
    public void PlayAttackAnimation(Vector2Int fromPosition, Vector2Int toPosition, float attackInterval)
    {
        if (unitAnimator == null) return;

        // Calcule la direction de l'attaque
        AnimationDirection direction = CalculateDirection(fromPosition, toPosition);

        // Détermine si un miroir est requis
        bool requiresMirror = RequiresMirror(direction);
        SetMirror(requiresMirror);

        // Construit le nom de l'animation d'attaque
        string attackAnimationName = GetAttackAnimationName(direction);

        // Joue l'animation
        unitAnimator.Play(attackAnimationName);

        // Ajuste la vitesse de l'animation selon attackInterval
        // Si l'animation dure 1s et attackInterval = 0.5s → speed = 2x
        // Si l'animation dure 1s et attackInterval = 2s → speed = 0.5x
        StartCoroutine(AdjustAttackSpeed(attackAnimationName, attackInterval));

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Animation {attackAnimationName}, Direction {direction}, Miroir: {requiresMirror}, AttackInterval: {attackInterval}s");
        }
    }

    // Ajuste la vitesse de l'animation pour correspondre à attackInterval
    private IEnumerator AdjustAttackSpeed(string attackAnimationName, float attackInterval)
    {
        // Attend une frame pour que l'animation soit chargée
        yield return null;

        // Récupère la durée originale de l'animation
        AnimatorStateInfo stateInfo = unitAnimator.GetCurrentAnimatorStateInfo(0);
        float originalAnimationLength = stateInfo.length;

        // Calcule la vitesse nécessaire
        float requiredSpeed = originalAnimationLength / attackInterval;
        unitAnimator.speed = requiredSpeed;

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController: Vitesse ajustée à {requiredSpeed}x (animation {originalAnimationLength}s → {attackInterval}s)");
        }

        // Attend la fin de l'attaque
        yield return new WaitForSeconds(attackInterval);

        // Remet la vitesse normale
        unitAnimator.speed = 1f;
    }

    // Retourne le nom de l'animation d'attaque selon la direction
    // Utilise la même convention que Walk mais préfixe Attack_
    private string GetAttackAnimationName(AnimationDirection direction)
    {
        switch (direction)
        {
            case AnimationDirection.North:
                return "Attack_N";
            case AnimationDirection.NorthWest:
            case AnimationDirection.NorthEast: // Miroir de NW
                return "Attack_NW";
            case AnimationDirection.West:
            case AnimationDirection.East: // Miroir de W
                return "Attack_W";
            case AnimationDirection.SouthWest:
            case AnimationDirection.SouthEast: // Miroir de SW
                return "Attack_SW";
            case AnimationDirection.South:
                return "Attack_S";
            default:
                return "Attack_S"; // Fallback
        }
    }

    // Interface pour revenir à l'animation idle
    // Appelée par MovementController quand l'unité s'arrête
    public void PlayIdleAnimation()
    {
        if (unitAnimator == null) return;

        // Arrête IdleReversed si en cours (Rootborn)
        if (isPlayingIdleReversed && idleReversedCoroutine != null)
        {
            StopCoroutine(idleReversedCoroutine);
            isPlayingIdleReversed = false;
        }

        // Remet le miroir par défaut
        SetMirror(false);

        // Joue l'animation idle
        unitAnimator.Play(idleAnimationName);

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Animation Idle");
        }
    }

    // Méthode spéciale Rootborn : joue IdleReversed pendant 1f puis callback
    // Doit être appelée AVANT le mouvement, bloque jusqu'à completion
    // Callback déclenché quand l'animation est terminée
    public void PlayRootbornIdleReversed(Action onComplete)
    {
        if (!IsRootborn())
        {
            // Si pas Rootborn, callback immédiat
            onComplete?.Invoke();
            return;
        }

        // Si IdleReversed déjà joué pour cette session, callback immédiat
        if (hasPlayedIdleReversedThisSession)
        {
            if (debugAnimations)
            {
                Debug.Log($"AnimatorController {gameObject.name}: IdleReversed déjà joué cette session, skip");
            }
            onComplete?.Invoke();
            return;
        }

        if (unitAnimator == null)
        {
            onComplete?.Invoke();
            return;
        }

        // Arrête toute IdleReversed en cours
        if (idleReversedCoroutine != null)
        {
            StopCoroutine(idleReversedCoroutine);
        }

        // Lance la coroutine IdleReversed
        idleReversedCoroutine = StartCoroutine(PlayIdleReversedCoroutine(onComplete));

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Début IdleReversed Rootborn ({rootbornIdleReversedDuration}s)");
        }

        // Marque comme joué pour cette session complète
        hasPlayedIdleReversedThisSession = true;
    }

    // Coroutine pour gérer l'animation IdleReversed du Rootborn
    // Joue l'animation pendant la durée spécifiée puis déclenche le callback
    private IEnumerator PlayIdleReversedCoroutine(Action onComplete)
    {
        isPlayingIdleReversed = true;

        // Joue l'animation IdleReversed
        unitAnimator.Play(idleReversedAnimationName);

        // Si synchronisation activée, ajuste la vitesse de l'animation
        if (syncAnimationWithDuration)
        {
            // Attend une frame pour que l'animation soit chargée
            yield return null;
            
            // Récupère la durée originale de l'animation
            AnimatorStateInfo stateInfo = unitAnimator.GetCurrentAnimatorStateInfo(0);
            float originalAnimationLength = stateInfo.length;
            
            // Calcule la vitesse nécessaire pour matcher la durée voulue
            float requiredSpeed = originalAnimationLength / rootbornIdleReversedDuration;
            unitAnimator.speed = requiredSpeed;
            
            if (debugAnimations)
            {
                Debug.Log($"Animation sync: durée originale {originalAnimationLength}s, vitesse ajustée à {requiredSpeed}");
            }
        }

        // Attend la durée spécifiée
        yield return new WaitForSeconds(rootbornIdleReversedDuration);

        // Remet la vitesse d'animation normale
        if (syncAnimationWithDuration)
        {
            unitAnimator.speed = 1f;
        }

        // Termine l'animation spéciale
        isPlayingIdleReversed = false;
        idleReversedCoroutine = null;

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Fin IdleReversed Rootborn");
        }

        // Déclenche le callback pour autoriser le mouvement
        onComplete?.Invoke();
    }

    // Reset le flag IdleReversed seulement quand l'unité s'arrête vraiment
    // Appelée par MovementController quand mouvement complètement terminé
    public void ResetIdleReversedSession()
    {
        hasPlayedIdleReversedThisSession = false;

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Session IdleReversed resetée");
        }
    }

    // Vérifie si l'IdleReversed a déjà été joué dans cette session
    // Utilisé par MovementController et AttackController pour éviter de rejouer
    public bool HasPlayedIdleReversedThisSession()
    {
        return hasPlayedIdleReversedThisSession;
    }

    // Calcule la direction d'animation entre deux positions
    // Convertit le delta en direction cardinale/diagonale
    // Retourne la direction correspondante pour l'animation
    private AnimationDirection CalculateDirection(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;

        // Normalise les deltas pour gérer les directions
        int deltaX = Mathf.Clamp(delta.x, -1, 1);
        int deltaY = Mathf.Clamp(delta.y, -1, 1);

        // Détermine la direction selon le delta
        if (deltaX == 0 && deltaY == 1)
            return AnimationDirection.North;
        else if (deltaX == 1 && deltaY == 1)
            return AnimationDirection.NorthEast;
        else if (deltaX == 1 && deltaY == 0)
            return AnimationDirection.East;
        else if (deltaX == 1 && deltaY == -1)
            return AnimationDirection.SouthEast;
        else if (deltaX == 0 && deltaY == -1)
            return AnimationDirection.South;
        else if (deltaX == -1 && deltaY == -1)
            return AnimationDirection.SouthWest;
        else if (deltaX == -1 && deltaY == 0)
            return AnimationDirection.West;
        else if (deltaX == -1 && deltaY == 1)
            return AnimationDirection.NorthWest;

        // Par défaut, retourne South si direction indéterminée
        return AnimationDirection.South;
    }

    // Détermine si une direction nécessite un miroir horizontal
    // Les directions Est (NE, E, SE) utilisent le miroir des directions Ouest
    private bool RequiresMirror(AnimationDirection direction)
    {
        return direction == AnimationDirection.NorthEast ||
               direction == AnimationDirection.East ||
               direction == AnimationDirection.SouthEast;
    }

    // Active/désactive le miroir du SpriteRenderer
    // Utilise flipX pour inverser horizontalement le sprite
    private void SetMirror(bool mirror)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = mirror;
        }
    }

    // Vérifie si cette unité est un Rootborn basé sur le nom du GameObject
    // Méthode simple et fiable pour identifier les Rootborn
    private bool IsRootborn()
    {
        return gameObject.name.Contains("Rootborn");
    }

    // Arrête toutes les animations en cours
    // Méthode publique pour reset depuis l'extérieur
    public void StopAllAnimations()
    {
        if (idleReversedCoroutine != null)
        {
            StopCoroutine(idleReversedCoroutine);
            idleReversedCoroutine = null;
            isPlayingIdleReversed = false;
        }

        // Reset la vitesse d'animation
        if (unitAnimator != null)
        {
            unitAnimator.speed = 1f;
        }

        PlayIdleAnimation();

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Toutes animations arrêtées");
        }
    }


    // Joue l'animation de mort de l'unité
    // Appelée par UnitStats quand l'unité meurt
    public void PlayDeathAnimation()
    {
        if (unitAnimator == null) return;

        // Arrête toutes les animations en cours
        if (idleReversedCoroutine != null)
        {
            StopCoroutine(idleReversedCoroutine);
            isPlayingIdleReversed = false;
        }

        // Remet le miroir par défaut
        SetMirror(false);

        // Joue l'animation de mort
        unitAnimator.Play("Death");

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Animation Death");
        }
    }

    // Méthode de debug pour afficher l'état de l'animator
    [ContextMenu("Debug Animator State")]
    private void DebugAnimatorState()
    {
        Debug.Log($"=== AnimatorController Debug - {gameObject.name} ===");
        Debug.Log($"Est Rootborn: {IsRootborn()}");
        Debug.Log($"IdleReversed en cours: {isPlayingIdleReversed}");
        Debug.Log($"Vitesse animator: {(unitAnimator != null ? unitAnimator.speed : 0f)}");
        Debug.Log($"Animation actuelle: {(unitAnimator != null ? unitAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle") : "Animator null")}");
        Debug.Log($"Miroir actif: {(spriteRenderer != null ? spriteRenderer.flipX : false)}");
    }
}

