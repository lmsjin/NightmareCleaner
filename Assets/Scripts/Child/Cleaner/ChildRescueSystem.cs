using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using TMPro;

public class ChildRescueSystem : MonoBehaviour
{
    private const string SpawnZoneTag = "SpawnZone";

    private BalanceConfig _cfg;
    private RescueMiniGameManager _manager;
    private Transform _playerTf;

    private readonly List<RescueChild> _children = new();
    private readonly List<ChildSpawnPoint> _availablePoints = new();

    private readonly HashSet<Collider> _rescuedRooms = new();

    private bool _rescuing;
    private RescueChild _current;
    private Collider _currentZone;
    private float _spawnTimer;

    private Collider[] _spawnZones;

    [Header("Child Spawn")]
    [SerializeField] private GameObject childPrefab;
    [SerializeField] private string childResourcePath = "Character/model/kid";
    [SerializeField] private Vector3 childVisualEuler = new(0f, 180f, 0f);
    [SerializeField, Range(0.1f, 2f)] private float childVisualScale = 0.75f;

    [Header("Editable Rescue Prompt")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string promptMessage = "[E] 아이 구출";

    void Awake()
    {
        _cfg = BalanceConfig.Resolve(null);

        if (_cfg == null)
            Debug.LogError("[ChildRescueSystem] BalanceConfig 로드 실패!");
        else
            Debug.Log($"[ChildRescueSystem] BalanceConfig 로드 성공 — " +
                      $"childCount:{_cfg.childCount}, interval:{_cfg.childSpawnInterval}");

        _manager = GetComponent<RescueMiniGameManager>();
        if (_manager == null)
            Debug.LogError("[ChildRescueSystem] RescueMiniGameManager가 씬에 연결되지 않았습니다.");
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    void Start()
    {
        StartCoroutine(InitializeAfterSceneReady());
    }

    IEnumerator InitializeAfterSceneReady()
    {
        yield return null; // 1프레임 대기 — 모든 오브젝트의 Awake/OnEnable 완료 보장

        EnsurePlayerTransform();

        CacheSpawnZones();
        CollectSpawnPoints();
        SpawnUntilCap();
    }

    void CacheSpawnZones()
    {
        var list = new List<Collider>();
        var colliders = FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var collider in colliders)
        {
            if (collider != null && collider.CompareTag(SpawnZoneTag))
                list.Add(collider);
        }

        _spawnZones = list.ToArray();

        if (_spawnZones.Length == 0)
            Debug.LogWarning("[ChildRescueSystem] No Collider with the 'SpawnZone' tag was found.");
        else
            Debug.Log($"[ChildRescueSystem] 스폰존 {_spawnZones.Length}개 캐시 완료");
    }

    void CollectSpawnPoints()
    {
        var all = FindObjectsByType<ChildSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var sp in all)
            if (sp != null)
                _availablePoints.Add(sp);

        Shuffle(_availablePoints);

        Debug.Log($"[ChildRescueSystem] 사용 가능한 스폰 포인트: {_availablePoints.Count}개");
    }

    void Update()
    {
        EnsurePlayerTransform();
        HandleChildSpawning();

        if (_rescuing || _playerTf == null) { ShowPrompt(false); return; }

        _children.RemoveAll(c => c == null);
        var near = Nearest();
        ShowPrompt(near != null);
        if (near == null) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            StartRescue(near);
    }

    void HandleChildSpawning()
    {
        _children.RemoveAll(c => c == null);

        int cap = _cfg != null ? Mathf.Max(0, _cfg.childCount) : 3;
        if (_children.Count >= cap) return;

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;

        _spawnTimer = _cfg != null ? Mathf.Max(0.1f, _cfg.childSpawnInterval) : 10f;

        SpawnOneChild();
    }

