using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace GearCraft.Gallery
{
    // Keep the originating scene loaded so returning never restarts a day or a battle.
    public static class GalleryNavigation
    {
        public const string SceneName = "Gallery";
        private static Scene sourceScene;
        private static float previousTimeScale;
        private static GameObject previousSelection;
        private static readonly List<Behaviour> suspended = new List<Behaviour>();
        private static readonly List<AudioSource> pausedAudio = new List<AudioSource>();

        public static bool IsOpen { get; private set; }
        public static bool IsBusy { get; private set; }
        public static string ReturnSceneName => IsOpen && sourceScene.IsValid() ? sourceScene.name : "Title";
        public static event Action Changed;

        public static void Open(Scene origin)
        {
            if (IsBusy || IsOpen || !GalleryProgress.IsUnlocked || !origin.IsValid()
                || origin != SceneManager.GetActiveScene() || origin.name == SceneName) return;
            OpenAsync(origin).Forget();
        }

        private static async UniTask OpenAsync(Scene origin)
        {
            IsBusy = true;
            IsOpen = true;
            sourceScene = origin;
            previousTimeScale = Time.timeScale;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Changed?.Invoke();
            try
            {
                SuspendSource();
                Time.timeScale = 0f;
                await SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(SceneName));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                RestoreSource();
            }
            finally
            {
                IsBusy = false;
                Changed?.Invoke();
            }
        }

        public static void Return()
        {
            if (IsBusy) return;
            if (!IsOpen || !sourceScene.IsValid() || !sourceScene.isLoaded)
            {
                Time.timeScale = 1f;
                SceneTransitionManager.LoadSceneWithTransition("Title");
                return;
            }
            ReturnAsync().Forget();
        }

        private static async UniTask ReturnAsync()
        {
            IsBusy = true;
            Changed?.Invoke();
            try
            {
                Scene gallery = SceneManager.GetSceneByName(SceneName);
                SceneManager.SetActiveScene(sourceScene);
                if (gallery.IsValid() && gallery.isLoaded) await SceneManager.UnloadSceneAsync(gallery);
                RestoreSource();
            }
            finally
            {
                IsBusy = false;
                Changed?.Invoke();
            }
        }

        private static void SuspendSource()
        {
            foreach (Behaviour behaviour in UnityEngine.Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool persistentInput = behaviour is MaterialManager || behaviour is ApplicationQuitter;
                if (!behaviour.enabled || (behaviour.gameObject.scene != sourceScene && !persistentInput)) continue;
                // Do not deactivate roots/AI objects: that cancels their coroutines and boss attack sequences.
                if (persistentInput || behaviour is Camera || behaviour is Canvas || behaviour is AudioListener
                    || behaviour is EventSystem || behaviour is BaseRaycaster || behaviour is PlayerController
                    || behaviour is EnemyController || behaviour is ArmRotation || behaviour is PlayerMovement
                    || behaviour is PauseManager || behaviour is PauseOnPanelActive || behaviour is UIHotkeyController
                    || behaviour is UIRangeDisplay || behaviour is GearCraft.Scripts.Main.MainIdleHelpController)
                {
                    suspended.Add(behaviour);
                    behaviour.enabled = false;
                }
            }
            foreach (AudioSource audio in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (audio.gameObject.scene != sourceScene || !audio.isPlaying) continue;
                pausedAudio.Add(audio);
                audio.Pause();
            }
        }

        private static void RestoreSource()
        {
            foreach (Behaviour behaviour in suspended) if (behaviour != null) behaviour.enabled = true;
            foreach (AudioSource audio in pausedAudio) if (audio != null) audio.UnPause();
            suspended.Clear();
            pausedAudio.Clear();
            Time.timeScale = previousTimeScale;
            IsOpen = false;
            if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            previousSelection = null;
            sourceScene = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            sourceScene = default;
            previousSelection = null;
            suspended.Clear();
            pausedAudio.Clear();
            IsOpen = false;
            IsBusy = false;
            Changed = null;
        }
    }
}
