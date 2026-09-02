using UnityEngine;

public class GhostChef : GhostBase
{
    protected override Color BodyColor => new(0.9f, 0.4f, 0.35f, 1f);
    protected override float BodyScale => 1.15f;
    protected override GhostVisualId VisualId => GhostVisualId.Chef;
    protected override string DefaultVisualResourcePath => "Ghost/all ghost_01";
    protected override float ColliderRadius => 0.5f;
    protected override float ColliderHeight => 1.25f;
    protected override bool IsSpecial => true;

    private float _soupRadius;
    private float _soupGroundOffset;
    private float _soupLifetime;
    private float _soupSpawnInterval;
    private int _soupCount;
    private float _soupPostDelay;
    private float _soupDamageInterval;

    private float SoupCooldown => _soupLifetime + _soupPostDelay;

    private enum ChefState { Idle, Trailing }
    private ChefState _state = ChefState.Idle;

    private float _cooldownTimer;
    private int _soupSpawned;
    private float _distSinceLastSoup;
    private Vector3 _prevPos;

    protected override void Configure(BalanceConfig cfg)
    {
        _maxHP = cfg.chefHP;
        _moveSpeed = cfg.chefSpeed;
        _contactDamage = cfg.chefDamage;

        _soupRadius = cfg.chefSoupRadius;
        _soupGroundOffset = cfg.chefSoupGroundOffset;
        _soupLifetime = cfg.chefSoupLifetime;
        _soupSpawnInterval = cfg.chefSoupSpawnInterval;
        _soupCount = cfg.chefSoupCount;
        _soupPostDelay = cfg.chefSoupPostDelay;
        _soupDamageInterval = cfg.chefSoupDamageInterval;
    }

    protected override void Start()
    {
        base.Start();
        _prevPos = transform.position;
        _cooldownTimer = SoupCooldown;
    }

    protected override void OnTick()
    {
        switch (_state)
        {
            case ChefState.Idle:
                UpdateIdle();
                break;
            case ChefState.Trailing:
                UpdateTrailing();
                break;
        }
    }

    private void UpdateIdle()
    {
        _prevPos = transform.position;

        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer <= 0f)
            EnterTrailing();
    }

    private void EnterTrailing()
    {
        _soupSpawned = 0;
        _distSinceLastSoup = 0f;
        _prevPos = transform.position;
        _state = ChefState.Trailing;
    }

    private void UpdateTrailing()
    {
        Vector3 currentPosition = transform.position;
        _distSinceLastSoup += Vector2.Distance(
            new Vector2(currentPosition.x, currentPosition.z),
            new Vector2(_prevPos.x, _prevPos.z));
        _prevPos = currentPosition;

        while (_distSinceLastSoup >= _soupSpawnInterval && _soupSpawned < _soupCount)
        {
            HotSoupPuddle.Spawn(
                transform.position,
                _soupRadius,
                _soupGroundOffset,
                _soupLifetime,
                _soupDamageInterval);

            _distSinceLastSoup -= _soupSpawnInterval;
            _soupSpawned++;
        }

        if (_soupSpawned >= _soupCount)
        {
            _cooldownTimer = SoupCooldown;
            _state = ChefState.Idle;
        }
    }

    protected override void OnConsumeBegin()
    {
    }
}

public class HotSoupPuddle : MonoBehaviour
{
    private const string LavaResourcePath = "Lava";
    private const float PuddleHalfHeight = 0.04f;
    private static readonly Vector3 LavaLocalEuler = new(-90f, 0f, 0f);
    private const float LavaVisualFinalZScale = 100f;

    private float _lifetime;
    private float _damageInterval;
    private float _damageTimer;

