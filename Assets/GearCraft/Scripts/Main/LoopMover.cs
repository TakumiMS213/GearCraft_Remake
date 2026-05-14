using UnityEngine;

public class LoopMover : MonoBehaviour
{
    [Header("移動設定")]
    public Transform pointA;   // 始点
    public Transform pointB;   // 終点
    public float moveSpeed = 2f;   // 移動速度
    public float arrivalThreshold = 0.05f; // 到達とみなす距離

    private Vector3 targetPos;

    void Start()
    {
        if (pointA == null || pointB == null)
        {
            Debug.LogError("PingPongMover: PointAまたはPointBが設定されていません。");
            enabled = false;
            return;
        }

        // 最初の目的地をBに設定
        targetPos = pointB.position;
    }

    void Update()
    {
        // 現在位置から目的地に向けて移動
        transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);

        // 到達したらターゲットを切り替える
        if (Vector3.Distance(transform.position, targetPos) < arrivalThreshold)
        {
            targetPos = (targetPos == pointA.position) ? pointB.position : pointA.position;
        }
    }

    // Sceneビューで分かりやすく線を描画
    void OnDrawGizmos()
    {
        if (pointA != null && pointB != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pointA.position, pointB.position);
            Gizmos.DrawSphere(pointA.position, 0.1f);
            Gizmos.DrawSphere(pointB.position, 0.1f);
        }
    }
}
