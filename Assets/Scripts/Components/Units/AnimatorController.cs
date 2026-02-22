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
    public const float ATTACK_ANIMATION_DURATION = 0.5f;

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
    private UnitStatsComponent unitStatsComponent;
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
        unitStatsComponent = GetComponent<UnitStatsComponent>();

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

        // Ne joue pas d'animation si l'unité est morte
        if (unitStatsComponent != null && unitStatsComponent.IsDead())
        {
            return;
        }

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

    // Interface pour déclencher animation d'attaque adaptée à attackSpeed
    public void PlayAttackAnimation(Vector2Int fromPosition, Vector2Int toPosition)
    {
        if (unitAnimator == null) return;

        // Ne joue pas d'animation si l'unité est morte
        if (unitStatsComponent != null && unitStatsComponent.IsDead())
        {
            return;
        }

        AnimationDirection direction = CalculateDirection(fromPosition, toPosition);
        bool requiresMirror = RequiresMirror(direction);
        SetMirror(requiresMirror);

        string attackAnimationName = GetAttackAnimationName(direction);
        unitAnimator.Play(attackAnimationName);

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Attack {attackAnimationName} (durée {ATTACK_ANIMATION_DURATION}s)");
        }
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

    // Joue l'animation Idle_Attack pour stance de combat
    // Boucle en attendant le prochain coup
    public void PlayIdleAttackAnimation(Vector2Int fromPosition, Vector2Int toPosition)
    {
        if (unitAnimator == null) return;

        // Ne joue pas d'animation si l'unité est morte
        if (unitStatsComponent != null && unitStatsComponent.IsDead())
        {
            return;
        }

        // Calcule la direction
        AnimationDirection direction = CalculateDirection(fromPosition, toPosition);

        // Gère le miroir
        bool requiresMirror = RequiresMirror(direction);
        SetMirror(requiresMirror);

        // Construit le nom Idle_Attack
        string idleAttackAnimationName = GetIdleAttackAnimationName(direction);

        // Joue l'animation en boucle
        unitAnimator.Play(idleAttackAnimationName);

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Animation {idleAttackAnimationName}, Direction {direction}, Miroir: {requiresMirror}");
        }
    }

    // Retourne le nom de l'animation Idle_Attack selon la direction
    private string GetIdleAttackAnimationName(AnimationDirection direction)
    {
        switch (direction)
        {
            case AnimationDirection.North:
                return "AttackIdle_N";
            case AnimationDirection.NorthWest:
            case AnimationDirection.NorthEast:
                return "AttackIdle_NW";
            case AnimationDirection.West:
            case AnimationDirection.East:
                return "AttackIdle_W";
            case AnimationDirection.SouthWest:
            case AnimationDirection.SouthEast:
                return "AttackIdle_SW";
            case AnimationDirection.South:
                return "AttackIdle_S";
            default:
                return "AttackIdle_S";
        }
    }

    // Interface pour revenir à l'animation idle
    // Appelée par MovementController quand l'unité s'arrête
    public void PlayIdleAnimation()
    {
        if (unitAnimator == null) return;

        // Ne joue pas d'animation si l'unité est morte
        if (unitStatsComponent != null && unitStatsComponent.IsDead())
        {
            return;
        }

        // Arrête IdleReversed si en cours (Rootborn)
        if (idleReversedCoroutine != null)
        {
            StopCoroutine(idleReversedCoroutine);
            idleReversedCoroutine = null;
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

        // Ne joue pas d'animation si l'unité est morte
        if (unitStatsComponent != null && unitStatsComponent.IsDead())
        {
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
            idleReversedCoroutine = null;
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

    // Retourne si IdleReversed a déjà été joué dans cette session
    // Utilisé par AttackController pour déterminer si AttackIdle peut être joué
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

    // Retourne le nom de l'animation actuellement jouée
    // Utilisé pour sauvegarder l'état avant une interruption (Hit, etc.)
    private string GetCurrentAnimationName()
    {
        AnimatorStateInfo stateInfo = unitAnimator.GetCurrentAnimatorStateInfo(0);

        // Le nom complet contient le layer, on extrait juste le nom de l'animation
        // Format: "Base Layer.Move_N" -> "Move_N"
        AnimatorClipInfo[] clipInfo = unitAnimator.GetCurrentAnimatorClipInfo(0);

        if (clipInfo.Length > 0)
        {
            return clipInfo[0].clip.name;
        }

        // Fallback si pas de clip trouvé
        return "Idle";
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

    // Vérifie si cette unité est un Rootborn via le ScriptableObject (UnitStatsData.isRootborn)
    private bool IsRootborn()
    {
        if (unitStatsComponent != null) return unitStatsComponent.IsRootborn();
        return false;
    }

    // Retourne l'état actuel des animations
    // Interface pour debug et coordination avec autres systèmes
    public bool IsPlayingSpecialAnimation()
    {
        return idleReversedCoroutine != null;
    }

    // Arrête toutes les animations en cours
    // Méthode publique pour reset depuis l'extérieur
    public void StopAllAnimations()
    {
        if (idleReversedCoroutine != null)
        {
            StopCoroutine(idleReversedCoroutine);
            idleReversedCoroutine = null;
        }

        // Reset le flag de session pour permettre le prochain cycle IdleReversed
        hasPlayedIdleReversedThisSession = false;

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
    // Appelée par UnitStatsComponent quand l'unité meurt
    public void PlayDeathAnimation()
    {
        if (unitAnimator == null) return;

        // Arrête toutes les animations en cours
        if (idleReversedCoroutine != null)
        {
            StopCoroutine(idleReversedCoroutine);
            idleReversedCoroutine = null;
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

    // Joue l'animation Hit selon la direction de l'attaquant
    // Sauvegarde l'animation en cours et la reprend exactement où elle en était après le Hit
    public void PlayHitAnimation(Vector2Int attackerPosition, Vector2Int victimPosition)
    {
        // Ne joue pas Hit si l'unité est morte (elle doit rester en Death)
        if (unitStatsComponent != null && unitStatsComponent.IsDead())
        {
            if (debugAnimations)
            {
                Debug.Log($"AnimatorController {gameObject.name}: Hit ignoré - unité morte");
            }
            return;
        }

        // Sauvegarde l'animation actuelle
        string currentAnimationName = GetCurrentAnimationName();

        // Si on est en train de jouer une animation Attack
        if (currentAnimationName.StartsWith("Attack_"))
        {
            if (debugAnimations)
            {
                Debug.Log($"AnimatorController {gameObject.name}: Hit ignoré - animation Attack en cours");
            }
            return;
        }

        // Vérifie si l'unité est un Rootborn en état Idle
        MovementController movementController = GetComponent<MovementController>();
        AttackController attackController = GetComponent<AttackController>();

        if (IsRootborn() && movementController != null &&
            movementController.GetCurrentState() == MovementController.MovementState.Idle &&
            attackController != null && !attackController.IsAttacking())
        {
            // Rootborn idle utilise toujours Hit_Idle
            if (debugAnimations)
            {
                Debug.Log($"AnimatorController {gameObject.name}: Animation Hit_Idle (Rootborn en Idle)");
            }
            unitAnimator.Play("Hit_Idle"); // Force le redémarrage à 0
            return;
        }

        // Calcule la direction du coup (attaquant vers victime)
        int deltaY = attackerPosition.y - victimPosition.y;

        // Détermine quelle animation Hit jouer selon direction verticale
        string hitAnimation;
        if (deltaY > 0)
        {
            // Coup vient du haut (N, NE, NW)
            hitAnimation = "Hit_NW";
        }
        else
        {
            // Coup vient du bas ou du côté (E, SE, S, SW, W)
            hitAnimation = "Hit_SW";
        }

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Animation {hitAnimation} (attaquant {attackerPosition} -> victime {victimPosition}), restaurera {currentAnimationName}");
        }

        unitAnimator.Play(hitAnimation);
        StartCoroutine(RestoreAnimationAfterHit(currentAnimationName, 0.333f));
    }

    // Coroutine pour restaurer l'animation précédente après le Hit
    // Rejoue l'animation exactement à la frame où elle en était
    private IEnumerator RestoreAnimationAfterHit(string animationName, float delay)
    {
        // Attend la fin de l'animation Hit
        yield return new WaitForSeconds(delay);

        // Rejoue l'animation à partir du temps sauvegardé
        unitAnimator.Play(animationName);

        if (debugAnimations)
        {
            Debug.Log($"AnimatorController {gameObject.name}: Animation {animationName}");
        }
    }

    // Méthode de debug pour afficher l'état de l'animator
    [ContextMenu("Debug Animator State")]
    private void DebugAnimatorState()
    {
        Debug.Log($"=== AnimatorController Debug - {gameObject.name} ===");
        Debug.Log($"Est Rootborn: {IsRootborn()}");
        Debug.Log($"IdleReversed en cours: {idleReversedCoroutine != null}");
        Debug.Log($"Vitesse animator: {(unitAnimator != null ? unitAnimator.speed : 0f)}");
        Debug.Log($"Animation actuelle: {(unitAnimator != null ? unitAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle") : "Animator null")}");
        Debug.Log($"Miroir actif: {(spriteRenderer != null ? spriteRenderer.flipX : false)}");
    }
}

