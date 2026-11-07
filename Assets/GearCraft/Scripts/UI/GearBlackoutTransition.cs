using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored black gear silhouettes assemble from the edges inward, using unscaled time.</summary>
public sealed class GearBlackoutTransition : MonoBehaviour
{
    [Serializable]
    private sealed class Gear
    {
        public RectTransform rect;
        public float pitchRadius;
        public int driver = -1;
        [Range(0f, 1f)] public float attachAt;
        [NonSerialized] public Vector2 position;
        [NonSerialized] public Vector2 entryPosition;
        [NonSerialized] public float angularSpeed;
        [NonSerialized] public float phase;
        [NonSerialized] public bool engaged;
    }

    [SerializeField] private RectTransform gearPlane;
    [SerializeField] private Image solidCover;
    [SerializeField] private AudioSource snapAudio;
    [SerializeField] private AudioClip snapClip;
    [SerializeField] private Gear[] gears;
    [SerializeField] private float centralDegreesPerSecond = 14f;
    private RectTransform viewport;
    private Tween motion;
    private float progress;
    private float clock;
    private float lastSnapTime = -1f;
    private bool initialized;
    private bool closing;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        viewport = (RectTransform)transform;
        for (int i = 0; i < gears.Length; i++)
        {
            Gear gear = gears[i];
            gear.position = gear.rect.anchoredPosition;
            gear.entryPosition = EntryPosition(gear.position, gear.rect.sizeDelta.x * 0.5f);
            if (gear.driver >= 0 && gear.driver < i)
            {
                Gear driver = gears[gear.driver];
                float ratio = driver.pitchRadius / Mathf.Max(1f, gear.pitchRadius);
                gear.angularSpeed = -driver.angularSpeed * ratio;
                Vector2 delta = gear.position - driver.position;
                float contactAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                gear.phase = contactAngle + 180f + (contactAngle - driver.phase) * ratio + 15f;
            }
            else
            {
                gear.angularSpeed = centralDegreesPerSecond;
                gear.phase = 0f;
            }
        }
    }

    public void SetImmediate(bool covered)
    {
        Initialize();
        motion?.Kill();
        motion = null;
        closing = false;
        progress = covered ? 1f : 0f;
        clock = 0f;
        Apply();
    }

    public Tween Play(bool cover, float duration, Action onComplete = null)
    {
        Initialize();
        motion?.Kill();
        closing = cover;
        if (duration <= 0f)
        {
            SetImmediate(cover);
            onComplete?.Invoke();
            return null;
        }
        motion = DOTween.To(() => progress, value => { progress = value; Apply(); },
                cover ? 1f : 0f, duration)
            .SetEase(Ease.Linear).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { closing = false; onComplete?.Invoke(); });
        return motion;
    }

    private void Update()
    {
        if (!initialized || progress <= 0f) return;
        clock += Time.unscaledDeltaTime;
        Apply();
    }

    // Opaque gears enter from beyond the viewport, then seat with a short mechanical rebound.
    public static float Engagement(float value, float attachAt) =>
        Mathf.Clamp01((value - attachAt) / 0.24f);

    public static Vector2 EntryPosition(Vector2 seatedPosition, float outerRadius)
    {
        Vector2 direction = seatedPosition.sqrMagnitude > 0f
            ? seatedPosition.normalized : new Vector2(0f, -1f);
        // Cover-fit makes every viewport a subset of this authored 1920 x 1080 plane.
        float radius = Mathf.Max(1f, outerRadius) + 24f;
        float travelX = Mathf.Abs(direction.x) > 0.001f
            ? (960f + radius - Mathf.Abs(seatedPosition.x)) / Mathf.Abs(direction.x)
            : float.PositiveInfinity;
        float travelY = Mathf.Abs(direction.y) > 0.001f
            ? (540f + radius - Mathf.Abs(seatedPosition.y)) / Mathf.Abs(direction.y)
            : float.PositiveInfinity;
        return seatedPosition + direction * Mathf.Max(0f, Mathf.Min(travelX, travelY));
    }

    public static float SeatingProgress(float value)
    {
        float t = Mathf.Clamp01(value);
        return t < 0.82f
            ? Mathf.SmoothStep(0f, 1.012f, t / 0.82f)
            : Mathf.Lerp(1.012f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.82f) / 0.18f));
    }

    public static float SealScale(float value) =>
        Mathf.Lerp(1f, 1.8f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, value)));

    private void Apply()
    {
        float fit = Mathf.Max(viewport.rect.width / 1920f, viewport.rect.height / 1080f);
        gearPlane.localScale = Vector3.one * Mathf.Max(0.01f, fit);
        // The backing becomes opaque only AFTER the gears themselves cover the viewport.
        solidCover.enabled = progress >= 0.9999f;
        float seal = SealScale(progress);
        foreach (Gear gear in gears)
        {
            float t = Engagement(progress, gear.attachAt);
            bool visible = t > 0f;
            gear.rect.gameObject.SetActive(visible);
            if (!visible) { gear.engaged = false; continue; }
            float settle = SeatingProgress(t);
            gear.rect.anchoredPosition = gear.entryPosition + (gear.position - gear.entryPosition) * settle;
            gear.rect.localScale = Vector3.one * seal;
            gear.rect.localRotation = Quaternion.Euler(0f, 0f, gear.phase + gear.angularSpeed * clock);
            if (t >= 0.82f && !gear.engaged && closing && Time.unscaledTime - lastSnapTime > 0.065f)
            {
                if (snapAudio != null && snapClip != null)
                {
                    snapAudio.pitch = Mathf.Clamp(300f / gear.pitchRadius, 0.85f, 1.35f);
                    snapAudio.PlayOneShot(snapClip, 0.3f);
                }
                lastSnapTime = Time.unscaledTime;
            }
            gear.engaged = t >= 0.82f;
        }
    }

    private void OnDisable()
    {
        motion?.Kill();
        motion = null;
        closing = false;
    }
}
