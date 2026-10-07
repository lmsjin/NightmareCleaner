using UnityEngine;

/// <summary>
/// 유령이 나타나기 전 출현 지점 바닥에 깜빡이는 빨간 원(출현 예고)이다. 기획서: 등장 1.5초 전 예고.
///
/// 순수한 "보여주기" 연출이라 네트워크 상태가 아니다.
/// Host가 RPC로 "여기에 곧 나온다"고 알려주면 각 컴퓨터가 알아서 그린다.
/// 정해진 시간이 지나면 스스로 사라진다. (정식 이펙트는 그래픽 담당이 교체)
/// </summary>
public class FusionSpawnWarning : MonoBehaviour
{
    private const float Radius = 0.9f;        // 원 반지름(m)
    private const float BlinkSpeed = 8.0f;    // 깜빡임 속도

    private float _duration;
    private float _elapsed;
    private Material _material;

    /// <summary>
    /// 출현 예고 원을 하나 만들어 화면에 보여준다.
    /// </summary>
    /// <param name="position">유령이 나타날 바닥 위치</param>
    /// <param name="seconds">예고 시간(이 시간이 끝나는 순간 유령이 생성된다)</param>
    public static void Spawn(Vector3 position, float seconds)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "FusionSpawnWarning";

        // 연출용이므로 충돌을 없앤다(플레이어·탄환·NavMesh에 영향 없음).
        Destroy(go.GetComponent<Collider>());

        // 바닥에 살짝 띄운 납작한 원판
        go.transform.position = position + Vector3.up * 0.03f;
        go.transform.localScale = new Vector3(Radius * 2f, 0.01f, Radius * 2f);

        Renderer renderer = go.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        FusionSpawnWarning warning = go.AddComponent<FusionSpawnWarning>();
        warning._duration = Mathf.Max(0.1f, seconds);
        warning._material = new Material(Shader.Find("Sprites/Default"));
        renderer.material = warning._material;
        warning.Refresh();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;

        if (_elapsed >= _duration)
        {
            Destroy(gameObject);
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        float t = _elapsed / _duration;

        // 시간이 지날수록 원이 작게 시작해 꽉 차고, 깜빡임이 진해진다 → "곧 나온다"는 느낌
        float scale = Mathf.Lerp(0.4f, 1.0f, t) * Radius * 2f;
        transform.localScale = new Vector3(scale, 0.01f, scale);

        float blink = 0.5f + 0.5f * Mathf.Sin(_elapsed * BlinkSpeed * (1f + t));
        float alpha = Mathf.Lerp(0.25f, 0.75f, blink);
        _material.color = new Color(1.0f, 0.2f, 0.2f, alpha);
    }

    private void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
        }
    }
}
