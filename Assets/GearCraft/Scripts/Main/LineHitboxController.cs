using System.Collections.Generic;
using UnityEngine;

public class LineHitboxController : MonoBehaviour
{
    private readonly HashSet<EnemyController> damagedEnemies = new HashSet<EnemyController>();
    private static Sprite fallbackSprite;

    private float damage;
    private float duration;
    private Vector2 size;
    private Collider2D hitboxCollider;

    public void Initialize(float hitDamage, float lifeTime, Vector2 hitboxSize, GameObject visualPrefab, Sprite visualSprite)
    {
        damage = hitDamage;
        duration = Mathf.Max(0.01f, lifeTime);
        size = hitboxSize;
        hitboxCollider = GetComponent<Collider2D>();

        CreateVisibleHitbox(visualPrefab, visualSprite);
        ApplyDamageToCurrentOverlaps();
        if (hitboxCollider != null)
        {
            Destroy(hitboxCollider, duration);
        }
        Destroy(gameObject, Mathf.Max(duration, 0.12f));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void ApplyDamageToCurrentOverlaps()
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(transform.position, size, transform.eulerAngles.z);
        foreach (Collider2D hit in hits)
        {
            TryDamage(hit);
        }
    }

    private void TryDamage(Collider2D other)
    {
        if (other == null || !other.CompareTag("Enemy")) return;

        EnemyController enemy = other.GetComponent<EnemyController>();
        if (enemy == null)
        {
            enemy = other.GetComponentInParent<EnemyController>();
        }

        if (enemy == null || damagedEnemies.Contains(enemy)) return;

        damagedEnemies.Add(enemy);
        enemy.TakeDamage(damage);
    }

    private void CreateVisibleHitbox(GameObject visualPrefab, Sprite visualSprite)
    {
        GameObject visual = visualPrefab != null
            ? Instantiate(visualPrefab, transform)
            : CreateFallbackVisual();

        visual.name = "VisibleHitbox";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = visual.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null) return;

        if (visualSprite != null)
        {
            renderer.sprite = visualSprite;
        }
    }

    private GameObject CreateFallbackVisual()
    {
        GameObject visual = new GameObject("VisibleHitbox");
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = GetFallbackSprite();
        renderer.color = new Color(0.4f, 0.9f, 1f, 0.35f);
        renderer.sortingOrder = 100;
        return visual;
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite == null)
        {
            fallbackSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        return fallbackSprite;
    }
}
