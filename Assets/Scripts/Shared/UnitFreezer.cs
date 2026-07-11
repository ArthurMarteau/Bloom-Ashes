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

    public static void Unfreeze(GameObject unit, bool enableComponents, bool unfreezeAnimator)
    {
        if (unit == null)
            return;

        if (enableComponents)
        {
            MovementController movementController = unit.GetComponent<MovementController>();
            if (movementController != null)
                movementController.enabled = true;

            AttackController attackController = unit.GetComponent<AttackController>();
            if (attackController != null)
                attackController.enabled = true;

            ChaseController chaseController = unit.GetComponent<ChaseController>();
            if (chaseController != null)
                chaseController.enabled = true;

            Collider2D collider = unit.GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = true;
        }

        if (unfreezeAnimator)
        {
            Animator animator = unit.GetComponent<Animator>();
            if (animator != null)
                animator.speed = 1f;
        }
    }

    // Fige une unité puis programme son dégel sur une échéance de tick déterministe.
    public static void FreezeForTicks(GameObject unit, int durationTicks)
    {
        if (unit == null || durationTicks <= 0)
            return;

        Freeze(unit, disableComponents: true, freezeAnimator: false);
        long currentTick = SimulationClockManager.Instance?.CurrentTick ?? 0;
        TimedUnitFreeze timer = unit.GetComponent<TimedUnitFreeze>();
        if (timer == null)
            timer = unit.AddComponent<TimedUnitFreeze>();
        timer.Initialize(currentTick + durationTicks);
    }
}
