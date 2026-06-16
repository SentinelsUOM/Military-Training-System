// =============================================================================
// NpcDoorAssist.cs
// Module 1 - Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// NPC traversal helper for the REALISTIC (MikeNspired XRI Starter Kit) doors
// that SceneBuilder now instantiates. The trainee opens those doors by turning
// their XRKnob handles, but generated NPCs are NavMeshAgents and cannot grab a
// knob. Without help they would be trapped behind a shut door's NavMeshObstacle.
//
// This component keeps the building traversable: a carving NavMeshObstacle on
// the leaf severs the navmesh while the door is shut, and when a NavMeshAgent
// approaches, the leaf is driven open KINEMATICALLY (swinging about its real
// hinge axis) and lets it close again behind them. The trainee's knob grab
// always takes priority — the NPC assist stands down while a handle is held.
//
// It deliberately drives the leaf transform directly rather than going through
// the XRI Door's knob/spring/limit logic, so it works regardless of how each
// door's hinge limits happen to be authored. The leaf is only ever returned to
// the XRI Door (made dynamic again) once it is back at its closed rest pose, so
// the two systems never fight.
//
// LOCATION NOTE: lives in the SceneBuilder folder (Assembly-CSharp) so it can
// reference both Module 1's DoorState enum and the MikeNspired XRI Door/XRKnob.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using MikeNspired.XRIStarterKit;
using TeamSentinels.ScenarioGeneration.DataModels;

/// <summary>
/// Lets NavMeshAgent NPCs pass through a realistic XRI hinge door that they
/// cannot physically grab. Added to the door root by ScenePrefabBuilder's
/// "Finalize Real Door" command (references auto-wire in <see cref="Awake"/> if
/// left empty, so a hand-added instance works too).
/// </summary>
[DisallowMultipleComponent]
public class NpcDoorAssist : MonoBehaviour
{
    [Header("References (auto-wired in Awake if left empty)")]
    [Tooltip("The XRI Door controller. Optional - only used to sync its startOpened flag.")]
    public Door door;

    [Tooltip("HingeJoint on the swinging leaf. Its axis defines the swing direction.")]
    public HingeJoint hinge;

    [Tooltip("Rigidbody of the swinging leaf (driven kinematically while an NPC passes).")]
    public Rigidbody leafBody;

    [Tooltip("Carving NavMeshObstacle on the leaf. On while shut, off once open.")]
    public NavMeshObstacle navObstacle;

    [Tooltip("Trainee knob handles. While any is grabbed, the NPC assist stands down.")]
    public XRKnob[] knobs;

    [Header("Swing")]
    [Tooltip("Degrees the leaf swings open for an NPC. Sign sets the swing direction.")]
    public float openAngle = 90f;

    [Tooltip("Angle past which the door counts as open (NavMesh obstacle releases).")]
    public float openThreshold = 15f;

    [Tooltip("Slerp speed for the kinematic NPC open / close.")]
    public float npcDriveSpeed = 3f;

    [Header("NPC sensor (local space, relative to door root)")]
    [Tooltip("Centre of the box used to detect approaching NavMeshAgents.")]
    public Vector3 sensorCenter = new Vector3(0f, 1.1f, 0f);

    [Tooltip("Half-extents of the NPC detection box.")]
    public Vector3 sensorHalfExtents = new Vector3(1.6f, 1.1f, 1.4f);

    // ── Internal ─────────────────────────────────────────────────────────────
    private Quaternion _closedLocalRot;
    private Quaternion _openLocalRot;
    private bool _npcDriving;
    private bool _wasKinematic;

    private Transform Leaf =>
        leafBody != null ? leafBody.transform :
        hinge != null ? hinge.transform : transform;

