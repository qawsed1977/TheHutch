using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy: patrol <-> chase <-> return
/// Implements IDamageable so Player's AttackHitbox can call TakeDamage.
/// Configure either two patrol points (pointA, pointB) or use patrolRadius around spawn.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Enemy : MonoBehaviour, IDamageable
{
    public enum PatrolMode { Points, Radius }

    [Header("Behaviour")]
    public PatrolMode patrolMode = PatrolMode.Points;

    [Tooltip("Patrol point A (if mode = Points). If empty, uses spawn position.")]
    public Transform patrolPointA;
    [Tooltip("Patrol point B (if mode = Points). If empty, uses spawn position + Vector2.right.")]
    public Transform patrolPointB;

    [Tooltip("If using Radius mode � enemy patrols back and forth inside this radius from spawn.")]
    public float patrolRadius = 3f;
    [Tooltip("Move speed while patrolling.")]
    public float patrolSpeed = 1.5f;
    [Tooltip("Move speed while chasing player.")]
    public float chaseSpeed = 3f;

    [Header("Detection")]
    [Tooltip("Start chasing when player is inside this radius.")]
    public float detectRadius = 4f;
    [Tooltip("Stop chasing if player is farther than this distance from spawn (or from patrol zone).")]
    public float loseInterestDistance = 8f;
    [Tooltip("Optional layer mask for player detection (set to Player layer).")]
    public LayerMask playerLayer;

    [Header("Combat")]
    public int maxHealth = 3;
    public int contactDamage = 1;
    [Tooltip("Knockback applied to enemy when hit by player.")]
    public float hitKnockbackForce = 4f;
    [Tooltip("Knockback applied to player when touching enemy.")]
    public float contactKnockbackForce = 5f;

    [Header("Misc")]
    [Tooltip("How long enemy remains after death before destroy (seconds).")]
    public float deathDestroyDelay = 1.5f;

    // Components
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer sr;

    // runtime state
    private Vector2 spawnPos;
    private Vector2 leftPoint;
    private Vector2 rightPoint;
    private Vector2 patrolTarget;
    private Transform playerT;

    private int currentHealth;
    private bool isDead = false;
    private bool isChasing = false;
    private bool facingRight = true;

    // small tolerance for target reach
    private const float reachEps = 0.05f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();

        spawnPos = transform.position;
        currentHealth = Mathf.Max(1, maxHealth);

        // setup patrol points
        if (patrolMode == PatrolMode.Points)
        {
            if (patrolPointA == null)
            {
                var go = new GameObject(name + "_patrolA");
                go.transform.position = spawnPos;
                patrolPointA = go.transform;
                go.transform.SetParent(transform.parent); // keep hierarchy tidy
            }
            if (patrolPointB == null)
            {
                var go = new GameObject(name + "_patrolB");
                go.transform.position = spawnPos + Vector2.right * (patrolRadius > 0 ? patrolRadius : 2f);
                patrolPointB = go.transform;
                go.transform.SetParent(transform.parent);
            }
            leftPoint = patrolPointA.position;
            rightPoint = patrolPointB.position;
        }
        else // radius
        {
            leftPoint = spawnPos - Vector2.right * patrolRadius;
            rightPoint = spawnPos + Vector2.right * patrolRadius;
        }

        // start patrol towards rightPoint
        patrolTarget = rightPoint;
    }

    private void Update()
    {
        if (isDead) return;

        // Find player transform lazily
        if (playerT == null)
        {
            var p = FindObjectOfType<PlayerMovement>();
            if (p != null) playerT = p.transform;
        }

        // detect player within radius (simple distance check)
        if (playerT != null)
        {
            float distToPlayer = Vector2.Distance(playerT.position, transform.position);
            float distPlayerToSpawn = Vector2.Distance(playerT.position, spawnPos);

            if (!isChasing)
            {
                if (distToPlayer <= detectRadius)
                {
                    isChasing = true;
                }
            }
            else
            {
                // if player moved too far from spawn, stop chasing and return
                if (distPlayerToSpawn > loseInterestDistance)
                {
                    isChasing = false;
                }
            }
        }

        // Animator parameter: Speed (for walk/run)
        var vel = rb.linearVelocity;
        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(vel.x));
            animator.SetBool("Grounded", IsGrounded());
        }
    }

    private void FixedUpdate()
    {
        if (isDead) { rb.linearVelocity = Vector2.zero; return; }

        if (isChasing && playerT != null)
        {
            Vector2 dir = (playerT.position - transform.position);
            dir.Normalize();
            rb.linearVelocity = new Vector2(dir.x * chaseSpeed, rb.linearVelocity.y);
            UpdateFacing(dir.x);
        }
        else
        {
            // not chasing -> patrol or return to patrol
            Vector2 target = patrolTarget;

            // If player forced enemy away (we left patrol zone), make target the nearest patrol edge to return to
            float distToTarget = Vector2.Distance(transform.position, target);

            // move towards patrol target
            Vector2 dir = (target - (Vector2)transform.position);
            if (dir.magnitude > reachEps)
            {
                dir.Normalize();
                rb.linearVelocity = new Vector2(dir.x * patrolSpeed, rb.linearVelocity.y);
                UpdateFacing(dir.x);
            }
            else
            {
                // reached target -> swap
                if (patrolMode == PatrolMode.Points)
                {
                    patrolTarget = (patrolTarget == (Vector2)leftPoint) ? rightPoint : leftPoint;
                }
                else
                {
                    patrolTarget = (patrolTarget == (Vector2)leftPoint) ? rightPoint : leftPoint;
                }
            }
        }
    }

    private bool IsGrounded()
    {
        // simple check: raycast down small distance from renderer bounds bottom
        if (sr == null) return true;
        Bounds b = sr.bounds;
        float checkDist = 0.05f;
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.01f), Vector2.down, checkDist, ~0); // all layers
        return hit.collider != null;
    }

    private void UpdateFacing(float xDir)
    {
        if (xDir > 0.05f && !facingRight)
        {
            facingRight = true;
            if (sr) sr.flipX = false;
        }
        else if (xDir < -0.05f && facingRight)
        {
            facingRight = false;
            if (sr) sr.flipX = true;
        }
    }

    // IDamageable
    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);

        // play hit animation if exists
        if (animator != null)
            animator.SetTrigger("Hit");

        // small knockback away from player (try to get player transform)
        Vector2 kbDir = Vector2.zero;
        var p = FindObjectOfType<PlayerMovement>();
        if (p != null)
            kbDir = ((Vector2)transform.position - (Vector2)p.transform.position).normalized;
        else
            kbDir = Vector2.up;

        rb.linearVelocity = kbDir * hitKnockbackForce;

        // if hp <=0 -> die
        if (currentHealth <= 0)
        {
            StartCoroutine(DieRoutine());
        }
    }

    // handle collision with player (contact damage)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        var pc = collision.collider.GetComponent<PlayerCombat>();
        if (pc != null)
        {
            // if player is invulnerable - skip
            if (pc.IsInvulnerable()) return;

            // damage player
            pc.TakeDamage(contactDamage);

            // apply knockback to player away from enemy
            var playerRb = collision.collider.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                Vector2 dir = (collision.transform.position - transform.position).normalized;
                playerRb.linearVelocity = dir * contactKnockbackForce;
            }
        }
    }

    // Optional: if using trigger for attack hitbox collisions, keep OnTriggerEnter2D to detect when player attack hits
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        // detect attack hitboxes: AttackHitbox is activated only during attack
        var ah = other.GetComponent<AttackHitbox>();
        if (ah != null && other.gameObject.activeInHierarchy)
        {
            // Damage already applied by AttackHitbox -> but we want knockback and maybe stagger
            // If AttackHitbox's OnTrigger already called TakeDamage on this enemy, here we only add knockback effect.
            // To be safe, call TakeDamage here as well if AttackHitbox didn't handle IDamageable - but AttackHitbox does call IDamageable.TakeDamage.
            // So we only add a small upward knockback so enemy doesn't get stuck.
            // compute direction from attacker (player)
            var player = FindObjectOfType<PlayerMovement>();
            Vector2 kbDir = Vector2.up;
            if (player != null)
                kbDir = ((Vector2)transform.position - (Vector2)player.transform.position).normalized;
            rb.linearVelocity = (kbDir + Vector2.up * 0.3f).normalized * hitKnockbackForce;
        }
    }

    private IEnumerator DieRoutine()
    {
        isDead = true;

        // stop physics
        rb.linearVelocity = Vector2.zero;
        rb.isKinematic = true;
        GetComponent<Collider2D>().enabled = false;

        // play death animation if exists
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        // wait for animation (small delay)
        yield return new WaitForSeconds(0.4f);

        // darken then fade
        if (sr != null)
        {
            // darken quickly
            Color orig = sr.color;
            float darkDur = 0.25f;
            float fadeDur = Mathf.Max(0.5f, deathDestroyDelay - darkDur);
            float t = 0f;
            while (t < darkDur)
            {
                sr.color = Color.Lerp(orig, Color.black, t / darkDur);
                t += Time.deltaTime;
                yield return null;
            }
            sr.color = Color.black;

            // fade out
            t = 0f;
            while (t < fadeDur)
            {
                float a = Mathf.Lerp(1f, 0f, t / fadeDur);
                Color c = sr.color; c.a = a; sr.color = c;
                t += Time.deltaTime;
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    // draw detection radii in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, loseInterestDistance);

        Gizmos.color = Color.green;
        if (patrolMode == PatrolMode.Points)
        {
            if (patrolPointA != null) Gizmos.DrawSphere(patrolPointA.position, 0.08f);
            if (patrolPointB != null) Gizmos.DrawSphere(patrolPointB.position, 0.08f);
            Gizmos.DrawLine(patrolPointA.position, patrolPointB.position);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, patrolRadius);
        }
    }
}
