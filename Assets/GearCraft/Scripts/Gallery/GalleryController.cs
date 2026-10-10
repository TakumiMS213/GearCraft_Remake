using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GearCraft.Gallery
{
    [RequireComponent(typeof(Canvas))]
    public sealed class GalleryController : MonoBehaviour
    {
        [SerializeField] private GalleryCatalogSO catalog;

        [System.Serializable]
        private sealed class Row
        {
            public GalleryListButton Button;
            public TMP_Text Label;
            public CanvasGroup Group;
            public GameObject Selection;
        }

        [Header("Scene Objects")]
        [SerializeField] private RectTransform root;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Image artwork;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text category;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text designation;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text unknown;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private TMP_Text count;
        [SerializeField] private TMP_Text accessState;
        [Tooltip("Scene buttons in the catalogue's level 1 / level 2 / level 3 / weapon order.")]
        [SerializeField] private List<Row> rows = new List<Row>();

        private List<GalleryEntry> entries;
        private GalleryProgressData shownProgress;
        private GalleryProgressData previewProgress;
        private bool initialized;
        private int selectedIndex = -1;

        private void Awake()
        {
            entries = catalog != null ? catalog.GetOrderedEntries() : new List<GalleryEntry>();
            if (!HasSceneReferences())
            {
                Debug.LogError("Gallery scene references are incomplete. Assign the UI objects and catalogue rows in the Inspector.", this);
                enabled = false;
                return;
            }

            // Click events and navigation are saved in the scene; only focus callbacks need binding.
            for (int i = 0; i < rows.Count; i++)
            {
                int index = i;
                rows[i].Button.Focused = () => SelectEntry(index);
            }
            initialized = true;
            Refresh();
        }

        private bool HasSceneReferences()
        {
            if (catalog == null || root == null || scroll == null || scroll.viewport == null || scroll.content == null
                || artwork == null || title == null || category == null || subtitle == null || designation == null
                || description == null || unknown == null || caption == null
                || count == null || accessState == null || rows == null || rows.Count != entries.Count)
                return false;

            foreach (Row row in rows)
                if (row == null || row.Button == null || row.Label == null || row.Group == null || row.Selection == null)
                    return false;
            return true;
        }

        private void OnEnable()
        {
            GalleryProgress.Changed += Refresh;
            if (initialized) Refresh();
        }

        private void OnDisable() => GalleryProgress.Changed -= Refresh;

        private void Start()
        {
            if (shownProgress != null && shownProgress.hasReachedEnding && selectedIndex >= 0 && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(rows[selectedIndex].Button.gameObject);
        }

        public void Refresh() => Refresh(previewProgress ?? GalleryProgress.Data);

        public void SetPreviewProgress(GalleryProgressData progress)
        {
            previewProgress = progress;
            Refresh();
        }

        public void ClearPreviewProgress()
        {
            previewProgress = null;
            Refresh();
        }

        /// <summary>Render a progress snapshot. This does not grant discoveries or write save data.</summary>
        public void Refresh(GalleryProgressData progress)
        {
            if (!initialized) return;
            shownProgress = progress ?? new GalleryProgressData();
            int discovered = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                bool known = entries[i].IsDiscovered(shownProgress);
                rows[i].Label.text = known ? entries[i].DisplayName : "？";
                rows[i].Button.interactable = shownProgress.hasReachedEnding;
                rows[i].Group.alpha = known ? 1f : 0.45f;
                if (known) discovered++;
            }
            count.text = $"発見 {discovered} / {entries.Count}";
            accessState.text = shownProgress.hasReachedEnding ? "敵機・武装記録" : "未開放";
            if (selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                selectedIndex = entries.FindIndex(entry => entry.IsDiscovered(shownProgress));
                if (selectedIndex < 0 && entries.Count > 0) selectedIndex = 0;
            }
            ShowEntry(selectedIndex);
        }

        public void SelectEntry(int index)
        {
            if (!initialized || !isActiveAndEnabled) return;
            if (shownProgress == null || !shownProgress.hasReachedEnding || index < 0 || index >= entries.Count) return;
            ShowEntry(index);
            // Keep controller/keyboard focus visible when moving beyond the viewport.
            Canvas.ForceUpdateCanvases();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, rows[index].Button.transform);
            Rect viewport = scroll.viewport.rect;
            float offset = bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y
                : bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : 0f;
            if (Mathf.Abs(offset) > 0.1f)
            {
                scroll.StopMovement();
                Vector2 position = scroll.content.anchoredPosition;
                position.y = Mathf.Clamp(position.y + offset, 0f, Mathf.Max(0f, scroll.content.rect.height - viewport.height));
                scroll.content.anchoredPosition = position;
            }
        }

        private void ShowEntry(int index)
        {
            selectedIndex = index;
            bool unlocked = shownProgress != null && shownProgress.hasReachedEnding;
            GalleryEntry entry = index >= 0 && index < entries.Count ? entries[index] : null;
            bool known = entry != null && entry.IsDiscovered(shownProgress);
            for (int i = 0; i < rows.Count; i++) rows[i].Selection.SetActive(unlocked && i == index);

            category.text = unlocked && entry != null ? GalleryEntry.CategoryName(entry.category) : "GALLERY";
            title.text = !unlocked ? "未開放" : known ? entry.DisplayName : "？";
            // Discovery also gates the text: a previously selected entry must not leak into an unknown slot.
            subtitle.text = known ? entry.subtitle : string.Empty;
            designation.text = known ? entry.designation : string.Empty;
            description.text = known ? entry.description : string.Empty;
            // Clear the previous sprite as well as hiding it, so unknown slots cannot leak artwork.
            artwork.sprite = known ? entry.Artwork : null;
            artwork.enabled = artwork.sprite != null;
            unknown.gameObject.SetActive(!artwork.enabled);
            unknown.text = known ? "画像未登録" : "？";
            unknown.fontSize = known ? 32f : 180f;
            caption.text = !unlocked ? "いずれかのエンディングに到達すると開放されます。"
                : entry == null ? "記録はまだありません。"
                : known ? (entry.enemy != null ? "遭遇済み" : entry.recipe != null ? "作成済み" : "入手済み")
                : entry.enemy != null ? "まだ遭遇していません。"
                : entry.recipe != null ? "まだ作成していません。" : "まだ入手していません。";
        }

    }
}