    private void Awake()
    {
        // Auto-wire anything the editor command didn't set.
        if (hinge == null)                       hinge = GetComponentInChildren<HingeJoint>();
        if (leafBody == null && hinge != null)   leafBody = hinge.GetComponent<Rigidbody>();
        if (door == null)                        door = GetComponentInChildren<Door>();
        if (knobs == null || knobs.Length == 0)  knobs = GetComponentsInChildren<XRKnob>(true);
        // Obstacle may sit on the root (axis-aligned doorway carve) or the leaf.
        if (navObstacle == null) navObstacle = GetComponentInChildren<NavMeshObstacle>(true);

        // Cache the rest pose now: Awake runs before any Start(), so this is the
        // authored "closed" rotation before the XRI Door snaps it on its Start.
        _closedLocalRot = Leaf.localRotation;
        Vector3 axis = hinge != null ? hinge.axis : Vector3.up;
        if (axis.sqrMagnitude < 1e-6f) axis = Vector3.up;
        _openLocalRot = _closedLocalRot * Quaternion.AngleAxis(openAngle, axis.normalized);
    }

    /// <summary>
    /// Applies Module 1's initial <see cref="DoorState"/>. Open pre-swings the
    /// leaf and tells the XRI Door to start opened; Closed/Locked leave it shut
    /// (the XRI door has no key system, so Locked is treated as Closed).
    /// Call after instantiating (door inactive) and before SetActive(true).
    /// </summary>
    public void ApplyState(DoorState state)
    {
        bool open = state == DoorState.Open;
        if (door != null) door.SetStartOpened(open);
        if (open) Leaf.localRotation = _openLocalRot;
        UpdateBlocking(CurrentAngle());
    }

    private void Update()
    {
        bool traineeHolding = false;
        if (knobs != null)
            foreach (XRKnob k in knobs)
                if (k != null && k.isSelected) { traineeHolding = true; break; }

        bool wantNpcOpen = !traineeHolding && NpcInDoorway();

        if (wantNpcOpen)      DriveNpcOpen();
        else if (_npcDriving) ReleaseNpcDrive();

        UpdateBlocking(CurrentAngle());
    }

    private void DriveNpcOpen()
    {
        if (!_npcDriving)
        {
            _npcDriving = true;
            if (leafBody != null)
            {
                _wasKinematic = leafBody.isKinematic;
                leafBody.isKinematic = true;
            }
        }
        Leaf.localRotation = Quaternion.Slerp(
            Leaf.localRotation, _openLocalRot, Time.deltaTime * npcDriveSpeed);
    }

    private void ReleaseNpcDrive()
    {
        Leaf.localRotation = Quaternion.Slerp(
            Leaf.localRotation, _closedLocalRot, Time.deltaTime * npcDriveSpeed);

        // Only hand the leaf back to physics once it's fully closed again, so the
        // XRI Door's hinge limits / spring never snap a half-open leaf.
        if (Quaternion.Angle(Leaf.localRotation, _closedLocalRot) < 1f)
        {
            Leaf.localRotation = _closedLocalRot;
            if (leafBody != null)
            {
                leafBody.isKinematic = _wasKinematic;
                if (!leafBody.isKinematic)
                {
                    leafBody.linearVelocity  = Vector3.zero;
                    leafBody.angularVelocity = Vector3.zero;
                }
            }
            _npcDriving = false;
        }
    }

    // Carving obstacle tracks the open state so NPC pathing matches the visible
    // door: blocking while shut, released once the leaf swings past the threshold.
    private void UpdateBlocking(float angle)
    {
        bool open = angle >= openThreshold;
        if (navObstacle != null && navObstacle.enabled == open)
            navObstacle.enabled = !open;
    }

    private float CurrentAngle() => Quaternion.Angle(Leaf.localRotation, _closedLocalRot);

    // NPCs are NavMeshAgent-driven; the trainee XR rig is not, so only agents
    // standing in the doorway trigger the kinematic auto-open assist.
    private bool NpcInDoorway()
    {
        Vector3 worldCenter = transform.TransformPoint(sensorCenter);
        Collider[] hits = Physics.OverlapBox(
            worldCenter, sensorHalfExtents, transform.rotation,
            ~0, QueryTriggerInteraction.Ignore);

        foreach (Collider h in hits)
            if (h.GetComponentInParent<NavMeshAgent>() != null)
                return true;
        return false;
    }
}
