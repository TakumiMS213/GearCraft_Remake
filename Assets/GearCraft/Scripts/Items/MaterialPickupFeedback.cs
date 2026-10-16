using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GearCraft.Scripts.Items
{
    public sealed class MaterialPickupFeedback : MonoBehaviour
    {
        [SerializeField] private RectTransform popupLayer;
        [SerializeField] private RectTransform popupTemplate;
        [SerializeField] private float duration = 1f;
        [SerializeField] private float headPadding = 14f;
        [SerializeField] private float riseDistance = 30f;
        [SerializeField] private float rowSpacing = 52f;
        [SerializeField] private int maxVisible = 6;

        public static MaterialPickupFeedback Instance { get; private set; }

        private sealed class Popup
        {
            public RectTransform rect;
            public Image icon;
            public TMP_Text quantity;
            public CanvasGroup group;
            public Transform player;
            public Collider2D body;
            public float age;
        }

        private readonly List<Popup> active = new List<Popup>();
        private readonly Stack<Popup> available = new Stack<Popup>();
        private Canvas canvas;
        private Camera worldCamera;

        private void OnEnable()
        {
            Instance = this;
            canvas = GetComponentInParent<Canvas>();
            worldCamera = Camera.main;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            for (int i = active.Count - 1; i >= 0; i--) Release(i);
        }

        public void Show(Transform player, Sprite icon, int amount)
        {
            if (player == null || icon == null || amount <= 0 || popupTemplate == null || popupLayer == null || canvas == null)
                return;

            if (active.Count >= Mathf.Max(1, maxVisible)) Release(0);
            Popup popup;
            if (available.Count > 0) popup = available.Pop();
            else
            {
                RectTransform rect = Instantiate(popupTemplate, popupLayer);
                rect.name = "MaterialPickupPopup";
                popup = new Popup
                {
                    rect = rect,
                    icon = rect.Find("Icon").GetComponent<Image>(),
                    quantity = rect.Find("Quantity").GetComponent<TMP_Text>(),
                    group = rect.GetComponent<CanvasGroup>()
                };
            }

            popup.player = player;
            popup.body = player.GetComponent<Collider2D>();
            popup.age = 0f;
            popup.icon.sprite = icon;
            popup.quantity.text = $"+{amount}";
            popup.group.alpha = 1f;
            popup.rect.gameObject.SetActive(true);
            active.Add(popup);
            UpdatePositions();
        }

        private void LateUpdate()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                active[i].age += Time.deltaTime;
                if (active[i].player == null || active[i].age >= Mathf.Max(0.1f, duration)) Release(i);
            }
            UpdatePositions();
        }

        private void UpdatePositions()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null || canvas == null || popupLayer == null) return;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            for (int i = 0; i < active.Count; i++)
            {
                Popup popup = active[i];
                if (popup.player == null) continue;
                Vector3 head = popup.player.position;
                head.y = popup.body != null ? popup.body.bounds.max.y : head.y + 1.2f;
                Vector3 screen = worldCamera.WorldToScreenPoint(head);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, screen, uiCamera, out Vector2 position))
                    continue;
                float progress = Mathf.Clamp01(popup.age / Mathf.Max(0.1f, duration));
                position.y += headPadding + popup.rect.rect.height * 0.5f + riseDistance * progress + (active.Count - 1 - i) * rowSpacing;
                float halfWidth = popup.rect.rect.width * 0.5f;
                position.x = Mathf.Clamp(position.x, popupLayer.rect.xMin + halfWidth, popupLayer.rect.xMax - halfWidth);
                popup.rect.anchoredPosition = position;
                popup.group.alpha = screen.z > 0f ? 1f - Mathf.InverseLerp(0.55f, 1f, progress) : 0f;
            }
        }

        private void Release(int index)
        {
            Popup popup = active[index];
            active.RemoveAt(index);
            popup.rect.gameObject.SetActive(false);
            popup.player = null;
            popup.body = null;
            available.Push(popup);
        }
    }
}
