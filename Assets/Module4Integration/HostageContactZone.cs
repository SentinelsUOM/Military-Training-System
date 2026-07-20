// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Lives in Assembly-CSharp so it can reference Module 2 (EventManager) directly.

using UnityEngine;

/// <summary>
/// Proximity zone that fires HostageContactStarted when the trainee walks
/// within range of the hostage — no grab, no button, just find the hostage and
/// approach. After that, the hostage's Module 2 FSM transitions to Follow
/// (which also calms it — SetScared(false)) and the NavMeshAgent escorts the
/// trainee to the safe zone.
///
/// Safety gate: contact only fires once every terrorist in the scenario is
/// Down. The area must actually be clear before the hostage will approach and
/// follow — "go find the hostage" is a post-firefight objective, not something
/// that can shortcut a live gunfight.
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
///   3. Add this component. Set the radius in the Inspector (default 2.5m).
/// </summary>
public class HostageContactZone : MonoBehaviour
{
    [Tooltip("Optional explicit hostage controller. If blank, found via parent.")]
    [SerializeField] private HostageController hostage;

    [Tooltip("Radius in metres. Trainee must come within this distance of the contact " +
             "zone to start the escort. 2.5m reads as \"walked right up to them\".")]
    [SerializeField] private float radius = 2.5f;

    [Tooltip("Polling interval in seconds. 0.25s feels instant and is cheap.")]
    [SerializeField] private float checkInterval = 0.25f;

    [Tooltip("If true, contact fires only once per session. Default false so the escort " +
             "re-establishes if the hostage is scared off (regresses to Fearful) and you " +
             "approach again. The hostage's own response cooldown prevents spam.")]
    [SerializeField] private bool fireOnce = false;

    [Tooltip("Require every terrorist in the scenario to be Down before contact can fire. " +
             "The hostage won't calm down and follow while the area still isn't safe.")]
    [SerializeField] private bool requireAllTerroristsDown = true;

    [Tooltip("Print contact events to the Console.")]
    [SerializeField] private bool echoToConsole = true;

    [Tooltip("Visualise the proximity sphere in the editor.")]
    [SerializeField] private Color gizmoColor = new Color(0.4f, 0.7f, 1f, 0.25f);

    private bool _fired;
    private float _nextCheck;
    private PlayerHealth _cachedPlayer;
    private bool _loggedUnsafe;

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

        // Already escorting (or rescued)? Don't re-fire — avoids per-tick event spam.
        // (If the hostage regresses to Fearful from a gunshot, this allows re-contact.)
        if (hostage.currentState == HostageState.Follow ||
            hostage.currentState == HostageState.Freed)
            return;

        if (requireAllTerroristsDown && !AreAllTerroristsDown())
        {
            if (echoToConsole && !_loggedUnsafe)
            {
                _loggedUnsafe = true;
                Debug.Log($"[HostageContactZone] {name}: trainee is close but the area isn't " +
                          "clear yet — hostage stays put until every terrorist is down.");
            }
            return;
        }
        _loggedUnsafe = false;

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
            Debug.Log($"[HostageContactZone] {name}: trainee within {dist:F2}m of {hostage.NPCId} — area clear, hostage calmed and now following.");

        if (fireOnce) enabled = false;
    }

    /// <summary>True once every TerroristController in the scene is in the Down state
    /// (or none exist at all — an all-hostage/no-threat scenario is trivially "safe").</summary>
    private static bool AreAllTerroristsDown()
    {
        var terrorists = FindObjectsByType<TerroristController>(FindObjectsSortMode.None);
        foreach (var t in terrorists)
        {
            if (t == null) continue;
            if (t.currentState != TerroristState.Down) return false;
        }
        return true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, radius);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
