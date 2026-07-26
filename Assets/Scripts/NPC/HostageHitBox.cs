using MikeNspired.XRIStarterKit;
using UnityEngine;

/// <summary>
/// Place on the hostage root (or a child collider) so player bullets register
/// damage on the hostage — i.e. friendly fire. SimpleCollisionDamage on the
/// bullet calls IDamageable.TakeDamage(), which routes to HostageController.TakeHit().
///
/// Shooting a hostage is a training failure: below the injured threshold the
/// hostage limps, and at 0 HP it dies.
///
/// Setup:
///   1. Add to the hostage NPC root GameObject (SceneBuilder does this automatically).
///   2. Ensure the hostage has at least one Collider on it or a child.
/// </summary>
[RequireComponent(typeof(HostageController))]
public class HostageHitBox : MonoBehaviour, IDamageable, IImpactType
{
    [Tooltip("Multiplier applied to incoming bullet damage before passing to HostageController.")]
    public float damageMultiplier = 1f;

    HostageController _controller;

    void Awake()
    {
        _controller = GetComponent<HostageController>();
    }

    // ── IDamageable ───────────────────────────────────────────────────────────

    public void TakeDamage(float damage, GameObject damager)
    {
        if (_controller == null) return;

        // Who fired? A terrorist's crossfire round now also reaches this hitbox (NpcShooterRaycast
        // routes through IDamageable). That is the CAPTORS wounding the hostage, NOT the trainee's
        // friendly fire — so it must not be tagged/penalised as friendly fire, and the AAR must not
        // blame the trainee for it.
        bool byTerrorist = damager != null && damager.GetComponentInParent<TerroristController>() != null;
        _controller.TakeHit(damage * damageMultiplier, null, byTrainee: !byTerrorist);

        // Only the trainee's own round is a "friendly_fire" incident. Report it so the AAR can
        // PENALISE it (the safety score's friendly-fire penalty keys off this event).
        if (!byTerrorist)
            EventManager.Instance?.Raise(new ScenarioEvent(
                ScenarioEventType.HostageHit,
                transform.position,
                damager,
                roomId: null,
                targetActorId: _controller.NPCId
            ));
    }

    // ── IImpactType — flesh decal ─────────────────────────────────────────────

    public ImpactType GetImpactType() => ImpactType.Flesh;
    public bool ShouldReparent => false;
}
