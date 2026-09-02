using UnityEngine;

// [멀티 슬라이스] 총구→명중점으로 짧게 번쩍이는 트레이서 선. 순수 연출(게임 로직 아님).
// 각 클라이언트가 ShowTracerClientRpc를 받아 Spawn()으로 생성한다.
public class NetTracer : MonoBehaviour
{
    float _life = 0.06f;

    public static void Spawn(Vector3 from, Vector3 to)
    {
        var go = new GameObject("NetTracer");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(1f, 0.85f, 0.4f, 1f);
        lr.endColor   = new Color(1f, 0.85f, 0.4f, 0f);
        lr.startWidth = 0.08f;
        lr.endWidth   = 0.03f;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        go.AddComponent<NetTracer>();
    }

    void Update()
    {
        _life -= Time.deltaTime;
        if (_life <= 0f) Destroy(gameObject);
    }
}
