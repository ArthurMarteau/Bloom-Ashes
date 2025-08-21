using UnityEngine;

public class HealthManager : MonoBehaviour
{
    private int health = 100; // Points de vie initiaux
    private int maxHealth = 100; // Points de vie maximum

    // Réduit les points de vie (dégâts)
    public void TakeDamage(int damage)
    {
        health -= damage;
        if (health < 0)
        {
            health = 0;
        }
    }

    // Augmente les points de vie (soins)
    public void Heal(int amount)
    {
        health += amount;
        if (health > maxHealth)
        {
            health = maxHealth;
        }
        // À toi de compléter cette méthode
    }

    // Retourne les points de vie restants
    public int GetHealth()
    {
        return health;
    }

    // Vérifie si le personnage est mort
    public bool IsDead()
    {
        return health == 0;
    }
}

