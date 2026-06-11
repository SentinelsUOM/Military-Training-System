using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves an NPC along a patrol route.
///
/// Two configuration modes:
///   • Multi-waypoint (preferred) — fill the <see cref="waypoints"/> list (or call
///     <see cref="SetWaypoints"/>). The NPC visits each point in order. When
///     <see cref="loop"/> is true the route wraps around (0→1→…→n→0); when false
///     it ping-pongs (0→…→n→…→0). Module 1's navigationContext supplies both the
///     ordered waypoints[] and the looping flag (see HANDOFF.md §4).
///   • Legacy two-point — assign only pointA / pointB. Kept for prefabs configured
///     before the multi-waypoint upgrade; behaves exactly as before (ping-pong A↔B).
/// </summary>
public class PatrolLine : MonoBehaviour
{
    [Header("Patrol Route (multi-waypoint, preferred)")]
    [Tooltip("Ordered patrol waypoints. When set (2+ entries), pointA/pointB are ignored.")]
    public List<Transform> waypoints = new List<Transform>();

    [Tooltip("True — wrap around the route (0→1→…→n→0).\n" +
             "False — ping-pong (reverse direction at the endpoints).")]
    public bool loop = false;

    [Header("Patrol Points (legacy two-point fallback)")]
    public Transform pointA;
    public Transform pointB;

    [Header("Movement")]
    public float moveSpeed = 1.2f;
    public float rotateSpeed = 6f;
    public float stopDistance = 0.2f;

    [Header("Stop / Look")]
    public bool isStopped = false;
    public Transform lookTarget; // XR Main Camera

    [Header("Animation")]
    public Animator anim;
    public string alertBoolName = "Alert";

    // Internal route state. _route is waypoints when present, else [pointA, pointB].
    readonly List<Transform> _route = new List<Transform>();
    int  _targetIndex = 0;
    int  _direction   = 1;   // +1 forward, -1 backward (ping-pong only)

    /// Index of the waypoint currently being walked toward (read-only; used by tests).
    public int CurrentTargetIndex => _targetIndex;

    /// Number of points in the active route (read-only; used by tests).
    public int RouteLength => _route.Count;

    void Start()
    {
        RebuildRoute();

        // IMPORTANT: avoid animation moving character root
        if (anim != null)
            anim.applyRootMotion = false;
    }

    /// <summary>
    /// Replace the patrol route at runtime. Called by SceneBuilder with the full
    /// ordered waypoint list from Module 1's navigationContext.
    /// </summary>
    public void SetWaypoints(IList<Transform> points, bool looping)
    {
        waypoints.Clear();
        if (points != null)
            foreach (var p in points)
                if (p != null) waypoints.Add(p);

        loop = looping;
        RebuildRoute();
    }

    /// <summary>
    /// Rebuilds the internal route from waypoints (preferred) or pointA/pointB
    /// (legacy fallback) and aims at the nearest-from-start target.
    /// </summary>
    void RebuildRoute()
    {
        _route.Clear();

        if (waypoints != null && waypoints.Count >= 2)
        {
            foreach (var w in waypoints)
                if (w != null) _route.Add(w);
        }

        // Legacy fallback — preserves the original A↔B ping-pong behaviour.
        if (_route.Count < 2 && pointA != null && pointB != null)
        {
            _route.Add(pointA);
            _route.Add(pointB);
        }

        _direction = 1;
        // Original behaviour started toward pointB; with a route that means index 1.
        _targetIndex = _route.Count >= 2 ? 1 : 0;
    }

    void Update()
    {
        // STOP MODE: do not move (rotation handled in LateUpdate)
        if (isStopped)
            return;

        // PATROL MODE
        if (_route.Count < 2) return;

        Transform target = _route[_targetIndex];
        if (target == null) { AdvanceTarget(); return; }

        Vector3 targetPos = target.position;
        targetPos.y = transform.position.y;

        float dist = Vector3.Distance(transform.position, targetPos);

        if (dist <= stopDistance)
        {
            AdvanceTarget();
            return;
        }

        // Rotate toward patrol target
        Vector3 moveDir = (targetPos - transform.position).normalized;
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion rot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * rotateSpeed);
        }

        // Move directly toward target
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            moveSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// Steps _targetIndex to the next waypoint — wrapping when loop is true,
    /// reversing direction at the endpoints when ping-ponging.
    /// </summary>
    void AdvanceTarget()
    {
        if (_route.Count < 2) return;

        if (loop)
        {
            _targetIndex = (_targetIndex + 1) % _route.Count;
            return;
        }

        // Ping-pong: reverse at either end of the route.
        int next = _targetIndex + _direction;
        if (next >= _route.Count || next < 0)
        {
            _direction = -_direction;
            next = _targetIndex + _direction;
        }
        _targetIndex = Mathf.Clamp(next, 0, _route.Count - 1);
    }

    // ✅ This runs AFTER animation updates bones, so it won’t get overridden
    void LateUpdate()
    {
        if (!isStopped) return;
        if (lookTarget == null) return;

        Vector3 dir = lookTarget.position - transform.position;
        dir.y = 0f; // rotate only on Y axis (no looking up/down)

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * rotateSpeed);
    }

    public void StopAndLook(Transform playerCam)
    {
        isStopped = true;
        lookTarget = playerCam;

        if (anim != null)
            anim.SetBool(alertBoolName, true); // AIM animation
    }

    public void ResumePatrol()
    {
        isStopped = false;
        lookTarget = null;

        if (anim != null)
            anim.SetBool(alertBoolName, false); // WALK animation
    }
}
