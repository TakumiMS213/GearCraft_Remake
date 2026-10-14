using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GearCraft.Gallery
{
    /// <summary>GEAR opens scene-authored controls that preview unlock states without saving.</summary>
    public sealed class GalleryDebugUnlockPanel : MonoBehaviour
    {
        [SerializeField] private GalleryController gallery;
        [SerializeField] private GalleryCatalogSO catalog;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private Button firstButton;

        private bool chordWasHeld;
        private GameObject previousSelection;

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void Update()
        {
            if (gameObject.scene.name != "Gallery" || SceneManager.GetActiveScene().name != "Gallery") return;

            bool chordHeld = IsGearHeld();
            if (chordHeld && !chordWasHeld)
            {
                if (IsOpen) EndTest();
                else Open();
            }
            chordWasHeld = chordHeld;
        }

        private void OnDisable()
        {
            if (IsOpen) EndTest();
            chordWasHeld = false;
        }

        public void Open()
        {
            if (IsOpen || panel == null || gallery == null || catalog == null) return;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            gallery.ClearPreviewProgress();
            panel.SetActive(true);
            SetState("通常の記録");
            if (firstButton != null && EventSystem.current != null) firstButton.Select();
        }

        public void ShowLocked()
        {
            Apply(new GalleryProgressData(), "未開放");
        }

        public void ShowEndingOnly()
        {
            Apply(new GalleryProgressData { hasReachedEnding = true }, "ED到達・未発見");
        }

        public void ShowPartial()
        {
            if (!IsOpen || catalog == null) return;
            var progress = new GalleryProgressData { hasReachedEnding = true };
            GalleryCategory? previousCategory = null;
            foreach (GalleryEntry entry in catalog.GetOrderedEntries())
            {
                if (previousCategory != entry.category) Discover(progress, entry);
                previousCategory = entry.category;
            }
            Apply(progress, "一部解放");
        }

        public void ShowAll()
        {
            if (!IsOpen || catalog == null) return;
            var progress = new GalleryProgressData { hasReachedEnding = true };
            foreach (GalleryEntry entry in catalog.GetOrderedEntries()) Discover(progress, entry);
            Apply(progress, "全解放");
        }

        public void EndTest()
        {
            if (panel != null) panel.SetActive(false);
            if (gallery != null) gallery.ClearPreviewProgress();
            if (EventSystem.current != null)
            {
                Selectable selectable = previousSelection != null ? previousSelection.GetComponent<Selectable>() : null;
                EventSystem.current.SetSelectedGameObject(selectable != null && selectable.IsActive() && selectable.IsInteractable()
                    ? previousSelection : null);
            }
            previousSelection = null;
        }

        private void Apply(GalleryProgressData progress, string state)
        {
            if (!IsOpen || gallery == null) return;
            gallery.SetPreviewProgress(progress);
            SetState(state);
        }

        private void SetState(string state)
        {
            if (stateLabel != null) stateLabel.text = "DEBUG / 解放テスト：" + state;
        }

        private static void Discover(GalleryProgressData progress, GalleryEntry entry)
        {
            if (entry.enemy != null) progress.RecordEnemy(entry.enemy.name);
            else if (entry.weapon != null) progress.RecordWeapon(entry.weapon.name);
            else if (entry.recipe != null) progress.RecordRecipe(entry.recipe.name);
            else progress.RecordMaterial(entry.material);
        }

        private static bool IsGearHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.gKey.isPressed && keyboard.eKey.isPressed
                && keyboard.aKey.isPressed && keyboard.rKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.G) && Input.GetKey(KeyCode.E)
                && Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.R);
#else
            return false;
#endif
        }
    }
}
