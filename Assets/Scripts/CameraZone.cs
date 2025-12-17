using UnityEngine;

public class CameraZone : MonoBehaviour
{
    [SerializeField] private bool enableYFollow;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) // у игрока должен быть тег Player
        {
            Debug.Log($"[CameraZone] Player вошёл в зону {gameObject.name}, enableYFollow = {enableYFollow}");

            CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
            if (cam != null)
            {
                cam.SetYFollow(enableYFollow);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[CameraZone] Player вышел из зоны {gameObject.name}");
        }
    }
}
