using UnityEngine;

public class HostageScareController : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;
    public string scaredBoolName = "Scared";

    [Header("Optional Audio")]
    public AudioSource audioSource;
    public AudioClip gaspClip;

    bool lastState = false;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    // Call true when gunfire starts, false when gunfire stops
    public void SetScared(bool scared)
    {
        if (animator == null) return;

        animator.SetBool(scaredBoolName, scared);

        // Optional: play gasp only once when entering scared
        if (scared && !lastState && audioSource != null && gaspClip != null)
            audioSource.PlayOneShot(gaspClip);

        lastState = scared;
    }
}
