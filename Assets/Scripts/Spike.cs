using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Spike : MonoBehaviour, IDamageable
{
    [Header("Spike Settings")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private Vector2 knockbackDirection = new Vector2(0f, 1f);
    [SerializeField] private float attackKnockbackForce = 7f;
    [SerializeField] private Vector2 attackKnockbackDirection = new Vector2(0f, 1f);

    private void OnTriggerEnter2D(Collider2D collision)
    {
        var playerCombat = collision.GetComponent<PlayerCombat>();
        var playerRb = collision.GetComponent<Rigidbody2D>();

        if (playerCombat == null || playerRb == null) return;

        // проверяем, есть ли активный хитбокс — значит удар
        var attackHitbox = collision.GetComponentInChildren<AttackHitbox>();
        if (attackHitbox != null && attackHitbox.gameObject.activeInHierarchy)
        {
            // удар по колючке — отталкиваем игрока вверх
            playerRb.linearVelocity = attackKnockbackDirection.normalized * attackKnockbackForce;
            return;
        }

        // обычное касание
        if (playerCombat.IsInvulnerable()) return;

        playerCombat.TakeDamage(damage);
        playerRb.linearVelocity = knockbackDirection.normalized * knockbackForce;
    }

    // --- IDamageable можно оставить пустым, если игрок не вызывает TakeDamage напрямую ---
    public void TakeDamage(int amount) { }
}
