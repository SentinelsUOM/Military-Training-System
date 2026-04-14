using MikeNspired.XRIStarterKit;
using UnityEngine;

/// <summary>
/// Raises a DoorOpened scenario event when a Door is unlocked for the first time.
///
/// Setup: attach to the same GameObject as the Door component.
/// Hooks automatically — no Inspector wiring required.
/// </summary>
[RequireComponent(typeof(Door))]
public class DoorOpenDetector : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Door>().OnDoorOpened.AddListener(OnDoorOpened);
    }

    void OnDoorOpened()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.DoorOpened,
            transform.position,
            gameObject
        ));
    }
}
