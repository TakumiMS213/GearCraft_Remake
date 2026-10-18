using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ボス本体の当たり判定が画面・地形に埋まらないよう、移動可能な原点の範囲を求める。
/// スポナーの上下移動やシーン内の復帰ポイントには依存しない。
/// </summary>
public sealed class BossArena
{
    private const float Clearance = 0.15f;
    private readonly Transform boss;
    private readonly Vector2 bodyMin;
    private readonly Vector2 bodyMax;
    private readonly Collider2D[] terrain;
    private Camera camera;

    public BossArena(EnemyController owner, bool containSprite = false)
    {
        boss = owner.transform;
        camera = Camera.main;
        Collider2D body = owner.GetComponent<Collider2D>();
        Bounds bounds = body != null ? body.bounds : new Bounds(boss.position, Vector3.one);
        SpriteRenderer sprite = owner.GetComponentInChildren<SpriteRenderer>();
        if (containSprite && sprite != null) bounds.Encapsulate(sprite.bounds);
        bodyMin = (Vector2)(bounds.min - boss.position);
        bodyMax = (Vector2)(bounds.max - boss.position);
        var surfaces = new List<Collider2D>();
        foreach (Collider2D collider in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (collider.CompareTag("Ground") || collider.CompareTag("Wall")) surfaces.Add(collider);
        }
        terrain = surfaces.ToArray();
    }

    public Rect GetMovementRect()
    {
        if (camera == null) camera = Camera.main;
        Vector3 min = new Vector3(-9f, -5f, 0f);
        Vector3 max = new Vector3(9f, 5f, 0f);
        if (camera != null)
        {
            float depth = camera.WorldToViewportPoint(boss.position).z;
            min = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            max = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        }

        float left = min.x;
        float right = max.x;
        float bottom = min.y;
        float top = max.y;
        Vector2 center = (min + max) * 0.5f;
        foreach (Collider2D surface in terrain)
        {
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy || surface.isTrigger)
                continue;

            Bounds bounds = surface.bounds;
            if (surface.CompareTag("Ground") && bounds.min.x < center.x && bounds.max.x > center.x)
            {
                bottom = Mathf.Max(bottom, bounds.max.y);
            }
            else if (surface.CompareTag("Wall"))
            {
                if (bounds.size.y > bounds.size.x && bounds.min.y < center.y && bounds.max.y > center.y)
                {
                    if (bounds.center.x > center.x) right = Mathf.Min(right, bounds.min.x);
                    else left = Mathf.Max(left, bounds.max.x);
                }
                else if (bounds.min.x < center.x && bounds.max.x > center.x && bounds.center.y > center.y)
                {
                    top = Mathf.Min(top, bounds.min.y);
                }
            }
        }

        left += Clearance - bodyMin.x;
        right -= Clearance + bodyMax.x;
        bottom += Clearance - bodyMin.y;
        top -= Clearance + bodyMax.y;
        // 本体が画面より大きい場合も、逆転した範囲をClampへ渡さない。
        if (right < left) left = right = (left + right) * 0.5f;
        if (top < bottom) bottom = top = (bottom + top) * 0.5f;
        return Rect.MinMaxRect(left, bottom, right, top);
    }

    public Vector3 Clamp(Vector3 position)
    {
        Rect rect = GetMovementRect();
        position.x = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
        position.y = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
        position.z = boss.position.z;
        return position;
    }

    public Vector3 GetPosition(float horizontal, float vertical)
    {
        Rect rect = GetMovementRect();
        return new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, horizontal),
            Mathf.Lerp(rect.yMin, rect.yMax, vertical), boss.position.z);
    }

    public Vector3 Ground(Vector3 position)
    {
        // 接地は視界の下端ではなく、実際の床の上面に合わせる。
        float floor = float.NegativeInfinity;
        foreach (Collider2D surface in terrain)
        {
            if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy ||
                surface.isTrigger || !surface.CompareTag("Ground")) continue;

            Bounds bounds = surface.bounds;
            if (bounds.min.x <= position.x + bodyMin.x && bounds.max.x >= position.x + bodyMax.x)
                floor = Mathf.Max(floor, bounds.max.y);
        }

        if (!float.IsNegativeInfinity(floor)) position.y = floor - bodyMin.y + 0.02f;
        else position.y = GetMovementRect().yMin;
        return position;
    }

    public Vector3 GetLeftMuzzlePosition(float projectileHalfWidth)
    {
        return boss.position + Vector3.left * (-bodyMin.x + projectileHalfWidth + Clearance);
    }
}
