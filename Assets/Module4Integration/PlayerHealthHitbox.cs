// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Lives in Assembly-CSharp so it can implement IDamageable from the XRI Starter Kit.

using MikeNspired.XRIStarterKit;
using UnityEngine;

/// <summary>
/// Bridges incoming bullet damage (via the XRI Starter Kit's IDamageable
/// interface) to the existing PlayerHealth component.
///
/// Why this exists
///   - PlayerHealth has TakeDamage(int) but does not implement IDamageable.
///   - SimpleCollisionDamage on bullet prefabs only calls IDamageable.TakeDamage.
///   - Without this adapter, terrorist bullets pass through the player and
///     PlayerHealth.health never drops, so the "player_down" end condition
///     can never fire.
///
/// Setup
///   - Place this on the PlayerHitbox GameObject (the same one that has
///     PlayerHealth and the player's collider). Drag PlayerHealth into the
///     Inspector slot, or leave blank to auto-look-up on the same GameObject.
/// </summary>
public class PlayerHealthHitbox : MonoBehaviour, IDamageable, IImpactType
{
    [Tooltip("Optional explicit PlayerHealth reference. If blank, found on this GameObject or parent.")]
    [SerializeField] private PlayerHealth playerHealth;

    [Tooltip("Multiplier on incoming damage. 1.0 = full, 2.0 = double, 0.5 = armor.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Print every hit to the Console for debugging.")]
    [SerializeField] private bool echoToConsole = true;

    [Header("Follow the trainee's head")]
    [Tooltip("Keep this damageable body collider underneath the trainee's head every frame. " +
             "The hitbox is parented to the XR Origin, but the head/camera moves independently of " +
             "it — so the moment the trainee walks anywhere, the collider is left behind (metres " +
             "away, often inside a wall) while the camera moves on. Bullets aimed at the trainee " +
             "then pass through empty space and never register: the trainee is effectively immortal " +
             "as soon as they move. Following the head keeps the body where the player actually is.")]
    [SerializeField] private bool followHead = true;

    private Transform _head;
    private float     _floorY;   // captured standing/floor height — kept constant while following in XZ

    private void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            Debug.LogError($"[PlayerHealthHitbox] {name}: no PlayerHealth found. Drag one into the Inspector.");

        _floorY = transform.position.y;
        if (Camera.main != null) _head = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (!followHead) return;
        if (_head == null)
        {
            if (Camera.main == null) return;
            _head = Camera.main.transform;
        }
        // Track the head in the horizontal plane; hold the original floor height so the standing
        // capsule stays grounded (single-storey layouts). Set directly — this collider is not a
        // rigidbody, so a per-frame move is a clean teleport that raycast bullets resolve fine.
        var h = _head.position;
        transform.position = new Vector3(h.x, _floorY, h.z);
    }

    public void TakeDamage(float damage, GameObject damager)
    {
        if (playerHealth == null) return;

        int dmg = Mathf.Max(0, Mathf.RoundToInt(damage * damageMultiplier));

        // Forward WHERE the hit came from so the damage feedback can point the trainee at the
        // shooter. Fall back to the direction-less overload when the damager is unknown, so no
        // misleading indicator is drawn.
        if (damager != null) playerHealth.TakeDamage(dmg, damager.transform.position);
        else                 playerHealth.TakeDamage(dmg);

        if (echoToConsole)
            Debug.Log($"[PlayerHealthHitbox] Player took {dmg} damage from {(damager != null ? damager.name : "unknown")} → HP={playerHealth.health}");
    }

    public ImpactType GetImpactType() => ImpactType.Flesh;
    public bool ShouldReparent => false;
}
