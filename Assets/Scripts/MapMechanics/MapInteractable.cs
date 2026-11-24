using UnityEngine;

public abstract class MapInteractable : MonoBehaviour
{
    // Front and back text faces display the same interaction instructions.
    public TextMesh[] labels;

    public abstract void Interact();

    // Only interactable objects that can break need to override this method.
    public virtual void SimulateDamage()
    {
    }

    protected void SetLabel(string message)
    {
        if (labels == null)
        {
            return;
        }

        foreach (TextMesh label in labels)
        {
            if (label != null)
            {
                label.text = message;
            }
        }
    }
}
