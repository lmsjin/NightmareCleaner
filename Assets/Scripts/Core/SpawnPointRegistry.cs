using System.Collections.Generic;
using UnityEngine;

// 모듈 B(GhostSpawnManager)가 이 목록을 읽어 유령을 스폰함.
// 스폰 포인트 오브젝트(창문·벽장·변기)는 Start()에서 Register()를 호출해 등록.
public class SpawnPointRegistry : MonoBehaviour
{
    public static SpawnPointRegistry Instance { get; private set; }

    private readonly List<Transform> _points = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(Transform point)     => _points.Add(point);
    public void Unregister(Transform point)   => _points.Remove(point);
    public IReadOnlyList<Transform> GetAll()  => _points;
}
