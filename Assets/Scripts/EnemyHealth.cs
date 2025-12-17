using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHp = 3;
    private int hp;

    private void Awake()
    {
        hp = maxHp;
    }

    public void TakeDamage(int amount)
    {
        hp -= amount;
        Debug.Log($"{name} took {amount} damage. HP left = {hp}");
        if (hp <= 0) Die();
    }

    private void Die()
    {
        // Играй анимацию/эффект, затем уничтожь
        Destroy(gameObject);
    }
}
