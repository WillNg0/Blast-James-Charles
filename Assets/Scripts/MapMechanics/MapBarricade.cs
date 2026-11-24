using UnityEngine;

public class MapBarricade : MapInteractable
{
    public GameObject barrier;
    public BoxCollider occupiedSpace;
    public int maxHealth = 3;

    public bool IsClosed { get; private set; }
    public bool IsBroken { get; private set; }
    public int Health { get; private set; }

    private readonly Collider[] nearbyColliders = new Collider[16];

    private void OnEnable()
    {
        Health = maxHealth;
        IsClosed = false;
        IsBroken = false;

        if (barrier != null)
        {
            barrier.SetActive(false);
        }

        RefreshLabel();
    }

    public override void Interact()
    {
        if (IsClosed)
        {
            OpenBarricade();
        }
        else
        {
            if (IsPlayerInDoorway())
            {
                SetLabel("BARRICADE\nStep clear of the doorway");
                return;
            }

            CloseBarricade();
        }

        RefreshLabel();
    }

    // Keep the player from being trapped inside the panel when it closes.
    private bool IsPlayerInDoorway()
    {
        if (occupiedSpace == null)
        {
            return false;
        }

        Vector3 center = occupiedSpace.transform.TransformPoint(occupiedSpace.center);
        Vector3 halfExtents = Vector3.Scale(occupiedSpace.size, occupiedSpace.transform.lossyScale) * 0.5f;
        int colliderCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            nearbyColliders,
            occupiedSpace.transform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < colliderCount; i++)
        {
            CharacterController player = nearbyColliders[i].GetComponentInParent<CharacterController>();

            if (player != null)
            {
                return true;
            }
        }

        return false;
    }

    private void OpenBarricade()
    {
        IsClosed = false;
        barrier.SetActive(false);
    }

    private void CloseBarricade()
    {
        Health = maxHealth;
        IsBroken = false;
        IsClosed = true;
        barrier.SetActive(true);
    }

    public void TakeDamage(int amount)
    {
        if (!IsClosed || amount <= 0)
        {
            return;
        }

        Health = Mathf.Max(0, Health - amount);

        if (Health == 0)
        {
            IsClosed = false;
            IsBroken = true;
            barrier.SetActive(false);
        }

        RefreshLabel();
    }

    // The B key stands in for enemy damage until enemy attacks are implemented.
    public override void SimulateDamage()
    {
        TakeDamage(1);
    }

    private void RefreshLabel()
    {
        if (IsBroken)
        {
            SetLabel("BARRICADE BROKEN\n[E] REBUILD & CLOSE");
        }
        else if (IsClosed)
        {
            SetLabel("BARRICADE " + Health + "/" + maxHealth + "\n[E] OPEN   [B] DAMAGE");
        }
        else
        {
            SetLabel("BARRICADE OPEN\n[E] CLOSE");
        }
    }
}
