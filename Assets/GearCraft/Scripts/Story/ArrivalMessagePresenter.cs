using DG.Tweening;
using UnityEngine;

public class ArrivalMessagePresenter : MonoBehaviour
{
    public static bool IsMessagePlaying { get; private set; }
    public static bool IsCameraPresentationPlaying { get; private set; }

    [SerializeField] private ArrivalMessageSequenceData sequence;
    [SerializeField] private ArrivalMessageSequenceData furnitureSequence;
    // Scene references in page order: gate repair, food, medical, craft table.
    [SerializeField] private Renderer[] furnitureFocusTargets;
    [SerializeField] private ArrivalMessageView view;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool requireStoryArrivalRequest = true;
    [SerializeField] private bool restoreCameraOnFinish = true;
    [SerializeField] private Vector3 finishCameraPosition = new Vector3(0f, 0f, -10f);
    [SerializeField, Min(0.1f)] private float finishOrthographicSize = 5.8f;
    [SerializeField, Min(0f)] private float finishCameraMoveDuration = 0.8f;

    private ArrivalMessageModel model;
    private ArrivalMessagePageData currentPage;
    private Tween currentTween;
    private float nextAutoAdvanceTime;
    private bool isPlaying;
    private bool hasBegun;
    private bool messageVisible;
    private bool ownsPresentation;
    private bool isFurnitureTour;
    private int pageIndex = -1;
    private Vector3 returnCameraPosition;
    private float returnOrthographicSize;
    private DormTaskProgress progress;
    private SceneTransitionManager sceneTransitionManager;

    private void Start()
    {
        targetCamera = targetCamera != null ? targetCamera : Camera.main;
        view = view != null ? view : GetComponent<ArrivalMessageView>();
        progress = StatusManager.Instance != null ? StatusManager.Instance.DormTasks : null;
        isFurnitureTour = progress != null && progress.NeedsFurnitureTutorial;
        if (!isFurnitureTour && requireStoryArrivalRequest && !StoryPlaybackRequest.HasArrivalMessageRequest)
            return;

        if (view == null || !view.IsConfigured || targetCamera == null ||
            (isFurnitureTour ? !HasFurnitureTourReferences() : sequence == null))
        {
            Debug.LogError("宿舎チュートリアルのシーン参照が不足しています。", this);
            return;
        }

        StoryPlaybackRequest.ConsumeArrivalMessageRequest();
        returnCameraPosition = isFurnitureTour ? targetCamera.transform.position : finishCameraPosition;
        returnOrthographicSize = isFurnitureTour ? targetCamera.orthographicSize : finishOrthographicSize;
        model = new ArrivalMessageModel(isFurnitureTour ? furnitureSequence : sequence);
        isPlaying = true;
        ownsPresentation = true;
        IsMessagePlaying = true;
        IsCameraPresentationPlaying = true;
        view.HideImmediate(true);
        // Update waits for the scene's arrival transition to reveal the room first.
    }

    private bool HasFurnitureTourReferences()
    {
        if (furnitureSequence == null || furnitureFocusTargets == null || furnitureFocusTargets.Length != 4)
            return false;
        for (int i = 0; i < 4; i++)
            if (furnitureFocusTargets[i] == null || furnitureSequence.GetPage(i) == null) return false;
        return furnitureSequence.GetPage(4) == null;
    }

    private void OnDisable()
    {
        currentTween?.Kill();
        if (!ownsPresentation) return;
        // Interrupted tours remain pending and can run again on the next dorm visit.
        view?.HideImmediate();
        isPlaying = false;
        ownsPresentation = false;
        IsMessagePlaying = false;
        IsCameraPresentationPlaying = false;
    }

    private void Update()
    {
        if (!isPlaying || IsSceneTransitioning() || Time.timeScale <= 0f) return;
        if (!hasBegun)
        {
            hasBegun = true;
            ShowNextPage();
            return;
        }

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            AdvanceByInput();
            return;
        }

        if (messageVisible && Time.unscaledTime >= nextAutoAdvanceTime) ShowNextPage();
    }

    private void AdvanceByInput()
    {
        if (!isPlaying || IsSceneTransitioning()) return;
        if (!messageVisible)
        {
            // A click during the pan finishes moving, but never skips that furniture's explanation.
            if (currentTween != null && currentTween.IsActive()) currentTween.Complete(true);
            return;
        }
        if (view.IsShowing)
        {
            view.CompleteShow();
            return;
        }
        ShowNextPage();
    }

    private bool IsSceneTransitioning()
    {
        if (sceneTransitionManager == null)
            sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();
        return sceneTransitionManager != null && sceneTransitionManager.IsLoading;
    }

    private void ShowNextPage()
    {
        currentTween?.Kill();
        view.HideImmediate(true);
        messageVisible = false;
        nextAutoAdvanceTime = float.PositiveInfinity;
        if (!model.TryMoveNext(out currentPage))
        {
            Finish();
            return;
        }
        pageIndex++;

        Vector3 cameraPosition = isFurnitureTour
            ? furnitureFocusTargets[pageIndex].bounds.center
            : currentPage.CameraPosition;
        cameraPosition.z = targetCamera.transform.position.z;
        if ((isFurnitureTour || currentPage.MoveCamera) && currentPage.CameraMoveDuration > 0f)
        {
            currentTween = DOTween.Sequence()
                .Append(targetCamera.transform.DOMove(cameraPosition, currentPage.CameraMoveDuration).SetEase(Ease.InOutQuad))
                .Join(targetCamera.DOOrthoSize(currentPage.CameraOrthographicSize, currentPage.CameraMoveDuration).SetEase(Ease.InOutQuad))
                .AppendCallback(ShowCurrentMessage)
                .SetUpdate(true);
        }
        else
        {
            if (isFurnitureTour || currentPage.MoveCamera)
            {
                targetCamera.transform.position = cameraPosition;
                targetCamera.orthographicSize = currentPage.CameraOrthographicSize;
            }
            ShowCurrentMessage();
        }
    }

    private void ShowCurrentMessage()
    {
        view.Show(currentPage);
        messageVisible = true;
        nextAutoAdvanceTime = Time.unscaledTime + view.FadeDuration + currentPage.AutoAdvanceSeconds;
    }

    private void Finish()
    {
        isPlaying = false;
        if (!restoreCameraOnFinish)
        {
            CompletePresentation();
            return;
        }

        Vector3 cameraPosition = returnCameraPosition;
        cameraPosition.z = targetCamera.transform.position.z;
        currentTween = DOTween.Sequence()
            .Append(targetCamera.transform.DOMove(cameraPosition, Mathf.Max(0.01f, finishCameraMoveDuration)).SetEase(Ease.InOutQuad))
            .Join(targetCamera.DOOrthoSize(returnOrthographicSize, Mathf.Max(0.01f, finishCameraMoveDuration)).SetEase(Ease.InOutQuad))
            .SetUpdate(true)
            .OnComplete(CompletePresentation);
    }

    private void CompletePresentation()
    {
        if (isFurnitureTour && progress != null) progress.hasSeenFurnitureTutorial = true;
        view.HideImmediate();
        ownsPresentation = false;
        IsMessagePlaying = false;
        IsCameraPresentationPlaying = false;
    }
}
