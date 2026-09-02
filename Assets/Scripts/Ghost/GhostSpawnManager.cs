using System.Collections.Generic;
using UnityEngine;

public class GhostSpawnManager : MonoBehaviour
{
    private BalanceConfig _cfg;
    private Transform _playerTf;
    private float _timer;
    private float _elapsed;
    private bool _stopped;
    private readonly List<GhostBase> _alive = new();

    void Awake() => _cfg = BalanceConfig.Resolve(null);

    void OnEnable()
    {
        GameEvents.OnGameOver += Stop;
        GameEvents.OnTimerEnd += Stop;
    }

    void OnDisable()
    {
        GameEvents.OnGameOver -= Stop;
        GameEvents.OnTimerEnd -= Stop;
    }

    void Start()
    {
        var ps = FindFirstObjectByType<PlayerStats>();
        if (ps != null) _playerTf = ps.transform;
        _timer = CurrentSpawnInterval;
    }

    void Update()
    {
        if (_stopped) return;
        _elapsed += Time.deltaTime;
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = CurrentSpawnInterval;
        _alive.RemoveAll(g => g == null);

        int currentMaxLimit = CurrentMaxGhostCount;
        if (_alive.Count >= currentMaxLimit) return;

        int spawnWaveCount = _cfg != null ? _cfg.spawnCountEachTime : 1;
        int count = Mathf.Min(spawnWaveCount, currentMaxLimit - _alive.Count);

        for (int i = 0; i < count; i++)
        {
            var ghost = SpawnNext();
            if (ghost != null) _alive.Add(ghost);
        }
    }

    void Stop() => _stopped = true;

    float ProgressT
    {
        get
        {
            float duration = _cfg != null ? _cfg.gameDurationSeconds : 600f;
            return Mathf.Clamp01(_elapsed / duration);
        }
    }

    float CurrentSpawnInterval
    {
        get
        {
            float start = _cfg != null ? _cfg.spawnIntervalSeconds : 5f;
            float min = _cfg != null ? _cfg.spawnIntervalMin : 1f;
            return Mathf.Lerp(start, min, ProgressT);
        }
    }

    // 시작값을 stepSeconds 간격으로 한 단계씩 endVal까지 끌어올리는 계단식 보간.
    // 게임 길이(gameDurationSeconds)가 끝나는 시점에 정확히 endVal에 도달하도록 단계 폭을 자동 계산.
    float StepLerp(float startVal, float endVal, float stepSeconds)
    {
        float duration = _cfg != null ? _cfg.gameDurationSeconds : 600f;
        stepSeconds = Mathf.Max(0.01f, stepSeconds);

        int totalSteps   = Mathf.Max(1, Mathf.FloorToInt(duration / stepSeconds));
        int elapsedSteps = Mathf.Min(totalSteps, Mathf.FloorToInt(_elapsed / stepSeconds));

        float stepAmount = (endVal - startVal) / totalSteps;
        return startVal + elapsedSteps * stepAmount;
    }

    int CurrentMaxGhostCount
    {
        get
        {
            float startMax = _cfg != null ? _cfg.maxGhostCountStart : 3f;
            float endMax   = _cfg != null ? _cfg.maxGhostCountMax   : 8f;
            float stepSec  = _cfg != null ? _cfg.stageStepSeconds   : 60f;
            return Mathf.RoundToInt(StepLerp(startMax, endMax, stepSec));
        }
    }

    GhostBase SpawnNext()
    {
        var points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        if (points != null && points.Length > 0)
        {
            float stepSec = _cfg != null ? _cfg.stageStepSeconds : 60f;
            float kitchenChance = _cfg != null
                ? StepLerp(_cfg.kitchenSpawnChanceStart, _cfg.kitchenSpawnChanceEnd, stepSec)
                : 0.5f;
            float storageChance = _cfg != null
                ? StepLerp(_cfg.storageSpawnChanceStart, _cfg.storageSpawnChanceEnd, stepSec)
                : 0.5f;

            var candidates = new List<SpawnPoint>();
            foreach (var p in points)
            {
                if (p.zone == GhostZone.Kitchen && Random.value >= kitchenChance) continue;
                if (p.zone == GhostZone.Storage && Random.value >= storageChance) continue;
                candidates.Add(p);
            }

            if (candidates.Count == 0)
                return GhostBase.Create<GhostJelly>(RandomAroundPlayer());

            var chosen = candidates[Random.Range(0, candidates.Count)];
            return CreateForZone(chosen.zone, chosen.transform.position);
        }
        return GhostBase.Create<GhostJelly>(RandomAroundPlayer());
    }

    GhostBase CreateForZone(GhostZone zone, Vector3 pos)
    {
        switch (zone)
        {
            case GhostZone.Kitchen: return GhostBase.Create<GhostChef>(pos);
            case GhostZone.Storage: return GhostBase.Create<GhostRepair>(pos);
            default:
                switch (Random.Range(0, 3))
                {
                    case 0: return GhostBase.Create<GhostJelly>(pos);
                    case 1: return GhostBase.Create<GhostChubby>(pos);
                    default: return GhostBase.Create<GhostSlim>(pos);
                }
        }
    }

    Vector3 RandomAroundPlayer()
    {
        Vector3 center = _playerTf != null ? _playerTf.position : transform.position;
        float radius = _cfg != null ? _cfg.spawnRadius : 10f;
        Vector2 c = Random.insideUnitCircle.normalized * radius;
        return new Vector3(center.x + c.x, center.y, center.z + c.y);
    }
}