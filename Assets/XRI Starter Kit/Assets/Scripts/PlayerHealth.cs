using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int health = 100;

    /// <summary>True once health has reached 0. Latches so death fires exactly once.</summary>
    public bool IsDead { get; private set; }

    /// <summary>Raised exactly once, the moment the trainee's health hits 0.
    /// Module4SessionController subscribes to end the mission as a FAIL immediately,
    /// instead of discovering death a frame later by polling.</summary>
    public event System.Action OnPlayerDied;

    public void TakeDamage(int dmg)
    {
        if (IsDead) return; // already down — ignore further damage
        health -= dmg;
        Debug.Log($"PLAYER HIT! HP = {health}");

        if (health <= 0)
        {
            health = 0;
            IsDead = true;
            Debug.Log("PLAYER DOWN");
            OnPlayerDied?.Invoke();
        }
    }
}
