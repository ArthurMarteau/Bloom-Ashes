using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class UnitAnimatorController : MonoBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void PlayWalkAnimation(Vector2 moveDelta)
    {
        animator.speed = 1f;

        if (moveDelta == Vector2.zero)
        {
            animator.Play("Idle");
            return;
        }

        float angle = Mathf.Atan2(moveDelta.y, moveDelta.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        string animName = "";
        bool flip = false;

        if (angle >= 337.5f || angle < 22.5f)
        {
            animName = "Walk_W";
            flip = true;
        }
        else if (angle < 67.5f)
        {
            animName = "Walk_NW";
            flip = true;
        }
        else if (angle < 112.5f)
        {
            animName = "Walk_N";
        }
        else if (angle < 157.5f)
        {
            animName = "Walk_NW";
        }
        else if (angle < 202.5f)
        {
            animName = "Walk_W";
        }
        else if (angle < 247.5f)
        {
            animName = "Walk_SW";
        }
        else if (angle < 292.5f)
        {
            animName = "Walk_S";
        }
        else
        {
            animName = "Walk_SW";
            flip = true;
        }

        animator.Play(animName);
        spriteRenderer.flipX = flip;
    }

    public void PlayIdleReversed()
    {
        animator.speed = 0.5f;
        animator.Play("Idle_Reversed");
    }



}

