using UnityEngine;

[DisallowMultipleComponent]
public class CraftSpaceFurnitureOutline : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 0.02f;
    [SerializeField] private int sortingOrderOffset = -1;

    private static readonly Vector2[] OutlineDirections =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right,
        new Vector2(1f, 1f).normalized,
        new Vector2(-1f, 1f).normalized,
        new Vector2(1f, -1f).normalized,
        new Vector2(-1f, -1f).normalized
    };

    private SpriteRenderer[] outlineRenderers;
    private bool isVisible;
    private static Material outlineMaterial;

    private void Awake()
    {
        targetRenderer = targetRenderer != null ? targetRenderer : GetComponent<SpriteRenderer>();
        CreateOutlineRenderers();
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (!isVisible)
        {
            return;
        }

        SyncOutlineRenderers();
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        EnsureOutlineRenderers();

        if (outlineRenderers == null)
        {
            return;
        }

        if (visible)
        {
            SyncOutlineRenderers();
        }

        foreach (SpriteRenderer outlineRenderer in outlineRenderers)
        {
            if (outlineRenderer != null)
            {
                outlineRenderer.gameObject.SetActive(visible);
            }
        }
    }

    private void EnsureOutlineRenderers()
    {
        if (outlineRenderers == null || outlineRenderers.Length == 0)
        {
            CreateOutlineRenderers();
        }
    }

    private void CreateOutlineRenderers()
    {
        if (targetRenderer == null)
        {
            return;
        }

        outlineRenderers = new SpriteRenderer[OutlineDirections.Length];
        for (int i = 0; i < OutlineDirections.Length; i++)
        {
            GameObject outlineObject = new GameObject("FurnitureOutline");
            outlineObject.transform.SetParent(targetRenderer.transform, false);
            outlineObject.transform.localPosition = OutlineDirections[i] * outlineWidth;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one;

            SpriteRenderer outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
            outlineRenderer.sharedMaterial = GetOutlineMaterial();
            outlineRenderers[i] = outlineRenderer;
        }

        SyncOutlineRenderers();
    }

    private void SyncOutlineRenderers()
    {
        if (targetRenderer == null || outlineRenderers == null)
        {
            return;
        }

        for (int i = 0; i < outlineRenderers.Length; i++)
        {
            SpriteRenderer outlineRenderer = outlineRenderers[i];
            if (outlineRenderer == null)
            {
                continue;
            }

            outlineRenderer.sprite = targetRenderer.sprite;
            outlineRenderer.drawMode = targetRenderer.drawMode;
            outlineRenderer.size = targetRenderer.size;
            outlineRenderer.tileMode = targetRenderer.tileMode;
            outlineRenderer.flipX = targetRenderer.flipX;
            outlineRenderer.flipY = targetRenderer.flipY;
            outlineRenderer.maskInteraction = targetRenderer.maskInteraction;
            outlineRenderer.sortingLayerID = targetRenderer.sortingLayerID;
            outlineRenderer.sortingOrder = targetRenderer.sortingOrder + sortingOrderOffset;
            outlineRenderer.sharedMaterial = GetOutlineMaterial();
            outlineRenderer.color = outlineColor;
            outlineRenderer.transform.localPosition = OutlineDirections[i] * outlineWidth;
        }
    }

    private static Material GetOutlineMaterial()
    {
        if (outlineMaterial != null)
        {
            return outlineMaterial;
        }

        outlineMaterial = Resources.Load<Material>("FurnitureOutlineMaterial");
        if (outlineMaterial != null)
        {
            return outlineMaterial;
        }

        Shader shader = Shader.Find("GearCraft/SpriteSolidColor");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        outlineMaterial = new Material(shader)
        {
            name = "Furniture Outline Material"
        };
        return outlineMaterial;
    }
}
