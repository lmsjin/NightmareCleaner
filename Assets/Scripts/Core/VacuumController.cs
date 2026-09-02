using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// 사격(히트스캔) 컨트롤러. 좌클릭 홀드 연사 + 마우스 조준.
// 판정은 히트스캔(즉발 레이)이고, 연출로 총구→명중점으로 날아가는 트레이서 탄환을 스폰한다.
// 상시 조준 레이저(LineRenderer)를 총구→조준점에 표시한다.
// 탄약은 SuctionCharge(탄창/재장전)로 관리한다.
// (클래스명 VacuumController는 씬 재배선을 피하려 유지 — 내부는 사격 로직)
[RequireComponent(typeof(PlayerStats))]
public class VacuumController : MonoBehaviour
{
    [SerializeField] private LayerMask      ghostLayer;
    [SerializeField] private ParticleSystem suctionVFX;   // (미사용) 옛 흡입 VFX
    [SerializeField] private Transform      nozzle;        // 총구 위치
    [SerializeField] private LayerMask      obstacleMask;  // 사선을 막는 벽/지형(미설정 시 자동)

    [Header("조준 레이저 / 트레이서")]
    [SerializeField] private Color aimLaserColor = new Color(1f, 0.3f, 0.2f, 0.9f);
    [SerializeField] private float aimLaserWidth = 0.04f;
    [SerializeField] private float fireLaserWidth = 0.14f;
    [SerializeField] private Color tracerColor = new Color(1f, 0.85f, 0.4f, 1f);
    [SerializeField] private float tracerWidth = 0.08f;
    [SerializeField] private float tracerLength = 1.4f;

    [Header("조준 레티클(동그란 조준선)")]
    [SerializeField] private Color reticleColor = new Color(1f, 0.3f, 0.2f, 0.9f);
    [SerializeField] private float reticleRadius = 0.4f;
    [SerializeField] private float reticleWidth = 0.05f;
    [SerializeField] private float reticleGroundOffset = 0.05f;

    [Header("명중 판정 / 조준 높이")]
    [SerializeField] private float hitRadius = 0.5f;   // 판정 빔(캡슐) 반경 — 클수록 관대
    [SerializeField] private float aimHeight = 0.7f;   // 지면 위 사격·레이저 시작 높이(유령 몸통). 총구가 지면 아래라 보정

    [Header("효과음")]
    [SerializeField] private AudioSource fireAudioSource;   // 비워두면 노즐(없으면 자신)에 자동 생성
    [SerializeField] private AudioClip   fireLoopClip;      // 발사 중 반복 사운드(원샷 클립 없을 때 대체)
    [SerializeField, Range(0f, 1f)] private float fireVolume = 0.8f;
    [SerializeField]                private float volumeFadeSpeed = 12f;
    [SerializeField, Range(0f, 1f)] private float audioSpatialBlend = 0f;

    private PlayerStats      _stats;
    private SuctionCharge    _charge;   // 탄약(없으면 무제한)
    private PlayerController  _player;   // 조준점 소스
    private int              _obstacleMask;

    private bool  _firing;     // 이번 프레임 발사 여부(애니/연출)
    private float _fireFlash;  // 발사 직후 레이저 강조 잔여시간
    private float _fireTimer;  // 연사 간격 카운트다운
    private bool  _audioOn;

    private LineRenderer _aimLaser;
    private LineRenderer _reticle;

    private float _fireInterval;
    private float _tracerSpeed;
    private float _knockback;

    public bool    IsFiring       => _firing || _fireFlash > 0f;
    public Vector3 NozzlePosition => nozzle != null ? nozzle.position : transform.position;

    void Start()
    {
        _stats  = GetComponent<PlayerStats>();
        _charge = GetComponent<SuctionCharge>();
        _player = GetComponent<PlayerController>();

        _obstacleMask = obstacleMask.value != 0 ? obstacleMask.value : ~ghostLayer.value;

        var cfg = BalanceConfig.Resolve(null);
        _fireInterval = cfg != null ? cfg.fireInterval       : 0.15f;
        _tracerSpeed  = cfg != null ? cfg.bulletTracerSpeed  : 60f;
        _knockback    = cfg != null ? cfg.ghostKnockbackForce : 6f;

        if (suctionVFX != null) suctionVFX.Stop();
        CreateAimLaser();
        CreateReticle();
        InitFireAudio();
    }

