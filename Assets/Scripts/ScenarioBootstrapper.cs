using System.Collections;
using UnityEngine;

/// <summary>
/// Handles scenario startup sequence:
///   1. Waits one frame for all NPC Awake() calls to finish registering
///   2. Calls EventManager.NotifyScenarioReady() to start the simulation
///
/// Setup:
///   Add this component to the ScenarioManager GameObject alongside EventManager.
///   NavMesh must be pre-baked in the Editor (Window → AI → Navigation → Bake).
/// </summary>
public class ScenarioBootstrapper : MonoBehaviour
{
    [Tooltip("Seconds to wait after scene load before raising ScenarioReady.\n" +
             "Increase if NPCs are still spawning when the event fires.")]
    public float startDelay = 0.1f;

    void Start()
    {
        StartCoroutine(StartupSequence());
    }

    IEnumerator StartupSequence()
    {
        // Wait for all NPC Awake() / Start() to finish registering
        yield return new WaitForSeconds(startDelay);

        // Fire ScenarioReady
        if (EventManager.Instance != null)
        {
            Debug.Log("[ScenarioBootstrapper] Firing ScenarioReady.");
            EventManager.Instance.NotifyScenarioReady();
        }
        else
        {
            Debug.LogError("[ScenarioBootstrapper] EventManager.Instance is null! " +
                           "Make sure EventManager is on the ScenarioManager GameObject " +
                           "and ScenarioManager is in the scene before any NPC.");
        }
    }
}
