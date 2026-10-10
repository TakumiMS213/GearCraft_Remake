using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GearCraft.Gallery
{
    [RequireComponent(typeof(Button))]
    public sealed class GalleryReturnButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private GalleryDebugUnlockPanel debugPanel;

        private void OnEnable()
        {
            GalleryNavigation.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => GalleryNavigation.Changed -= Refresh;

        private void Refresh()
        {
            GetComponent<Button>().interactable = !GalleryNavigation.IsBusy;
            if (label != null) label.text = GalleryNavigation.ReturnSceneName == "Title" ? "タイトルへ戻る" : "メニューに戻る";
        }

        public void Return()
        {
            if (debugPanel != null && debugPanel.IsOpen) debugPanel.EndTest();
            GalleryNavigation.Return();
        }
    }
}
