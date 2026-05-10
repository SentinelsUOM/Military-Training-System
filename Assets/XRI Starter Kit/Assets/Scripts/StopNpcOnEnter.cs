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

        // Auto-shooting on trigger removed — combat is now driven entirely by
        // PerceptionController (NPC shoots only when it actually sees the player).
        // The trigger still stops the patrol and plays the voice line.
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<XROrigin>() == null) return;

        // Resume patrol
        if (npcPatrol != null)
            npcPatrol.ResumePatrol();

        // Reset so it can play again next time (optional)
        hasPlayed = false;
    }
}
