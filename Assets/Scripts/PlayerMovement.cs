using UnityEngine;
using System.Collections.Generic;

public class PlayerMovement : MonoBehaviour
{
    private const float GroundedVerticalSpeed = -2f;
    private const float MinimumBounceNormal = 0.7f;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float gravity = -9.8f;
    public float jumpHeight = 0.67f;

    [Header("Movement Smoothing")]
    [SerializeField] private float acceleration = 12f;

    private Vector3 currentMoveDirection;

    [Header("Visual")]
    [SerializeField] private Transform characterVisual;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 540f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private Vector3 playerVelocity;

    private bool isGrounded;
    private bool landingFromAbove;
    private bool isRunning;
    private bool isMoving;

    private readonly Dictionary<Object, float> speedModifiers =
        new Dictionary<Object, float>();

    // Exposed for Animator
    public bool IsGrounded => isGrounded;
    public bool IsRunning => isRunning;
    public bool IsMoving => isMoving;
    public bool IsJumping { get; private set; }
    public bool WasLaunched { get; private set; }

    public float VerticalSpeed => playerVelocity.y;

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

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && playerVelocity.y <= 0f)
        {
            IsJumping = false;
        }
    }

    public void ProcessMove(Vector2 input)
    {
        if (controller == null || !controller.enabled)
            return;

        landingFromAbove =
            !controller.isGrounded && playerVelocity.y <= 0;

        isMoving = input.sqrMagnitude > 0.01f;

        Vector3 moveDirection = Vector3.zero;

        if (cameraTransform != null)
        {
            // Camera-relative movement
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            // Ignore camera's vertical angle
            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            moveDirection =
                forward * input.y +
                right * input.x;
        }
        else
        {
            // Fallback
            moveDirection.x = input.x;
            moveDirection.z = input.y;
        }

        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        // Rotate Mushy toward movement direction
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(moveDirection);

            characterVisual.rotation = Quaternion.RotateTowards(
                characterVisual.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        float currentSpeed =
            isRunning ? runSpeed : walkSpeed;

        currentMoveDirection = Vector3.Lerp(
            currentMoveDirection,
            moveDirection,
            acceleration * Time.deltaTime
        );

        controller.Move(
            currentMoveDirection *
            currentSpeed *
            CurrentSpeedMultiplier *
            Time.deltaTime
        );

        // Gravity
        playerVelocity.y += gravity * Time.deltaTime;

        if (isGrounded && playerVelocity.y < 0)
        {
            playerVelocity.y = GroundedVerticalSpeed;
        }

        controller.Move(playerVelocity * Time.deltaTime);
    }

    public void SetRunning(bool running)
    {
        isRunning = running;
    }

    public void Jump()
    {
        if (isGrounded)
        {
            playerVelocity.y =
                Mathf.Sqrt(jumpHeight * -3.0f * gravity);

            IsJumping = true;
        }
    }

    // Required by BounceMushroom
    public void Launch(float verticalSpeed)
    {
        playerVelocity.y = verticalSpeed;
        isGrounded = false;
        landingFromAbove = false;

        IsJumping = true;
        WasLaunched = true;
    }

    public void ClearLaunch()
    {
        WasLaunched = false;
    }

    // Required by SporeVent
    public void SetSpeedModifier(
        Object source,
        float multiplier)
    {
        if (source != null)
        {
            speedModifiers[source] =
                Mathf.Clamp(multiplier, 0.1f, 1f);
        }
    }

    // Required by SporeVent
    public void RemoveSpeedModifier(Object source)
    {
        if (source != null)
        {
            speedModifiers.Remove(source);
        }
    }

    private void OnControllerColliderHit(
        ControllerColliderHit hit)
    {
        if (!landingFromAbove ||
            playerVelocity.y > 0 ||
            hit.normal.y < MinimumBounceNormal)
        {
            return;
        }

        BounceMushroom mushroom =
            hit.collider.GetComponentInParent<BounceMushroom>();

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