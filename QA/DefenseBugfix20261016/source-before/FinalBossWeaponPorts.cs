using UnityEngine;

/// <summary>
/// EK-9の画像に合わせてPrefab上に配置する発射口。
/// 各Transformの右方向を射出方向とし、Visual配下で振動にも追従する。
/// </summary>
public sealed class FinalBossWeaponPorts : MonoBehaviour
{
    [SerializeField] private Transform[] gunMuzzles;
    [SerializeField] private Transform[] missileMuzzles;

    public int MissileMuzzleCount => missileMuzzles != null ? missileMuzzles.Length : 0;

    public Transform GetGunMuzzle(int shotIndex) => GetMuzzle(gunMuzzles, shotIndex);
    public Transform GetMissileMuzzle(int shotIndex) => GetMuzzle(missileMuzzles, shotIndex);

    private static Transform GetMuzzle(Transform[] muzzles, int shotIndex)
    {
        return muzzles != null && muzzles.Length > 0
            ? muzzles[Mathf.Max(0, shotIndex) % muzzles.Length]
            : null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawMuzzles(gunMuzzles, Color.yellow);
        DrawMuzzles(missileMuzzles, Color.cyan);
    }

    private static void DrawMuzzles(Transform[] muzzles, Color color)
    {
        if (muzzles == null) return;
        Gizmos.color = color;
        foreach (Transform muzzle in muzzles)
        {
            if (muzzle == null) continue;
            Gizmos.DrawWireSphere(muzzle.position, 0.08f);
            Gizmos.DrawRay(muzzle.position, muzzle.right * 0.5f);
        }
    }
#endif
}
