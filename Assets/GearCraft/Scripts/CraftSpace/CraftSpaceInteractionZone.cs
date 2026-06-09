using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CraftSpaceInteractionZone : MonoBehaviour
{
    [SerializeField] private CraftSpaceInteractionAction actionType;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private UIRangeDisplay interactionController;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private float promptVerticalOffset = 0.4f;
    [SerializeField] private CraftSpaceFurnitureOutline furnitureOutline;

    public CraftSpaceInteractionAction ActionType => actionType;
    public GameObject PromptObject => promptObject;

    private RectTransform promptRect;
    private Renderer anchorRenderer;
    private bool isPromptActive;

    private void Awake()
    {
        Collider2D zoneCollider = GetComponent<Collider2D>();
        zoneCollider.isTrigger = true;
        anchorRenderer = GetComponent<Renderer>();
        furnitureOutline = furnitureOutline != null ? furnitureOutline : GetComponent<CraftSpaceFurnitureOutline>();
        if (furnitureOutline == null && GetComponent<SpriteRenderer>() != null)
        {
            furnitureOutline = gameObject.AddComponent<CraftSpaceFurnitureOutline>();
        }

        if (interactionController == null)
        {
            interactionController = FindFirstObjectByType<UIRangeDisplay>();
        }

        if (promptObject != null)
        {
            promptRect = promptObject.GetComponent<RectTransform>();
            targetCanvas = targetCanvas != null ? targetCanvas : promptObject.GetComponentInParent<Canvas>(true);
        }

        targetCamera = targetCamera != null ? targetCamera : Camera.main;

        SetPromptActive(false);
    }

    private void LateUpdate()
    {
        if (!isPromptActive)
        {
            return;
        }

        UpdatePromptPosition();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        interactionController?.RegisterZone(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        interactionController?.UnregisterZone(this);
    }

    public void SetPromptActive(bool active)
    {
        isPromptActive = active;
        furnitureOutline?.SetVisible(active);

        if (promptObject != null)
        {
            if (active)
            {
                UpdatePromptPosition();
            }

            promptObject.SetActive(active);
        }
    }

    private void UpdatePromptPosition()
    {
        if (promptRect == null || targetCanvas == null)
        {
            return;
        }

        Camera worldCamera = GetTargetCamera();
        if (worldCamera == null)
        {
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(GetPromptWorldPosition());
        RectTransform canvasRect = targetCanvas.transform as RectTransform;

        if (targetCanvas.renderMode == RenderMode.WorldSpace)
        {
            promptRect.position = worldCamera.ScreenToWorldPoint(screenPosition);
            return;
        }

        Camera uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
        {
            promptRect.anchoredPosition = localPoint;
        }
    }

    private Vector3 GetPromptWorldPosition()
    {
        if (anchorRenderer != null)
        {
            Bounds bounds = anchorRenderer.bounds;
            return new Vector3(bounds.center.x, bounds.max.y + promptVerticalOffset, transform.position.z);
        }

        return transform.position + Vector3.up * promptVerticalOffset;
    }

    private Camera GetTargetCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        return targetCamera;
    }
}
