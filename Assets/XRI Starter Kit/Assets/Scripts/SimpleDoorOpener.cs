using UnityEngine;
using UnityEngine.AI;

public class SimpleDoorOpener : MonoBehaviour
{
    public Transform doorPivot;
    public float openAngle = 90f;
    public float openSpeed = 2f;
    public Collider doorCollider; // assign in inspector

    public NavMeshObstacle obstacleToDisable; // drag door's NavMeshObstacle here

    private bool open = false;
    private Quaternion closedRot;
    private Quaternion openRot;

    void Start()
    {
        if (doorPivot == null) doorPivot = transform;
        closedRot = doorPivot.rotation;
        openRot = doorPivot.rotation * Quaternion.Euler(0, openAngle, 0);
    }

    void Update()
    {
        if (!doorPivot) return;
        var target = open ? openRot : closedRot;
        doorPivot.rotation = Quaternion.Slerp(doorPivot.rotation, target, Time.deltaTime * openSpeed);
    }

    public void OpenDoor()
    {
        open = true;
        if (doorCollider != null) doorCollider.enabled = false;
        if (obstacleToDisable != null) obstacleToDisable.enabled = false;
    }
}