    void EnsurePlayerTransform()
    {
        if (_playerTf != null) return;

        var ps = FindFirstObjectByType<PlayerStats>();
        if (ps != null)
        {
            _playerTf = ps.transform;
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerTf = player.transform;
    }

    void SpawnUntilCap()
    {
        int cap = _cfg != null ? Mathf.Max(0, _cfg.childCount) : 3;
        for (int i = _children.Count; i < cap; i++)
        {
            if (!SpawnOneChild())
                break;
        }

        _spawnTimer = _cfg != null ? Mathf.Max(0.1f, _cfg.childSpawnInterval) : 10f;
    }

    bool HasAvailableRoom()
    {
        if (_spawnZones == null || _spawnZones.Length == 0)
            return false; // 스폰존이 없으면 스폰 불가 (fallback 없음)

        foreach (var col in _spawnZones)
        {
            if (col == null) continue;
            if (_rescuedRooms.Contains(col)) continue;
            if (!IsRoomOccupied(col)) return true;
        }
        return false;
    }

    bool SpawnOneChild()
    {
        if (TrySpawnFromPool())
            return true;

        // fallback 제거 — 스폰 가능한 방이 없으면 그냥 스폰하지 않음
        Debug.Log("[ChildRescueSystem] 스폰 가능한 방이 없어 이번 스폰을 건너뜁니다.");
        return false;
    }

    bool TrySpawnFromPool()
    {
        if (_availablePoints.Count == 0) return false;

        Shuffle(_availablePoints);

        for (int i = 0; i < _availablePoints.Count; i++)
        {
            ChildSpawnPoint point = _availablePoints[i];
            if (point == null) { _availablePoints.RemoveAt(i); i--; continue; }

            Collider zone = GetZoneAt(point.transform.position);

            // 스폰존에 속하지 않는 포인트는 사용하지 않음 (제거)
            if (zone == null)
            {
                Debug.LogWarning($"[ChildRescueSystem] ChildSpawnPoint is outside SpawnZone and was skipped: {point.name} {point.transform.position}");
                _availablePoints.RemoveAt(i); i--;
                continue;
            }

            if (_rescuedRooms.Contains(zone))
            {
                _availablePoints.RemoveAt(i); i--;
                continue;
            }

            if (IsSpawnPointOccupied(point.transform.position)) continue;

            Vector3 raisedPos = GetSpawnPosition(point.transform.position, zone);
            var child = RescueChild.Create(raisedPos, childPrefab, childResourcePath, transform, childVisualEuler, childVisualScale);

            if (child == null)
            {
                continue;
            }

            _availablePoints.RemoveAt(i);
            _children.Add(child);
            Debug.Log($"[ChildRescueSystem] 아이 스폰: {raisedPos} | 남은 포인트: {_availablePoints.Count}");
            return true;
        }

        return false;
    }

    Vector3 GetSpawnPosition(Vector3 requestedPosition, Collider zone)
    {
        float searchRadius = _cfg != null ? Mathf.Max(0.1f, _cfg.childSpawnSearchRadius) : 1.5f;
        float radius = _cfg != null ? Mathf.Max(0.1f, _cfg.childSpawnClearanceRadius) : 0.35f;
        float height = _cfg != null ? Mathf.Max(radius * 2f, _cfg.childSpawnClearanceHeight) : 1.2f;
        float groundOffset = _cfg != null ? Mathf.Max(0.01f, _cfg.childSpawnGroundOffset) : 0.05f;

        Vector3 groundPosition = requestedPosition;
        if (NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, searchRadius, NavMesh.AllAreas) &&
            IsInsideZoneXZ(zone, hit.position, 0.05f))
        {
            groundPosition = hit.position;
        }

        Vector3 bottom = groundPosition + Vector3.up * (radius + groundOffset);
        Vector3 top = groundPosition + Vector3.up * (height - radius + groundOffset);
        if (Physics.CheckCapsule(bottom, top, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            Debug.LogWarning($"[ChildRescueSystem] 스폰 위치 주변에 충돌체가 있지만 아이를 생성합니다: {groundPosition}");

        return groundPosition + Vector3.up * (height * 0.5f + groundOffset);
    }

    Collider GetZoneAt(Vector3 worldPos)
    {
        foreach (var col in _spawnZones)
        {
            if (col == null) continue;
            if (IsInsideZoneXZ(col, worldPos, 0.25f)) return col;
        }
        return null;
    }

    bool IsInsideZoneXZ(Collider zone, Vector3 worldPos, float padding)
    {
        if (zone == null) return false;

        Bounds b = zone.bounds;
        b.Expand(new Vector3(padding, 0f, padding));
        return worldPos.x >= b.min.x && worldPos.x <= b.max.x &&
               worldPos.z >= b.min.z && worldPos.z <= b.max.z;
    }

    bool IsRoomOccupied(Collider zone)
    {
        Bounds b = zone.bounds;
        b.Expand(0.1f);
        foreach (var c in _children)
        {
            if (c == null) continue;
            if (b.Contains(c.transform.position)) return true;
        }
        return false;
    }

    bool IsSpawnPointOccupied(Vector3 spawnPosition)
    {
        float radius = _cfg != null ? Mathf.Max(0.1f, _cfg.childSpawnClearanceRadius) : 0.35f;
        float sqrRadius = radius * radius;

        foreach (var c in _children)
        {
            if (c == null) continue;
            Vector3 delta = c.transform.position - spawnPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude <= sqrRadius)
                return true;
        }

        return false;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    RescueChild Nearest()
    {
        float range = _cfg != null ? _cfg.childInteractRange : 2.5f;
        float best = range * range;
        RescueChild result = null;
        foreach (var c in _children)
        {
            if (c == null) continue;
            float d = (c.transform.position - _playerTf.position).sqrMagnitude;
            if (d <= best) { best = d; result = c; }
        }
        return result;
    }

    void StartRescue(RescueChild child)
    {
        _rescuing = true;
        _current = child;
        _currentZone = GetZoneAt(child.transform.position);
        ShowPrompt(false);
        Time.timeScale = 0f;
        GameEvents.RaiseMiniGameStart();
        if (_manager != null)
            _manager.Play(OnMiniGameDone);
        else
            OnMiniGameDone(0);
    }

    void OnMiniGameDone(int cleared)
    {
        Time.timeScale = PauseMenuController.IsPaused ? 0f : 1f;

        bool success = cleared > 0;
        GameEvents.RaiseMiniGameEnd(success);

        if (success)
        {
            int baseScore = _cfg != null ? _cfg.scoreChildRescue : 100;
            int perStage = _cfg != null ? _cfg.scorePerSequenceStage : 30;
            int score = baseScore + cleared * perStage;
            GameEvents.RaiseChildRescued(score);

            if (_current != null)
            {
                _children.Remove(_current);
                Destroy(_current.gameObject);
            }

            if (_currentZone != null)
                _rescuedRooms.Add(_currentZone);
        }
        else if (_current != null)
        {
            _current.FailCount++;
        }

        _rescuing = false;
        _current = null;
        _currentZone = null;
    }

    void ShowPrompt(bool on)
    {
        if (promptText != null)
            promptText.text = promptMessage;
        if (promptRoot != null && promptRoot.activeSelf != on)
            promptRoot.SetActive(on);
    }

#if UNITY_EDITOR
    public void SetupEditablePrompt()
    {
        if (promptRoot == null)
        {
            promptRoot = EditableMiniGameUI.CreateCanvas(transform, "RescuePromptCanvas", 80);
            Image background = EditableMiniGameUI.CreateImage(promptRoot.transform, "Background", null, new Vector2(360f, 70f));
            background.color = new Color(0f, 0f, 0f, 0.55f);
            RectTransform rect = background.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 160f);
            promptText = EditableMiniGameUI.CreateText(background.transform, "PromptText", 34f, Vector2.zero, new Vector2(360f, 70f));
            promptText.text = promptMessage;
        }
        promptRoot.SetActive(false);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}