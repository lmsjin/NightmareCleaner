using UnityEngine;

public class RescueChild : MonoBehaviour
{
    private const float ColliderHeight = 1.2f;
    private const float ColliderRadius = 0.35f;

    public int FailCount;

    public static RescueChild Create(
        Vector3 position,
        GameObject prefab = null,
        string fallbackResourcePath = "Character/model/kid",
        Transform parent = null,
        Vector3 visualEuler = default,
        float visualScale = 1f)
    {
        var go = new GameObject("RescueChild");
        if (parent != null) go.transform.SetParent(parent, true);
        go.name = "RescueChild";
        go.transform.position = position;

        var col = go.AddComponent<CapsuleCollider>();
        col.isTrigger = true;
        col.radius = ColliderRadius;
        col.height = ColliderHeight;
        col.center = Vector3.zero;

        var rc = go.AddComponent<RescueChild>();
        rc.CreateVisual(prefab, fallbackResourcePath, visualEuler, visualScale);
        return rc;
    }

    void CreateVisual(GameObject prefab, string fallbackResourcePath, Vector3 visualEuler, float visualScale)
    {
        if (prefab == null && !string.IsNullOrEmpty(fallbackResourcePath))
            prefab = Resources.Load<GameObject>(fallbackResourcePath);

        if (prefab != null)
        {
            GameObject visual = Instantiate(prefab, transform);
            visual.name = "KidVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(visualEuler);
            visual.transform.localScale = Vector3.one;
            DisableVisualColliders(visual);
            FitVisualToTrigger(visual.transform, visualScale);
            return;
        }

        CreateFallbackMarker(visualScale);
    }

    void CreateFallbackMarker(float visualScale)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        marker.name = "KidMarker";
        marker.transform.SetParent(transform, false);
        marker.transform.localScale = new Vector3(ColliderRadius * 1.7f, ColliderHeight * 0.5f, ColliderRadius * 1.7f) * Mathf.Max(0.1f, visualScale);
        if (marker.TryGetComponent<Collider>(out var col))
            Destroy(col);
        ApplyColor(marker, new Color(1f, 0.85f, 0.3f, 1f));
    }

    void DisableVisualColliders(GameObject visual)
    {
        foreach (var col in visual.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    void FitVisualToTrigger(Transform visual, float visualScale)
    {
        if (!TryGetVisualBounds(visual, out Bounds bounds) || bounds.size.y <= 0.001f)
            return;

        float targetHeight = ColliderHeight * 0.95f * Mathf.Max(0.1f, visualScale);
        float scale = targetHeight / bounds.size.y;
        visual.localScale *= scale;

        if (!TryGetVisualBounds(visual, out bounds))
            return;

        Vector3 triggerBottom = transform.position + Vector3.down * (ColliderHeight * 0.5f);
        Vector3 targetCenter = transform.position;
        Vector3 boundsCenter = bounds.center;
        Vector3 offset = new Vector3(
            targetCenter.x - boundsCenter.x,
            triggerBottom.y - bounds.min.y,
            targetCenter.z - boundsCenter.z);

        visual.position += offset;
    }

    bool TryGetVisualBounds(Transform visual, out Bounds bounds)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    void ApplyColor(Color c)
    {
        ApplyColor(gameObject, c);
    }

    void ApplyColor(GameObject target, Color c)
    {
        if (!target.TryGetComponent<Renderer>(out var rend)) return;
        var m = rend.material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.color = c;
    }
}
