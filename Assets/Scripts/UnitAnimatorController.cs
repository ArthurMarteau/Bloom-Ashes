using UnityEngine;

public class UnitAnimatorController : MonoBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetDirection(Vector2 direction)
    {
        {
            float angle = Mathf.Atan2(direction.y, direction.x)*Mathf.Rad2Deg;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
