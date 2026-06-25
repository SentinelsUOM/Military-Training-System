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
        _controller.TakeHit(damage * damageMultiplier);
    }

    // ── IImpactType — flesh decal ─────────────────────────────────────────────

    public ImpactType GetImpactType() => ImpactType.Flesh;
    public bool ShouldReparent => false;
}
