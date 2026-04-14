using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Raises a configurable scenario event when the player enters this trigger collider.
///
/// Setup:
///   1. Add to any GameObject that has a Collider with "Is Trigger" enabled.
///   2. Set eventType in the Inspector to match the zone's purpose
///      (e.g. RoomBreached for a doorway, GunshotHeard for an NPC hearing radius).
///
/// The collider's position is used as the event origin.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TriggerZoneDetector : MonoBehaviour
{
    [Tooltip("Which event to raise when the player enters this zone.")]
    public ScenarioEventType eventType = ScenarioEventType.RoomBreached;

    void OnTriggerEnter(Collider other)
    {
        // Only react to the XR player rig, not stray physics objects
        if (other.GetComponentInParent<XROrigin>() == null) return;

        EventManager.Instance?.Raise(new ScenarioEvent(
            eventType,
            transform.position,
            other.gameObject
        ));
    }
}
