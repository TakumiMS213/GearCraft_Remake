using UnityEngine;

public class PunkDriveRotation : MonoBehaviour
{
    public Vector3 speed = new Vector3(0, 0, 360f); // 1秒で90°回転

    void Update()
    {
        transform.Rotate(speed * Time.deltaTime);
    }
}
