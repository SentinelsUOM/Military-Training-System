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
        EventManager.Instance.Raise(new ScenarioEvent(
            ScenarioEventType.ShotFired,
            transform.position,
            gameObject
        ));
    }
}
