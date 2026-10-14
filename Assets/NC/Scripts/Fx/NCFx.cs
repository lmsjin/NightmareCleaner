using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 순수 연출 모음(네트워크 상태 아님). Host가 RPC로 "여기서 일어났다"고 알리면 각 PC가 그린다.
/// 정식 이펙트가 오기 전까지 기본 도형으로 만든 임시 연출이다(배경·이펙트 담당이 교체).
///
/// 성능: 연사·소멸이 잦아도 끊기지 않도록
///  - 조각은 **미리 만들어 재사용(풀링)** 하고 지우지 않는다.
///  - 머티리얼은 **공용 1개**만 쓰고, 색은 MaterialPropertyBlock으로 바꾼다(복제본을 만들지 않음).
///  - 콜라이더가 생기는 CreatePrimitive 대신 내장 메시로 렌더러만 만든다.
/// </summary>
public static class NCFx
{
    private static Material _shared;
    private static MaterialPropertyBlock _block;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static readonly Stack<NCFxPuff> PuffPool = new Stack<NCFxPuff>();
    private static readonly Stack<NCFxShot> ShotPool = new Stack<NCFxShot>();
    private static readonly Stack<NCFxWarning> WarningPool = new Stack<NCFxWarning>();

    private static Material Shared
    {
        get
        {
            if (_shared == null)
            {
                _shared = new Material(Shader.Find("Sprites/Default")) { name = "NC_FxShared" };
            }

            return _shared;
        }
    }

    public static Mesh SphereMesh => Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
    public static Mesh CylinderMesh => Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
    public static Mesh CubeMesh => Resources.GetBuiltinResource<Mesh>("Cube.fbx");

    /// <summary>렌더러만 있는 도형(콜라이더 없음, 공용 머티리얼).</summary>
    public static MeshRenderer NewShape(Mesh mesh, string name, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Shared;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    /// <summary>머티리얼 복제 없이 색만 바꾼다.</summary>
    public static void SetColor(Renderer renderer, Color color)
    {
        _block ??= new MaterialPropertyBlock();
        _block.SetColor(ColorId, color);
        renderer.SetPropertyBlock(_block);
    }

    // ───────── 공개 연출 ─────────

    /// <summary>구름탄: 몽글몽글한 구름 덩어리가 총구에서 명중 지점까지 빠르게 날아간다.</summary>
    public static void Shot(Vector3 from, Vector3 to, Color slotColor, bool hit)
    {
        NCFxShot shot = Pop(ShotPool);
        if (shot == null)
        {
            shot = new GameObject("NC_Shot").AddComponent<NCFxShot>();
        }

        shot.Play(from, to, Color.Lerp(Color.white, slotColor, 0.35f), hit);
    }

    /// <summary>출현 예고: 바닥의 붉은 원이 조여 들며 깜빡인다. seconds 뒤 사라진다.</summary>
    public static void SpawnWarning(Vector3 position, float seconds)
    {
        NCFxWarning warning = Pop(WarningPool);
        if (warning == null)
        {
            warning = new GameObject("NC_SpawnWarning").AddComponent<NCFxWarning>();
        }

        warning.Play(position + Vector3.up * 0.03f, seconds);
    }

    /// <summary>유령 소멸: 몸 색 구름이 퍼지며 사라진다.</summary>
    public static void GhostPop(Vector3 position, Color color)
    {
        if (Application.isPlaying)
        {
            Burst(position, color, 7, 0.45f, 2.6f, 0.45f);
        }
    }

    /// <summary>더미 정리: 먼지 구름.</summary>
    public static void DustPuff(Vector3 position)
    {
        Burst(position, new Color(0.85f, 0.8f, 0.72f, 0.8f), 9, 0.5f, 1.8f, 0.6f);
    }

    /// <summary>명중 지점의 작은 구름 터짐.</summary>
    public static void Impact(Vector3 position, Color color)
    {
        Burst(position, color, 4, 0.25f, 1.8f, 0.25f);
    }

    private static void Burst(Vector3 position, Color color, int count, float size, float speed, float life)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y) * 0.6f;
            GetPuff().Play(position, size, color, direction * speed, life);
        }
    }

    /// <summary>다운 표식: 머리 위 붉은 십자(회전). 플레이어마다 한 번만 만든다.</summary>
    public static GameObject CreateDownMarker(Transform parent)
    {
        GameObject root = new GameObject("NC_DownMarker");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0f, 2.4f, 0f);

        MeshRenderer a = NewShape(CubeMesh, "Bar", root.transform);
        a.transform.localScale = new Vector3(0.55f, 0.16f, 0.16f);
        SetColor(a, NCStyle.Danger);

        MeshRenderer b = NewShape(CubeMesh, "Bar", root.transform);
        b.transform.localScale = new Vector3(0.16f, 0.55f, 0.16f);
        SetColor(b, NCStyle.Danger);

        root.AddComponent<NCFxSpin>();
        root.SetActive(false);
        return root;
    }

    // ───────── 풀 ─────────

    public static NCFxPuff GetPuff()
    {
        NCFxPuff puff = Pop(PuffPool);
        if (puff == null)
        {
            MeshRenderer renderer = NewShape(SphereMesh, "NC_Puff");
            puff = renderer.gameObject.AddComponent<NCFxPuff>();
        }

        return puff;
    }

    public static void Return(NCFxPuff puff)
    {
        puff.gameObject.SetActive(false);
        PuffPool.Push(puff);
    }

    public static void Return(NCFxShot shot)
    {
        shot.gameObject.SetActive(false);
        ShotPool.Push(shot);
    }

    public static void Return(NCFxWarning warning)
    {
        warning.gameObject.SetActive(false);
        WarningPool.Push(warning);
    }

    /// <summary>풀에서 꺼낸다. 씬이 다시 불려 파괴된 항목은 건너뛴다.</summary>
    private static T Pop<T>(Stack<T> pool) where T : Component
    {
        while (pool.Count > 0)
        {
            T item = pool.Pop();
            if (item != null)
            {
                item.gameObject.SetActive(true);
                return item;
            }
        }

        return null;
    }
}

