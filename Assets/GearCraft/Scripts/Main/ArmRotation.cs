using UnityEngine;

public class ArmRotation : MonoBehaviour
{
    // --- Arm関連 ---
    public Transform player;        // Player本体
    public Transform sholder;       // 肩（回転の基準点）
    public Transform arm;           // 腕（Playerの子じゃない）
    public Vector3[] armOffsets;    // 武器ごとのArm位置オフセット
    public Vector3[] armScales;     // 武器ごとのArmスケール
    public Vector3[] armBaseRotations; // 武器ごとのArm基準回転（XYZ軸）

    private PlayerController playerController;

    void Start()
    {
        playerController = player.GetComponent<PlayerController>();
    }

    void Update()
    {
        if (playerController == null) return;

        // 近接武器では回転処理をスキップ
        WeaponDataSO currentWeaponData = StatusManager.Instance != null ? StatusManager.Instance.currentWeapon : null;
        bool isMelee = currentWeaponData != null && currentWeaponData.weaponType == WeaponType.Melee;

        if (isMelee)
        {
            // Armをプレイヤーに追従させる（位置のみ）
            if (armOffsets != null && armOffsets.Length > 0)
                arm.position = player.position + armOffsets[0];
            return; // 回転処理はスキップ
        }

        // 以下、遠距離武器の回転処理
        int weaponIndex = 0; // デフォルト
        if (armOffsets != null && armOffsets.Length > 1)
            weaponIndex = Mathf.Clamp(weaponIndex, 0, armOffsets.Length - 1);
        Vector3 armOffset = armOffsets[weaponIndex];
        Vector3 armScale = armScales[weaponIndex];
        Vector3 armBaseRotation = (armBaseRotations != null && armBaseRotations.Length > weaponIndex) ? armBaseRotations[weaponIndex] : Vector3.zero;

        // マウス位置取得
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = Mathf.Abs(Camera.main.transform.position.z - arm.position.z);
        Vector3 worldMousePos = Camera.main.ScreenToWorldPoint(mousePos);

        // Armの回転（マウス追従）
        Vector3 armDirection = worldMousePos - arm.position;
        float armAngle = Mathf.Atan2(armDirection.y, armDirection.x) * Mathf.Rad2Deg;
        arm.rotation = Quaternion.Euler(armBaseRotation + new Vector3(0, 0, armAngle));
        arm.localPosition = armOffset;
        arm.localScale = armScale;

        // 肩の回転
        mousePos.z = 0f;
        Vector3 shoulderDirection = mousePos - sholder.position;
        float shoulderAngle = Mathf.Atan2(shoulderDirection.y, shoulderDirection.x) * Mathf.Rad2Deg;
        shoulderAngle = NormalizeAngle(shoulderAngle);
        float shoulderAngle180 = Mathf.Clamp(shoulderAngle > 180f ? shoulderAngle - 360f : shoulderAngle, -90f, 90f);
        shoulderAngle = shoulderAngle180 < 0 ? shoulderAngle180 + 360f : shoulderAngle180;
        sholder.rotation = Quaternion.Euler(0f, 0f, shoulderAngle);

        // Armの位置・回転を肩基準で調整
        Vector3 rotatedOffset = sholder.rotation * armOffset;
        arm.position = sholder.position + rotatedOffset;
        arm.rotation = sholder.rotation;
    }


    // --- 角度正規化 ---
    private float NormalizeAngle(float angle)
    {
        while (angle < 0f) angle += 360f;
        while (angle >= 360f) angle -= 360f;
        return angle;
    }

    // --- 腕を1回転させる ---
    public void RotateArmOnce(float duration = 1f)
    {
        StartCoroutine(RotateArmCoroutine(duration));
    }

    private System.Collections.IEnumerator RotateArmCoroutine(float duration)
    {
        float elapsed = 0f;
        float startAngle = 0f;
        float endAngle = -360f;

        // 初期のオフセットを計算
        Vector3 initialOffset = arm.position - sholder.position;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float angle = Mathf.Lerp(startAngle, endAngle, t);

            // 毎フレーム、肩（pivot）を再取得
            Vector3 pivot = sholder.position;

            // 現在の回転を適用（肩基準）
            Vector3 rotatedOffset = Quaternion.Euler(0f, 0f, angle) * initialOffset;

            // 腕を肩に追従させつつ回転
            arm.position = pivot + rotatedOffset;
            arm.rotation = Quaternion.Euler(0f, 0f, angle);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 最終位置と回転を合わせる
        Vector3 finalPivot = sholder.position;
        arm.position = finalPivot + (Quaternion.Euler(0f, 0f, endAngle) * initialOffset);
        arm.rotation = Quaternion.Euler(0f, 0f, endAngle);
    }
}
