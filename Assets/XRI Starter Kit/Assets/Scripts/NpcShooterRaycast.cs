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

    [Header("Hostage")]
    public HostageScareController hostage;   // drag hostage1 here
    public bool controlHostageScare = true;  // tick this
    public HostageRunawayController runawayHostage;


    private Coroutine loop;

    public void StartFiring()
    {
        Debug.Log("StartFiring() called");
        if (loop != null) return;

        // Hostage 1: scared in place
        if (controlHostageScare && hostage != null)
            hostage.SetScared(true);

        // Hostage 2: run away + get down scared
        if (runawayHostage != null)
            runawayHostage.OnGunfire(true);

        loop = StartCoroutine(FireLoop());
    }

    public void StopFiring()
    {
        Debug.Log("StopFiring() called");
        if (loop == null) return;

        StopCoroutine(loop);
        loop = null;

        if (controlHostageScare && hostage != null)
            hostage.SetScared(false);

        if (runawayHostage != null)
            runawayHostage.OnGunfire(false);
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
            Debug.Log("NPC shot hit: " + hit.collider.name);

            var health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health != null) health.TakeDamage(damage);
        }
        else
        {
            Debug.Log("NPC shot missed (no hit)");
        }
    }
}
