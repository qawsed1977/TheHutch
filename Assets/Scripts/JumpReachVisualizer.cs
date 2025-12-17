using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visualizer для зоны досягаемости прыжка игрока.
/// Включается на объекте Player (можно включать/выключать в иерархии).
/// Рисует несколько траекторий (без коллизий) и контур максимальной области.
/// 
/// Небольшое расширение: можно задать manualOrigin (Transform) или смещение origin по Y (originYOffset),
/// чтобы траектории исходили не из центра, а с нужной высоты (напр., из ног).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Transform))]
public class JumpReachVisualizer : MonoBehaviour
{
    [Header("Source (optional)")]
    public PlayerMovement playerMovement; // если не назначено, будет попытка найти на том же объекте

    [Header("Origin adjustments")]
    [Tooltip("Если указан, то origin будет взят из этого Transform.position (вместо transform.position).")]
    public Transform manualOrigin = null;
    [Tooltip("Дополнительное смещение по Y (в мировых единицах). Добавляется к вычисленному origin Y.")]
    public float originYOffset = 0f;

    [Header("Visualization")]
    [Range(3, 64)] public int samplesPerTrajectory = 40;
    [Range(1, 8)] public int horizontalSamples = 5; // сколько разных горизонтальных скоростей визуализировать
    public Color trajectoryColor = new Color(0f, 0.7f, 1f, 0.9f);
    public Color maxAreaColor = new Color(1f, 0.4f, 0.0f, 0.6f);

    [Header("Simulation")]
    public float fixedDelta = 0.02f; // временной шаг симуляции
    public bool assumeFullHold = true; // рисовать для максимально удержанного прыжка
    public bool drawForNoHold = true;  // рисовать для прыжка без удержания
    public bool drawForFullHold = true; // рисовать для удержания на max

    private void OnValidate()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
    }

    private void OnDrawGizmos()
    {
        if (playerMovement == null) return;

        // получаем параметры
        float moveSpeed = playerMovement.MoveSpeed;
        float jumpForce = playerMovement.JumpForce;
        float jumpHoldForce = playerMovement.JumpHoldForce;
        float holdDuration = playerMovement.JumpHoldDuration;

        // физика
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        float gravity = Physics2D.gravity.y * (rb != null ? rb.gravityScale : 1f); // gravity < 0

        // центр - позиция игрока (начало прыжка)
        Vector3 origin;

        // если указан manualOrigin, используем его позицию (и добавляем originYOffset)
        if (manualOrigin != null)
        {
            origin = manualOrigin.position;
        }
        else
        {
            origin = transform.position;
        }

        // Применяем смещение по Y (только Y)
        origin = new Vector3(origin.x, origin.y + originYOffset, origin.z);

        // рисуем несколько траекторий с разными горизонтальными скоростями (от -moveSpeed до +moveSpeed)
        Gizmos.color = trajectoryColor;

        int hSamples = Mathf.Max(1, horizontalSamples);
        for (int i = 0; i < hSamples; i++)
        {
            // горизонтальная скорость для этой траектории
            float t = (hSamples == 1) ? 0f : (float)i / (hSamples - 1); // 0..1
            float vx = Mathf.Lerp(-moveSpeed, moveSpeed, t);

            // две траектории: без удержания и с полным удержанием (опционально)
            if (drawForNoHold)
                DrawTrajectory(origin, vx, jumpForce, 0f, gravity);

            if (drawForFullHold)
            {
                float addedVy = jumpHoldForce * holdDuration; // приближённая суммарная прибавка
                DrawTrajectory(origin, vx, jumpForce + addedVy, 0f, gravity);
            }
        }

        // контур максимальной области: используем полный hold (максимальная vy)
        if (drawForFullHold)
        {
            float addedVyMax = jumpHoldForce * holdDuration;
            float vy = jumpForce + addedVyMax;
            float timeTotal = (vy <= 0f || gravity == 0f) ? 0f : (2f * vy / -gravity); // t = 2*vy/|g|
            float maxHorizRange = Mathf.Abs(moveSpeed * timeTotal);
            float maxHeight = (gravity == 0f) ? 0f : (vy * vy) / (2f * -gravity);

            // рисуем прямоугольный контур (от -maxHorizRange до +maxHorizRange, от 0 до maxHeight)
            Gizmos.color = maxAreaColor;
            Vector3 bl = origin + new Vector3(-maxHorizRange, 0f, 0f);
            Vector3 br = origin + new Vector3(maxHorizRange, 0f, 0f);
            Vector3 tl = origin + new Vector3(-maxHorizRange, maxHeight, 0f);
            Vector3 tr = origin + new Vector3(maxHorizRange, maxHeight, 0f);

            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(bl, tl);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tl, tr);

            // вспомогательные линии: центр -> max left/right
            Gizmos.DrawLine(origin, bl);
            Gizmos.DrawLine(origin, br);
        }
    }

    private void DrawTrajectory(Vector3 origin, float vx, float initialVy, float startT, float gravity)
    {
        // рисуем параболу до момента, когда y<=0 (возврат на начальный уровень)
        if (gravity == 0f)
            return;

        float t = 0f;
        float dt = fixedDelta;
        Vector3 prev = origin;
        int steps = samplesPerTrajectory;
        for (int i = 1; i <= steps; i++)
        {
            t = (i / (float)steps) * (2f * initialVy / -gravity); // параметизуем по доле времени полёта
            float x = origin.x + vx * t;
            float y = origin.y + initialVy * t + 0.5f * gravity * t * t;
            Vector3 pt = new Vector3(x, y, origin.z);
            Gizmos.DrawLine(prev, pt);
            prev = pt;
        }
    }
}
