// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Lives in Assembly-CSharp so it can reference Module 2 (EventManager,
// HostageController, HostageState) directly.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger zone that represents the safe extraction point. The trainee enters
/// via OnTriggerEnter (their collider is reliable). Hostages are detected by
/// periodic proximity polling because most hostage rigs don't have a collider
/// at the root that overlaps trigger callbacks consistently.
///
/// When the trainee is inside the zone AND at least one Following hostage's
/// transform is inside the zone's bounds, that hostage is marked Freed.
///
/// Reveal behaviour: the zone's visual marker (SafeZoneBeacon, if present)
/// starts disabled and is only enabled here once a hostage is actually being
/// escorted (Follow) or already delivered (Freed) — so the trainee has to find
/// the hostage first; the extraction point isn't given away up front. The
/// trigger/detection logic itself is always live regardless of the marker's
/// visibility.
///
/// Setup
///   1. Place an empty GameObject at the extraction point.
///   2. Add a BoxCollider or SphereCollider. Set isTrigger = true. Size it
///      to enclose the safe area.
///   3. Add this component.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExtractionZone : MonoBehaviour
{
    [Tooltip("How often (seconds) to scan for hostages inside the zone. " +
             "0.25s is plenty smooth and cheap.")]
    [SerializeField] private float pollInterval = 0.25f;

    [Tooltip("Print zone events to the Console for debugging.")]
    [SerializeField] private bool echoToConsole = true;

    [Tooltip("Visualise the trigger volume in the editor.")]
    [SerializeField] private Color gizmoColor = new Color(0.2f, 1f, 0.4f, 0.25f);

    private bool _playerInside;
    private bool _revealed;
    private float _nextPoll;
    private Collider _zoneCollider;
    private SafeZoneBeacon _beacon;
    private readonly HashSet<HostageController> _alreadyFreed = new HashSet<HostageController>();

    private void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void Awake()
    {
        _zoneCollider = GetComponent<Collider>();
        if (_zoneCollider != null && !_zoneCollider.isTrigger)
            Debug.LogWarning($"[ExtractionZone] {name}: collider is not set to isTrigger.");
    }

    private void Start()
    {
        // Looked up in Start (not Awake): SceneBuilder adds this component BEFORE
        // SafeZoneBeacon on the same GameObject in the same frame, and
        // AddComponent invokes Awake() synchronously — so GetComponent here
        // would still find nothing if done in Awake(). Start() runs after every
        // sibling component on the object has been added and Awoken.
        _beacon = GetComponent<SafeZoneBeacon>();

        // If the marker already came in enabled (e.g. hand-placed in a scene,
        // not spawned by SceneBuilder with it pre-disabled), don't fight that.
        if (_beacon != null && _beacon.enabled) _revealed = true;
    }

    private PlayerHealth _cachedPlayer;

    private void Update()
    {
        if (Time.time < _nextPoll) return;
        _nextPoll = Time.time + pollInterval;

        if (!_revealed && AnyHostageEscortedOrDelivered())
        {
            _revealed = true;
            if (_beacon != null) _beacon.enabled = true;
            if (echoToConsole)
                Debug.Log("[ExtractionZone] Hostage secured — safe zone marker revealed.");
        }

        // Determine "player inside" by POLLING the player's position — VR rig
        // trigger callbacks (OnTriggerEnter/Exit) are unreliable, so we test the
        // actual head/rig position against the zone bounds every tick instead.
        bool wasInside = _playerInside;
        _playerInside = IsInsideZone(GetPlayerPosition());

        if (echoToConsole && _playerInside != wasInside)
            Debug.Log($"[ExtractionZone] Trainee {(_playerInside ? "entered" : "left")} safe zone.");

        if (_playerInside) EvaluateRescue();
    }

    /// <summary>True once any hostage has started being escorted (or already
    /// been delivered) — the signal that the marker should reveal itself.</summary>
    private static bool AnyHostageEscortedOrDelivered()
    {
        foreach (var hc in FindObjectsByType<HostageController>(FindObjectsSortMode.None))
        {
            if (hc == null) continue;
            if (hc.currentState == HostageState.Follow || hc.currentState == HostageState.Freed)
                return true;
        }
        return false;
    }

    /// <summary>Player position: head camera first (always moves in VR), else the PlayerHealth rig.</summary>
    private Vector3? GetPlayerPosition()
    {
        if (Camera.main != null) return Camera.main.transform.position;
        if (_cachedPlayer == null) _cachedPlayer = FindFirstObjectByType<PlayerHealth>();
        return _cachedPlayer != null ? _cachedPlayer.transform.position : (Vector3?)null;
    }

    /// <summary>True if a world point lies within the zone bounds (Y flattened to the zone centre).</summary>
    private bool IsInsideZone(Vector3? worldPos)
    {
        if (worldPos == null || _zoneCollider == null) return false;
        Vector3 p = worldPos.Value;
        p.y = _zoneCollider.bounds.center.y; // ignore height — the head is ~1.6 m up
        return (_zoneCollider.ClosestPoint(p) - p).sqrMagnitude < 0.0001f;
    }

    private void EvaluateRescue()
    {
        if (!_playerInside) return;
        if (_zoneCollider == null) return;
        if (EventManager.Instance == null) return;

        // Find every Following hostage whose transform sits inside the zone bounds.
        var hostages = FindObjectsByType<HostageController>(FindObjectsSortMode.None);
        int rescued = 0;
        foreach (var hc in hostages)
        {
            if (hc == null) continue;
            if (_alreadyFreed.Contains(hc)) continue;
            if (hc.currentState == HostageState.Freed) continue;

            // Only escorted (Following) hostages count.
            if (hc.currentState != HostageState.Follow) continue;

            // Hostage must be standing inside the zone bounds (Y-flattened).
            if (!IsInsideZone(hc.transform.position)) continue;

            EventManager.Instance.Raise(new ScenarioEvent(
                ScenarioEventType.HostageFreed,
                hc.transform.position,
                hc.gameObject,
                roomId: null,
                targetActorId: hc.NPCId
            ));

            _alreadyFreed.Add(hc);
            rescued++;

            if (echoToConsole)
                Debug.Log($"[ExtractionZone] Hostage {hc.NPCId} rescued (in zone, was Following).");
        }

        if (rescued > 0 && echoToConsole)
            Debug.Log($"[ExtractionZone] Marked {rescued} hostage(s) as Freed. " +
                      $"Module4SessionController will end the session once every registered hostage is Freed.");
    }

    private void OnDrawGizmos()
    {
        var c = GetComponent<Collider>();
        if (c == null) return;

        Gizmos.color = gizmoColor;
        switch (c)
        {
            case BoxCollider box:
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
                break;
            case SphereCollider sphere:
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawSphere(sphere.center, sphere.radius);
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                Gizmos.matrix = Matrix4x4.identity;
                break;
        }
    }
}