    public static HotSoupPuddle Spawn(
        Vector3 position,
        float radius,
        float groundOffset,
        float lifetime,
        float damageInterval)
    {
        float floorY = FindFloorY(position);
        Vector3 spawnPosition = new(position.x, floorY + groundOffset + PuddleHalfHeight, position.z);

        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "LavaPuddle";
        go.tag = "Untagged";
        go.layer = LayerMask.NameToLayer("Default");
        go.transform.position = spawnPosition;
        go.transform.localScale = new Vector3(radius, PuddleHalfHeight, radius);

        Renderer rootRenderer = go.GetComponent<Renderer>();
        if (rootRenderer != null)
            Destroy(rootRenderer);

        Collider trigger = go.GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;

        CreateLavaVisual(go.transform, radius);

        var puddle = go.AddComponent<HotSoupPuddle>();
        puddle._lifetime = lifetime;
        puddle._damageInterval = damageInterval;
        puddle._damageTimer = 0f;
        return puddle;
    }

    private static void CreateLavaVisual(Transform parent, float radius)
    {
        GameObject lavaPrefab = Resources.Load<GameObject>(LavaResourcePath);
        if (lavaPrefab == null)
        {
            Debug.LogWarning($"[HotSoupPuddle] Resources/{LavaResourcePath} was not found. Using fallback cylinder.");
            CreateFallbackVisual(parent, radius);
            return;
        }

        GameObject visual = Instantiate(lavaPrefab, parent);
        visual.name = "Lava";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(LavaLocalEuler);
        visual.transform.localScale = Vector3.one;

        FitVisualToRadius(visual.transform, radius);
        visual.transform.localScale = new Vector3(
            visual.transform.localScale.x,
            visual.transform.localScale.y,
            LavaVisualFinalZScale);
        AlignVisualToPuddle(parent, visual.transform);
        DisableVisualColliders(visual);
    }

    private static void CreateFallbackVisual(Transform parent, float radius)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "LavaFallback";
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(radius, PuddleHalfHeight, radius);

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = renderer.material;
            Color color = new(0.85f, 0.25f, 0.1f, 0.6f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.color = color;
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        }
    }

    private static void FitVisualToRadius(Transform visual, float radius)
    {
        Bounds bounds = CalculateRendererBounds(visual);
        float diameter = Mathf.Max(0.01f, radius * 2f);
        float maxXZ = Mathf.Max(bounds.size.x, bounds.size.z);

        if (maxXZ <= 0.0001f)
        {
            visual.localScale = new Vector3(radius, PuddleHalfHeight, radius);
            return;
        }

        float scale = diameter / maxXZ;
        visual.localScale = Vector3.one * scale;
    }

    private static Bounds CalculateRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private static void AlignVisualToPuddle(Transform parent, Transform visual)
    {
        Bounds bounds = CalculateRendererBounds(visual);
        float desiredBottomY = parent.position.y - PuddleHalfHeight;
        Vector3 offset = new(
            parent.position.x - bounds.center.x,
            desiredBottomY - bounds.min.y,
            parent.position.z - bounds.center.z);

        visual.position += offset;
    }

    private static void DisableVisualColliders(GameObject visual)
    {
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
            collider.enabled = false;
    }

    private static float FindFloorY(Vector3 position)
    {
        float floorY = position.y;
        Ray ray = new(position + Vector3.up * 3f, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 10f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return floorY;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            Collider collider = hit.collider;
            if (collider == null) continue;
            if (collider.GetComponentInParent<GhostBase>() != null) continue;
            if (collider.GetComponentInParent<IPlayerStats>() != null) continue;
            if (collider.GetComponentInParent<HotSoupPuddle>() != null) continue;

            return hit.point.y;
        }

        return floorY;
    }

    void Update()
    {
        _damageTimer = Mathf.Max(0f, _damageTimer - Time.deltaTime);
        _lifetime -= Time.deltaTime;
        if (_lifetime <= 0f)
            Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        DealDamage(other);
        _damageTimer = _damageInterval;
    }

    void OnTriggerStay(Collider other)
    {
        if (_damageTimer > 0f) return;
        if (!other.CompareTag("Player")) return;

        DealDamage(other);
        _damageTimer = _damageInterval;
    }

    private void DealDamage(Collider other)
    {
        var stats = other.GetComponentInParent<IPlayerStats>()
                    ?? other.GetComponent<IPlayerStats>();
        stats?.TakeDamage(1);
    }
}
