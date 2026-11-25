using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    private PlayerInput.OnFootActions onFoot;

    private PlayerMovement move;
    private PlayerLook look;

    private void Awake()
    {
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;

        move = GetComponent<PlayerMovement>();
        look = GetComponent<PlayerLook>();

        // Jump
        onFoot.Jump.performed += ctx => move.Jump();

        // Sprint starts when Shift is pressed
        onFoot.Sprint.performed += ctx =>
            move.SetRunning(true);

        // Sprint stops when Shift is released
        onFoot.Sprint.canceled += ctx =>
            move.SetRunning(false);
    }

    private void Update()
    {
        move.ProcessMove(
            onFoot.Movement.ReadValue<Vector2>()
        );
    }

    private void LateUpdate()
    {
        look.ProcessLook(
            onFoot.Look.ReadValue<Vector2>()
        );
    }

    private void OnEnable()
    {
        onFoot.Enable();
    }

    private void OnDisable()
    {
        onFoot.Disable();
    }
}