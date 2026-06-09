using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private Vector3 lastPosition;

    public RectTransform[] walls;  // 背景画像6枚
    [SerializeField] private bool enableCanvasParallax = false;
    public float parallaxScale = 1f;  // 全背景共通のパララックス倍率

    public float backgroundMoveThresholdX = 3f;  // このX座標以上で背景が動く

    private Canvas canvas;
    private Animator anim = null;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        lastPosition = transform.position;

        if (walls != null && walls.Length > 0)
        {
            canvas = walls[0].GetComponentInParent<Canvas>();
        }
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (ArrivalMessagePresenter.IsMessagePlaying)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if (anim != null)
            {
                anim.SetBool("run", false);
            }

            lastPosition = transform.position;
            return;
        }

        float moveInput = Input.GetAxisRaw("Horizontal");
        bool moveBackground = transform.position.x >= backgroundMoveThresholdX;
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);

        if(moveInput > 0)
        {
            anim.SetBool("run", true);
            transform.localScale = new Vector3(-4, 4, 4);
        }
        if(moveInput < 0)
        {
            anim.SetBool("run", true);
            transform.localScale = new Vector3(4, 4, 4);
        }
        if(moveInput == 0){anim.SetBool("run", false);}

        Vector3 delta = transform.position - lastPosition;

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
}
