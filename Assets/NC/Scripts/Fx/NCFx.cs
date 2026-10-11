using UnityEngine;

/// <summary>
/// 순수 연출 모음(네트워크 상태 아님). Host가 RPC로 "여기서 일어났다"고 알리면 각 PC가 그린다.
/// 정식 이펙트가 오기 전까지 프리미티브로 만든 임시 연출이다(배경·이펙트 담당이 교체).
/// </summary>
public static class NCFx
{
    private static Material _material;

    /// <summary>투명·무조명 공용 머티리얼(색은 인스턴스마다 바꾼다).</summary>
    public static Material NewMaterial(Color color)
    {
        if (_material == null)
        {
            _material = new Material(Shader.Find("Sprites/Default"));
        }

        return new Material(_material) { color = color };
    }

    /// <summary>프리미티브를 만들되 콜라이더는 없앤다(연출은 충돌·탄환·길찾기에 영향 없음).</summary>
    public static GameObject Primitive(PrimitiveType type, string name, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());

        Renderer renderer = go.GetComponent<Renderer>();
        renderer.material = NewMaterial(color);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go;
    }

    /// <summary>구름탄: 몽글몽글한 구름 덩어리가 총구에서 명중 지점까지 빠르게 날아간다.</summary>
    public static void Shot(Vector3 from, Vector3 to, Color slotColor, bool hit)
    {
        GameObject root = new GameObject("NC_Shot");
        NCFxShot shot = root.AddComponent<NCFxShot>();
        shot.Init(from, to, Color.Lerp(Color.white, slotColor, 0.35f), hit);
    }

    /// <summary>출현 예고: 바닥의 붉은 원이 조여 들며 깜빡인다. seconds 뒤 사라진다.</summary>
    public static void SpawnWarning(Vector3 position, float seconds)
    {
        GameObject root = new GameObject("NC_SpawnWarning");
        root.transform.position = position + Vector3.up * 0.03f;
        root.AddComponent<NCFxWarning>().Init(seconds);
    }

    /// <summary>유령 소멸: 몸 색 구름이 퍼지며 사라진다.</summary>
    public static void GhostPop(Vector3 position, Color color)
    {
        if (Application.isPlaying == false)
        {
            return;
        }

        Burst(position, color, 7, 0.45f, 2.6f, 0.45f);
    }

    /// <summary>더미 정리: 먼지 구름.</summary>
    public static void DustPuff(Vector3 position)
    {
        Burst(position, new Color(0.85f, 0.8f, 0.72f, 0.8f), 9, 0.5f, 1.8f, 0.6f);
    }

    private static void Burst(Vector3 position, Color color, int count, float size, float speed, float life)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject puff = Primitive(PrimitiveType.Sphere, "NC_Puff", color);
            puff.transform.position = position;
            puff.transform.localScale = Vector3.one * size;

            Vector3 direction = Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y) * 0.6f;
            puff.AddComponent<NCFxPuff>().Init(direction * speed, life);
        }
    }

    /// <summary>다운 표식: 머리 위 붉은 십자(회전).</summary>
    public static GameObject CreateDownMarker(Transform parent)
    {
        GameObject root = new GameObject("NC_DownMarker");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, 2.4f, 0f);

        Color red = NCStyle.Danger;
        GameObject a = Primitive(PrimitiveType.Cube, "Bar", red);
        a.transform.SetParent(root.transform, false);
        a.transform.localScale = new Vector3(0.55f, 0.16f, 0.16f);

        GameObject b = Primitive(PrimitiveType.Cube, "Bar", red);
        b.transform.SetParent(root.transform, false);
        b.transform.localScale = new Vector3(0.16f, 0.55f, 0.16f);

        root.AddComponent<NCFxSpin>();
        root.SetActive(false);
        return root;
    }
}

/// <summary>구름탄 비행.</summary>
public class NCFxShot : MonoBehaviour
{
    private const float Speed = 45f;
    private const int PuffCount = 4;

    private Transform[] _puffs;
    private Vector3 _from;
    private Vector3 _to;
    private float _duration;
    private float _time;
    private bool _hit;
    private Color _color;

    public void Init(Vector3 from, Vector3 to, Color color, bool hit)
    {
        _from = from;
        _to = to;
        _hit = hit;
        _color = color;
        _duration = Mathf.Max(0.05f, Vector3.Distance(from, to) / Speed);

        _puffs = new Transform[PuffCount];
        for (int i = 0; i < PuffCount; i++)
        {
            GameObject puff = NCFx.Primitive(PrimitiveType.Sphere, "Cloud", color);
            puff.transform.SetParent(transform, false);
            float size = Mathf.Lerp(0.34f, 0.18f, i / (float)PuffCount);
            puff.transform.localScale = Vector3.one * size;
            _puffs[i] = puff.transform;
        }

        Update();
    }

