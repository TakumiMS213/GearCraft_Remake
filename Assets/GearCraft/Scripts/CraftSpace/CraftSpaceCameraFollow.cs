using DG.Tweening;
using UnityEngine;

public class CraftSpaceCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private SpriteRenderer horizontalBoundsSource;
    [SerializeField] private Vector2 offset;
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = false;
    [SerializeField, Min(0f)] private float smoothTime = 0.18f;
    [SerializeField] private bool clampX = true;
    [SerializeField] private Vector2 xLimits = new Vector2(-5.5f, 5.5f);
    [SerializeField] private Transform[] moveWithCamera;

    private Camera followCamera;
    private Vector3 velocity;
    private Vector3 lastCameraPosition;

    private void Awake()
    {
        followCamera = GetComponent<Camera>();
        lastCameraPosition = transform.position;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (horizontalBoundsSource == null)
        {
            GameObject wall = GameObject.Find("World_Wall");
            if (wall != null)
            {
                horizontalBoundsSource = wall.GetComponent<SpriteRenderer>();
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null || ArrivalMessagePresenter.IsCameraPresentationPlaying)
        {
            MoveSyncedObjectsByCameraDelta();
            return;
        }

        Vector3 current = transform.position;
        Vector3 desired = current;

        if (followX)
        {
            desired.x = target.position.x + offset.x;
            if (clampX)
            {
                desired.x = Mathf.Clamp(desired.x, GetMinCameraX(), GetMaxCameraX());
            }
        }

        if (followY)
        {
            desired.y = target.position.y + offset.y;
        }

        transform.position = smoothTime <= 0f
            ? desired
            : Vector3.SmoothDamp(current, desired, ref velocity, smoothTime);

        MoveSyncedObjectsByCameraDelta();
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        Vector3 position = transform.position;
        if (followX)
        {
            position.x = target.position.x + offset.x;
            if (clampX)
            {
                position.x = Mathf.Clamp(position.x, GetMinCameraX(), GetMaxCameraX());
            }
        }

        if (followY)
        {
            position.y = target.position.y + offset.y;
        }

        transform.DOKill();
        transform.position = position;
        velocity = Vector3.zero;
        MoveSyncedObjectsByCameraDelta();
    }

    private void MoveSyncedObjectsByCameraDelta()
    {
        Vector3 cameraDelta = transform.position - lastCameraPosition;
        lastCameraPosition = transform.position;

        if (cameraDelta == Vector3.zero || moveWithCamera == null)
        {
            return;
        }

        foreach (Transform syncedObject in moveWithCamera)
        {
            if (syncedObject != null)
            {
                syncedObject.position += cameraDelta;
            }
        }
    }

    private float GetMinCameraX()
    {
        if (!TryGetHorizontalCameraLimits(out float minX, out _))
        {
            return xLimits.x;
        }

        return minX;
    }

    private float GetMaxCameraX()
    {
        if (!TryGetHorizontalCameraLimits(out _, out float maxX))
        {
            return xLimits.y;
        }

        return maxX;
    }

    private bool TryGetHorizontalCameraLimits(out float minX, out float maxX)
    {
        minX = xLimits.x;
        maxX = xLimits.y;

        if (horizontalBoundsSource == null || followCamera == null || !followCamera.orthographic)
        {
            return false;
        }

        Bounds bounds = horizontalBoundsSource.bounds;
        float halfViewWidth = followCamera.orthographicSize * followCamera.aspect;

        minX = bounds.min.x + halfViewWidth;
        maxX = bounds.max.x - halfViewWidth;

        if (minX > maxX)
        {
            float centerX = bounds.center.x;
            minX = centerX;
            maxX = centerX;
        }

        return true;
    }
}
