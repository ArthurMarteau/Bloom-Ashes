using UnityEngine;

// Helper partagé pour figer une unité sans porter d'état Unity.
public static class UnitFreezer
{
    public static void Freeze(GameObject unit, bool disableComponents, bool freezeAnimator)
    {
        if (unit == null)
            return;

        // Freeze animator en premier — avant toute transition déclenchée par le freeze des contrôleurs.
        if (freezeAnimator)
        {
            Animator animator = unit.GetComponent<Animator>();
            if (animator != null)
                animator.speed = 0f;
        }

        MovementController movementController = unit.GetComponent<MovementController>();
        if (movementController != null && movementController.enabled)
            movementController.FreezeInPlace();

        AttackController attackController = unit.GetComponent<AttackController>();
        if (attackController != null && attackController.enabled)
            attackController.StopAttack();

        ChaseController chaseController = unit.GetComponent<ChaseController>();
        if (chaseController != null && chaseController.enabled)
            chaseController.StopChase();

        if (disableComponents)
        {
            if (movementController != null)
                movementController.enabled = false;

            if (attackController != null)
                attackController.enabled = false;

            if (chaseController != null)
                chaseController.enabled = false;

            Collider2D collider = unit.GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;
        }

    }
}
