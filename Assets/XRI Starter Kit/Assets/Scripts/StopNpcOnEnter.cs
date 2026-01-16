using UnityEngine;
using Unity.XR.CoreUtils;

public class StopNpcOnEnter : MonoBehaviour
{
    [Header("Refs")]
    public PatrolLine npcPatrol;
    public Transform playerCamera;          // XR Origin > Main Camera
    public NpcShooterRaycast npcShooter;

    [Header("Audio (Stop Line)")]
    public AudioSource npcAudioSource;      // NPC AudioSource
    public AudioClip stopLine;              // "Hey! Stop there!"
    public bool playOnce = true;

    private bool hasPlayed = false;

    private void OnTriggerEnter(Collider other)
    {
        // XR-safe: detect player rig
        if (other.GetComponentInParent<XROrigin>() == null) return;

        // If you didn't drag camera, auto-find it
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        // Stop and look
        if (npcPatrol != null && playerCamera != null)
            npcPatrol.StopAndLook(playerCamera);

        // Play stop voice line
        if (npcAudioSource != null && stopLine != null)
        {
            if (!playOnce || !hasPlayed)
            {
                npcAudioSource.Stop();
                npcAudioSource.PlayOneShot(stopLine);
                hasPlayed = true;
            }
        }

        // Start shooting
        if (npcShooter != null && playerCamera != null)
        {
            npcShooter.target = playerCamera;
            npcShooter.StartFiring();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<XROrigin>() == null) return;

        // Resume patrol
        if (npcPatrol != null)
            npcPatrol.ResumePatrol();

        // Stop firing
        if (npcShooter != null)
            npcShooter.StopFiring();

        // Reset so it can play again next time (optional)
        hasPlayed = false;
    }
}