    private void Update()
    {
        _time += Time.deltaTime;

        for (int i = 0; i < _puffs.Length; i++)
        {
            // 뒤쪽 덩어리는 조금씩 늦게 따라온다.
            float t = Mathf.Clamp01((_time - i * 0.015f) / _duration);
            Vector3 side = Vector3.up * Mathf.Sin((t + i) * 12f) * 0.05f;
            _puffs[i].position = Vector3.Lerp(_from, _to, t) + side;
        }

        if (_time >= _duration + PuffCount * 0.015f)
        {
            if (_hit)
            {
                NCFxShotImpact.Spawn(_to, _color);
            }

            Destroy(gameObject);
        }
    }
}

/// <summary>명중 지점의 작은 구름 터짐.</summary>
public static class NCFxShotImpact
{
    public static void Spawn(Vector3 position, Color color)
    {
        for (int i = 0; i < 4; i++)
        {
            GameObject puff = NCFx.Primitive(PrimitiveType.Sphere, "Impact", color);
            puff.transform.position = position;
            puff.transform.localScale = Vector3.one * 0.25f;
            puff.AddComponent<NCFxPuff>().Init(Random.onUnitSphere * 1.8f, 0.25f);
        }
    }
}

/// <summary>퍼지며 작아지고 투명해지는 구름 한 덩어리.</summary>
public class NCFxPuff : MonoBehaviour
{
    private Vector3 _velocity;
    private float _life;
    private float _age;
    private Vector3 _startScale;
    private Material _material;
    private Color _color;

    public void Init(Vector3 velocity, float life)
    {
        _velocity = velocity;
        _life = life;
        _startScale = transform.localScale;
        _material = GetComponent<Renderer>().material;
        _color = _material.color;
    }

    private void Update()
    {
        _age += Time.deltaTime;
        float t = _age / _life;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += _velocity * Time.deltaTime;
        _velocity *= 1f - 4f * Time.deltaTime;
        transform.localScale = _startScale * (1f + t * 0.8f);
        _material.color = new Color(_color.r, _color.g, _color.b, _color.a * (1f - t));
    }
}

/// <summary>출현 예고 원.</summary>
public class NCFxWarning : MonoBehaviour
{
    private const float Radius = 0.9f;

    private float _duration;
    private float _age;
    private Transform _disc;
    private Transform _ring;
    private Material _discMaterial;
    private Material _ringMaterial;

    public void Init(float seconds)
    {
        _duration = Mathf.Max(0.1f, seconds);

        GameObject disc = NCFx.Primitive(PrimitiveType.Cylinder, "Disc", NCStyle.GhostTelegraph);
        disc.transform.SetParent(transform, false);
        _disc = disc.transform;
        _discMaterial = disc.GetComponent<Renderer>().material;

        GameObject ring = NCFx.Primitive(PrimitiveType.Cylinder, "Ring", Color.white);
        ring.transform.SetParent(transform, false);
        _ring = ring.transform;
        _ringMaterial = ring.GetComponent<Renderer>().material;

        Update();
    }

    private void Update()
    {
        _age += Time.deltaTime;
        float t = _age / _duration;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        // 바깥 링이 안쪽으로 조여 들어 "곧 나온다"를 알린다.
        float ring = Mathf.Lerp(Radius * 2.6f, Radius * 2f, t);
        _ring.localScale = new Vector3(ring, 0.005f, ring);
        _ringMaterial.color = new Color(1f, 0.8f, 0.85f, 0.25f + 0.35f * t);

        float blink = 0.5f + 0.5f * Mathf.Sin(_age * Mathf.Lerp(10f, 26f, t));
        _disc.localScale = new Vector3(Radius * 2f, 0.01f, Radius * 2f);
        _disc.localPosition = Vector3.up * 0.005f;
        Color c = NCStyle.GhostTelegraph;
        _discMaterial.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.2f, 0.6f, blink));
    }
}

/// <summary>천천히 도는 표식.</summary>
public class NCFxSpin : MonoBehaviour
{
    private void Update()
    {
        transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
        transform.localPosition = new Vector3(0f, 2.4f + Mathf.Sin(Time.time * 4f) * 0.08f, 0f);
    }
}
