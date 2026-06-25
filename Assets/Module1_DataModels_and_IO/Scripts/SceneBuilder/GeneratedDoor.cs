// =============================================================================
// GeneratedDoor.cs
// Module 1 - Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Runtime behaviour for the procedurally generated doors that SceneBuilder
// places at every DoorData. The door is a physics-hinged leaf the trainee can
// GRAB AND PUSH with the controllers (XRGrabInteractable + HingeJoint). The
// initial DoorState from Module 1 decides whether it starts open, closed, or
// locked; a locked door's hinge is clamped shut until Unlock() is called.
//
// NPCs cannot grab, so a closed door would trap them behind its NavMeshObstacle.
// To keep the building traversable, an NPC standing in the doorway briefly
// drives the leaf open kinematically and lets it close again behind them. NPC
// presence is detected with a Physics.OverlapBox rather than a trigger collider,
// so the door root needs no Rigidbody — this avoids the nested-rigidbody
// instability that made the leaf sag and tilt.
//
// LOCATION NOTE: lives in the SceneBuilder folder (Assembly-CSharp), so it can
// reference Module 2's EventManager / ScenarioEvent for the DoorOpened event.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TeamSentinels.ScenarioGeneration.DataModels;

/// <summary>
/// Physics swing door for generated scenes. The trainee grabs and pushes the
/// leaf; NPCs are let through by a short kinematic auto-open. A carving
/// NavMeshObstacle keeps NPC pathing correct (severed while shut, open once the
/// leaf swings past <see cref="openThreshold"/>).
/// </summary>
[DisallowMultipleComponent]
public class GeneratedDoor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Rigidbody of the swinging leaf (carries the HingeJoint and XRGrabInteractable).")]
    public Rigidbody leafBody;

    [Tooltip("HingeJoint constraining the leaf to swing about its vertical edge.")]
    public HingeJoint hinge;

    [Tooltip("Carving NavMeshObstacle on the leaf. Enabled while shut to block NPC pathing.")]
    public NavMeshObstacle navObstacle;

    [Tooltip("Grab interactable on the leaf. Used to detect when the trainee is holding the door.")]
    public XRGrabInteractable grab;

    [Header("Swing")]
    [Tooltip("Maximum open angle in degrees about the hinge.")]
    public float openAngle = 90f;

    [Tooltip("Angle past which the door counts as open (NavMesh opens, DoorOpened may fire).")]
    public float openThreshold = 15f;

    [Tooltip("Slerp speed for the NPC kinematic auto-open / auto-close.")]
    public float npcDriveSpeed = 3f;

    [Header("NPC sensor (local space, relative to door root)")]
    [Tooltip("Centre of the box used to detect approaching NPCs.")]
    public Vector3 sensorCenter = new Vector3(0f, 1.1f, 0f);

    [Tooltip("Half-extents of the NPC detection box.")]
    public Vector3 sensorHalfExtents = new Vector3(1.4f, 1.1f, 1.2f);

    [Header("State")]
    [Tooltip("Initial door state, set by SceneBuilder from DoorData.state.")]
    public DoorState state = DoorState.Closed;

    // ── Internal ─────────────────────────────────────────────────────────────
    private Quaternion _closedLocalRot;
    private Quaternion _openLocalRot;
    private bool _eventFired;
    private bool _npcDriving;
    private bool _aiHold;       // set by Open()/Close() so NPC AI can drive a door directly

    private Transform Leaf => leafBody != null ? leafBody.transform : transform;

    private void Start()
    {
        if (hinge == null)    hinge    = GetComponentInChildren<HingeJoint>();
        if (leafBody == null && hinge != null) leafBody = hinge.GetComponent<Rigidbody>();
        if (grab == null && leafBody != null)  grab     = leafBody.GetComponent<XRGrabInteractable>();

        _closedLocalRot = Leaf.localRotation;
        _openLocalRot   = _closedLocalRot * Quaternion.Euler(0f, openAngle, 0f);

        ConfigureHingeLimits();

        // Open doors start swung; closed/locked start shut.
        if (state == DoorState.Open)
            Leaf.localRotation = _openLocalRot;

        UpdateBlocking(CurrentAngle());
    }

    private void Update()
    {
        bool grabbed     = grab != null && grab.isSelected;
        bool npcPresent  = _aiHold || NpcInDoorway();
        bool wantNpcOpen = npcPresent && state != DoorState.Locked && !grabbed;

        // The trainee's grab takes priority; the NPC kinematic assist only runs
        // when no hand is holding the door.
        if (wantNpcOpen)      DriveNpcOpen();
        else if (_npcDriving) ReleaseNpcDrive();

        float angle = CurrentAngle();
        UpdateBlocking(angle);
        MaybeRaiseTraineeOpen(angle);
    }

    // ── Public API (callable by Module 2 NPC AI) ──────────────────────────────

    /// <summary>Holds the door open kinematically (used by NPC AI). No-op if locked.</summary>
    [ContextMenu("Test/Open")]
    public void Open()
    {
        if (state != DoorState.Locked) _aiHold = true;
    }

    /// <summary>Releases an AI-held door so it returns to closed.</summary>
    [ContextMenu("Test/Close")]
    public void Close() => _aiHold = false;

    /// <summary>Unlocks a locked door, leaving it shut but now grabbable/openable.</summary>
    [ContextMenu("Test/Unlock")]
    public void Unlock()
    {
        if (state != DoorState.Locked) return;
        state = DoorState.Closed;
        ConfigureHingeLimits();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ConfigureHingeLimits()
    {
        if (hinge == null) return;
        JointLimits l = hinge.limits;
        l.min = 0f;
        l.max = state == DoorState.Locked ? 0f : openAngle; // locked => clamped shut
        hinge.limits    = l;
        hinge.useLimits = true;
    }

    private void DriveNpcOpen()
    {
        _npcDriving = true;
        if (leafBody != null && !leafBody.isKinematic) leafBody.isKinematic = true;
        Leaf.localRotation = Quaternion.Slerp(
            Leaf.localRotation, _openLocalRot, Time.deltaTime * npcDriveSpeed);
    }

    private void ReleaseNpcDrive()
    {
        Leaf.localRotation = Quaternion.Slerp(
            Leaf.localRotation, _closedLocalRot, Time.deltaTime * npcDriveSpeed);

        if (Quaternion.Angle(Leaf.localRotation, _closedLocalRot) < 1f)
        {
            Leaf.localRotation = _closedLocalRot;
            if (leafBody != null)
            {
                leafBody.isKinematic = false;
                leafBody.linearVelocity        = Vector3.zero;
                leafBody.angularVelocity = Vector3.zero;
            }
            _npcDriving = false;
        }
    }

    // Carving obstacle tracks the open state so NPC pathing matches what the
    // trainee sees. The leaf swings aside when open, so it stops blocking.
    private void UpdateBlocking(float angle)
    {
        bool open = angle >= openThreshold;
        if (navObstacle != null && navObstacle.enabled == open)
            navObstacle.enabled = !open;
    }

    // Only the trainee physically swinging a door raises the alert; the NPC
    // kinematic assist does not (NPCs moving through their own building should
    // not alert the rest of the force).
    private void MaybeRaiseTraineeOpen(float angle)
    {
        if (_eventFired || _npcDriving) return;
        if (angle < openThreshold) return;

        _eventFired = true;
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.DoorOpened, transform.position, gameObject));
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
