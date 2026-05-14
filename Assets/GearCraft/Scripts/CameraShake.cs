using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class CameraShake : MonoBehaviour
{
    [Header("Shake Parameters")]
    [SerializeField] private float defaultDuration = 0.3f;
    [SerializeField] private float defaultStrength = 0.5f;
    [SerializeField] private int defaultVibrato = 10;
    [SerializeField] private float defaultRandomness = 90f;

    private Tween currentShake;
    private Vector3 initialPosition;

    private void Awake()
    {
        initialPosition = transform.localPosition;
    }

    /// <summary>
    /// カメラをシェイクさせる（awaitで完了を待つことも可能）
    /// </summary>
    public async UniTask ShakeAsync(
        float? duration = null,
        float? strength = null,
        int? vibrato = null,
        float? randomness = null)
    {
        // 既存シェイク中なら停止
        if (currentShake != null && currentShake.IsActive())
        {
            currentShake.Kill();
            transform.localPosition = initialPosition;
        }

        float d = duration ?? defaultDuration;
        float s = strength ?? defaultStrength;
        int v = vibrato ?? defaultVibrato;
        float r = randomness ?? defaultRandomness;

        // DoTweenのDOShakePositionを使用
        currentShake = transform.DOShakePosition(
            duration: d,
            strength: s,
            vibrato: v,
            randomness: r,
            snapping: false,
            fadeOut: true
        ).SetEase(Ease.OutQuad);

        // 終了待ち（await可能）
        await currentShake.AsyncWaitForCompletion();

        // 終了後位置リセット（微ズレ防止）
        transform.localPosition = initialPosition;
    }

    /// <summary>
    /// 単発で呼び出すだけ（待たない）
    /// </summary>
    public void Shake(
        float? duration = null,
        float? strength = null,
        int? vibrato = null,
        float? randomness = null)
    {
        ShakeAsync(duration, strength, vibrato, randomness).Forget();
    }
}
