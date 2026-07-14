using System.Collections;
using UnityEngine;

public class NpcShooterRaycast : MonoBehaviour
{
    [Header("Refs")]
    public Transform firePoint;
    public Transform target;
    public LayerMask hitMask = ~0;

    [Header("Shooting")]
    [Tooltip("Rounds per second WITHIN a burst.")]
    public float fireRate = 8f;
    [Tooltip("Cone of inaccuracy. 1.5 deg was laser-accurate — real CQB fire is far looser, " +
             "and an enemy who never misses just feels unfair. 5 deg = suppressive, not sniper.")]
    public float spreadDegrees = 5f;
    public float range = 80f;
    public int damage = 10;

    [Header("Burst Fire")]
    [Tooltip("Rounds fired in one burst before pausing. A steady metronome of single shots reads " +
             "as a machine; humans fire in bursts.")]
    public int shotsPerBurst = 3;
    [Tooltip("Seconds of pause between bursts (re-acquire / assess).")]
    public float burstPause = 1.2f;

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

    /// <summary>
    /// Fires ONE deliberately-missed but fully AUDIBLE round — the doctrinal "warning shot".
    /// Same muzzle flash and gunshot report as a live round, but it is aimed to miss and does
    /// no damage. This is the loudest, least-missable rung of the hostage escalation ladder:
    /// at the Lindt Café siege the gunman fired a shot into a wall ~2 minutes before he
    /// executed the hostage, and the Coroner found that shot should itself have triggered an
    /// immediate assault. A silent countdown gives the trainee nothing to react to; a gunshot
    /// does.
    /// </summary>
    public void FireWarningShot()
    {
        // Deliberately NO raycast and NO damage — it is a warning, not an attack.
        PlayShotFx();
    }

    /// <summary>
    /// The point-blank execution shot. Muzzle flash and gunshot report ONLY — the hostage's
    /// death is scripted (HostageController.Execute), not simulated, so a raycast here would
    /// be wrong: at contact distance the barrel is inside the hostage's head and the trace
    /// could just as easily strike the captor's own arm. Without this the execution was
    /// completely SILENT — the single most important beat in the mission had no gunshot.
    /// </summary>
    public void FireExecutionShot()
    {
        PlayShotFx();
    }

    /// <summary>Muzzle flash + gunshot report, with no ballistics. Shared by every shot whose
    /// outcome is scripted rather than traced.</summary>
    void PlayShotFx()
    {
        if (muzzleFlash != null) muzzleFlash.Play();
        if (fireAudioSource != null && fireClip != null) fireAudioSource.PlayOneShot(fireClip);
    }

    IEnumerator FireLoop()
    {
        float delay = 1f / Mathf.Max(0.1f, fireRate);
        while (true)
        {
            int rounds = Mathf.Max(1, shotsPerBurst);
            for (int i = 0; i < rounds; i++)
            {
                FireOnce();
                yield return new WaitForSeconds(delay);
            }
            // Pause between bursts — this is what turns a metronome into a person.
            if (burstPause > 0f) yield return new WaitForSeconds(burstPause);
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
            // Resolve the trainee's health. Walking UP the hierarchy alone is not enough:
            // the XR rig's own colliders (CharacterController / capsule on "XR Origin") sit
            // ABOVE PlayerHealth in the tree, so a bullet that struck the rig capsule found
            // no PlayerHealth above it — or worse, found a SECOND, duplicate PlayerHealth
            // that the mission wasn't watching. Result: the trainee was effectively immortal.
            // So: try upward first, then fall back to searching the whole rig.
            var health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health == null)
                health = hit.collider.transform.root.GetComponentInChildren<PlayerHealth>();

            // Pass the MUZZLE position, not the impact point — the trainee needs to know which
            // direction they are being shot FROM so they can turn and break line of sight.
            if (health != null) health.TakeDamage(damage, firePoint.position);
        }
    }
}