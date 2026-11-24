using UnityEngine;

public class MapGate : MapInteractable
{
    // Gates with the same group open together, such as the three Rise entrances.
    public string unlockGroup;
    public string areaName;
    public GameObject barrier;

    public bool IsUnlocked { get; private set; }

    private void OnEnable()
    {
        IsUnlocked = false;

        if (barrier != null)
        {
            barrier.SetActive(true);
        }

        RefreshLabel();
    }

    public override void Interact()
    {
        Unlock();
    }

    public void Unlock()
    {
        foreach (MapGate gate in FindObjectsByType<MapGate>())
        {
            if (gate.gameObject.scene == gameObject.scene && gate.unlockGroup == unlockGroup)
            {
                gate.OpenGate();
            }
        }
    }

    private void OpenGate()
    {
        IsUnlocked = true;

        if (barrier != null)
        {
            barrier.SetActive(false);
        }

        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (IsUnlocked)
        {
            SetLabel(areaName + "\nOPEN");
        }
        else
        {
            SetLabel(areaName + "\n[E] UNLOCK - FREE");
        }
    }
}
