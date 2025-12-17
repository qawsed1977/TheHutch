using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PlayerCombat component (или объект игрока). Если пусто, попытается найти Player в сцене.")]
    public PlayerCombat playerCombat;

    [Header("Heart sprites")]
    public Sprite heartFull;
    public Sprite heartEmpty;

    [Header("UI Container")]
    [Tooltip("RectTransform куда будут помещаться сердца (обычно дочерний объект Canvas).")]
    public RectTransform heartsContainer;

    [Header("Layout")]
    public float spacing = 4f; // расстояние между сердцами в пикселях
    public Vector2 heartSize = new Vector2(24f, 24f);

    private List<Image> hearts = new List<Image>();

    private void Awake()
    {
        // попытка найти PlayerCombat, если не назначен в инспекторе
        if (playerCombat == null)
        {
            #if UNITY_2023_1_OR_NEWER
            playerCombat = Object.FindFirstObjectByType<PlayerCombat>();
            #else
                        playerCombat = FindObjectOfType<PlayerCombat>();
            #endif

        }

        // создаём контейнер если не назначен
        if (heartsContainer == null)
            CreateDefaultCanvasAndContainer();

        // проверка спрайтов — если не назначены, предупреждаем (и не ломаем)
        if (heartFull == null || heartEmpty == null)
        {
            Debug.LogWarning("HealthUI: heartFull or heartEmpty sprite is not assigned in inspector.", this);
        }

        // Подписываемся на событие (если есть)
        if (playerCombat != null)
        {
            playerCombat.OnHealthChanged += OnHealthChanged;

            // ВАЖНО: сразу инициализируем UI из текущих значений,
            // чтобы не зависеть от порядка вызовов Awake()
            int current = playerCombat.GetCurrentHealth();
            int max = (playerCombat.GetMaxHealth()); // требует геттера в PlayerCombat
            RebuildHearts(max);
            UpdateHearts(current);
        }
    }

    private void OnDestroy()
    {
        if (playerCombat != null)
            playerCombat.OnHealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int current, int max)
    {
        RebuildHearts(max);
        UpdateHearts(current);
    }

    // перестраиваем коллекцию изображений (если max изменился)
    private void RebuildHearts(int max)
    {
        // защитный clamp
        if (max < 0) max = 0;

        // если количество совпадает — ничего не делать
        if (hearts.Count == max) return;

        // удаляем старые
        foreach (var img in hearts)
        {
            if (img != null)
                DestroyImmediate(img.gameObject);
        }
        hearts.Clear();

        // создаём новые
        for (int i = 0; i < max; i++)
        {
            GameObject go = new GameObject("Heart_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(heartsContainer, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = heartSize;

            var img = go.GetComponent<Image>();
            img.sprite = heartEmpty;
            img.preserveAspect = true;

            hearts.Add(img);
        }

        LayoutHearts();
    }

    private void LayoutHearts()
    {
        float x = 0f;
        for (int i = 0; i < hearts.Count; i++)
        {
            var rt = hearts[i].GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = heartSize;
            x += heartSize.x + spacing;
        }

        if (heartsContainer != null)
            heartsContainer.sizeDelta = new Vector2(Mathf.Max(0f, x - spacing), heartSize.y);
    }

    private void UpdateHearts(int current)
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            if (i < current)
                hearts[i].sprite = heartFull;
            else
                hearts[i].sprite = heartEmpty;
        }
    }

    private void CreateDefaultCanvasAndContainer()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject goCanvas = new GameObject("Canvas", typeof(Canvas));
            canvas = goCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            goCanvas.AddComponent<CanvasScaler>();
            goCanvas.AddComponent<GraphicRaycaster>();
        }

        GameObject containerGo = new GameObject("HeartsContainer", typeof(RectTransform));
        containerGo.transform.SetParent(canvas.transform, false);
        var rt = containerGo.GetComponent<RectTransform>();

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(8f, -8f);
        heartsContainer = rt;
    }
}
