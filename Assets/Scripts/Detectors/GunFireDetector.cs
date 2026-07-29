using System.Collections.Generic;
using MikeNspired.XRIStarterKit;
using UnityEngine;

/// <summary>
/// Listens to ProjectileWeapon.BulletFiredEvent and raises a ShotFired scenario event.
///
/// Setup: attach this component to the same GameObject as ProjectileWeapon (the player weapon).
/// ProjectileWeapon itself is NOT modified.
/// </summary>
[RequireComponent(typeof(ProjectileWeapon))]
public class GunFireDetector : MonoBehaviour
{
    [Tooltip("Metres within which a terrorist counts as a valid visible target for the " +
             "negligent-discharge check. Generous on purpose — this only flags shots fired " +
             "with NOTHING visible at all, not poor aim.")]
    [SerializeField] private float negligentDischargeCheckRange = 30f;

    [Tooltip("Layers that block line of sight for the negligent-discharge check. Leave as " +
             "'Nothing' to block on everything solid, matching PerceptionController's default.")]
    [SerializeField] private LayerMask obstacleLayers;

    private ProjectileWeapon _weapon;

    void Awake()
    {
        _weapon = GetComponent<ProjectileWeapon>();
        if (_weapon == null)
        {
            Debug.LogError($"[GunFireDetector] {name}: no ProjectileWeapon on this GameObject — " +
                           "ShotFired can NEVER be raised. Detector must sit on the weapon that fires.");
            return;
        }
        _weapon.BulletFiredEvent.AddListener(OnBulletFired);
        Debug.Log($"[GunFireDetector] {name}: subscribed to BulletFiredEvent on '{_weapon.name}'. " +
                  "Ready to raise ShotFired on every shot.");
    }

    void OnDestroy()
    {
        // Clean up listener so there are no dangling references if the weapon is destroyed
        if (_weapon != null)
            _weapon.BulletFiredEvent.RemoveListener(OnBulletFired);
    }

    void OnBulletFired()
    {
        if (EventManager.Instance == null)
        {
            Debug.LogWarning($"[GunFireDetector] {name}: bullet fired but EventManager.Instance is NULL — " +
                             "no ShotFired raised. Is the ScenarioManager/EventManager in the scene?");
            return;
        }

        Debug.Log($"[GunFireDetector] {name}: BULLET FIRED at {transform.position:F1} → raising ShotFired.");

        // Negligent-discharge check (Module 4 accuracy/safety telemetry, additive): was ANY
        // living terrorist even visible when this round was fired? A round fired with nothing
        // visible at all is a "wild" shot — a measurable safety/discipline failure with
        // published expert (~17%) vs novice (~61%) benchmark rates. This is deliberately
        // generous (no aim/FOV requirement) — it only flags shots at literally nothing.
        List<string> tags = AnyTerroristVisible()
            ? null
            : new List<string> { "no_target_in_los" };

        EventManager.Instance.Raise(new ScenarioEvent(
            ScenarioEventType.ShotFired,
            transform.position,
            gameObject,
            tags: tags
        ));
    }

    private bool AnyTerroristVisible()
    {
        var terrorists = FindObjectsByType<TerroristController>(FindObjectsSortMode.None);
        if (terrorists.Length == 0) return false;

        Vector3 origin = transform.position;
        int mask = obstacleLayers.value != 0 ? obstacleLayers.value : ~0;

        foreach (var t in terrorists)
        {
            if (t.currentState == TerroristState.Down) continue;

            Vector3 chest = t.transform.position + Vector3.up * 1.4f;
            Vector3 toChest = chest - origin;
            float dist = toChest.magnitude;
            if (dist > negligentDischargeCheckRange) continue;

            if (!Physics.Raycast(origin, toChest.normalized, out RaycastHit hit, dist, mask,
                    QueryTriggerInteraction.Ignore))
            {
                return true; // nothing blocking, ray reached the terrorist unobstructed
            }
            if (hit.collider != null && hit.collider.transform.IsChildOf(t.transform))
            {
                return true; // hit the terrorist itself before any obstruction
            }
        }
        return false;
    }
}
