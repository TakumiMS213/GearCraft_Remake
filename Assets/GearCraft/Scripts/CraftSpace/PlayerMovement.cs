using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private float moveInput;
    private Vector3 lastPosition;

    public RectTransform[] walls;  // 背景画像6枚
    [SerializeField] private bool enableCanvasParallax = false;
    public float parallaxScale = 1f;  // 全背景共通のパララックス倍率

    public float backgroundMoveThresholdX = 3f;  // このX座標以上で背景が動く

    private Canvas canvas;
    private Animator anim = null;
    private SceneTransitionManager sceneTransitionManager;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        lastPosition = transform.position;

        if (walls != null && walls.Length > 0)
        {
            canvas = walls[0].GetComponentInParent<Canvas>();
        }
        anim = GetComponent<Animator>();
        sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();
    }

    void Update()
    {
        bool blocked = IsInputBlocked();
        moveInput = blocked ? 0f : Input.GetAxisRaw("Horizontal");
        // FixedUpdate does not run at timeScale 0: clear motion immediately when a panel pauses play.
        if (blocked) StopHorizontalMovement();
        if (anim != null) anim.SetBool("run", moveInput != 0f);
        // Flip only the artwork; mirroring the body moves its offset collider at a wall/floor contact.
        if (spriteRenderer != null && moveInput != 0f) spriteRenderer.flipX = moveInput < 0f;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        float input = IsInputBlocked() ? 0f : moveInput;
        rb.linearVelocity = new Vector2(input * moveSpeed, rb.linearVelocity.y);
    }

    private void StopHorizontalMovement()
    {
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void OnDisable()
    {
        moveInput = 0f;
        StopHorizontalMovement();
        if (anim != null) anim.SetBool("run", false);
    }

    private void LateUpdate()
    {
        if (IsInputBlocked())
        {
            lastPosition = transform.position;
            return;
        }

        bool moveBackground = transform.position.x >= backgroundMoveThresholdX;
        if (enableCanvasParallax && moveBackground && walls != null && walls.Length > 0 && canvas != null)
        {
            Vector2 screenDelta = RectTransformUtility.WorldToScreenPoint(Camera.main, transform.position) -
                                  RectTransformUtility.WorldToScreenPoint(Camera.main, lastPosition);

            Vector2 canvasDelta = screenDelta / canvas.scaleFactor;

            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] == null) continue;

                Vector2 newAnchoredPos = walls[i].anchoredPosition;
                // プレイヤーの動きと反対方向に動かす
                newAnchoredPos.x -= canvasDelta.x * parallaxScale;
                walls[i].anchoredPosition = newAnchoredPos;
            }
            
        }

        lastPosition = transform.position;
    }

    private bool IsInputBlocked()
    {
        if (sceneTransitionManager == null)
            sceneTransitionManager = FindFirstObjectByType<SceneTransitionManager>();
        return Time.timeScale <= 0f || QuitConfirmationController.BlocksPlayerInput || RunSaveUI.BlocksGameplay ||
            ArrivalMessagePresenter.IsMessagePlaying || ArrivalMessagePresenter.IsCameraPresentationPlaying ||
            sceneTransitionManager != null && sceneTransitionManager.IsLoading;
    }
}
