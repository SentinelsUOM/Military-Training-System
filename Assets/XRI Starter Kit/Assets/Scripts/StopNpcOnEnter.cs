using UnityEngine;

public class StopNpcOnEnter : MonoBehaviour
{
    public PatrolLine npcPatrol;
    public Transform playerCamera;
    public AudioSource npcAudioSource;
    public AudioClip stopLine;

    private bool hasSpoken = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Stop immediately + look at player
        npcPatrol.StopAndLook(playerCamera);

        // Speak only once while you are inside
        if (!hasSpoken && npcAudioSource != null && stopLine != null)
        {
            npcAudioSource.Stop();
            npcAudioSource.PlayOneShot(stopLine);
            hasSpoken = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Keep forcing stop + look (prevents any movement)
        npcPatrol.StopAndLook(playerCamera);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Resume patrol when player leaves
        npcPatrol.ResumePatrol();

        // Reset so it can speak again next time you enter
        hasSpoken = false;
    }
}
