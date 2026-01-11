using UnityEngine;

public class PatrolLine : MonoBehaviour
{
    public Transform pointA;
    public Transform pointB;

    public float moveSpeed = 1.2f;
    public float rotateSpeed = 6f;
    public float stopDistance = 0.1f;

    private Transform target;

    void Start()
    {
        target = pointB;
    }

    void Update()
    {
        if (pointA == null || pointB == null) return;

        // Move toward target
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        // If reached target, swap (turn back)
        if (toTarget.magnitude <= stopDistance)
        {
            target = (target == pointA) ? pointB : pointA;
            return;
        }

        // Rotate toward target (smooth turn)
        if (toTarget.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotateSpeed);
        }

        // Move forward
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }
}
