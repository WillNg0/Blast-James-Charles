using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float speed = 8f;
    public float radius = 0.18f;
    public float lifetime = 4f;
    public int damage = 10;
    public Vector3 Direction { get; private set; }

    private Health target;
    private Transform owner;
    private float age;
    private bool launched;

    public void Launch(Health player, Transform source, Vector3 direction)
    {
        target = player;
        owner = source;
        Direction = direction.normalized;
        launched = true;
    }

    private void Update()
    {
        if (!launched)
        {
            return;
        }

        float distance = speed * Time.deltaTime;
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, Direction, distance,
            ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

        foreach (RaycastHit hit in hits)
        {
            if (owner != null && hit.transform.IsChildOf(owner))
            {
                continue;
            }

            // Enemy projectiles do not damage or collide with other enemies.
            if (hit.collider.GetComponentInParent<FlyingEnemy>() != null || hit.collider.GetComponentInParent<SlimeEnemy>() != null)
            {
                continue;
            }

            Health health = hit.collider.GetComponentInParent<Health>();

            if (health != null && health == target)
            {
                health.TakeDamage(damage);
            }

            Destroy(gameObject);
            return;
        }

        transform.position += Direction * distance;
        age += Time.deltaTime;

        if (age >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
