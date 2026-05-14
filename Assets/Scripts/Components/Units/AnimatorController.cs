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
    private float rootbornIdleReversedDuration = 0.5f;  // Durée IdleReversed Rootborn
    private bool syncAnimationWithDuration = true;     // Synchronise animation avec durée
    public const float ATTACK_ANIMATION_DURATION = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugAnimations = true;

    // Variables d'état d'animation
    private Animator unitAnimator;
    private SpriteRenderer spriteRenderer;
    private UnitStatsComponent unitStatsComponent;
    private MovementController movementController;
    private AttackController attackController;
    private Coroutine idleReversedCoroutine;
    // Alterne à chaque fin de IdleMove pour le cycle Idle_SW ↔ Idle_SW miroir
    private bool isIdleMirrored = false;

    // Cache des noms d'animations universels
    private Dictionary<AnimationDirection, string> walkAnimations;
    private string idleAnimationName = AnimationNames.Idle;
    private string idleReversedAnimationName = AnimationNames.IdleReversed; // Spécifique Rootborn
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
            GameLogger.Error($"AnimatorController sur {gameObject.name}: Composant Animator manquant!", this);
            return;
        }

        if (spriteRenderer == null)
        {
            GameLogger.Error($"AnimatorController sur {gameObject.name}: Composant SpriteRenderer manquant!", this);
            return;
        }

        movementController = GetComponent<MovementController>();
        attackController = GetComponent<AttackController>();

        // Initialise la vitesse d'animation à 1 pour toutes les unités
        unitAnimator.speed = 1f;

        // Configure les noms d'animations universels
        SetupAnimationNames();

        // Récupère le behaviour sur l'état IdleMove et s'abonne au callback de sortie
        IdleMoveExitBehaviour idleMoveBehaviour = unitAnimator.GetBehaviour<IdleMoveExitBehaviour>();
        if (idleMoveBehaviour != null)
            idleMoveBehaviour.onIdleMoveExit = OnIdleMoveCompleted;

        if (debugAnimations)
        {
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController initialisé pour {gameObject.name}, Rootborn: {IsRootborn()}", this);
        }
    }

    // Configure les noms d'animations universels pour toutes les unités
    // Utilise le même nommage pour tous types d'unités
    private void SetupAnimationNames()
    {
        walkAnimations = new Dictionary<AnimationDirection, string>
        {
            { AnimationDirection.North,     AnimationNames.WalkN  },
            { AnimationDirection.NorthWest, AnimationNames.WalkNW },
            { AnimationDirection.West,      AnimationNames.WalkW  },
            { AnimationDirection.SouthWest, AnimationNames.WalkSW },
            { AnimationDirection.South,     AnimationNames.WalkS  },
            // Les directions miroir utilisent les mêmes animations
            { AnimationDirection.NorthEast, AnimationNames.WalkNW }, // Miroir de NW
            { AnimationDirection.East,      AnimationNames.WalkW  }, // Miroir de W
            { AnimationDirection.SouthEast, AnimationNames.WalkSW }  // Miroir de SW
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

        // Réinitialise le cycle idle miroir — le mouvement annule l'alternance en cours
        isIdleMirrored = false;

        // Détermine si un miroir est requis
        bool requiresMirror = RequiresMirror(direction);
        SetMirror(requiresMirror);

        // Récupère le nom de l'animation correspondante
        if (walkAnimations.TryGetValue(direction, out string animationName))
        {
            unitAnimator.Play(animationName);

            if (debugAnimations)
            {
                GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Animation {animationName}, Direction {direction}, Miroir: {requiresMirror}", this);
            }
        }
        else
        {
            GameLogger.Warn(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Animation non trouvée pour direction {direction}", this);
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

        // Réinitialise le cycle idle miroir — l'attaque annule l'alternance en cours
        isIdleMirrored = false;

        bool requiresMirror = RequiresMirror(direction);
        SetMirror(requiresMirror);

        string attackAnimationName = GetDirectionalAnimationName(AnimationNames.PrefixAttack, direction);
        unitAnimator.Play(attackAnimationName);

        if (debugAnimations)
        {
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Attack {attackAnimationName} (durée {ATTACK_ANIMATION_DURATION}s)", this);
        }
    }

    // Joue l'animation Idle_Attack pour stance de combat
    // Boucle en attendant le prochain coup
    public void PlayIdleAttackAnimation(Vector2Int fromPosition, Vector2Int toPosition)
    {
        if (unitAnimator == null) return;
        
        // Ne pas écraser une anim Attack en cours (taguée "Attack")
        if (unitAnimator.GetCurrentAnimatorStateInfo(0).IsTag("Attack")) return;

        // Ne joue pas d'animation si l'unité est morte
        if (unitStatsComponent != null && unitStatsComponent.IsDead())
        {
            return;
        }

        // Calcule la direction
        AnimationDirection direction = CalculateDirection(fromPosition, toPosition);

        // Réinitialise le cycle idle miroir — le combat annule l'alternance en cours
        isIdleMirrored = false;

        // Gère le miroir
        bool requiresMirror = RequiresMirror(direction);
        SetMirror(requiresMirror);

        // Construit le nom Idle_Attack
        string idleAttackAnimationName = GetDirectionalAnimationName(AnimationNames.PrefixAttackIdle, direction);

        // Joue l'animation en boucle
        unitAnimator.Play(idleAttackAnimationName);

        if (debugAnimations)
        {
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Animation {idleAttackAnimationName}, Direction {direction}, Miroir: {requiresMirror}", this);
        }
    }

    // Construit le nom d'animation directionnelle à partir d'un préfixe et d'une direction
    // Factorise la logique commune à Attack_ et AttackIdle_ (même switch de suffixes)
    private string GetDirectionalAnimationName(string prefix, AnimationDirection direction)
    {
        switch (direction)
        {
            case AnimationDirection.North:
                return prefix + AnimationNames.SuffixN;
            case AnimationDirection.NorthWest:
            case AnimationDirection.NorthEast: // Miroir de NW
                return prefix + AnimationNames.SuffixNW;
            case AnimationDirection.West:
            case AnimationDirection.East: // Miroir de W
                return prefix + AnimationNames.SuffixW;
            case AnimationDirection.SouthWest:
            case AnimationDirection.SouthEast: // Miroir de SW
                return prefix + AnimationNames.SuffixSW;
            case AnimationDirection.South:
            default:
                return prefix + AnimationNames.SuffixS;
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
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Animation Idle", this);
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
                GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: IdleReversed déjà joué cette session, skip", this);
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
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Début IdleReversed Rootborn ({rootbornIdleReversedDuration}s)", this);
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
                GameLogger.Log(GameLogger.Category.UI, $"Animation sync: durée originale {originalAnimationLength}s, vitesse ajustée à {requiredSpeed}", this);
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
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Fin IdleReversed Rootborn", this);
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
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Session IdleReversed resetée", this);
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

    // Retourne true si l'Animator est actuellement dans un état Hit
    // Utilisé par AttackController pour ne pas écraser le trigger Hit avec PlayIdleAttackAnimation
    public bool IsPlayingHitAnimation()
    {
        if (unitAnimator == null) return false;
        AnimatorStateInfo stateInfo = unitAnimator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(AnimationNames.HitNW)
            || stateInfo.IsName(AnimationNames.HitSW)
            || stateInfo.IsName(AnimationNames.HitIdle);
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

        // Reset le cycle idle miroir
        isIdleMirrored = false;

        // Reset la vitesse d'animation
        if (unitAnimator != null)
        {
            unitAnimator.speed = 1f;
            // Annule tout trigger Hit en attente pour éviter un déclenchement parasite
            unitAnimator.ResetTrigger("Hit");
        }

        PlayIdleAnimation();

        if (debugAnimations)
        {
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Toutes animations arrêtées", this);
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

        // Annule tout trigger Hit en attente pour éviter un flash d'animation après la mort
        unitAnimator.ResetTrigger("Hit");

        // Remet le miroir par défaut
        SetMirror(false);

        // Joue l'animation de mort
        unitAnimator.Play(AnimationNames.Death);

        if (debugAnimations)
        {
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Animation Death", this);
        }
    }

    // Joue l'animation Hit via les paramètres Animator (Trigger + Bools)
    // La machine d'états Unity gère la transition et le retour à l'animation précédente
    public void PlayHitAnimation(Vector2Int attackerPosition, Vector2Int victimPosition)
    {
        if (unitAnimator == null) return;

        // Ne joue pas Hit si l'unité est morte (elle doit rester en Death)
        if (unitStatsComponent != null && unitStatsComponent.IsDead()) return;

        // Ignore si un état tagué "Attack" est actif — AttackIdle_ (non tagué) reste interruptible
        if (unitAnimator.GetCurrentAnimatorStateInfo(0).IsTag("Attack")) return;

        // Cas spécial Rootborn en Idle : joue Hit_Idle sans calcul de direction
        // On reset le miroir uniquement ici car Hit_Idle est directionless (direction S)
        if (IsRootborn()
            && movementController != null
            && movementController.GetCurrentState() == MovementController.MovementState.Idle
            && attackController != null
            && !attackController.IsAttacking())
        {
            isIdleMirrored = false;
            SetMirror(false);

            if (debugAnimations)
                GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Hit_Idle (Rootborn en Idle)", this);

            unitAnimator.Play(AnimationNames.HitIdle);
            return;
        }

        // Calcule la direction du coup (attaquant vers victime) pour choisir Hit_NW ou Hit_SW
        // N, NW → Hit_NW | NE → Hit_NW miroir
        // W, SW, S → Hit_SW | E, SE → Hit_SW miroir
        Vector2Int delta = attackerPosition - victimPosition;
        int dx = Mathf.Clamp(delta.x, -1, 1);
        int dy = Mathf.Clamp(delta.y, -1, 1);

        bool isHitNW = (dy > 0) || (dy == 0 && dx == 0);
        bool mirror  = (dx > 0); // NE, E, SE → miroir horizontal

        if (debugAnimations)
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Hit_{(isHitNW ? "NW" : "SW")} (attaquant {attackerPosition} -> victime {victimPosition}, miroir: {mirror})", this);

        // Reset le cycle idle uniquement ici, APRÈS avoir calculé le miroir Hit
        // Évite d'écraser le flipX de l'AttackIdle en cours avant la transition
        isIdleMirrored = false;
        SetMirror(mirror);
        // IsHitIdle uniquement sur le Rootborn — les autres unités n'ont pas ce paramètre
        if (IsRootborn()) unitAnimator.SetBool("IsHitIdle", false);
        unitAnimator.SetBool("IsHitNW", isHitNW);
        unitAnimator.SetTrigger("Hit");
    }

    // Appelé par IdleMoveExitBehaviour quand l'état IdleMove se termine
    // Toggle le miroir pour alterner Idle_SW normal et Idle_SW miroir
    private void OnIdleMoveCompleted()
    {
        isIdleMirrored = !isIdleMirrored;
        SetMirror(isIdleMirrored);

        if (debugAnimations)
            GameLogger.Log(GameLogger.Category.UI, $"AnimatorController {gameObject.name}: Alternance idle miroir → {isIdleMirrored}", this);
    }

    // Méthode de debug pour afficher l'état de l'animator
    [ContextMenu("Debug Animator State")]
    private void DebugAnimatorState()
    {
        GameLogger.Log(GameLogger.Category.UI, $"=== AnimatorController Debug - {gameObject.name} ===", this);
        GameLogger.Log(GameLogger.Category.UI, $"Est Rootborn: {IsRootborn()}", this);
        GameLogger.Log(GameLogger.Category.UI, $"IdleReversed en cours: {idleReversedCoroutine != null}", this);
        GameLogger.Log(GameLogger.Category.UI, $"Cycle idle miroir actif: {isIdleMirrored}", this);
        GameLogger.Log(GameLogger.Category.UI, $"Vitesse animator: {(unitAnimator != null ? unitAnimator.speed : 0f)}", this);
        GameLogger.Log(GameLogger.Category.UI, $"Animation actuelle: {(unitAnimator != null ? unitAnimator.GetCurrentAnimatorStateInfo(0).IsName(AnimationNames.Idle) : "Animator null")}", this);
        GameLogger.Log(GameLogger.Category.UI, $"Miroir actif: {(spriteRenderer != null ? spriteRenderer.flipX : false)}", this);
    }
}

