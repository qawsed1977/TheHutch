using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float smoothSpeed = 5f;

    [Header("Level Bounds")]
    [SerializeField] private float minX;
    [SerializeField] private float maxX;
    [SerializeField] private float minY;
    [SerializeField] private float maxY;

    [Header("Player Screen Position (0 = левый/нижний край, 1 = правый/верхний край)")]
    [Range(0f, 1f)][SerializeField] private float xScreenPos = 0.5f; // по умолчанию центр
    [Range(0f, 1f)][SerializeField] private float yScreenPos = 0.5f; // по умолчанию центр

    private Camera cam;
    private float camHalfWidth;
    private float camHalfHeight;

    private float lookAheadFactorRight;
    private float lookAheadFactorLeft;
    private float currentLookAhead;

    private bool followY = true;
    private float frozenY;

    private void Start()
    {
        cam = Camera.main;
        camHalfWidth = cam.orthographicSize * cam.aspect;
        camHalfHeight = cam.orthographicSize;

        UpdateLookAheadFactors();
        currentLookAhead = lookAheadFactorRight;
        frozenY = transform.position.y;
    }

    private void LateUpdate()
    {
        if (!player) return;

        // обновляем lookAhead в реальном времени, если меняем xScreenPos в инспекторе
        UpdateLookAheadFactors();

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        // --- направление движения по X ---
        if (rb.linearVelocity.x > 0.1f)
            currentLookAhead = lookAheadFactorRight;
        else if (rb.linearVelocity.x < -0.1f)
            currentLookAhead = lookAheadFactorLeft;

        // --- X ---
        float offsetX = (currentLookAhead - 0.5f) * 2f * camHalfWidth;
        float targetX = player.position.x + offsetX;
        float smoothX = Mathf.Lerp(transform.position.x, targetX, Time.deltaTime * smoothSpeed);
        smoothX = Mathf.Clamp(smoothX, minX + camHalfWidth, maxX - camHalfWidth);

        // --- Y ---
        float smoothY;
        if (followY)
        {
            float offsetY = (yScreenPos - 0.5f) * 2f * camHalfHeight;
            float targetY = player.position.y + offsetY;

            smoothY = Mathf.Lerp(transform.position.y, targetY, Time.deltaTime * smoothSpeed);
            smoothY = Mathf.Clamp(smoothY, minY + camHalfHeight, maxY - camHalfHeight);

            frozenY = smoothY;
        }
        else
        {
            smoothY = frozenY;
        }

        transform.position = new Vector3(smoothX, smoothY, transform.position.z);
    }

    private void UpdateLookAheadFactors()
    {
        lookAheadFactorRight = xScreenPos;
        lookAheadFactorLeft = 1f - xScreenPos;
    }

    // Управление из CameraZone
    public void SetYFollow(bool value)
    {
        followY = value;

        if (!followY && player != null)
        {
            float offsetY = (yScreenPos - 0.5f) * 2f * camHalfHeight;
            frozenY = Mathf.Clamp(player.position.y + offsetY, minY + camHalfHeight, maxY - camHalfHeight);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(minX, -100, 0), new Vector3(minX, 100, 0));
        Gizmos.DrawLine(new Vector3(maxX, -100, 0), new Vector3(maxX, 100, 0));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(-100, minY, 0), new Vector3(100, minY, 0));
        Gizmos.DrawLine(new Vector3(-100, maxY, 0), new Vector3(100, maxY, 0));
    }
}
