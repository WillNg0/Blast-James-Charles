using UnityEngine;
using System.Collections.Generic;

public class SporeVent : MonoBehaviour
{
    public enum VentPhase
    {
        Resting,
        Warning,
        Active
    }

    private static readonly Color RestingColor = new Color(0.3f, 0.4f, 0.22f);
    private static readonly Color WarningDarkColor = new Color(0.65f, 0.45f, 0.12f);
    private static readonly Color WarningLightColor = new Color(1f, 0.92f, 0.16f);
    private static readonly Color ActiveColor = new Color(0.48f, 0.85f, 0.22f);

    public BoxCollider effectVolume;
    public Renderer indicator;
    public GameObject cloud;
    public TextMesh[] labels;

    public float restSeconds = 5f;
    public float warningSeconds = 2f;
    public float activeSeconds = 3f;
    public float speedMultiplier = 0.65f;

    public VentPhase Phase { get; private set; }

    private float elapsedTime;
    private Material indicatorMaterial;
    private readonly Collider[] nearbyColliders = new Collider[32];
    private readonly HashSet<PlayerMovement> affectedPlayers = new HashSet<PlayerMovement>();
    private readonly HashSet<PlayerMovement> playersInCloud = new HashSet<PlayerMovement>();

    private void Awake()
    {
        if (indicator != null)
        {
            // Each vent needs its own tint without changing the shared material asset.
            indicatorMaterial = indicator.material;
        }
    }

    private void OnEnable()
    {
        elapsedTime = 0;
        Phase = VentPhase.Resting;
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        float cycleLength = restSeconds + warningSeconds + activeSeconds;
        float cyclePosition = elapsedTime % Mathf.Max(0.1f, cycleLength);

        UpdatePhase(cyclePosition);
        UpdateVisuals();
        UpdateLabels(cyclePosition);
        FindPlayersInCloud();
        UpdateSlowEffects();
    }

    private void UpdatePhase(float cyclePosition)
    {
        if (cyclePosition < restSeconds)
        {
            Phase = VentPhase.Resting;
        }
        else if (cyclePosition < restSeconds + warningSeconds)
        {
            Phase = VentPhase.Warning;
        }
        else
        {
            Phase = VentPhase.Active;
        }
    }

    private void UpdateVisuals()
    {
        if (cloud != null)
        {
            cloud.SetActive(Phase == VentPhase.Active);
        }

        if (indicatorMaterial == null)
        {
            return;
        }

        if (Phase == VentPhase.Active)
        {
            indicatorMaterial.color = ActiveColor;
        }
        else if (Phase == VentPhase.Warning)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12);
            indicatorMaterial.color = Color.Lerp(WarningDarkColor, WarningLightColor, pulse);
        }
        else
        {
            indicatorMaterial.color = RestingColor;
        }
    }

    private void UpdateLabels(float cyclePosition)
    {
        string message;

        if (Phase == VentPhase.Active)
        {
            message = "SPORES ACTIVE\nMovement 65% - go around";
        }
        else if (Phase == VentPhase.Warning)
        {
            int secondsUntilBurst = Mathf.CeilToInt(restSeconds + warningSeconds - cyclePosition);
            message = "SPORE VENT - WARNING\nBurst in " + secondsUntilBurst + "s";
        }
        else
        {
            message = "SPORE VENT - RESTING\nClear bypass on the outside";
        }

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

    private void FindPlayersInCloud()
    {
        playersInCloud.Clear();

        if (Phase != VentPhase.Active || effectVolume == null)
        {
            return;
        }

        Vector3 center = effectVolume.transform.TransformPoint(effectVolume.center);
        Vector3 halfExtents = Vector3.Scale(effectVolume.size, effectVolume.transform.lossyScale) * 0.5f;
        int colliderCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            nearbyColliders,
            effectVolume.transform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < colliderCount; i++)
        {
            PlayerMovement player = nearbyColliders[i].GetComponentInParent<PlayerMovement>();

            if (player != null)
            {
                playersInCloud.Add(player);
            }
        }
    }

    // Remove this vent's slow on exit, then apply it to players still in the cloud.
    private void UpdateSlowEffects()
    {
        foreach (PlayerMovement player in affectedPlayers)
        {
            if (player != null && !playersInCloud.Contains(player))
            {
                player.RemoveSpeedModifier(this);
            }
        }

        foreach (PlayerMovement player in playersInCloud)
        {
            if (player != null)
            {
                player.SetSpeedModifier(this, speedMultiplier);
            }
        }

        affectedPlayers.Clear();

        foreach (PlayerMovement player in playersInCloud)
        {
            affectedPlayers.Add(player);
        }
    }

    private void OnDisable()
    {
        foreach (PlayerMovement player in affectedPlayers)
        {
            if (player != null)
            {
                player.RemoveSpeedModifier(this);
            }
        }

        affectedPlayers.Clear();
        playersInCloud.Clear();

        if (cloud != null)
        {
            cloud.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (indicatorMaterial != null)
        {
            Destroy(indicatorMaterial);
        }
    }
}
