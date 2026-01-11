using UnityEngine;
using System.Collections;

public class StopNpcOnEnter : MonoBehaviour
{
    public PatrolLine npcPatrol;
    public Transform playerCamera;
    public AudioSource npcAudioSource;
    public AudioClip stopLine;

    private bool hasTriggered = false;
    private Coroutine routine;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (hasTriggered) return;

        hasTriggered = true;

        // Speak while still walking
        if (npcAudioSource != null && stopLine != null)
        {
            npcAudioSource.Stop();
            npcAudioSource.PlayOneShot(stopLine);
            routine = StartCoroutine(StopWhenAudioEnds());
        }
        else
        {
            // If no audio assigned, stop immediately
            npcPatrol.StopAndLook(playerCamera);
        }
    }

    IEnumerator StopWhenAudioEnds()
    {
        while (npcAudioSource != null && npcAudioSource.isPlaying)
            yield return null;

        npcPatrol.StopAndLook(playerCamera);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Cancel waiting if needed
        if (routine != null) StopCoroutine(routine);

        // Resume patrol
        npcPatrol.ResumePatrol();

        // Allow it to happen again next time
        hasTriggered = false;
    }
}
