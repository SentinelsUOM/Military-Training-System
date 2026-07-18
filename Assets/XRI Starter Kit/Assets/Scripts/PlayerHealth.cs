using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int health = 100;

    [Tooltip("Starting/maximum health. Drives the damage vignette: the closer health gets to 0, " +
             "the redder the trainee's peripheral vision. 10 rounds at 10 damage = dead.")]
    public int maxHealth = 100;

    /// <summary>True once health has reached 0. Latches so death fires exactly once.</summary>
    public bool IsDead { get; private set; }

    /// <summary>Remaining health as 0–1. Used to drive damage feedback intensity.</summary>
    public float HealthFraction => Mathf.Clamp01(health / (float)Mathf.Max(1, maxHealth));

    /// <summary>Raised on every round that lands, carrying the world position the shot was fired
    /// FROM — null when the source is unknown. PlayerDamageFeedback uses it to point a directional
    /// indicator at the shooter, so the trainee learns to turn and take cover rather than being
    /// killed by an enemy they never located.</summary>
    public event System.Action<Vector3?> OnDamaged;

    /// <summary>Raised exactly once, the moment the trainee's health hits 0.</summary>
    public event System.Action OnPlayerDied;

    /// <summary>Damage from an unknown direction (no directional indicator will be shown).</summary>
    public void TakeDamage(int dmg) => ApplyDamage(dmg, null);

    /// <summary>Damage from a known shooter. <paramref name="sourcePosition"/> is where the round
    /// came FROM, not where it hit.</summary>
    public void TakeDamage(int dmg, Vector3 sourcePosition) => ApplyDamage(dmg, sourcePosition);

    private void ApplyDamage(int dmg, Vector3? sourcePosition)
    {
        if (IsDead) return; // already down — ignore further damage

        health -= dmg;
        Debug.Log($"PLAYER HIT! HP = {health}");

        // Fire BEFORE the death check so the final, killing round still registers as a hit
        // (haptics + vignette flash), rather than the trainee simply blacking out with no
        // indication of what happened.
        OnDamaged?.Invoke(sourcePosition);

        if (health <= 0)
        {
            health = 0;
            IsDead = true;
            Debug.Log("PLAYER DOWN");
            OnPlayerDied?.Invoke();
        }
    }
}