    void Update()
    {
        _fireFlash = Mathf.Max(0f, _fireFlash - Time.deltaTime);
        if (_fireTimer > 0f) _fireTimer -= Time.deltaTime;
        _firing = false;

        bool canAct = Time.timeScale != 0f;

        // 사격·레이저 원점 = 총구 XZ + 조준 평면 높이(Y). PlayerController의 조준 평면과 Y를 일치시켜
        // 레이저가 완전 수평이 되게 한다(_player 없으면 aimHeight 폴백).
        float aimY = (_player != null && _player.HasAim)
            ? _player.AimPoint.y
            : transform.position.y + aimHeight;
        Vector3 origin = new Vector3(NozzlePosition.x, aimY, NozzlePosition.z);
        Vector3 aimDir = GetAimDirection(origin, out Vector3 aimPoint);

        // R키 수동 재장전
        if (canAct && _charge != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            _charge.StartReload();

        // 좌클릭 홀드 연사
        bool wantFire = canAct && Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (wantFire && _fireTimer <= 0f && aimDir.sqrMagnitude > 0.0001f)
        {
            if (_charge == null || _charge.TryConsume())
            {
                Fire(origin, aimDir);
                _fireTimer = _fireInterval;
            }
        }

        UpdateAimLaser(origin, aimDir, aimPoint, canAct);
        UpdateReticle(aimPoint, canAct);
        UpdateFireAudio(wantFire && (_charge == null || !_charge.IsReloading));
    }

    // 마우스 조준점 방향(수평). PlayerController가 계산한 AimPoint 재사용.
    Vector3 GetAimDirection(Vector3 origin, out Vector3 aimPoint)
    {
        if (_player != null && _player.HasAim)
        {
            Vector3 t = _player.AimPoint;
            t.y = origin.y;
            Vector3 d = t - origin;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f)
            {
                aimPoint = t;
                return d.normalized;
            }
        }

        Vector3 f = transform.forward; f.y = 0f;
        f = f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
        aimPoint = origin + f * _stats.SuctionRange;
        return f;
    }

    void Fire(Vector3 origin, Vector3 dir)
    {
        _firing = true;
        _fireFlash = 0.05f;

        float range = _stats.SuctionRange;
        Vector3 hitPoint = origin + dir * range;

        // 두꺼운 빔(캡슐 겹침) — SphereCast의 '시작 겹침 무시' 사각이 없어 크고 작은 유령 모두 검출.
        Collider[] cols = Physics.OverlapCapsule(origin, origin + dir * range, hitRadius,
            ghostLayer, QueryTriggerInteraction.Ignore);

        Collider best = null;
        float bestT = float.MaxValue;
        Vector3 bestCenter = hitPoint;
        foreach (var c in cols)
        {
            Vector3 center = c.bounds.center;
            float t = Vector3.Dot(center - origin, dir); // 전방 진행 거리
            if (t < -hitRadius) continue;                // 뒤쪽 제외
            if (IsBlockedByWall(origin, center)) continue;
            if (t < bestT) { bestT = t; best = c; bestCenter = center; }
        }

        if (best != null)
        {
            hitPoint = bestCenter; // 트레이서를 맞은 유령까지

            var dmg = best.GetComponentInParent<IDamageable>();
            dmg?.TakeDamage(_stats.SuctionPower);

            var kb = best.GetComponentInParent<IKnockbackable>();
            kb?.ApplyKnockback(dir, _knockback);
        }
        else if (TryNearestWall(origin, dir, range, out float wallDist))
        {
            // 유령 미명중 — 트레이서를 벽까지만 그려 자연스럽게
            hitPoint = origin + dir * wallDist;
        }

        SpawnTracer(origin, hitPoint);
    }

    // --- 조준 레이저 ---
    void CreateAimLaser()
    {
        var go = new GameObject("AimLaser");
        go.layer = 5; // UI — 미니맵 카메라 컬링에서 제외(메인 카메라는 렌더)
        go.transform.SetParent(transform, false);
        _aimLaser = go.AddComponent<LineRenderer>();
        _aimLaser.useWorldSpace = true;
        _aimLaser.positionCount = 2;
        _aimLaser.numCapVertices = 2;
        _aimLaser.textureMode = LineTextureMode.Stretch;
        _aimLaser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _aimLaser.receiveShadows = false;
        _aimLaser.material = new Material(Shader.Find("Sprites/Default"));
        _aimLaser.startColor = aimLaserColor;
        _aimLaser.endColor = new Color(aimLaserColor.r, aimLaserColor.g, aimLaserColor.b, aimLaserColor.a * 0.35f);
        _aimLaser.enabled = false;
    }

    void UpdateAimLaser(Vector3 origin, Vector3 dir, Vector3 aimPoint, bool canAct)
    {
        if (_aimLaser == null) return;

        bool show = canAct && (_player == null || _player.HasAim);
        _aimLaser.enabled = show;
        if (!show) return;

        // 도달 수평 거리 = 조준점(마우스)까지 → 커서가 가까우면 짧아진다. (사거리로 상한)
        Vector3 flat = aimPoint - origin; flat.y = 0f;
        float endDist = Mathf.Min(flat.magnitude, _stats.SuctionRange);

        // 사이에 벽/유령이 더 가까우면 거기서 멈춤
        if (TryNearestWall(origin, dir, endDist, out float wallDist)) endDist = Mathf.Min(endDist, wallDist);
        if (Physics.Raycast(origin, dir, out RaycastHit gh, endDist, ghostLayer, QueryTriggerInteraction.Ignore))
            endDist = Mathf.Min(endDist, gh.distance);

        // 원점(=조준 평면 높이)에서 완전 수평 → 바닥에 박히지 않고 레티클과 같은 높이에서 만남
        _aimLaser.SetPosition(0, origin);
        _aimLaser.SetPosition(1, origin + dir * endDist);

        float w = _fireFlash > 0f ? fireLaserWidth : aimLaserWidth;
        _aimLaser.startWidth = w;
        _aimLaser.endWidth = w * 0.5f;
    }

