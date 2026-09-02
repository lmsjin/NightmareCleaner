using System.Collections;
using Unity.Netcode;
using UnityEngine;

// [멀티 슬라이스] 플레이어 체력. 서버(호스트)가 권위 — 유령이 서버에서 TakeDamageServer를 호출한다.
// 피격 시: 무적(i-frame) + 넉백(소유자가 적용) + 깜빡임(모두 표시). HP 0이면 잠시 후 부활(제자리, 풀피).
// ※ 플레이어 프리팹에 이 컴포넌트를 추가한다.
[RequireComponent(typeof(NetworkObject))]
public class NetPlayerHealth : NetworkBehaviour
{
    public NetworkVariable<int>  Hp    = new NetworkVariable<int>();
    public NetworkVariable<int>  MaxHp = new NetworkVariable<int>();
    public NetworkVariable<bool> Dead  = new NetworkVariable<bool>();

    [SerializeField] float blinkInterval = 0.08f; // 깜빡임 주기

    private float _invSeconds     = 1f;   // 무적 지속(초)
    private float _respawnDelay   = 3f;   // 사망 후 부활까지(초)
    private float _knockbackForce = 6f;   // 피격 넉백 세기
    private float _invTimer;               // 서버 i-frame 타이머
    private float _respawnTimer;

    private Renderer[]         _renderers;
    private NetPlayerController _controller;
    private Coroutine          _blink;

    public override void OnNetworkSpawn()
    {
        _renderers  = GetComponentsInChildren<Renderer>(true); // 모든 클라(깜빡임용)
        _controller = GetComponent<NetPlayerController>();

        if (!IsServer) return;
        var c = BalanceConfig.Resolve(null);
        MaxHp.Value     = c != null ? c.playerMaxHP      : 5;
        _invSeconds     = c != null ? c.invincibleSeconds : 1f;
        _knockbackForce = c != null ? c.knockbackForce    : 6f;
        Hp.Value   = MaxHp.Value;
        Dead.Value = false;
    }

    void Update()
    {
        if (!IsServer) return;
        if (_invTimer > 0f) _invTimer -= Time.deltaTime;

        if (Dead.Value)
        {
            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f) Respawn();
        }
    }

    // 서버에서만 호출됨(유령의 접촉 공격 등). sourcePos = 때린 대상 위치(넉백 방향 계산).
    public void TakeDamageServer(int amount, Vector3 sourcePos)
    {
        if (!IsServer) return;
        if (Dead.Value || _invTimer > 0f) return; // 죽었거나 무적이면 무시

        Hp.Value = Mathf.Max(0, Hp.Value - amount);
        _invTimer = _invSeconds;

        Vector3 dir = transform.position - sourcePos; dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = -transform.forward;
        HitClientRpc(dir.normalized, _knockbackForce, _invSeconds); // 전원에 피격 연출

        if (Hp.Value <= 0)
        {
            Dead.Value = true;
            _respawnTimer = _respawnDelay;
        }
    }

    // 서버 → 전원: 깜빡임(모두) + 넉백(내 캐릭터만)
    [ClientRpc]
    void HitClientRpc(Vector3 dir, float force, float blinkDur)
    {
        if (_blink != null) StopCoroutine(_blink);
        _blink = StartCoroutine(Blink(blinkDur));

        if (IsOwner && _controller != null)
            _controller.ApplyKnockback(dir, force);
    }

    IEnumerator Blink(float dur)
    {
        float t = 0f;
        bool on = true;
        while (t < dur)
        {
            on = !on;
            SetRenderers(on);
            yield return new WaitForSeconds(blinkInterval);
            t += blinkInterval;
        }
        SetRenderers(true);
        _blink = null;
    }

    void SetRenderers(bool on)
    {
        if (_renderers == null) return;
        foreach (var r in _renderers) if (r != null) r.enabled = on;
    }

    void Respawn()
    {
        Hp.Value = MaxHp.Value;
        Dead.Value = false;
        _invTimer = _invSeconds; // 부활 직후 잠깐 무적
    }

    // 내 화면에만 내 HP 표시
    void OnGUI()
    {
        if (!IsOwner) return;
        string s = Dead.Value
            ? "DEAD - respawning..."
            : $"HP: {Hp.Value}/{MaxHp.Value}";
        GUI.Label(new Rect(10, 170, 300, 30), s);
    }
}
