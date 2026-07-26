using MikeNspired.XRIStarterKit;
using UnityEngine;

/// <summary>
/// Place on the NPC root (or any child collider) so player bullets register damage.
/// SimpleCollisionDamage on the bullet calls IDamageable.TakeDamage() on collision,
/// which routes here and forwards to TerroristController.TakeHit().
///
/// Setup:
///   1. Add this component to the terrorist NPC root GameObject.
///   2. Make sure the NPC has at least one Collider on it or a child.
///   3. The bullet prefab already has SimpleCollisionDamage — no changes needed there.
/// </summary>
[RequireComponent(typeof(TerroristController))]
public class NPCHitBox : MonoBehaviour, IDamageable, IImpactType
{
    [Tooltip("Multiplier applied to incoming bullet damage before passing to TerroristController.\n" +
             "1.0 = full damage, 0.5 = half damage (for harder difficulty), 2.0 = headshot zone etc.")]
    public float damageMultiplier = 1f;

    TerroristController _controller;

    void Awake()
    {
        _controller = GetComponent<TerroristController>();
    }

    // ── IDamageable ───────────────────────────────────────────────────────────

    public void TakeDamage(float damage, GameObject damager)
    {
        if (_controller == null) return;
        // Terrorists don't frag each other: ignore a round whose shooter is another terrorist
        // (their crossfire is meant to threaten the trainee and can wound the hostage, not kill
        // the squad). The trainee's bullets have no TerroristController on the damager, so they
        // still land normally.
        if (damager != null && damager.GetComponentInParent<TerroristController>() != null) return;
        _controller.TakeHit(damage * damageMultiplier);

        // Module 4 integration hook (additive — fires per bullet impact for accuracy metrics)
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.TerroristHit,
            transform.position,
            damager,
            roomId: null,
            targetActorId: _controller.NPCId
        ));
    }

    // ── IImpactType — tells bullet to use flesh decal ─────────────────────────

    public ImpactType GetImpactType() => ImpactType.Flesh;
    public bool ShouldReparent => false;
}
