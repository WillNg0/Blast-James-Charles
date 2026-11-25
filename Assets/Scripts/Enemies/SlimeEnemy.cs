using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class SlimeEnemy : MonoBehaviour
{
    public enum SlimeState
    {
        Chasing,
        WindingUp,
        Lunging,
        Recovering,
        Dead
    }

    public Transform visualRoot;
    public GameObject[] appearances;
    public float moveSpeed = 2.8f;
    public float attackRange = 1.65f;
    public float damageRange = 1.4f;
    public float warningSeconds = 0.65f;
    public float lungeSeconds = 0.25f;
    public float recoverySeconds = 1.1f;
    public int attackDamage = 10;

    public SlimeState State { get; private set; }
    public int AppearanceIndex { get; private set; }
    public Health Target { get; private set; }

    private NavMeshAgent agent;
    private Health health;
    private Animator animator;
    private Vector3 visualPosition;
    private Vector3 visualScale;
    private Vector3 lungeDirection;
    private float stateTime;
    private float nextPathUpdate;
    private float hopTime;
    private bool damageApplied;
    private MapBarricade attackedBarricade;
    private string currentAnimation;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        health.Died += OnDeath;
        visualPosition = visualRoot.localPosition;
        visualScale = visualRoot.localScale;
        AppearanceIndex = Random.Range(0, appearances.Length);

        for (int i = 0; i < appearances.Length; i++)
        {
            appearances[i].SetActive(i == AppearanceIndex);
        }

        animator = appearances[AppearanceIndex].GetComponent<Animator>();
        animator.applyRootMotion = false;
        hopTime = Random.Range(0f, Mathf.PI * 2f);
        State = SlimeState.Chasing;
    }

    public void SetTarget(Health target)
    {
        Target = target;
        nextPathUpdate = 0;
    }

    private void Update()
    {
        if (State == SlimeState.Dead)
        {
            UpdateDeath();
            return;
        }

        if (!agent.isOnNavMesh)
        {
            return;
        }

        if (Target == null || Target.IsDead)
        {
            agent.isStopped = true;
            PlayAnimation("Idle");
            return;
        }

        stateTime += Time.deltaTime;

        switch (State)
        {
            case SlimeState.Chasing:
                ChaseTarget();
                break;
            case SlimeState.WindingUp:
                if (stateTime >= warningSeconds)
                {
                    BeginLunge();
                }
                break;
            case SlimeState.Lunging:
                UpdateLunge();
                break;
            case SlimeState.Recovering:
                if (stateTime >= recoverySeconds)
                {
                    ChangeState(SlimeState.Chasing);
                }
                break;
        }

        UpdateAppearance();
    }

    private void ChaseTarget()
    {
        agent.isStopped = false;
        agent.speed = moveSpeed;

        if (Time.time >= nextPathUpdate)
        {
            nextPathUpdate = Time.time + 0.2f;

            if (NavMesh.SamplePosition(Target.transform.position, out NavMeshHit hit, 1.5f, agent.areaMask))
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                // A player on an inaccessible platform cannot pull a slime off the ground.
                agent.ResetPath();
            }
        }

        Vector3 toTarget = Target.transform.position - transform.position;
        bool inReach = FlatDistance(toTarget) <= attackRange && Mathf.Abs(toTarget.y) < 1.8f;

        if (inReach && HasLineOfSight(Target.transform.position + Vector3.up * 0.2f, Target.transform))
        {
            attackedBarricade = null;
            BeginWarning();
            return;
        }

        MapBarricade barricade = FindBlockingBarricade(toTarget);

        if (barricade != null)
        {
            attackedBarricade = barricade;
            BeginWarning();
        }
    }

    private MapBarricade FindBlockingBarricade(Vector3 toTarget)
    {
        Vector3 direction = Vector3.ProjectOnPlane(toTarget, Vector3.up).normalized;
        Vector3 origin = transform.position + Vector3.up * 0.55f;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, 1.8f, ~0, QueryTriggerInteraction.Ignore))
        {
            MapBarricade barricade = hit.collider.GetComponentInParent<MapBarricade>();

            if (barricade != null && barricade.IsClosed)
            {
                return barricade;
            }
        }

        return null;
    }

    private void BeginWarning()
    {
        agent.isStopped = true;
        ChangeState(SlimeState.WindingUp);
        PlayAnimation("Idle");
    }

    private void BeginLunge()
    {
        Vector3 targetPosition = attackedBarricade != null
            ? attackedBarricade.transform.position
            : Target.transform.position;
        lungeDirection = Vector3.ProjectOnPlane(targetPosition - transform.position, Vector3.up).normalized;
        damageApplied = false;
        ChangeState(SlimeState.Lunging);
        PlayAnimation("Attack");
    }

    private void UpdateLunge()
    {
        float step = 5f * Time.deltaTime;
        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // The lunge commits to a direction and stops at physical walls or the player.
        bool blocked = Physics.SphereCast(origin, 0.45f, lungeDirection, out RaycastHit obstacle,
            step + 0.06f, ~0, QueryTriggerInteraction.Ignore);

        if (!blocked && attackedBarricade == null)
        {
            Vector3 destination = transform.position + lungeDirection * step;

            if (!NavMesh.Raycast(transform.position, destination, out NavMeshHit edge, agent.areaMask))
            {
                agent.Move(lungeDirection * step);
            }
        }

        ApplyAttackDamage();

        if (stateTime >= lungeSeconds)
        {
            ChangeState(SlimeState.Recovering);
            PlayAnimation("Idle");
        }
    }

    private void ApplyAttackDamage()
    {
        if (damageApplied)
        {
            return;
        }

        if (attackedBarricade != null)
        {
            if (attackedBarricade.IsClosed && FindBlockingBarricade(lungeDirection) == attackedBarricade)
            {
                attackedBarricade.TakeDamage(1);
                damageApplied = true;
            }

            return;
        }

        Vector3 toTarget = Target.transform.position - transform.position;
        bool inReach = FlatDistance(toTarget) <= damageRange && Mathf.Abs(toTarget.y) < 1.8f;
        bool inFront = Vector3.Dot(lungeDirection, Vector3.ProjectOnPlane(toTarget, Vector3.up).normalized) > 0.25f;

        if (inReach && inFront && HasLineOfSight(Target.transform.position + Vector3.up * 0.2f, Target.transform))
        {
            Target.TakeDamage(attackDamage);
            damageApplied = true;
        }
    }

    private bool HasLineOfSight(Vector3 destination, Transform expectedTarget)
    {
        Vector3 origin = transform.position + Vector3.up * 0.55f;
        Vector3 direction = destination - origin;

        if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return hit.transform == expectedTarget || hit.transform.IsChildOf(expectedTarget);
    }

    private static float FlatDistance(Vector3 offset)
    {
        return new Vector2(offset.x, offset.z).magnitude;
    }

    private void ChangeState(SlimeState nextState)
    {
        State = nextState;
        stateTime = 0;
    }

    private void UpdateAppearance()
    {
        Vector3 offset = Vector3.zero;
        Vector3 scale = visualScale;

        if (State == SlimeState.Chasing && agent.velocity.sqrMagnitude > 0.05f)
        {
            hopTime += Time.deltaTime * 9f;
            offset.y = Mathf.Abs(Mathf.Sin(hopTime)) * 0.18f;
            PlayAnimation("Move");
        }
        else if (State == SlimeState.WindingUp)
        {
            float progress = Mathf.Clamp01(stateTime / warningSeconds);
            scale = Vector3.Scale(visualScale, new Vector3(1f + progress * 0.18f, 1f - progress * 0.3f, 1f + progress * 0.18f));
            Vector3 facing = attackedBarricade != null ? attackedBarricade.transform.position : Target.transform.position;
            Vector3 direction = Vector3.ProjectOnPlane(facing - transform.position, Vector3.up);

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10f);
            }
        }
        else if (State == SlimeState.Chasing)
        {
            PlayAnimation("Idle");
        }

        visualRoot.localPosition = visualPosition + offset;
        visualRoot.localScale = scale;
    }

    private void PlayAnimation(string stateName)
    {
        if (currentAnimation == stateName)
        {
            return;
        }

        currentAnimation = stateName;
        animator.CrossFadeInFixedTime(stateName, 0.08f, 0);
    }

    private void OnDeath(Health source)
    {
        ChangeState(SlimeState.Dead);
        agent.enabled = false;
        GetComponent<Collider>().enabled = false;
        animator.enabled = false;
    }

    private void UpdateDeath()
    {
        stateTime += Time.deltaTime;
        float remaining = 1f - Mathf.Clamp01(stateTime / 0.45f);
        visualRoot.localScale = Vector3.Scale(visualScale, new Vector3(remaining, remaining * remaining, remaining));

        if (remaining <= 0)
        {
            Destroy(gameObject);
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
