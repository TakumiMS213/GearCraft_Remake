using UnityEngine;

public class MoveForward : MonoBehaviour
{
    // インスペクタから設定できるスピード（単位: 単位/秒）
    public float speed = -5;

    void Update()
    {
        // 1秒間に speed だけ進む（Time.deltaTimeでフレームに依存しない移動）
        transform.Translate(Vector3.right * speed * Time.deltaTime);
    }
}
