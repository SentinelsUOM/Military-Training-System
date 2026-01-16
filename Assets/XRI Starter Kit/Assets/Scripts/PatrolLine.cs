using UnityEngine;

public class PatrolLine : MonoBehaviour
{
    [Header("Patrol Points")]
    public Transform pointA;
    public Transform pointB;

    [Header("Movement")]
    public float moveSpeed = 1.2f;
    public float rotateSpeed = 6f;
    public float stopDistance = 0.2f;

    [Header("Stop / Look")]
    public bool isStopped = false;
    public Transform lookTarget; // XR Main Camera

    [Header("Animation")]
    public Animator anim;
    public string alertBoolName = "Alert";

    private Transform target;

    void Start()
    {
        target = pointB;

        // IMPORTANT: avoid animation moving character root
        if (anim != null)
            anim.applyRootMotion = false;
    }

    void Update()
    {
        // STOP MODE: do not move (rotation handled in LateUpdate)
        if (isStopped)
            return;

        // PATROL MODE
        if (pointA == null || pointB == null) return;

        Vector3 targetPos = target.position;
        targetPos.y = transform.position.y;

        float dist = Vector3.Distance(transform.position, targetPos);

        if (dist <= stopDistance)
        {
            target = (target == pointA) ? pointB : pointA;
            return;
        }

        // Rotate toward patrol target
        Vector3 moveDir = (targetPos - transform.position).normalized;
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion rot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * rotateSpeed);
        }

        // Move directly toward target
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            moveSpeed * Time.deltaTime
        );
    }

    // ✅ This runs AFTER animation updates bones, so it won’t get overridden
    void LateUpdate()
    {
        if (!isStopped) return;
        if (lookTarget == null) return;

        Vector3 dir = lookTarget.position - transform.position;
        dir.y = 0f; // rotate only on Y axis (no looking up/down)

        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * rotateSpeed);
    }

    public void StopAndLook(Transform playerCam)
    {
        isStopped = true;
        lookTarget = playerCam;

        if (anim != null)
            anim.SetBool(alertBoolName, true); // AIM animation
    }

    public void ResumePatrol()
    {
        isStopped = false;
        lookTarget = null;

        if (anim != null)
            anim.SetBool(alertBoolName, false); // WALK animation
    }
}
