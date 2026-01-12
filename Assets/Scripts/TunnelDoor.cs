using UnityEngine;

public class TunnelDoor : MonoBehaviour
{
    public Transform door;
    public Vector3 openOffset = new Vector3(0, 3f, 0);

    public GameObject uiPanel;

    public void StartGame()
    {
        // Hide UI panel
        if (uiPanel != null)
            uiPanel.SetActive(false);

        // Open tunnel door
        if (door != null)
            door.position += openOffset;
    }
}
