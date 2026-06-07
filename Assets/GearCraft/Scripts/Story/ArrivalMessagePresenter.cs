using DG.Tweening;
using UnityEngine;

public class ArrivalMessagePresenter : MonoBehaviour
{
    public static bool IsMessagePlaying { get; private set; }
    public static bool IsCameraPresentationPlaying { get; private set; }

    [SerializeField] private ArrivalMessageSequenceData sequence;
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

    private void Start()
    {
        if (requireStoryArrivalRequest && !StoryPlaybackRequest.ConsumeArrivalMessageRequest())
        {
            IsMessagePlaying = false;
            IsCameraPresentationPlaying = false;
            return;
        }

        targetCamera = targetCamera != null ? targetCamera : Camera.main;
        view = view != null ? view : GetComponent<ArrivalMessageView>();
        model = new ArrivalMessageModel(sequence);
        isPlaying = true;
        IsMessagePlaying = true;
        IsCameraPresentationPlaying = true;
        ShowNextPage();
    }

    private void OnDisable()
    {
        if (isPlaying)
        {
            IsMessagePlaying = false;
            IsCameraPresentationPlaying = false;
        }
    }

    private void Update()
    {
        if (!isPlaying)
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
        if (currentTween != null && currentTween.IsActive())
        {
            currentTween.Complete();
        }

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
            Finish();
            return;
        }

        currentTween?.Kill();
        Sequence pageTween = DOTween.Sequence();

        if (currentPage.MoveCamera && targetCamera != null)
        {
            Vector3 cameraPosition = currentPage.CameraPosition;
            cameraPosition.z = targetCamera.transform.position.z;
            pageTween.Join(targetCamera.transform.DOMove(cameraPosition, currentPage.CameraMoveDuration).SetEase(Ease.InOutQuad));
            pageTween.Join(targetCamera.DOOrthoSize(currentPage.CameraOrthographicSize, currentPage.CameraMoveDuration).SetEase(Ease.InOutQuad));
        }

        Tween viewTween = view == null ? null : view.Show(currentPage);
        if (viewTween != null)
        {
            pageTween.Join(viewTween);
        }

        currentTween = pageTween;
        currentTween.SetUpdate(true);
        float transitionSeconds = Mathf.Max(currentPage.CameraMoveDuration, view == null ? 0f : view.FadeDuration);
        nextAutoAdvanceTime = Time.unscaledTime + transitionSeconds + currentPage.AutoAdvanceSeconds;
    }

    private void Finish()
    {
        isPlaying = false;
        IsMessagePlaying = false;
        currentTween?.Kill();
        view?.Hide();

        if (!restoreCameraOnFinish || targetCamera == null)
        {
            IsCameraPresentationPlaying = false;
            return;
        }

        Vector3 cameraPosition = finishCameraPosition;
        cameraPosition.z = targetCamera.transform.position.z;
        DOTween.Sequence()
            .Join(targetCamera.transform.DOMove(cameraPosition, finishCameraMoveDuration).SetEase(Ease.InOutQuad))
            .Join(targetCamera.DOOrthoSize(finishOrthographicSize, finishCameraMoveDuration).SetEase(Ease.InOutQuad))
            .SetUpdate(true)
            .OnComplete(() => IsCameraPresentationPlaying = false);
    }
}
