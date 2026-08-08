// =============================================================================
// PlayerBodyAnimator.cs
// Drives the visible third-person trainee body (a Mixamo character standing in
// for the invisible XR rig) using the SAME Animator Controller and parameter
// set the terrorist NPCs use (Speed/Firing/Alert on NPC_Walk.controller) — so
// the trainee shows a walk-with-rifle pose while moving and a proper
// aim/firing pose while shooting, matching what evaluators see on the NPCs.
//
// Setup: attach to the visible body root (the Animator's GameObject), parented
// under the XR Origin. Assign characterRoot (defaults to the XR Origin parent,
// used for movement-speed sampling) and playerWeapon (the ProjectileWeapon the
// trainee fires) in the Inspector, or leave weapon null to auto-find it each
// time a new weapon is grabbed (see ReacquireWeapon).
// =============================================================================

using UnityEngine;
using MikeNspired.XRIStarterKit;

public class PlayerBodyAnimator : MonoBehaviour
{
    [Tooltip("Transform whose world-position delta drives the Speed parameter — normally the " +
             "XR Origin (the body's parent). Defaults to this object's parent if left unassigned.")]
    public Transform characterRoot;

    [Tooltip("The trainee's weapon. Its BulletFiredEvent drives the Firing pose. Leave unassigned " +
             "and call ReacquireWeapon() (or let Update's auto-search below run) once the trainee " +
             "actually picks a weapon up.")]
    public ProjectileWeapon playerWeapon;

    [Tooltip("How long (s) Firing stays true after the last shot — keeps single/burst fire reading " +
             "as a held aim pose instead of flickering back to idle between rounds.")]
    public float firingHoldTime = 0.35f;

    [Tooltip("Walk speed (m/s) that maps to the Locomotion blend tree's full Speed=1 walk pose.")]
    public float walkSpeedNormalizer = 1.6f;

    Animator _animator;
    Vector3 _lastPos;
    float _lastFireTime = -999f;
    ProjectileWeapon _subscribedWeapon;

    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int FiringParam = Animator.StringToHash("Firing");
    static readonly int AlertParam = Animator.StringToHash("Alert");

    void Awake()
    {
        _animator = GetComponent<Animator>();
        if (characterRoot == null) characterRoot = transform.parent;
        _lastPos = characterRoot != null ? characterRoot.position : transform.position;
    }

    void OnEnable()
    {
        if (playerWeapon != null) Subscribe(playerWeapon);
    }

    void OnDisable()
    {
        if (_subscribedWeapon != null) _subscribedWeapon.BulletFiredEvent.RemoveListener(OnBulletFired);
        _subscribedWeapon = null;
    }

    void Update()
    {
        if (_animator == null || characterRoot == null) return;

        // Movement speed from world-position delta — matches the trainee's actual locomotion
        // regardless of how it's driven (teleport, smooth locomotion, or scripted for a recording).
        Vector3 pos = characterRoot.position;
        float dist = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(_lastPos.x, 0f, _lastPos.z));
        float speed = Time.deltaTime > 0f ? dist / Time.deltaTime : 0f;
        _lastPos = pos;

        float normalizedSpeed = walkSpeedNormalizer > 0f ? Mathf.Clamp01(speed / walkSpeedNormalizer) : 0f;
        _animator.SetFloat(SpeedParam, normalizedSpeed, 0.12f, Time.deltaTime);

        bool firing = Time.time - _lastFireTime <= firingHoldTime;
        _animator.SetBool(FiringParam, firing);
        _animator.SetBool(AlertParam, firing);

        // Auto-pick-up: if no weapon is wired yet, or the trainee swapped weapons, latch onto
        // whichever ProjectileWeapon is currently in an XR grab-selected state.
        if (playerWeapon == null || !ReferenceEquals(playerWeapon, _subscribedWeapon))
        {
            if (playerWeapon != null) Subscribe(playerWeapon);
        }
    }

    void Subscribe(ProjectileWeapon weapon)
    {
        if (_subscribedWeapon != null) _subscribedWeapon.BulletFiredEvent.RemoveListener(OnBulletFired);
        _subscribedWeapon = weapon;
        _subscribedWeapon.BulletFiredEvent.AddListener(OnBulletFired);
    }

    /// <summary>Call when the trainee grabs a (possibly new) weapon so the Firing pose tracks it.</summary>
    public void ReacquireWeapon(ProjectileWeapon weapon)
    {
        playerWeapon = weapon;
        Subscribe(weapon);
    }

    void OnBulletFired() => _lastFireTime = Time.time;
}
