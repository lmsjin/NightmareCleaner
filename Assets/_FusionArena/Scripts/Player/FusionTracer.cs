using UnityEngine;

/// <summary>
/// 총구에서 명중 지점까지 짧게 번쩍이는 트레이서(예광탄) 선이다.
///
/// 순수한 "보여주기" 연출이라 네트워크 상태가 아니다.
/// Host가 RPC로 "여기서 저기로 쐈다"고 알려주면 각 컴퓨터가 알아서 그린다.
/// 0.06초 뒤 스스로 사라진다.
/// </summary>
public class FusionTracer : MonoBehaviour
{
    // 남은 수명(초).
    private float _life = 0.06f;

    /// <summary>
    /// 트레이서 선을 하나 만들어 화면에 보여준다.
    /// </summary>
    /// <param name="from">총구 위치</param>
    /// <param name="to">명중 지점(또는 최대 사거리 끝)</param>
    public static void Spawn(Vector3 from, Vector3 to)
    {
        GameObject go = new GameObject("FusionTracer");

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));

        // 총구 쪽은 밝게, 끝으로 갈수록 투명해지도록 한다.
        lr.startColor = new Color(1.0f, 0.85f, 0.4f, 1.0f);
        lr.endColor = new Color(1.0f, 0.85f, 0.4f, 0.0f);
        lr.startWidth = 0.08f;
        lr.endWidth = 0.03f;

        lr.SetPosition(0, from);
        lr.SetPosition(1, to);

        go.AddComponent<FusionTracer>();
    }

    private void Update()
    {
        _life -= Time.deltaTime;

        if (_life <= 0.0f)
        {
            Destroy(gameObject);
        }
    }
}
