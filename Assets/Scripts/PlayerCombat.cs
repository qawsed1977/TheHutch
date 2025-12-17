using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private float invulDuration = 2.0f; // теперь 2 сек, редактируема€ в Unity
    [SerializeField, Range(0, 5)] private int currentHealth = 5;
    private bool isInvulnerable = false;

    // событие: (currentHealth, maxHealth)
    public event Action<int, int> OnHealthChanged;

    [Header("Attack")]
    [SerializeField] private float attackDuration = 0.15f;
    [SerializeField] private float attackCooldown = 0.25f;
    [SerializeField] private int attackDamage = 1;

    [Header("Hitboxes (assign child GameObjects with Trigger Collider2D)")]
    public GameObject hitboxUp;
    public GameObject hitboxDown;
    public GameObject hitboxLeft;
    public GameObject hitboxRight;

    private PlayerControls controls;
    private PlayerMovement pm;
    private Animator animator;
    private SpriteRenderer sr;

    private bool canAttack = true;

    private void Awake()
    {
        controls = new PlayerControls();
        pm = GetComponent<PlayerMovement>();
        animator = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();

        // защита: maxHealth минимум 1
        if (maxHealth < 1) maxHealth = 1;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (currentHealth == 0) currentHealth = maxHealth;

        // выключаем хитбоксы, если они назначены
        SetAllHitboxesActive(false);

        // уведомл€ем UI об инициализации
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void OnEnable()
    {
        controls.Player.Enable();
        controls.Player.Attack.performed += OnAttackPerformed;
    }

    private void OnDisable()
    {
        controls.Player.Attack.performed -= OnAttackPerformed;
        controls.Player.Disable();
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        TryAttack();
    }

    public void TryAttack()
    {
        if (!canAttack) return;
        StartCoroutine(DoAttack());
    }

    private IEnumerator DoAttack()
    {
        canAttack = false;

        Vector2 move = controls.Player.Move.ReadValue<Vector2>();

        GameObject chosenHitbox = null;
        bool usedFacing = false;

        if (move.y > 0.5f)
        {
            chosenHitbox = hitboxUp;
            animator?.SetTrigger("AttackUp");
        }
        else if (move.y < -0.5f && !pm.IsGrounded)
        {
            chosenHitbox = hitboxDown;
            animator?.SetTrigger("AttackDown");
        }
        else if (move.y < -0.5f && pm.IsGrounded)
        {
            usedFacing = true;
        }
        else if (move.x > 0.1f)
        {
            chosenHitbox = hitboxRight;
            animator?.SetTrigger("AttackRight");
        }
        else if (move.x < -0.1f)
        {
            chosenHitbox = hitboxLeft;
            animator?.SetTrigger("AttackLeft");
        }
        else
        {
            usedFacing = true;
        }

        if (usedFacing)
        {
            if (pm.FacingDirection >= 0)
            {
                chosenHitbox = hitboxRight;
                animator?.SetTrigger("AttackRight");
            }
            else
            {
                chosenHitbox = hitboxLeft;
                animator?.SetTrigger("AttackLeft");
            }
        }

        if (chosenHitbox != null)
        {
            var ah = chosenHitbox.GetComponent<AttackHitbox>();
            if (ah != null)
                ah.damage = attackDamage;

            chosenHitbox.SetActive(true);
        }

        yield return new WaitForSeconds(attackDuration);

        if (chosenHitbox != null)
            chosenHitbox.SetActive(false);

        yield return new WaitForSeconds(attackCooldown);

        canAttack = true;
    }

    private void SetAllHitboxesActive(bool active)
    {
        if (hitboxUp) hitboxUp.SetActive(active);
        if (hitboxDown) hitboxDown.SetActive(active);
        if (hitboxLeft) hitboxLeft.SetActive(active);
        if (hitboxRight) hitboxRight.SetActive(active);
    }

    public void TakeDamage(int dmg)
    {
        if (isInvulnerable) return;

        currentHealth -= dmg;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // уведомл€ем UI
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (animator != null)
            animator.SetTrigger("Hurt");

        StartCoroutine(InvulnerabilityCoroutine());

        if (currentHealth <= 0)
            Die();
    }

    private IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;

        float blinkInterval = 0.12f;
        float elapsed = 0f;
        while (elapsed < invulDuration)
        {
            sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }
        sr.enabled = true;
        isInvulnerable = false;
    }

    private void Die()
    {
        animator?.SetTrigger("Die");
        var mv = GetComponent<PlayerMovement>();
        if (mv != null) mv.enabled = false;
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public bool IsInvulnerable()
    {
        return isInvulnerable;
    }


    public int GetCurrentHealth() => currentHealth;
    // возвращает текущий максимум ’ѕ
    public int GetMaxHealth() => maxHealth;


    // позволим UI мен€ть maxHP во врем€ игры (опционально)
    public void SetMaxHealth(int newMax)
    {
        if (newMax < 1) newMax = 1;
        maxHealth = newMax;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
