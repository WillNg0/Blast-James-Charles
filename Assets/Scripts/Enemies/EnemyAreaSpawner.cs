using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAreaSpawner : MonoBehaviour
{
    [Serializable]
    public class EnemyType
    {
        public GameObject prefab;
        [Min(1f)] public float spawnInterval = 12f;
        [Min(1)] public int maximumAlive = 2;
    }

    public Health player;
    public MapGate requiredGate;
    public Transform[] spawnPoints;
    public Transform enemyContainer;
    public EnemyType[] enemyTypes;
    [Min(0f)] public float initialDelay = 3f;
    [Min(1f)] public float minimumPlayerDistance = 6f;
    [Min(0f)] public float spawnSpread = 1.5f;

    public int AliveCount => livingEnemies.Count;

    private class SpawnedEnemy
    {
        public Health health;
        public int typeIndex;
    }

    private readonly List<SpawnedEnemy> livingEnemies = new List<SpawnedEnemy>();
    private float[] nextSpawnTimes;
    private bool areaWasOpen;

    private void OnEnable()
    {
        areaWasOpen = false;
    }

    private void Start()
    {
        if (player == null || enemyContainer == null || spawnPoints == null || spawnPoints.Length == 0
            || enemyTypes == null || enemyTypes.Length == 0)
        {
            Debug.LogError("Assign a player, enemy container, spawn points, and enemy types to " + name + ".", this);
            enabled = false;
        }
    }

    private void Update()
    {
        RemoveMissingEnemies();

        if (player == null || player.IsDead || (requiredGate != null && !requiredGate.IsUnlocked))
        {
            areaWasOpen = false;
            return;
        }

        if (!areaWasOpen)
        {
            nextSpawnTimes = new float[enemyTypes.Length];
            for (int i = 0; i < enemyTypes.Length; i++)
            {
                nextSpawnTimes[i] = Time.time + initialDelay + i * 2f;
            }
            areaWasOpen = true;
        }

        for (int i = 0; i < enemyTypes.Length; i++)
        {
            if (Time.time < nextSpawnTimes[i])
            {
                continue;
            }

            EnemyType type = enemyTypes[i];
            nextSpawnTimes[i] = Time.time + Mathf.Max(1f, type.spawnInterval);
            if (CountEnemies(i) >= type.maximumAlive || type.prefab == null)
            {
                continue;
            }

            if (!TrySpawn(type.prefab, i))
            {
                // A player or another enemy occupying an entrance should only delay spawning.
                nextSpawnTimes[i] = Time.time + 2f;
            }
        }
    }

    private int CountEnemies(int typeIndex)
    {
        int count = 0;
        foreach (SpawnedEnemy enemy in livingEnemies)
        {
            if (enemy.typeIndex == typeIndex)
            {
                count++;
            }
        }
        return count;
    }

    private bool TrySpawn(GameObject prefab, int typeIndex)
    {
        NavMeshAgent settings = prefab.GetComponent<NavMeshAgent>();
        if (settings == null || prefab.GetComponent<Health>() == null)
        {
            return false;
        }

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            if (point == null)
            {
                continue;
            }

            Vector2 spread = UnityEngine.Random.insideUnitCircle * spawnSpread;
            Vector3 candidate = point.position + new Vector3(spread.x, 0, spread.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit floor, 1f, settings.areaMask)
                || Vector3.Distance(candidate, floor.position) > 1f
                || Vector3.Distance(floor.position, player.transform.position) < minimumPlayerDistance)
            {
                continue;
            }

            // Check the physical spawn volume as well as the navigation mesh.
            float radius = Mathf.Max(0.2f, settings.radius);
            Vector3 bottom = floor.position + Vector3.up * (radius + 0.1f);
            Vector3 top = floor.position + Vector3.up * Mathf.Max(radius + 0.1f, settings.height - radius);
            if (Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            Vector3 facing = Vector3.ProjectOnPlane(player.transform.position - floor.position, Vector3.up);
            Quaternion rotation = facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing) : point.rotation;
            GameObject instance = Instantiate(prefab, floor.position, rotation, enemyContainer);
            instance.name = prefab.name;
            Health health = instance.GetComponent<Health>();
            if (!AssignTarget(instance))
            {
                Destroy(instance);
                return false;
            }

            if (instance.TryGetComponent(out FlyingEnemy flyer))
            {
                flyer.projectileContainer = enemyContainer;
            }

            livingEnemies.Add(new SpawnedEnemy { health = health, typeIndex = typeIndex });
            health.Died += OnEnemyDied;
            return true;
        }

        return false;
    }

    private bool AssignTarget(GameObject enemy)
    {
        if (enemy.TryGetComponent(out SlimeEnemy slime))
        {
            slime.SetTarget(player);
        }
        else if (enemy.TryGetComponent(out FlyingEnemy flyer))
        {
            flyer.SetTarget(player);
        }
        else if (enemy.TryGetComponent(out ChargingMushroom mushroom))
        {
            mushroom.SetTarget(player);
        }
        else if (enemy.TryGetComponent(out RockGolem golem))
        {
            golem.SetTarget(player);
        }
        else
        {
            return false;
        }

        return true;
    }

    private void OnEnemyDied(Health source)
    {
        source.Died -= OnEnemyDied;
        livingEnemies.RemoveAll(enemy => enemy.health == source);
    }

    private void RemoveMissingEnemies()
    {
        for (int i = livingEnemies.Count - 1; i >= 0; i--)
        {
            Health health = livingEnemies[i].health;
            if (health == null || health.IsDead)
            {
                if (health != null)
                {
                    health.Died -= OnEnemyDied;
                }
                livingEnemies.RemoveAt(i);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (SpawnedEnemy enemy in livingEnemies)
        {
            if (enemy.health != null)
            {
                enemy.health.Died -= OnEnemyDied;
            }
        }
    }
}
