using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthPack : MonoBehaviour
{
    [SerializeField] private int healAmount = 20;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object has a health component or interface
        PlayerStats player = other.GetComponent<PlayerStats>();
        if (player != null)
        {
            player.Heal(healAmount);
            Destroy(gameObject);
        }

        // Check for EnemyStats
        EnemyStats enemy = other.GetComponent<EnemyStats>();
        if (enemy != null)
        {
            enemy.Heal(healAmount);
            Destroy(gameObject);
        }
    }
}
