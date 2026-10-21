using UnityEngine;

/// <summary>A small world-space meter above the player; never blocks pointer input.</summary>
public sealed class RailChargeIndicator : MonoBehaviour
{
    private LineRenderer background;
    private LineRenderer fill;
    private Material material;
    private bool visible;
    private float fraction;

    public void Show(bool show, float value)
    {
        visible = show;
        fraction = Mathf.Clamp01(value);
        if (show && background == null) CreateMeter();
        if (background != null) background.enabled = show;
        if (fill != null) fill.enabled = show && fraction > 0f;
    }

    private void CreateMeter()
    {
        material = new Material(Shader.Find("Sprites/Default"));
        background = CreateLine("ChargeBackground", 0.1f, 200);
        fill = CreateLine("ChargeFill", 0.055f, 201);
        background.startColor = background.endColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
    }

    private LineRenderer CreateLine(string objectName, float width, int order)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        var line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = line.endWidth = width;
        line.sortingOrder = order;
        return line;
    }

    private void LateUpdate()
    {
        if (!visible || background == null) return;
        Vector3 left = transform.position + new Vector3(-0.4f, 1.6f, 0f);
        background.SetPosition(0, left);
        background.SetPosition(1, left + Vector3.right * 0.8f);
        fill.SetPosition(0, left);
        fill.SetPosition(1, left + Vector3.right * (0.8f * fraction));
        fill.startColor = fill.endColor = fraction >= 0.999f
            ? new Color(1f, 0.8f, 0.25f) : new Color(0.6f, 0.85f, 1f);
    }

    private void OnDisable() => Show(false, 0f);

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
