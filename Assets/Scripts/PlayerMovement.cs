using UnityEngine;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    private const float GroundedVerticalSpeed = -2f;
    private const float MinimumBounceNormal = 0.7f;

    public float speed = 5f;
    public float gravity = -9.8f;
    public float jumpHeight = 0.67f;

    private CharacterController controller;
    private Vector3 playerVelocity;
    private bool isGrounded;
    private bool landingFromAbove;
    private readonly Dictionary<Object, float> speedModifiers = new Dictionary<Object, float>();

    public float VerticalSpeed => playerVelocity.y;

    // Use the strongest slow effect instead of multiplying overlapping slows together.
    public float CurrentSpeedMultiplier
    {
        get
        {
            float multiplier = 1f;

            foreach (KeyValuePair<Object, float> modifier in speedModifiers)
            {
                if (modifier.Key != null)
                {
                    multiplier = Mathf.Min(multiplier, modifier.Value);
                }
            }

            return multiplier;
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        isGrounded = controller.isGrounded;
    }

    // Receives movement input from InputManager.
    public void ProcessMove(Vector2 input)
    {
        if (controller == null || !controller.enabled)
        {
            return;
        }

        landingFromAbove = !controller.isGrounded && playerVelocity.y <= 0;

        Vector3 moveDirection = Vector3.zero;
        moveDirection.x = input.x;
        moveDirection.z = input.y;
        controller.Move(transform.TransformDirection(moveDirection) * speed * CurrentSpeedMultiplier * Time.deltaTime);

        // A small downward speed keeps the controller in contact with the ground.
        playerVelocity.y += gravity * Time.deltaTime;

        if (isGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = GroundedVerticalSpeed;
        }

        controller.Move(playerVelocity * Time.deltaTime);
    }

    public void Jump()
    {
        if (isGrounded)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -3.0f * gravity);
        }
    }

    // Bounce mushrooms change vertical speed only; normal input handles air steering.
    public void Launch(float verticalSpeed)
    {
        playerVelocity.y = verticalSpeed;
        isGrounded = false;
        landingFromAbove = false;
    }

    public void SetSpeedModifier(Object source, float multiplier)
    {
        if (source != null)
        {
            speedModifiers[source] = Mathf.Clamp(multiplier, 0.1f, 1f);
        }
    }

    public void RemoveSpeedModifier(Object source)
    {
        if (source != null)
        {
            speedModifiers.Remove(source);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Only a downward landing on a cap can bounce the player.
        if (!landingFromAbove || playerVelocity.y > 0 || hit.normal.y < MinimumBounceNormal)
        {
            return;
        }

        BounceMushroom mushroom = hit.collider.GetComponentInParent<BounceMushroom>();

        if (mushroom != null)
        {
            mushroom.TryBounce(this);
        }
    }

    private void OnDisable()
    {
        speedModifiers.Clear();
    }
}
