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
        if (!isActiveAndEnabled) return;
        // 既存シェイク中なら停止
        StopCurrentShake();

        float d = duration ?? defaultDuration;
        float s = strength ?? defaultStrength;
        int v = vibrato ?? defaultVibrato;
        float r = randomness ?? defaultRandomness;

        // DoTweenのDOShakePositionを使用
        Tween shake = transform.DOShakePosition(
            duration: d,
            strength: s,
            vibrato: v,
            randomness: r,
            snapping: false,
            fadeOut: true
        ).SetEase(Ease.OutQuad);
        currentShake = shake;

        // 終了待ち（await可能）
        await shake.AsyncWaitForCompletion();

        // 古いシェイクの完了が、新しく開始したシェイクや破棄済みカメラを触らない。
        if (this != null && currentShake == shake)
        {
            currentShake = null;
            transform.localPosition = initialPosition;
        }
    }

    private void StopCurrentShake()
    {
        Tween shake = currentShake;
        currentShake = null;
        if (shake == null) return;
        if (shake.IsActive()) shake.Kill();
        transform.localPosition = initialPosition;
    }

    private void OnDisable() => StopCurrentShake();
    private void OnDestroy() => StopCurrentShake();

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
