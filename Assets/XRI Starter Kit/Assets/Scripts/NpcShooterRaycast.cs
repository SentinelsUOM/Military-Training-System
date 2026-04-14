using System.Collections;
using UnityEngine;

public class NpcShooterRaycast : MonoBehaviour
{
    [Header("Refs")]
    public Transform firePoint;
    public Transform target;
    public LayerMask hitMask = ~0;

    [Header("Shooting")]
    public float fireRate = 2f;
    public float spreadDegrees = 1.5f;
    public float range = 80f;
    public int damage = 10;

    [Header("FX / Audio")]
    public ParticleSystem muzzleFlash;
    public AudioSource fireAudioSource;
    public AudioClip fireClip;

    [Header("Hostage 1 (scared in place)")]
    public HostageScareController hostage;
    public bool controlHostageScare = true;

    [Header("Hostage 2 (runaway)")]
    public HostageRunawayController runawayHostage;

    private Coroutine loop;

    /// True while the NPC is actively firing. Read by TerroristController to sync Engage state.
    public bool IsFiring => loop != null;

    // ✅ TEST buttons in inspector (right click component header)
    [ContextMenu("TEST -> StartFiring")]
    void TestStartFiring() => StartFiring();

    [ContextMenu("TEST -> StopFiring")]
    void TestStopFiring() => StopFiring();

    public void StartFiring()
    {
        Debug.Log("[NpcShooterRaycast] StartFiring() called on: " + gameObject.name);

        if (loop != null) return;

        if (controlHostageScare && hostage != null)
            hostage.SetScared(true);

        if (runawayHostage != null)
        {
            Debug.Log("[NpcShooterRaycast] Calling runawayHostage.OnGunfire(true)");
            runawayHostage.OnGunfire(true);
        }
        else
        {
            Debug.LogWarning("[NpcShooterRaycast] Runaway Hostage is NULL (not assigned).");
        }

        loop = StartCoroutine(FireLoop());
    }

    public void StopFiring()
    {
        Debug.Log("[NpcShooterRaycast] StopFiring() called on: " + gameObject.name);

        if (loop == null) return;

        StopCoroutine(loop);
        loop = null;

        if (controlHostageScare && hostage != null)
            hostage.SetScared(false);

        if (runawayHostage != null)
        {
            Debug.Log("[NpcShooterRaycast] Calling runawayHostage.OnGunfire(false)");
            runawayHostage.OnGunfire(false);
        }
    }

    IEnumerator FireLoop()
    {
        float delay = 1f / Mathf.Max(0.1f, fireRate);
        while (true)
        {
            FireOnce();
            yield return new WaitForSeconds(delay);
        }
    }

    void FireOnce()
    {
        if (firePoint == null) { Debug.LogWarning("No firePoint"); return; }
        if (target == null) { Debug.LogWarning("No target"); return; }

        if (muzzleFlash != null) muzzleFlash.Play();
        if (fireAudioSource != null && fireClip != null) fireAudioSource.PlayOneShot(fireClip);

        Vector3 dir = (target.position - firePoint.position).normalized;

        float angle = spreadDegrees * Mathf.Deg2Rad;
        dir += new Vector3(
            Random.Range(-angle, angle),
            Random.Range(-angle, angle),
            Random.Range(-angle, angle)
        );
        dir.Normalize();

        Vector3 start = firePoint.position + firePoint.forward * 0.05f;
        Debug.DrawRay(start, dir * 10f, Color.red, 0.2f);

        if (Physics.Raycast(start, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            var health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health != null) health.TakeDamage(damage);
        }
    }
}