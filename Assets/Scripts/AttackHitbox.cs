using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AttackHitbox : MonoBehaviour
{
    [HideInInspector] public int damage = 1;

    private void Awake()
    {
        // хитбокс выключаем по умолчанию (активируется PlayerCombat)
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // пытаемся найти компонент реализующий IDamageable
        var dmg = other.GetComponent<IDamageable>();
        if (dmg != null)
        {
            dmg.TakeDamage(damage);
            return;
        }

        // fallback: если у объекта есть EnemyHealth (на случай, если у кого-то не настроено IDamageable)
        var enemy = other.GetComponent<EnemyHealth>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            return;
        }
    }
}
