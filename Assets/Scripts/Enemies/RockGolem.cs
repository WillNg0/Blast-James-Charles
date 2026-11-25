using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class RockGolem : MonoBehaviour
{
    public enum GolemState
    {
        Pursuing,
        WindingUp,
        Recovering,
        Dead
    }

    public Animator animator;
    public Collider bodyCollider;
    public LineRenderer slamMarker;
    public float moveSpeed = 1.7f;
    public float attackRange = 3.5f;
    public float slamRadius = 2.8f;
    public float warningSeconds = 1.25f;
    public float animationImpactSeconds = 1.7f;
    public float recoverySeconds = 1.8f;
    public float deathSeconds = 2.5f;
    public int attackDamage = 30;

    public GolemState State { get; private set; }
    public Health Target { get; private set; }
    public Vector3 SlamPoint { get; private set; }

    private NavMeshAgent agent;
    private Health health;
    private Collider targetBody;
    private MapBarricade attackedBarricade;
    private float stateTime;
    private float nextPathUpdate;
    private string currentAnimation;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        health.Died += OnDeath;
        slamMarker.enabled = false;
        State = GolemState.Pursuing;
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

        if (State == GolemState.Dead)
        {
            if (stateTime >= deathSeconds)
            {
                Destroy(gameObject);
            }
            return;
        }

        if (!agent.isOnNavMesh)
        {
            return;
        }

        if (Target == null || Target.IsDead)
        {
            agent.isStopped = true;
            slamMarker.enabled = false;
            ChangeState(GolemState.Pursuing);
            PlayAnimation("Idle", 1);
            return;
        }

        switch (State)
        {
            case GolemState.Pursuing:
                Pursue();
                break;
            case GolemState.WindingUp:
                UpdateWarning();
                break;
            case GolemState.Recovering:
                UpdateRecovery();
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
            nextPathUpdate = Time.time + 0.25f;
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
        if (Vector3.ProjectOnPlane(offset, Vector3.up).magnitude > attackRange || Mathf.Abs(offset.y) > 2f)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up;
        RaycastHit? obstruction = FirstObstruction(origin, TargetPoint);
        attackedBarricade = null;
        if (obstruction.HasValue && !obstruction.Value.transform.IsChildOf(Target.transform))
        {
            attackedBarricade = obstruction.Value.collider.GetComponentInParent<MapBarricade>();
            if (attackedBarricade == null || !attackedBarricade.IsClosed)
            {
                return;
            }
        }

        BeginWarning();
    }

    private void BeginWarning()
    {
        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false;

        Vector3 aim = attackedBarricade != null ? attackedBarricade.transform.position : TargetPoint;
        Vector3 direction = Vector3.ProjectOnPlane(aim - transform.position, Vector3.up);
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // Commit before the wind-up so the player can escape the marked ground.
        SlamPoint = transform.position + transform.forward * 1.6f;
        if (NavMesh.SamplePosition(SlamPoint, out NavMeshHit floor, 1f, agent.areaMask))
        {
            SlamPoint = floor.position;
        }

        ChangeState(GolemState.WindingUp);
        PlayAnimation("Slam", animationImpactSeconds / Mathf.Max(0.1f, warningSeconds));
        DrawMarker(slamRadius, new Color(1f, 0.65f, 0.1f));
    }

    private void UpdateWarning()
    {
        float progress = Mathf.Clamp01(stateTime / warningSeconds);
        Color color = Color.Lerp(new Color(1f, 0.65f, 0.1f), new Color(1f, 0.15f, 0.05f), progress);
        slamMarker.startColor = slamMarker.endColor = color;
        slamMarker.widthMultiplier = 0.06f + progress * 0.07f;

        if (stateTime >= warningSeconds)
        {
            ApplySlamDamage();
            ChangeState(GolemState.Recovering);
            DrawMarker(slamRadius, new Color(1f, 0.9f, 0.5f));
        }
    }

    private void ApplySlamDamage()
    {
        Vector3 point = TargetPoint;
        Vector3 nearest = targetBody != null ? targetBody.ClosestPoint(SlamPoint + Vector3.up * 0.6f) : point;
        bool inRadius = Vector3.ProjectOnPlane(nearest - SlamPoint, Vector3.up).magnitude <= slamRadius;
        bool nearGround = targetBody != null
            ? targetBody.bounds.min.y <= SlamPoint.y + 1.1f && targetBody.bounds.max.y >= SlamPoint.y - 0.3f
            : Mathf.Abs(point.y - SlamPoint.y) <= 1.1f;

        // Check cover before breaking a barricade, so one slam cannot hit through it.
        RaycastHit? cover = FirstObstruction(transform.position + Vector3.up, point);
        if (inRadius && nearGround && (!cover.HasValue || cover.Value.transform.IsChildOf(Target.transform)))
        {
            Target.TakeDamage(attackDamage);
        }

        if (attackedBarricade != null && attackedBarricade.IsClosed)
        {
            Vector3 barrierPoint = attackedBarricade.barrier.GetComponentInChildren<Collider>().bounds.center;
            RaycastHit? obstacle = FirstObstruction(transform.position + Vector3.up, barrierPoint);
            bool inReach = Vector3.ProjectOnPlane(barrierPoint - SlamPoint, Vector3.up).magnitude <= slamRadius;
            if (inReach && obstacle.HasValue && obstacle.Value.collider.GetComponentInParent<MapBarricade>() == attackedBarricade)
            {
                attackedBarricade.TakeDamage(3);
            }
        }
    }

    private RaycastHit? FirstObstruction(Vector3 origin, Vector3 destination)
    {
        Vector3 offset = destination - origin;
        RaycastHit[] hits = Physics.RaycastAll(origin, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        foreach (RaycastHit hit in hits)
        {
            if (!hit.transform.IsChildOf(transform))
            {
                return hit;
            }
        }
        return null;
    }

    private void UpdateRecovery()
    {
        if (stateTime < 0.3f)
        {
            float progress = stateTime / 0.3f;
            DrawMarker(slamRadius * (1f + progress * 0.12f), new Color(1f, 0.9f, 0.5f, 1f - progress));
        }
        else
        {
            slamMarker.enabled = false;
            if (stateTime >= 0.8f)
            {
                PlayAnimation("Idle", 1);
            }
        }

        if (stateTime >= recoverySeconds)
        {
            ChangeState(GolemState.Pursuing);
            nextPathUpdate = 0;
        }
    }

    private void DrawMarker(float radius, Color color)
    {
        const int segments = 64;
        slamMarker.enabled = true;
        slamMarker.useWorldSpace = true;
        slamMarker.loop = true;
        slamMarker.positionCount = segments;
        slamMarker.startColor = slamMarker.endColor = color;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            slamMarker.SetPosition(i, SlamPoint + new Vector3(Mathf.Cos(angle) * radius, 0.08f, Mathf.Sin(angle) * radius));
        }
    }

    private void ChangeState(GolemState state)
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
            animator.CrossFadeInFixedTime(stateName, 0.1f, 0);
        }
    }

    private void OnDeath(Health source)
    {
        ChangeState(GolemState.Dead);
        agent.enabled = false;
        bodyCollider.enabled = false;
        slamMarker.enabled = false;
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
