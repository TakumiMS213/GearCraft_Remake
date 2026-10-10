using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GearCraft.Gallery
{
    [RequireComponent(typeof(Button))]
    public sealed class GalleryEntranceButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string unlockedText = "GALLERY";
        [SerializeField] private GameObject lockedTooltip;
        private Button button;
        private bool pointerInside;

        private void Awake() => button = GetComponent<Button>();

        private void OnEnable()
        {
            GalleryProgress.Changed += Refresh;
            GalleryNavigation.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GalleryProgress.Changed -= Refresh;
            GalleryNavigation.Changed -= Refresh;
            pointerInside = false;
            if (lockedTooltip != null) lockedTooltip.SetActive(false);
        }

        private void Refresh()
        {
            if (button == null) button = GetComponent<Button>();
            bool unlocked = GalleryProgress.IsUnlocked;
            button.interactable = unlocked && !GalleryNavigation.IsBusy;
            if (label != null) label.text = unlocked ? unlockedText : unlockedText + "\n<size=48%>ED到達後に開放</size>";
            RefreshTooltip();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            RefreshTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            RefreshTooltip();
        }

        private void RefreshTooltip()
        {
            if (lockedTooltip != null)
                lockedTooltip.SetActive(pointerInside && !GalleryProgress.IsUnlocked &&
                    !GalleryNavigation.IsBusy && !GalleryNavigation.IsOpen);
        }

        public void OpenGallery() => GalleryNavigation.Open(gameObject.scene);
    }
}
