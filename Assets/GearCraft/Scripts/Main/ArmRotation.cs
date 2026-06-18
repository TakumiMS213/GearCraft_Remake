using UnityEngine;

public class ArmRotation : MonoBehaviour
{
    [System.Serializable]
    public class WeaponArmPoseRule
    {
        public string armTriggerName;
        public int poseIndex;
    }

    // --- Arm関連 ---
    public Transform player;        // Player本体
    public Transform sholder;       // 肩（回転の基準点）
    public Transform arm;           // 腕（Playerの子じゃない）
    public Vector3[] armOffsets;    // 武器ごとのArm位置オフセット
    public Vector3[] armScales;     // 武器ごとのArmスケール
    public Vector3[] armBaseRotations; // 武器ごとのArm基準回転（XYZ軸）
    public WeaponArmPoseRule[] poseRules;

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
            int meleePoseIndex = GetPoseIndex(currentWeaponData);
            ApplyPose(meleePoseIndex);
            arm.position = player.position + GetOffset(meleePoseIndex);
            return; // 回転処理はスキップ
        }

        // 以下、遠距離武器の回転処理
        int weaponIndex = GetPoseIndex(currentWeaponData);
        Vector3 armOffset = GetOffset(weaponIndex);
        Vector3 armScale = GetScale(weaponIndex);
        Vector3 armBaseRotation = GetBaseRotation(weaponIndex);

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

    private int GetPoseIndex(WeaponDataSO weapon)
    {
        if (weapon == null)
        {
            return 0;
        }

        if (poseRules != null)
        {
            for (int i = 0; i < poseRules.Length; i++)
            {
                WeaponArmPoseRule rule = poseRules[i];
                if (rule != null && rule.armTriggerName == weapon.armTriggerName)
                {
                    return ClampPoseIndex(rule.poseIndex);
                }
            }
        }

        switch (weapon.armTriggerName)
        {
            case "Assault":
            case "RailCraft":
            case "SteamGatling":
                return ClampPoseIndex(1);
            case "SteamShoot":
            case "SteamThrower":
                return ClampPoseIndex(2);
            case "GearCraft_Axe":
                return ClampPoseIndex(3);
            default:
                return ClampPoseIndex(0);
        }
    }

    private int ClampPoseIndex(int index)
    {
        int maxLength = armOffsets != null ? armOffsets.Length : 0;
        if (armScales != null && armScales.Length > maxLength) maxLength = armScales.Length;
        if (armBaseRotations != null && armBaseRotations.Length > maxLength) maxLength = armBaseRotations.Length;
        if (maxLength <= 0) return 0;
        return Mathf.Clamp(index, 0, maxLength - 1);
    }

    private Vector3 GetOffset(int index)
    {
        if (armOffsets == null || armOffsets.Length == 0) return Vector3.zero;
        return armOffsets[Mathf.Clamp(index, 0, armOffsets.Length - 1)];
    }

    private Vector3 GetScale(int index)
    {
        if (armScales == null || armScales.Length == 0) return Vector3.one;
        Vector3 scale = armScales[Mathf.Clamp(index, 0, armScales.Length - 1)];
        if (index == 3 &&
            StatusManager.Instance != null &&
            StatusManager.Instance.gearCraftAxeSizeMultiplier > 1f)
        {
            scale *= StatusManager.Instance.gearCraftAxeSizeMultiplier;
        }

        return scale;
    }

    private Vector3 GetBaseRotation(int index)
    {
        if (armBaseRotations == null || armBaseRotations.Length == 0) return Vector3.zero;
        return armBaseRotations[Mathf.Clamp(index, 0, armBaseRotations.Length - 1)];
    }

    private void ApplyPose(int index)
    {
        if (arm == null) return;
        arm.localScale = GetScale(index);
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
