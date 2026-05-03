// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Lives in Assembly-CSharp so it can reference Module 2 (EventManager) directly.

using UnityEngine;

/// <summary>
/// Proximity zone that fires HostageContactStarted when the trainee walks
/// within range of the hostage. After that, the hostage's Module 2 FSM
/// transitions to Follow and the NavMeshAgent escorts the trainee.
///
/// Detection method
///   Polls every contactCheckInterval seconds. Finds the player by looking up
///   PlayerHealth in the scene and measuring distance from the contactZone's
///   position. This avoids the flakiness of trigger-collider overlap (which
///   depends on which child collider on the VR rig hits which trigger volume).
///
/// Setup
///   1. Add a child GameObject to the hostage (e.g. "ContactZone").
///   2. Place it at chest height — Y=1 works.
///   3. Add this component. Set the radius in the Inspector (default 3.0m).
/// </summary>
public class HostageContactZone : MonoBehaviour
{
    [Tooltip("Optional explicit hostage controller. If blank, found via parent.")]
    [SerializeField] private HostageController hostage;

    [Tooltip("Radius in metres. Trainee must come within this distance of the contact " +
             "zone to start the escort. 3.0m works for VR-scale rooms.")]
    [SerializeField] private float radius = 3.0f;

    [Tooltip("Polling interval in seconds. 0.25s feels instant and is cheap.")]
    [SerializeField] private float checkInterval = 0.25f;

    [Tooltip("If true, contact fires only once per session.")]
    [SerializeField] private bool fireOnce = true;

    [Tooltip("Print contact events to the Console.")]
    [SerializeField] private bool echoToConsole = true;

    [Tooltip("Visualise the proximity sphere in the editor.")]
    [SerializeField] private Color gizmoColor = new Color(0.4f, 0.7f, 1f, 0.25f);

    private bool _fired;
    private float _nextCheck;
    private PlayerHealth _cachedPlayer;

    private void Awake()
    {
        if (hostage == null) hostage = GetComponentInParent<HostageController>();
        if (hostage == null)
            Debug.LogError($"[HostageContactZone] {name}: no HostageController found on parent. Disable this component.");
    }

    private void Update()
    {
        if (_fired && fireOnce) return;
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + checkInterval;

        if (hostage == null) return;

        // Player position priority order:
        //   1. Camera.main (the head-tracked camera, ALWAYS moves with the player in VR)
        //   2. PlayerHealth's transform (fallback — may be a static GameObject in some VR rigs)
        Vector3 playerPos;
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            playerPos = mainCam.transform.position;
        }
        else
        {
            if (_cachedPlayer == null)
            {
                _cachedPlayer = FindFirstObjectByType<PlayerHealth>();
                if (_cachedPlayer == null) return;
            }
            playerPos = _cachedPlayer.transform.position;
        }

        float dist = Vector3.Distance(transform.position, playerPos);
        if (dist > radius) return;

        // Trainee is within range — fire the contact event.
        if (EventManager.Instance == null)
        {
            Debug.LogWarning("[HostageContactZone] EventManager.Instance is null — drop the contact.");
            return;
        }

        // Resolve the GameObject to use as Instigator for HostageController's Follow target.
        // Prefer the cached PlayerHealth (parent rig) so the hostage follows the player rig
        // — not the camera, which can clip through walls in roomscale VR.
        if (_cachedPlayer == null) _cachedPlayer = FindFirstObjectByType<PlayerHealth>();
        GameObject instigator = _cachedPlayer != null ? _cachedPlayer.gameObject :
                                (Camera.main != null ? Camera.main.gameObject : null);

        EventManager.Instance.Raise(new ScenarioEvent(
            ScenarioEventType.HostageContactStarted,
            hostage.transform.position,
            instigator,
            roomId: null,
            targetActorId: hostage.NPCId
        ));

        _fired = true;

        if (echoToConsole)
            Debug.Log($"[HostageContactZone] {name}: trainee within {dist:F2}m of {hostage.NPCId} — contact established, hostage now following.");

        if (fireOnce) enabled = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, radius);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
