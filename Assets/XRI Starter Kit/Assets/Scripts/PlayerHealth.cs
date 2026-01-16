using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int health = 100;

    public void TakeDamage(int dmg)
    {
        health -= dmg;
        Debug.Log($"PLAYER HIT! HP = {health}");

        if (health <= 0)
        {
            Debug.Log("PLAYER DOWN");
        }
    }
}
