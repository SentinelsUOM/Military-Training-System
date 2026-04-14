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
        _weapon.BulletFiredEvent.AddListener(OnBulletFired);
    }

    void OnDestroy()
    {
        // Clean up listener so there are no dangling references if the weapon is destroyed
        if (_weapon != null)
            _weapon.BulletFiredEvent.RemoveListener(OnBulletFired);
    }

    void OnBulletFired()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.ShotFired,
            transform.position,
            gameObject
        ));
    }
}
