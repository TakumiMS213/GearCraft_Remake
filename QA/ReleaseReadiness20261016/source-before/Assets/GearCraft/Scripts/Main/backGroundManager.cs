using UnityEngine;

public class backGroundManager : MonoBehaviour
{
    public Transform player;           // プレイヤーのTransform
    public float parallaxFactor = 0.5f; // 背景の追従度（0～1）

    private Vector3 previousPlayerPosition;

    void Start()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }
        previousPlayerPosition = player.position;
    }

    void LateUpdate()
    {
        Vector3 deltaMovement = player.position - previousPlayerPosition;

        // 背景をわずかに動かす
        transform.position += new Vector3(deltaMovement.x * parallaxFactor, deltaMovement.y * parallaxFactor, 0f);

        previousPlayerPosition = player.position;
    }
}
