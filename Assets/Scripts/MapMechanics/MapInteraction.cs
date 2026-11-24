using UnityEngine;
using UnityEngine.InputSystem;

public class MapInteraction : MonoBehaviour
{
    private const float NearbySearchRadius = 2.8f;
    private const float TargetHeight = 1.5f;
    private const float MinimumFacingDot = 0.4f;

    public Camera view;
    public float reach = 4f;

    public MapInteractable CurrentTarget { get; private set; }

    private readonly Collider[] nearbyColliders = new Collider[24];
    private InputAction interactAction;
    private InputAction damageAction;
    private bool interactionRequested;
    private bool damageRequested;

    private void Awake()
    {
        interactAction = new InputAction("Map interact", InputActionType.Button, "<Keyboard>/e");
        damageAction = new InputAction("Barricade damage", InputActionType.Button, "<Keyboard>/b");
        interactAction.performed += OnInteractPerformed;
        damageAction.performed += OnDamagePerformed;
    }

    private void OnEnable()
    {
        interactAction.Enable();
        damageAction.Enable();
    }

    private void OnDisable()
    {
        interactAction.Disable();
        damageAction.Disable();
        interactionRequested = false;
        damageRequested = false;
    }

    private void OnDestroy()
    {
        interactAction.Dispose();
        damageAction.Dispose();
    }

    // Remember button presses until Update has found the object the player is facing.
    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        interactionRequested = true;
    }

    private void OnDamagePerformed(InputAction.CallbackContext context)
    {
        damageRequested = true;
    }

    private void Update()
    {
        CurrentTarget = null;
        bool shouldInteract = interactionRequested;
        bool shouldDamage = damageRequested;
        interactionRequested = false;
        damageRequested = false;

        if (view == null)
        {
            return;
        }

        CurrentTarget = FindTargetInView();

        if (CurrentTarget == null)
        {
            CurrentTarget = FindNearbyTarget();
        }

        if (CurrentTarget == null)
        {
            return;
        }

        if (shouldInteract)
        {
            CurrentTarget.Interact();
        }

        if (shouldDamage)
        {
            CurrentTarget.SimulateDamage();
        }
    }

    private MapInteractable FindTargetInView()
    {
        RaycastHit hit;
        bool foundTarget = Physics.Raycast(
            view.transform.position,
            view.transform.forward,
            out hit,
            reach,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (foundTarget)
        {
            return hit.collider.GetComponentInParent<MapInteractable>();
        }

        return null;
    }

    // A nearby target can still be used while looking at its sign above the doorway.
    // Include triggers so the barricade can be closed while its solid panel is open.
    private MapInteractable FindNearbyTarget()
    {
        int colliderCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            NearbySearchRadius,
            nearbyColliders,
            ~0,
            QueryTriggerInteraction.Collide);

        MapInteractable closestTarget = null;
        float closestDistanceSquared = float.PositiveInfinity;

        for (int i = 0; i < colliderCount; i++)
        {
            MapInteractable candidate = nearbyColliders[i].GetComponentInParent<MapInteractable>();

            if (candidate == null)
            {
                continue;
            }

            Vector3 targetPosition = candidate.transform.position + Vector3.up * TargetHeight;
            Vector3 directionToTarget = targetPosition - view.transform.position;
            float facingDot = Vector3.Dot(view.transform.forward, directionToTarget.normalized);

            if (facingDot <= MinimumFacingDot || directionToTarget.sqrMagnitude >= closestDistanceSquared)
            {
                continue;
            }

            if (!HasClearLineOfSight(candidate, directionToTarget))
            {
                continue;
            }

            closestTarget = candidate;
            closestDistanceSquared = directionToTarget.sqrMagnitude;
        }

        return closestTarget;
    }

    private bool HasClearLineOfSight(MapInteractable target, Vector3 directionToTarget)
    {
        RaycastHit hit;
        bool isBlocked = Physics.Raycast(
            view.transform.position,
            directionToTarget.normalized,
            out hit,
            directionToTarget.magnitude,
            ~0,
            QueryTriggerInteraction.Ignore);

        if (!isBlocked)
        {
            return true;
        }

        return hit.collider.GetComponentInParent<MapInteractable>() == target;
    }
}
