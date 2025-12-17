using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerDash : MonoBehaviour
{
    [Header("Afterimage Visual FX")]
    [SerializeField] private Color afterImageTint = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color afterImageGlow = new Color(1.2f, 1.2f, 1.2f, 1f);
    [SerializeField] private float glowIntensity = 1.0f;


    [Header("Dash skill")]
    [SerializeField] private bool hasDash = false; // true when player picked up skill
    [SerializeField] private float dashDistance = 6f; // units
    [SerializeField] private float dashDuration = 0.18f; // seconds
    [SerializeField] private float dashCooldown = 0.5f; // seconds after dash before next
    [SerializeField] private Key keyboardKey = Key.LeftShift; // default key (Shift)
    //[SerializeField] private string gamepadButton = "ButtonSouth"; // if needed

    [Header("Air behavior")]
    [SerializeField] private bool airNoFall = true; // if true, during dash in air player won't lose altitude

    [Header("Afterimage (trail)")]
    [SerializeField] private float afterImageInterval = 0.03f;
    [SerializeField] private float afterImageLifetime = 0.35f;
    [SerializeField] private float afterImageFadeTime = 0.25f;
    [SerializeField] private Color afterImageColor = new Color(1, 1, 1, 0.6f);

    private PlayerControls controls;
    private Rigidbody2D rb;
    private PlayerMovement pm;
    private SpriteRenderer sr;
    private float lastDashTime = -999f;
    private bool isDashing = false;

    private float originalGravityScale;
    private Coroutine dashCoroutine;

    private void Awake()
    {
        controls = new PlayerControls();
        rb = GetComponent<Rigidbody2D>();
        pm = GetComponent<PlayerMovement>();
        sr = GetComponent<SpriteRenderer>();
        originalGravityScale = rb.gravityScale;
    }

    private void OnEnable()
    {
        controls.Player.Enable();
        // NOTE: requires that your Player InputActions asset contains an action named "Dash"
        // If it doesn't exist, see instructions below to add it or use keyboard check in Update.
        controls.Player.Dash.performed += OnDashPerformed;
    }

    private void OnDisable()
    {
        controls.Player.Dash.performed -= OnDashPerformed;
        controls.Player.Disable();
    }

    private void OnDashPerformed(InputAction.CallbackContext ctx)
    {
        TryStartDash();
    }

    private void Update()
    {
        // fallback for keyboard-only testing: allow pressing the assigned key
        if (Keyboard.current != null && Keyboard.current[keyboardKey].wasPressedThisFrame)
        {
            TryStartDash();
        }
    }

    private void TryStartDash()
    {
        if (!hasDash) return;
        if (Time.time < lastDashTime + dashCooldown) return;
        if (pm == null || rb == null) return;
        if (isDashing) return;

        // start dash
        dashCoroutine = StartCoroutine(DoDash());
    }

    private IEnumerator DoDash()
    {
        isDashing = true;
        pm.InDash = true;
        lastDashTime = Time.time;

        int dir = pm.FacingDirection >= 0 ? 1 : -1;

        float speed = dashDistance / Mathf.Max(0.01f, dashDuration);

        // 1) Полностью обнуляем вертикальную скорость перед началом рывка
        rb.linearVelocity = new Vector2(0f, 0f);

        // 2) Отключаем гравитацию для РОВНОГО движения по горизонтали
        rb.gravityScale = 0f;

        float elapsed = 0f;
        float nextAfterImage = 0f;

        while (elapsed < dashDuration)
        {
            // 3) Устанавливаем ЧИСТО горизонтальную скорость
            rb.linearVelocity = new Vector2(dir * speed, 0f);

            // afterimages
            if (elapsed >= nextAfterImage)
            {
                SpawnAfterImage();
                nextAfterImage += afterImageInterval;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 4) В конце рывка полностью остановить игрока (по всем осям)
        rb.linearVelocity = Vector2.zero;

        // 5) Вернуть гравитацию
        rb.gravityScale = originalGravityScale;

        // 6) Небольшая задержка, если нужно (оставляем нулевую)
        yield return new WaitForSeconds(0.0f);

        pm.InDash = false;
        isDashing = false;
    }


    private void SpawnAfterImage()
    {
        if (sr == null) return;

        GameObject go = new GameObject("AfterImage");
        go.transform.position = transform.position;
        go.transform.rotation = transform.rotation;

        var copy = go.AddComponent<SpriteRenderer>();
        copy.sprite = sr.sprite;
        copy.sortingLayerID = sr.sortingLayerID;
        copy.sortingOrder = sr.sortingOrder - 1;
        copy.flipX = sr.flipX;

        // Основной цвет (прозрачная копия)
        Color tint = afterImageTint;

        // Добавляем белый "светящий" слой
        Color glow = afterImageGlow * glowIntensity;

        // Складываем цвета (красиво светит)
        Color final = new Color(
            Mathf.Clamp01(tint.r + glow.r - 1f),
            Mathf.Clamp01(tint.g + glow.g - 1f),
            Mathf.Clamp01(tint.b + glow.b - 1f),
            tint.a
        );

        copy.color = final;

        StartCoroutine(FadeAndDestroy(copy, afterImageLifetime));
    }


    private IEnumerator FadeAndDestroy(SpriteRenderer copy, float life)
    {
        float t = 0f;
        Color initial = copy.color;

        while (t < life)
        {
            float a = Mathf.Lerp(initial.a, 0f, t / life);
            // Цвет остаётся светлым, но прозрачным
            copy.color = new Color(initial.r, initial.g, initial.b, a);
            t += Time.deltaTime;
            yield return null;
        }

        if (copy != null)
            Destroy(copy.gameObject);
    }


    // Public API to set that player now has dash (call when player picks up the ability)
    public void GrantDash()
    {
        hasDash = true;
    }

    public bool HasDash() => hasDash;

    // optional: allow setting cooldown from code
    public void SetCooldown(float s) => dashCooldown = s;
}
