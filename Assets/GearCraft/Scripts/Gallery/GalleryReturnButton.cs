using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace GearCraft.Gallery
{
    [RequireComponent(typeof(Button))]
    public sealed class GalleryReturnButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private GalleryDebugUnlockPanel debugPanel;
        private int lastBackFrame = -1;

        private void OnEnable()
        {
            GalleryNavigation.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => GalleryNavigation.Changed -= Refresh;

        private void LateUpdate()
        {
            // Run after the source scene's Update handlers, so this press cannot also
            // close its menu or quit the title when the source scene is restored.
            if (CanGoBack() && IsCancelPressed()) Back();
        }

        private bool CanGoBack() => isActiveAndEnabled && !GalleryNavigation.IsBusy
            && gameObject.scene.name == GalleryNavigation.SceneName
            && SceneManager.GetActiveScene() == gameObject.scene;

        private static bool IsCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var module = EventSystem.current != null
                ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
            if (module != null)
            {
                var action = module.cancel != null ? module.cancel.action : null;
                // Honor the scene's configured binding, even when focus is on a scrollbar
                // or another Selectable that does not implement ICancelHandler.
                return action != null && action.enabled && action.WasPerformedThisFrame();
            }
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        public void Back()
        {
            if (!CanGoBack() || lastBackFrame == Time.frameCount) return;
            lastBackFrame = Time.frameCount;
            if (debugPanel != null && debugPanel.IsOpen)
            {
                debugPanel.EndTest();
                return;
            }
            Return();
        }

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
