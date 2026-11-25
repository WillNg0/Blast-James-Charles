using System;
using UnityEngine;

// Shared damage receiver for the player and enemies.
public class Health : MonoBehaviour
{
    public int maxHealth = 100;
    public int CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;
    public event Action<Health> Damaged;
    public event Action<Health> Died;

    private void Awake()
    {
        ResetHealth();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        Damaged?.Invoke(this);

        if (IsDead)
        {
            Died?.Invoke(this);
        }
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
    }
}
