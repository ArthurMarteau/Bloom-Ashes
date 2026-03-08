using UnityEngine;

// StateMachineBehaviour attaché à l'état IdleMove dans l'Animator
// Notifie AnimatorController via callback quand l'état IdleMove se termine
// Doit être dans son propre fichier pour apparaître dans le menu Add Behaviour de Unity
public class IdleMoveExitBehaviour : StateMachineBehaviour
{
    public System.Action onIdleMoveExit;

    // Déclenche le callback à la sortie de l'état IdleMove
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        onIdleMoveExit?.Invoke();
    }
}
