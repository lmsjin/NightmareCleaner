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

    /// <summary>탄환 비행 시작. 소멸 시점은 호출한 쪽이 StopAt으로 알린다.</summary>
    public static NCFxShot Launch(Vector3 from, Vector3 direction, float speed, float range, Color color)
    {
        NCFxShot shot = Pop(ShotPool);
        if (shot == null)
        {
            shot = new GameObject("NC_Shot").AddComponent<NCFxShot>();
        }

        shot.Play(from, direction, speed, range, color);
        return shot;
    }

    /// <summary>유령 소멸: 몸 색 구름이 퍼지며 사라진다.</summary>
    public static void GhostPop(Vector3 position, Color color)
    {
        if (Application.isPlaying)
        {
            Burst(position, color, 7, 0.45f, 2.6f, 0.45f);
        }
    }

    /// <summary>명중 지점의 작은 구름 터짐.</summary>
    public static void Impact(Vector3 position, Color color)
    {
        Burst(position, color, 4, 0.25f, 1.8f, 0.25f);
    }

    /// <summary>대쉬로 지나간 자리의 옅은 구름.</summary>
    public static void DashTrail(Vector3 position, Color color)
    {
        color.a = 0.45f;
        GetPuff().Play(position, 0.55f, color, Vector3.up * 0.2f, 0.3f);
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

/// <summary>탄환 비행(풀링). 등속으로 날아가며 구름 조각 꼬리를 끈다. 소멸은 StopAt으로만 한다.</summary>
public class NCFxShot : MonoBehaviour
{
    private const int PuffCount = 4;
    private const float Spacing = 0.13f;

    private readonly NCFxPuff[] _puffs = new NCFxPuff[PuffCount];
    private Vector3 _from;
    private Vector3 _direction;
    private float _speed;
    private float _range;
    private float _travelled;
    private Color _color;
    private bool _flying;

    public void Play(Vector3 from, Vector3 direction, float speed, float range, Color color)
    {
        _from = from;
        _direction = direction;
        _speed = speed;
        _range = range;
        _color = color;
        _travelled = 0f;
        _flying = true;
        transform.position = from;

        for (int i = 0; i < PuffCount; i++)
        {
            NCFxPuff puff = NCFx.GetPuff();
            puff.Play(from, Mathf.Lerp(0.32f, 0.16f, i / (float)PuffCount), color, Vector3.zero, 1f);
            puff.Driven = true;
            _puffs[i] = puff;
        }
    }

    private void Update()
    {
        _travelled = Mathf.Min(_travelled + _speed * Time.deltaTime, _range);
        transform.position = _from + _direction * _travelled;

        for (int i = 0; i < PuffCount; i++)
        {
            if (_puffs[i] != null)
            {
                _puffs[i].transform.position = _from + _direction * Mathf.Max(0f, _travelled - i * Spacing);
            }
        }
    }

    public void StopAt(Vector3 position, bool impact)
    {
        if (_flying == false)
        {
            return;
        }

        _flying = false;

        for (int i = 0; i < PuffCount; i++)
        {
            if (_puffs[i] != null)
            {
                NCFx.Return(_puffs[i]);
            }

            _puffs[i] = null;
        }

        if (impact)
        {
            NCFx.Impact(position, _color);
        }

        NCFx.Return(this);
    }
}
