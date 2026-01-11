using UnityEngine;

public class PatrolLine : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;

    public float moveSpeed = 1.2f;
    public float rotateSpeed = 6f;
    public float stopDistance = 0.2f;

    [Header("Stop / Look")]
    public bool isStopped = false;
    public Transform lookTarget; // XR Main Camera

    private Transform target;

    void Start()
    {
        target = pointB;
    }

    void Update()
    {
        // If stopped: only look at player
        if (isStopped && lookTarget != null)
        {
            LookAt(lookTarget.position);
            return;
        }

        if (pointA == null || pointB == null) return;

        Vector3 targetPos = target.position;
        targetPos.y = transform.position.y; // keep same height

        float dist = Vector3.Distance(transform.position, targetPos);

        // Reached target? swap
        if (dist <= stopDistance)
        {
            target = (target == pointA) ? pointB : pointA;
            return;
        }

        // Rotate toward target
        Vector3 dir = (targetPos - transform.position).normalized;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * rotateSpeed);
        }

        // Move directly toward target (NO orbiting)
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            moveSpeed * Time.deltaTime
        );
    }

    void LookAt(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * rotateSpeed);
    }

    public void StopAndLook(Transform playerCam)
    {
        isStopped = true;
        lookTarget = playerCam;
    }

    public void ResumePatrol()
    {
        isStopped = false;
        lookTarget = null;
    }
}
