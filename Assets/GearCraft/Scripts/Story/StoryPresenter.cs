using DG.Tweening;
using UnityEngine;

public class StoryPresenter : MonoBehaviour
{
    [SerializeField] private StorySequenceData sequence;
    [SerializeField] private StoryView view;
    [SerializeField] private SceneTransitionManager sceneTransitionManager;
    [SerializeField] private string fallbackReturnSceneName = "CraftSpace";

    private StoryPlaybackModel model;
    private StoryPageData currentPage;
    private float nextAutoAdvanceTime;
    private bool isLoadingNextScene;

    private void Start()
    {
        model = new StoryPlaybackModel(sequence);
        ShowNextPage();
    }

    private void Update()
    {
        if (isLoadingNextScene)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            AdvanceByInput();
            return;
        }

        if (currentPage != null && Time.unscaledTime >= nextAutoAdvanceTime)
        {
            ShowNextPage();
        }
    }

    private void AdvanceByInput()
    {
        if (view != null && view.IsShowing)
        {
            view.CompleteShow();
        }

        ShowNextPage();
    }

    private void ShowNextPage()
    {
        if (!model.TryMoveNext(out currentPage))
        {
            LoadReturnScene();
            return;
        }

        Tween tween = view == null ? null : view.Show(currentPage);
        float fadeTime = tween == null ? 0f : view.FadeDuration;
        nextAutoAdvanceTime = Time.unscaledTime + fadeTime + currentPage.AutoAdvanceSeconds;
    }

    private void LoadReturnScene()
    {
        isLoadingNextScene = true;

        string returnSceneName = string.IsNullOrWhiteSpace(StoryPlaybackRequest.ReturnSceneName)
            ? fallbackReturnSceneName
            : StoryPlaybackRequest.ReturnSceneName;

        StoryPlaybackRequest.ClearReturnScene();

        if (sceneTransitionManager != null)
        {
            sceneTransitionManager.LoadScene(returnSceneName);
            return;
        }

        SceneTransitionManager.LoadSceneWithTransition(returnSceneName);
    }
}
