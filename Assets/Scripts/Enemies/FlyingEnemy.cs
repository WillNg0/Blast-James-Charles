using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class FlyingEnemy : MonoBehaviour
{
    public enum EnemyKind
    {
        Bee,
        Dragonfly
    }
    public enum FlightState
    {
        Approaching,
        Warning,
        Attacking,
        Recovering,
        Dead
    }

    public EnemyKind kind;
    public Transform body;
    public Collider bodyCollider;
    public GameObject chargeIndicator;
    public Animator animator;
    public EnemyProjectile projectilePrefab;
    public Transform projectileContainer;
    public float hoverHeight = 2.6f;
    public float moveSpeed = 3.6f;
    public float warningSeconds = 0.7f;
    public float recoverySeconds = 1.5f;
    public int attackDamage = 10;

    public FlightState State { get; private set; }
    public Health Target { get; private set; }
    public Vector3 AimPoint => targetBody != null ? targetBody.bounds.center : Target.transform.position + Vector3.up;

    private NavMeshAgent agent;
    private Health health;
    private Collider targetBody;
    private Vector3 bodyPosition;
    private Vector3 bodyScale;
    private Vector3 attackStart;
    private Vector3 attackEnd;
    private Vector3 recoveryStart;
    private Quaternion attackRotation;
    private Quaternion stingRotation;
    private float stateTime;
    private float nextPathUpdate;
    private float bobPhase;
    private bool alternateSide;
    private string currentAnimation;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        health.Died += OnDeath;
        bodyPosition = body.position;
        bodyScale = body.localScale;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        bodyPosition = HoverPosition();
        body.position = bodyPosition;
        State = FlightState.Approaching;
        SetCharge(false);
        PlayAnimation("Fly");
    }

    public void SetTarget(Health target)
    {
        Target = target;
        targetBody = target != null ? target.GetComponent<Collider>() : null;
        nextPathUpdate = 0;
    }

    private void Update()
    {
        stateTime += Time.deltaTime;

        if (State == FlightState.Dead)
        {
            float remaining = 1f - Mathf.Clamp01(stateTime / 0.55f);
            body.localScale = bodyScale * remaining;
            body.position = bodyPosition - Vector3.up * stateTime;
            if (remaining <= 0)
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
            SetCharge(false);
            return;
        }

        switch (State)
        {
            case FlightState.Approaching:
                Approach();
                break;
            case FlightState.Warning:
                Warn();
                break;
            case FlightState.Attacking:
                Dive();
                break;
            case FlightState.Recovering:
                Recover();
                break;
        }

        body.position = bodyPosition;
    }

    private void Approach()
    {
        agent.isStopped = false;
        agent.speed = moveSpeed;
        agent.stoppingDistance = kind == EnemyKind.Bee ? 3.2f : 7f;
        PlayAnimation("Fly");

        if (Time.time >= nextPathUpdate)
        {
            nextPathUpdate = Time.time + 0.25f;
            if (NavMesh.SamplePosition(Target.transform.position, out NavMeshHit floor, 1.6f, agent.areaMask))
            {
                agent.SetDestination(floor.position);
            }
        }

        MoveBody(HoverPosition(), false);
        FaceTarget();
        float distance = Vector3.Distance(bodyPosition, AimPoint);
        float attackDistance = kind == EnemyKind.Bee ? 4.2f : 10f;

        if (distance <= attackDistance && ClearShot(bodyPosition, AimPoint))
        {
            agent.isStopped = true;
            attackStart = bodyPosition;
            ChangeState(FlightState.Warning);
            SetCharge(kind == EnemyKind.Dragonfly);
        }
    }

    private Vector3 HoverPosition()
    {
        float height = hoverHeight + Mathf.Sin(Time.time * 3f + bobPhase) * 0.12f;
        // Flight follows the ground route. A roof limits hover height rather than being crossed.
        Vector3 origin = transform.position + Vector3.up * 0.6f;
        RaycastHit[] roofs = Physics.SphereCastAll(origin, 0.48f, Vector3.up, height - 0.6f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(roofs, (first, second) => first.distance.CompareTo(second.distance));
        foreach (RaycastHit roof in roofs)
        {
            if (roof.transform.IsChildOf(transform) || roof.collider.GetComponentInParent<FlyingEnemy>() != null)
            {
                continue;
            }
            height = Mathf.Max(0.7f, roof.distance + 0.5f);
            break;
        }
        return transform.position + Vector3.up * height;
    }

    private void Warn()
    {
        FaceTarget();
        if (kind == EnemyKind.Bee)
        {
            Vector3 away = (attackStart - AimPoint).normalized;
            MoveBody(attackStart + away * (0.45f * Mathf.Clamp01(stateTime / warningSeconds)), false);
        }
        else
        {
            float pulse = 1f + 0.3f * Mathf.Sin(stateTime * 18f);
            chargeIndicator.transform.localScale = Vector3.one * 0.24f * pulse;
            MoveBody(attackStart, false);
        }

        if (stateTime < warningSeconds)
        {
            return;
        }
        SetCharge(false);

        if (!ClearShot(bodyPosition, AimPoint))
        {
            BeginRecovery();
            return;
        }

        if (kind == EnemyKind.Bee)
        {
            attackStart = bodyPosition;
            Vector3 direction = (AimPoint - attackStart).normalized;
            attackEnd = AimPoint + direction * 1.1f;
            attackRotation = body.rotation;
            // Turn the head away so the abdomen and stinger lead the dive.
            stingRotation = Quaternion.LookRotation(-direction, Vector3.up);
            ChangeState(FlightState.Attacking);
            PlayAnimation("Attack");
        }
        else
        {
            Vector3 direction = (AimPoint - bodyPosition).normalized;
            var shot = Instantiate(projectilePrefab, bodyPosition + direction * 0.7f, Quaternion.LookRotation(direction), projectileContainer);
            shot.Launch(Target, transform, direction);
            BeginRecovery();
        }
    }

    private void Dive()
    {
        const float turnSeconds = 0.14f;
        float turnProgress = Mathf.SmoothStep(0, 1, Mathf.Clamp01(stateTime / turnSeconds));
        body.rotation = Quaternion.Slerp(attackRotation, stingRotation, turnProgress);
        if (stateTime < turnSeconds)
        {
            return;
        }

        float diveTime = stateTime - turnSeconds;
        Vector3 desired = Vector3.Lerp(attackStart, attackEnd, Mathf.Clamp01(diveTime / 0.6f));
        if (!MoveBody(desired, true) || diveTime >= 0.6f)
        {
            BeginRecovery();
        }
    }

    private void BeginRecovery()
    {
        recoveryStart = bodyPosition;
        ChangeState(FlightState.Recovering);
        PlayAnimation("Fly");
        if (kind == EnemyKind.Dragonfly)
        {
            Reposition();
        }
    }

    private void Reposition()
    {
        alternateSide = !alternateSide;
        Vector3 away = Vector3.ProjectOnPlane(transform.position - Target.transform.position, Vector3.up).normalized;
        if (away.sqrMagnitude < 0.01f)
        {
            away = Vector3.forward;
        }
        Vector3 side = Vector3.Cross(Vector3.up, away) * (alternateSide ? 1f : -1f);
        Vector3 candidate = Target.transform.position + away * 6f + side * 5f;
        candidate.y = transform.position.y;
        var path = new NavMeshPath();

        if (NavMesh.SamplePosition(candidate, out NavMeshHit floor, 2f, agent.areaMask)
            && agent.CalculatePath(floor.position, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            agent.stoppingDistance = 0.4f;
            agent.isStopped = false;
            agent.SetPath(path);
        }
    }

    private void Recover()
    {
        float progress = Mathf.Clamp01(stateTime / 0.65f);
        MoveBody(Vector3.Lerp(recoveryStart, HoverPosition(), progress), false);
        FaceTarget();
        if (stateTime >= recoverySeconds)
        {
            ChangeState(FlightState.Approaching);
            nextPathUpdate = 0;
        }
    }

    private bool MoveBody(Vector3 desired, bool canSting)
    {
        Vector3 difference = desired - bodyPosition;
        float distance = difference.magnitude;
        if (distance < 0.001f)
        {
            return true;
        }
        // The hidden ground route prevents flyers from bypassing locked areas or the map boundary.
        Vector3 ground = new Vector3(desired.x, transform.position.y, desired.z);
        if (!NavMesh.SamplePosition(ground, out NavMeshHit floor, 1.2f, agent.areaMask)
            || NavMesh.Raycast(transform.position, floor.position, out NavMeshHit edge, agent.areaMask))
        {
            return false;
        }

        RaycastHit[] hits = Physics.SphereCastAll(bodyPosition, 0.45f, difference.normalized, distance,
            ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
            {
                continue;
            }
            if (hit.collider.GetComponentInParent<FlyingEnemy>() != null)
            {
                continue;
            }
            if (canSting && hit.collider.GetComponentInParent<Health>() == Target)
            {
                Target.TakeDamage(attackDamage);
            }
            bodyPosition += difference.normalized * Mathf.Max(0, hit.distance - 0.03f);
            return false;
        }

        bodyPosition = desired;
        return true;
    }

    private bool ClearShot(Vector3 origin, Vector3 destination)
    {
        Vector3 direction = destination - origin;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
            ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(Target.transform))
            {
                continue;
            }
            if (hit.collider.GetComponentInParent<FlyingEnemy>() != null)
            {
                continue;
            }
            return false;
        }
        return true;
    }

    private void FaceTarget()
    {
        Vector3 direction = Vector3.ProjectOnPlane(AimPoint - bodyPosition, Vector3.up);
        if (direction.sqrMagnitude > 0.001f)
        {
            body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);
        }
    }

    private void ChangeState(FlightState next)
    {
        State = next;
        stateTime = 0;
    }

    private void SetCharge(bool active)
    {
        if (chargeIndicator != null)
        {
            chargeIndicator.SetActive(active);
        }
    }

    private void PlayAnimation(string stateName)
    {
        if (animator == null || currentAnimation == stateName)
        {
            return;
        }
        currentAnimation = stateName;
        animator.CrossFadeInFixedTime(stateName, 0.08f, 0);
    }

    private void OnDeath(Health source)
    {
        ChangeState(FlightState.Dead);
        agent.enabled = false;
        bodyCollider.enabled = false;
        SetCharge(false);
        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= OnDeath;
        }
    }
}
