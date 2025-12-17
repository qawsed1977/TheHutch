using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ParallaxController : MonoBehaviour
{
    [Serializable]
    public struct LayerEntry
    {
        [Tooltip("Имя Sorting Layer (например: SuperBG, BG, Tiles, Player, FG)")]
        public string sortingLayerName;

        [Tooltip("Parallax factor: 0 = static (не двигается), 1 = движется вместе с камерой.")]
        public Vector2 parallaxFactor;
    }

    [Header("Настройка слоёв (по имени Sorting Layer)")]
    [SerializeField] private LayerEntry[] layers = new LayerEntry[0];

    [Header("Игнорировать объекты")]
    [Tooltip("Если объект имеет любой из этих тегов — он не будет двигаться параллаксом.")]
    [SerializeField] private string[] excludedTags = new string[] { /* например: "UI", "IgnoreParallax" */ };

    [Tooltip("Если true — объекты с компонентом PlayerMovement будут игнорироваться (т.е. сам игрок).")]
    [SerializeField] private bool excludePlayerWithComponent = true;

    // internal
    private class ParallaxObject { public Transform t; public Vector3 initialPos; }
    private Dictionary<string, List<ParallaxObject>> grouped = new Dictionary<string, List<ParallaxObject>>();

    private Camera cam;
    private Vector3 camStartPos;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        camStartPos = cam.transform.position;
    }

    private void Start()
    {
        Refresh();
    }

    /// <summary>Rescan scene for SpriteRenderers and group them by sortingLayerName matching 'layers'.</summary>
    [ContextMenu("Refresh Parallax Objects")]
    public void Refresh()
    {
        grouped.Clear();

        // подготовим словарь ключей
        for (int i = 0; i < layers.Length; i++)
        {
            string name = layers[i].sortingLayerName;
            if (!string.IsNullOrEmpty(name) && !grouped.ContainsKey(name))
                grouped.Add(name, new List<ParallaxObject>());
        }

        // нормализуем excludedTags: убираем пустые/пробельные элементы
        var cleanExcluded = new List<string>();
        if (excludedTags != null)
        {
            foreach (var t in excludedTags)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    cleanExcluded.Add(t.Trim());
            }
        }

        // найдём все SpriteRenderer (включая неактивные)
        #if UNITY_2023_1_OR_NEWER
        var sprites = GameObject.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        #else
                var sprites = FindObjectsOfType<SpriteRenderer>(true);
        #endif

        foreach (var sr in sprites)
        {
            if (sr == null || sr.gameObject == null) continue;

            // игнор: если объект - дочерний от камеры (например UI внутри камеры) - пропускаем
            if (sr.transform.IsChildOf(transform)) continue;

            // игнор по тегу — безопасное сравнение строкой (не используем CompareTag)
            bool skipByTag = false;
            if (cleanExcluded.Count > 0)
            {
                string objTag = sr.gameObject.tag; // безопасно возвращает "Untagged" если ничего не назначено
                foreach (var t in cleanExcluded)
                {
                    if (string.Equals(objTag, t, StringComparison.Ordinal))
                    {
                        skipByTag = true;
                        break;
                    }
                }
            }
            if (skipByTag) continue;

            // игнор игрока по компоненту (если включено)
            if (excludePlayerWithComponent)
            {
                if (sr.GetComponentInParent<PlayerMovement>() != null) continue;
            }

            string layerName = sr.sortingLayerName;
            if (grouped.ContainsKey(layerName))
            {
                grouped[layerName].Add(new ParallaxObject()
                {
                    t = sr.transform,
                    initialPos = sr.transform.position
                });
            }
        }
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        Vector3 camDelta = cam.transform.position - camStartPos;

        for (int i = 0; i < layers.Length; i++)
        {
            string name = layers[i].sortingLayerName;
            if (string.IsNullOrEmpty(name)) continue;
            if (!grouped.TryGetValue(name, out var list) || list == null) continue;

            Vector2 factor = layers[i].parallaxFactor;
            Vector3 offset = new Vector3(camDelta.x * factor.x, camDelta.y * factor.y, 0f);

            for (int j = 0; j < list.Count; j++)
            {
                var pobj = list[j];
                if (pobj == null || pobj.t == null) continue;
                pobj.t.position = pobj.initialPos + offset;
            }
        }
    }

    /// <summary> Reset initial positions to current transforms (useful after editing in Scene while running or moving background objects manually) </summary>
    [ContextMenu("Reset Initial Positions")]
    public void ResetInitialPositions()
    {
        foreach (var kv in grouped)
            foreach (var pobj in kv.Value)
                if (pobj != null && pobj.t != null)
                    pobj.initialPos = pobj.t.position;

        camStartPos = cam.transform.position;
    }

    /// <summary> Convenience: update or add layer entry at runtime. </summary>
    public void SetLayer(string sortingLayerName, Vector2 factor)
    {
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].sortingLayerName == sortingLayerName)
            {
                layers[i].parallaxFactor = factor;
                Refresh();
                return;
            }
        }
        var tmp = new List<LayerEntry>(layers) { new LayerEntry { sortingLayerName = sortingLayerName, parallaxFactor = factor } };
        layers = tmp.ToArray();
        Refresh();
    }
}