/// <summary>퍼지며 작아지고 투명해지는 구름 한 덩어리(풀링). 구름탄의 조각으로도 쓴다.</summary>
public class NCFxPuff : MonoBehaviour
{
    private Renderer _renderer;
    private Vector3 _velocity;
    private float _life;
    private float _age;
    private float _size;
    private Color _color;

    /// <summary>true면 스스로 움직이지 않고 구름탄(NCFxShot)이 위치를 정한다.</summary>
    public bool Driven { get; set; }

    public void Play(Vector3 position, float size, Color color, Vector3 velocity, float life)
    {
        _renderer ??= GetComponent<Renderer>();
        transform.position = position;
        transform.localScale = Vector3.one * size;
        _size = size;
        _color = color;
        _velocity = velocity;
        _life = Mathf.Max(0.01f, life);
        _age = 0f;
        Driven = false;
        NCFx.SetColor(_renderer, color);
    }

    private void Update()
    {
        if (Driven)
        {
            return;
        }

        _age += Time.deltaTime;
        float t = _age / _life;

        if (t >= 1f)
        {
            NCFx.Return(this);
            return;
        }

        transform.position += _velocity * Time.deltaTime;
        _velocity *= 1f - 4f * Time.deltaTime;
        transform.localScale = Vector3.one * (_size * (1f + t * 0.8f));
        NCFx.SetColor(_renderer, new Color(_color.r, _color.g, _color.b, _color.a * (1f - t)));
    }
}

/// <summary>구름탄 비행(풀링). 구름 조각 4개를 빌려 쓰고 끝나면 돌려준다.</summary>
public class NCFxShot : MonoBehaviour
{
    private const float Speed = 45f;
    private const int PuffCount = 4;
    private const float Stagger = 0.015f;

    private readonly NCFxPuff[] _puffs = new NCFxPuff[PuffCount];
    private Vector3 _from;
    private Vector3 _to;
    private float _duration;
    private float _time;
    private bool _hit;
    private Color _color;

    public void Play(Vector3 from, Vector3 to, Color color, bool hit)
    {
        _from = from;
        _to = to;
        _hit = hit;
        _color = color;
        _time = 0f;
        _duration = Mathf.Max(0.05f, Vector3.Distance(from, to) / Speed);

        for (int i = 0; i < PuffCount; i++)
        {
            float size = Mathf.Lerp(0.34f, 0.18f, i / (float)PuffCount);
            NCFxPuff puff = NCFx.GetPuff();
            puff.Play(from, size, color, Vector3.zero, 1f);
            puff.Driven = true;
            _puffs[i] = puff;
        }
    }

    private void Update()
    {
        _time += Time.deltaTime;

        for (int i = 0; i < PuffCount; i++)
        {
            // 뒤쪽 덩어리는 조금씩 늦게 따라온다.
            float t = Mathf.Clamp01((_time - i * Stagger) / _duration);
            Vector3 wobble = Vector3.up * (Mathf.Sin((t + i) * 12f) * 0.05f);
            _puffs[i].transform.position = Vector3.Lerp(_from, _to, t) + wobble;
        }

        if (_time < _duration + PuffCount * Stagger)
        {
            return;
        }

        for (int i = 0; i < PuffCount; i++)
        {
            NCFx.Return(_puffs[i]);
            _puffs[i] = null;
        }

        if (_hit)
        {
            NCFx.Impact(_to, _color);
        }

        NCFx.Return(this);
    }
}

/// <summary>출현 예고 원(풀링).</summary>
public class NCFxWarning : MonoBehaviour
{
    private const float Radius = 0.9f;

    private float _duration;
    private float _age;
    private MeshRenderer _disc;
    private MeshRenderer _ring;

    public void Play(Vector3 position, float seconds)
    {
        if (_disc == null)
        {
            _disc = NCFx.NewShape(NCFx.CylinderMesh, "Disc", transform);
            _ring = NCFx.NewShape(NCFx.CylinderMesh, "Ring", transform);
            _disc.transform.localScale = new Vector3(Radius * 2f, 0.01f, Radius * 2f);
            _disc.transform.localPosition = Vector3.up * 0.005f;
        }

        transform.position = position;
        _duration = Mathf.Max(0.1f, seconds);
        _age = 0f;
        Refresh();
    }

    private void Update()
    {
        _age += Time.deltaTime;

        if (_age >= _duration)
        {
            NCFx.Return(this);
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        float t = _age / _duration;

        // 바깥 링이 안쪽으로 조여 들어 "곧 나온다"를 알린다.
        float ring = Mathf.Lerp(Radius * 2.6f, Radius * 2f, t);
        _ring.transform.localScale = new Vector3(ring, 0.005f, ring);
        NCFx.SetColor(_ring, new Color(1f, 0.8f, 0.85f, 0.25f + 0.35f * t));

        float blink = 0.5f + 0.5f * Mathf.Sin(_age * Mathf.Lerp(10f, 26f, t));
        Color c = NCStyle.GhostTelegraph;
        NCFx.SetColor(_disc, new Color(c.r, c.g, c.b, Mathf.Lerp(0.2f, 0.6f, blink)));
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