    // --- 조준 레티클(지면 원형 링) ---
    void CreateReticle()
    {
        var go = new GameObject("AimReticle");
        go.layer = 5; // UI — 미니맵 제외
        go.transform.SetParent(transform, false);
        _reticle = go.AddComponent<LineRenderer>();
        _reticle.useWorldSpace = true;
        _reticle.loop = true;
        _reticle.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _reticle.receiveShadows = false;
        _reticle.material = new Material(Shader.Find("Sprites/Default"));
        _reticle.startColor = reticleColor;
        _reticle.endColor = reticleColor;
        _reticle.startWidth = reticleWidth;
        _reticle.endWidth = reticleWidth;

        const int segments = 24;
        _reticle.positionCount = segments;
        _reticle.enabled = false;
    }

    void UpdateReticle(Vector3 aimPoint, bool canAct)
    {
        if (_reticle == null) return;

        bool show = canAct && (_player == null || _player.HasAim);
        _reticle.enabled = show;
        if (!show) return;

        // 진짜 지면 조준점(마우스→바닥) 위에 링을 얹는다. aimPoint는 총구 높이라 폴백용.
        Vector3 ground = (_player != null && _player.HasAim) ? _player.AimPoint : aimPoint;
        Vector3 center = ground + Vector3.up * reticleGroundOffset;
        int segments = _reticle.positionCount;
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            _reticle.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * reticleRadius);
        }
    }

    // --- 트레이서 탄환 ---
    void SpawnTracer(Vector3 from, Vector3 to)
    {
        var go = new GameObject("ShotTracer");
        go.layer = 5; // UI — 미니맵 제외
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = tracerColor;
        lr.endColor = new Color(tracerColor.r, tracerColor.g, tracerColor.b, 0f);
        lr.startWidth = tracerWidth;
        lr.endWidth = tracerWidth * 0.4f;
        StartCoroutine(TracerRoutine(go, lr, from, to));
    }

    IEnumerator TracerRoutine(GameObject go, LineRenderer lr, Vector3 from, Vector3 to)
    {
        float total = Vector3.Distance(from, to);
        float traveled = 0f;
        while (traveled < total)
        {
            traveled += _tracerSpeed * Time.deltaTime;
            Vector3 head = Vector3.MoveTowards(from, to, traveled);
            Vector3 tail = Vector3.MoveTowards(from, to, Mathf.Max(0f, traveled - tracerLength));
            lr.SetPosition(0, tail);
            lr.SetPosition(1, head);
            yield return null;
        }
        Destroy(go);
    }

    // --- 사선 차단 검사(플레이어 자신/자식 제외) ---
    bool IsBlockedByWall(Vector3 origin, Vector3 target)
    {
        Vector3 dir = target - origin;
        float dist = dir.magnitude;
        if (dist < 0.0001f) return false;
        return TryNearestWall(origin, dir / dist, dist, out _);
    }

    bool TryNearestWall(Vector3 origin, Vector3 dir, float maxDist, out float dist)
    {
        dist = maxDist;
        bool found = false;
        var hits = Physics.RaycastAll(origin, dir, maxDist, _obstacleMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            var t = h.collider.transform;
            if (t == transform || t.IsChildOf(transform)) continue; // 자신/자식은 벽 아님
            if (h.distance < dist) { dist = h.distance; found = true; }
        }
        return found;
    }

    // --- 발사 사운드 (홀드 중 루프) ---
    void InitFireAudio()
    {
        if (fireAudioSource == null)
        {
            var host = nozzle != null ? nozzle.gameObject : gameObject;
            fireAudioSource = host.AddComponent<AudioSource>();
            fireAudioSource.spatialBlend = audioSpatialBlend;
        }
        fireAudioSource.playOnAwake = false;
        fireAudioSource.loop        = true;
        fireAudioSource.clip        = fireLoopClip;
        fireAudioSource.volume      = 0f;
    }

    void UpdateFireAudio(bool on)
    {
        if (fireAudioSource == null) return;

        if (on && !_audioOn && fireLoopClip != null && !fireAudioSource.isPlaying)
            fireAudioSource.Play();
        _audioOn = on;

        float target = on ? fireVolume : 0f;
        fireAudioSource.volume = Mathf.MoveTowards(
            fireAudioSource.volume, target, volumeFadeSpeed * Time.unscaledDeltaTime);

        if (!on && fireAudioSource.isPlaying && fireAudioSource.volume <= 0.0001f)
            fireAudioSource.Stop();
    }
}
