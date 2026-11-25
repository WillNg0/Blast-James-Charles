using UnityEngine;

public class PlayerAnimationController : MonoBehaviour
{
    private Animator animator;
    private PlayerMovement movement;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponentInParent<PlayerMovement>();
    }

    private void Update()
    {
        if (animator == null || movement == null)
            return;

        bool moving = movement.IsMoving;
        bool running = movement.IsRunning;
        bool jumping = movement.IsJumping;

        animator.SetBool("isJumping", jumping);

        animator.SetBool(
            "isWalking",
            moving && !running && !jumping
        );

        animator.SetBool(
            "isRunning",
            moving && running && !jumping
        );

        // Restart jump animation every time a bounce launches Mushy
        if (movement.WasLaunched)
        {
            animator.Play("Jump", 0, 0f);
            movement.ClearLaunch();
        }
    }
}