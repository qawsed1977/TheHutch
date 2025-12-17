using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    private PlayerControls controls;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private Vector2 moveInput;
    [SerializeField] private float moveSpeed = 5f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 8f;          // начальный импульс
    [SerializeField] private float jumpHoldForce = 3f;      // сила, пока держим
    [SerializeField] private float jumpHoldDuration = 0.25f; // максимум удержания
    private float jumpTimeCounter;
    private bool isJumping;
    private bool jumpHeld;   // кнопка удерживается
    private bool jumpQueued; // учёт «удержания до следующего прыжка»

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    private bool isGrounded;

    [Header("Collision checks")]
    [SerializeField] private Collider2D bodyCollider; // если не назначен — будет найден автоматически
    [SerializeField] private float groundCheckDistance = 0.08f; // дистанция вниз для boxcast
    [SerializeField] private float wallCheckDistance = 0.06f;   // дистанция для боковой проверки

    // --- Публичные свойства (используются в PlayerCombat)
    public bool IsGrounded => isGrounded;
    public int FacingDirection { get; private set; } = 1; // 1 = вправо, -1 = влево

    // ADDED: флаг — игрок в рывке
    public bool InDash { get; set; } = false;

    // в конце списка публичных свойств (например после FacingDirection)
    public float MoveSpeed => moveSpeed;
    public float JumpForce => jumpForce;
    public float JumpHoldForce => jumpHoldForce;
    public float JumpHoldDuration => jumpHoldDuration;
    public float GroundCheckRadius => groundCheckRadius;

    private void Awake()
    {
        controls = new PlayerControls();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // автопоиск GroundCheck, если забыли назначить в инспекторе
        if (groundCheck == null)
        {
            var t = transform.Find("GroundCheck");
            if (t != null) groundCheck = t;
        }

        // автопоиск основного collider'а (если не назначен)
        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }
    }

    private void OnEnable()
    {
        controls.Player.Enable();

        controls.Player.Move.performed += OnMove;
        controls.Player.Move.canceled += OnMove;

        // используем started для более надёжного срабатывания на нажатие
        controls.Player.Jump.started += OnJumpPressed;
        controls.Player.Jump.canceled += OnJumpReleased;
    }

    private void OnDisable()
    {
        controls.Player.Move.performed -= OnMove;
        controls.Player.Move.canceled -= OnMove;

        controls.Player.Jump.started -= OnJumpPressed;
        controls.Player.Jump.canceled -= OnJumpReleased;

        controls.Player.Disable();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnJumpPressed(InputAction.CallbackContext context)
    {
        jumpHeld = true;

        if (isGrounded)
        {
            StartJump();
        }
        else
        {
            // если в воздухе, то «запоминаем» прыжок (для "jump buffering")
            jumpQueued = true;
        }
    }

    private void OnJumpReleased(InputAction.CallbackContext context)
    {
        jumpHeld = false;
        isJumping = false; // прекращаем «boost»
    }

    private void StartJump()
    {
        isJumping = true;
        jumpTimeCounter = jumpHoldDuration;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private void FixedUpdate()
    {
        // --- улучшенная проверка на землю (BoxCast вниз) ---
        if (bodyCollider != null)
        {
            var b = bodyCollider.bounds;
            Vector2 boxOrigin = new Vector2(b.center.x, b.min.y + 0.01f);
            Vector2 boxSize = new Vector2(b.size.x * 0.9f, 0.02f);
            RaycastHit2D hit = Physics2D.BoxCast(boxOrigin, boxSize, 0f, Vector2.down, groundCheckDistance + 0.01f, groundLayer);
            isGrounded = hit.collider != null;
        }
        else
        {
            if (groundCheck != null && groundLayer != 0)
                isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
            else
                isGrounded = false;
        }

        // --- wall detection + horizontal movement handling ---
        bool isTouchingWall = false;
        if (bodyCollider != null && Mathf.Abs(moveInput.x) > 0.05f)
        {
            Vector2 dir = moveInput.x > 0 ? Vector2.right : Vector2.left;
            var b = bodyCollider.bounds;
            Vector2 boxOrigin = new Vector2(b.center.x, b.center.y);
            Vector2 boxSize = new Vector2(b.size.x * 0.5f, b.size.y * 0.9f);
            RaycastHit2D wallHit = Physics2D.BoxCast(boxOrigin, boxSize, 0f, dir, wallCheckDistance, groundLayer);
            isTouchingWall = wallHit.collider != null;
        }

        // Если упираемся в стену и не стоим на земле — зануляем горизонтальную скорость, чтобы падать
        if (isTouchingWall && !isGrounded)
        {
            // ADDED: если in dash — не перебиваем вертикаль/горизонталь — dash сам управляет
            if (!InDash)
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
        else
        {
            // ADDED: блокируем обычное движение при рывке — Dash управляет скоростью
            if (!InDash)
            {
                rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
            }
            // иначе — ничего не делаем: Dash удержит скорость
        }

        // «boost» прыжка при удержании
        if (isJumping && jumpHeld)
        {
            if (jumpTimeCounter > 0f)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y + jumpHoldForce * Time.fixedDeltaTime);
                jumpTimeCounter -= Time.fixedDeltaTime;
            }
            else
            {
                isJumping = false;
            }
        }

        // автоматический повтор прыжка, если кнопка всё ещё удерживается
        if (isGrounded && jumpQueued)
        {
            StartJump();
            jumpQueued = false;
        }

        // управление анимацией (проверяем, что параметр есть)
        float speed = Mathf.Abs(moveInput.x);
        if (animator != null)
        {
            animator.SetFloat("Speed", speed);
            SetAnimatorBoolSafe("IsGrounded", isGrounded);
            // ADDED: передаём флаг рывка (если есть параметр)
            SetAnimatorBoolSafe("InDash", InDash);
        }

        // отражение по X + сохранение направления взгляда
        if (spriteRenderer != null)
        {
            if (moveInput.x > 0.1f)
            {
                spriteRenderer.flipX = false;
                FacingDirection = 1;
            }
            else if (moveInput.x < -0.1f)
            {
                spriteRenderer.flipX = true;
                FacingDirection = -1;
            }
        }
    }

    // безопасная установка бул-параметра аниматора (чтобы не было логов, если параметра нет)
    private void SetAnimatorBoolSafe(string param, bool value)
    {
        if (animator == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.name == param && p.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(param, value);
                return;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

#if UNITY_EDITOR
        if (bodyCollider != null)
        {
            var b = bodyCollider.bounds;
            UnityEditor.Handles.DrawWireCube(b.center, b.size);
        }
#endif
    }
}
