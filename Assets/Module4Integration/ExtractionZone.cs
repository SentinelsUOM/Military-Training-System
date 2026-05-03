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
    private float _nextPoll;
    private Collider _zoneCollider;
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

    private void OnTriggerEnter(Collider other)
    {
        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            _playerInside = true;
            if (echoToConsole) Debug.Log($"[ExtractionZone] Trainee entered safe zone.");
            EvaluateRescue();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var ph = other.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            _playerInside = false;
            if (echoToConsole) Debug.Log($"[ExtractionZone] Trainee left safe zone.");
        }
    }

    private void Update()
    {
        if (Time.time < _nextPoll) return;
        _nextPoll = Time.time + pollInterval;

        if (!_playerInside) return;
        EvaluateRescue();
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

            // Robust proximity: does the zone collider's closest-point match the hostage position?
            Vector3 closest = _zoneCollider.ClosestPoint(hc.transform.position);
            if (Vector3.SqrMagnitude(closest - hc.transform.position) > 0.0001f)
                continue; // hostage is outside the zone bounds

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
