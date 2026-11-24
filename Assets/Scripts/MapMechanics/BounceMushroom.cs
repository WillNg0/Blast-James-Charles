using UnityEngine;

public class BounceMushroom : MonoBehaviour
{
    private const float BounceCooldown = 0.25f;

    // Controls how high the player bounces. Horizontal movement stays under player control.
    public float verticalLaunchSpeed = 8f;

    private float lastBounceTime = -10f;

    private void OnEnable()
    {
        // Make the mushroom ready immediately when a new run starts.
        lastBounceTime = -10f;
    }

    public bool TryBounce(PlayerMovement movement)
    {
        bool isCoolingDown = Time.time - lastBounceTime < BounceCooldown;

        if (!isActiveAndEnabled || movement == null || isCoolingDown)
        {
            return false;
        }

        lastBounceTime = Time.time;
        movement.Launch(verticalLaunchSpeed);
        return true;
    }
}
