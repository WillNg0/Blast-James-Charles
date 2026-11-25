using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class ChargingMushroom : MonoBehaviour
{
    public enum MushroomState
    {
        Pursuing,
        WindingUp,
        Charging,
        Recovering,
        Stunned,
        Dead
    }

    public Transform visualRoot;
    public Animator animator;
    public CapsuleCollider bodyCollider;
    public float moveSpeed = 3.1f;
    public float chargeSpeed = 10f;
    public float chargeRange = 8f;
    public float warningSeconds = 1f;
    public float recoverySeconds = 1.2f;
    public float stunSeconds = 2.4f;
    public int attackDamage = 20;

    public MushroomState State { get; private set; }
    public Health Target { get; private set; }
    public Vector3 ChargeDirection { get; private set; }

    private NavMeshAgent agent;
    private Health health;
    private Collider targetBody;
    private Vector3 originalScale;
    private Quaternion originalRotation;
    private float stateTime;
    private float nextPathUpdate;
    private float distanceCharged;
    private bool damageApplied;
    private MapBarricade targetedBarricade;
    private string currentAnimation;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        health.Died += OnDeath;
        originalScale = visualRoot.localScale;
        originalRotation = visualRoot.localRotation;
        State = MushroomState.Pursuing;
    }

    public void SetTarget(Health target)
    {
        Target = target;
        targetBody = target != null ? target.GetComponent<Collider>() : null;
        nextPathUpdate = 0;
    }

    private Vector3 TargetPoint => targetBody != null ? targetBody.bounds.center : Target.transform.position + Vector3.up;

    private void Update()
    {
        stateTime += Time.deltaTime;

        if (State == MushroomState.Dead)
        {
            if (stateTime >= 1.1f)
            {
                Destroy(gameObject);
            }
            return;
        }

        if (!agent.isOnNavMesh || Target == null)
        {
            return;
        }

        if (Target.IsDead)
        {
            agent.isStopped = true;
            PlayAnimation("Idle", 1);
            return;
        }

        switch (State)
        {
            case MushroomState.Pursuing:
                Pursue();
                break;
            case MushroomState.WindingUp:
                WindUp();
                break;
            case MushroomState.Charging:
                Charge();
                break;
            case MushroomState.Recovering:
            case MushroomState.Stunned:
                float duration = State == MushroomState.Stunned ? stunSeconds : recoverySeconds;
                if (State == MushroomState.Stunned)
                {
                    visualRoot.localRotation = originalRotation * Quaternion.Euler(0, 0, Mathf.Sin(stateTime * 12f) * 5f);
                }
                if (stateTime >= duration)
                {
                    visualRoot.localRotation = originalRotation;
                    ChangeState(MushroomState.Pursuing);
                    nextPathUpdate = 0;
                }
                break;
        }
    }

    private void Pursue()
    {
        agent.isStopped = false;
        agent.updateRotation = true;
        agent.speed = moveSpeed;
        PlayAnimation(agent.velocity.sqrMagnitude > 0.05f ? "Walk" : "Idle", 1);

        if (Time.time >= nextPathUpdate)
        {
            nextPathUpdate = Time.time + 0.2f;
            if (NavMesh.SamplePosition(Target.transform.position, out NavMeshHit floor, 1.6f, agent.areaMask))
            {
                agent.SetDestination(floor.position);
            }
            else
            {
                agent.ResetPath();
            }
        }

        Vector3 offset = TargetPoint - transform.position;
        if (Vector3.ProjectOnPlane(offset, Vector3.up).magnitude > chargeRange || Mathf.Abs(offset.y) > 2f)
        {
            return;
        }

        targetedBarricade = null;
        Vector3 origin = transform.position + Vector3.up;
        RaycastHit[] hits = Physics.RaycastAll(origin, TargetPoint - origin, Vector3.Distance(origin, TargetPoint), ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
            {
                continue;
            }
            if (hit.transform.IsChildOf(Target.transform))
            {
                BeginWarning();
                return;
            }

            MapBarricade barricade = hit.collider.GetComponentInParent<MapBarricade>();
            if (barricade != null && barricade.IsClosed)
            {
                targetedBarricade = barricade;
                BeginWarning();
            }
            return;
        }

        BeginWarning();
    }

    private void BeginWarning()
    {
        agent.isStopped = true;
        agent.updateRotation = false;
        ChangeState(MushroomState.WindingUp);
        PlayAnimation("Windup", 0.65f);
    }

    private void WindUp()
    {
        Vector3 point = targetedBarricade != null ? targetedBarricade.transform.position : TargetPoint;
        Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
        }

        // A visible crouch and forward cap tilt announce the rush.
        float progress = Mathf.Clamp01(stateTime / warningSeconds);
        visualRoot.localScale = Vector3.Scale(originalScale, new Vector3(1.08f, 1f - 0.18f * progress, 1.08f));
        visualRoot.localRotation = originalRotation * Quaternion.Euler(12f * progress, 0, 0);

        if (stateTime >= warningSeconds)
        {
            ChargeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(ChargeDirection);
            distanceCharged = 0;
            damageApplied = false;
            visualRoot.localScale = originalScale;
            agent.ResetPath();
            ChangeState(MushroomState.Charging);
            PlayAnimation("Run", 1.5f);
        }
    }

    private void Charge()
    {
        float distance = Mathf.Min(chargeSpeed * Time.deltaTime, chargeRange - distanceCharged);
        Vector3 bottom = transform.position + Vector3.up * (bodyCollider.radius + 0.05f);
        Vector3 top = transform.position + Vector3.up * (bodyCollider.height - bodyCollider.radius);
        RaycastHit[] hits = Physics.CapsuleCastAll(bottom, top, bodyCollider.radius, ChargeDirection,
            distance + 0.04f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
            {
                continue;
            }

            Health struck = hit.collider.GetComponentInParent<Health>();
            if (struck == Target)
            {
                DealPlayerDamage();
                Recover(false);
                return;
            }

            if (hit.normal.y > 0.5f)
            {
                continue;
            }

            MapBarricade barricade = hit.collider.GetComponentInParent<MapBarricade>();
            if (barricade != null && barricade.IsClosed && !damageApplied)
            {
                barricade.TakeDamage(2);
                damageApplied = true;
            }

            bool otherEnemy = struck != null;
            Recover(!otherEnemy);
            return;
        }

        // Stop at navigation edges and closed gates; a charge never bends around them.
        Vector3 destination = transform.position + ChargeDirection * distance;
        if (!NavMesh.SamplePosition(destination, out NavMeshHit floor, 0.8f, agent.areaMask)
            || NavMesh.Raycast(transform.position, floor.position, out NavMeshHit edge, agent.areaMask))
        {
            Recover(true);
            return;
        }

        Vector3 sidewaysCorrection = Vector3.ProjectOnPlane(floor.position - destination, Vector3.up);
        if (sidewaysCorrection.sqrMagnitude > 0.0225f)
        {
            Recover(true);
            return;
        }

        agent.Move(floor.position - transform.position);
        distanceCharged += distance;
        CheckCloseContact();

        if (distanceCharged >= chargeRange)
        {
            Recover(false);
        }
    }

    private void CheckCloseContact()
    {
        if (damageApplied || targetBody == null)
        {
            return;
        }

        Vector3 center = bodyCollider.bounds.center;
        Vector3 nearest = targetBody.ClosestPoint(center);
        Vector3 offset = TargetPoint - center;
        bool inFront = Vector3.Dot(ChargeDirection, Vector3.ProjectOnPlane(offset, Vector3.up).normalized) > 0.2f;
        if (Vector3.Distance(nearest, center) <= bodyCollider.radius + 0.1f && inFront)
        {
            DealPlayerDamage();
            Recover(false);
        }
    }

    private void DealPlayerDamage()
    {
        if (!damageApplied)
        {
            Target.TakeDamage(attackDamage);
            damageApplied = true;
        }
    }

    private void Recover(bool hitScenery)
    {
        agent.isStopped = true;
        visualRoot.localScale = originalScale;
        visualRoot.localRotation = originalRotation;
        ChangeState(hitScenery ? MushroomState.Stunned : MushroomState.Recovering);
        PlayAnimation(hitScenery ? "Stunned" : "Idle", 1);
    }

    private void ChangeState(MushroomState state)
    {
        State = state;
        stateTime = 0;
    }

    private void PlayAnimation(string stateName, float speed)
    {
        animator.speed = speed;
        if (currentAnimation != stateName)
        {
            currentAnimation = stateName;
            animator.CrossFadeInFixedTime(stateName, 0.08f, 0);
        }
    }

    private void OnDeath(Health source)
    {
        ChangeState(MushroomState.Dead);
        agent.enabled = false;
        bodyCollider.enabled = false;
        visualRoot.localScale = originalScale;
        visualRoot.localRotation = originalRotation;
        PlayAnimation("Death", 1);
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= OnDeath;
        }
    }
}
