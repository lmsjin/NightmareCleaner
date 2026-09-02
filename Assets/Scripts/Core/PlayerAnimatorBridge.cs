using UnityEngine;

// 애니메이션 캐릭터 연결 다리.
// 코드(CharacterController 이동 / VacuumController 흡입)의 상태를 Animator 파라미터로 전달한다.
// 플레이어 루트에 붙이면 자식에 있는 Animator를 자동으로 찾아 구동한다.
//  - 이동/회전은 PlayerController가 이미 처리 → 애니메이션은 "제자리 동작"만 필요(Apply Root Motion 끄기).
//  - Animator Controller에 아래 파라미터를 만들어 두면 자동 반영(없으면 그 항목만 조용히 무시):
//      float "Speed"      : 0(정지)~1(전력 이동)
//      bool  "Suctioning" : 흡입 중 여부
[RequireComponent(typeof(CharacterController))]
public class PlayerAnimatorBridge : MonoBehaviour
{
    [SerializeField] private string speedParam     = "Speed";
    [SerializeField] private string suctionParam   = "Suctioning";
    [SerializeField] private bool   normalizeSpeed = true; // 0~1로 정규화(블렌드 트리용)
    [SerializeField] private float  damp           = 0.1f; // 속도 전환 부드러움(초)

    private Animator           _anim;
    private CharacterController _cc;
    private PlayerStats        _stats;
    private VacuumController   _vacuum;

    private bool _hasSpeed, _hasSuction;

    void Start()
    {
        _cc     = GetComponent<CharacterController>();
        _stats  = GetComponent<PlayerStats>();
        _vacuum = GetComponent<VacuumController>();
        _anim   = GetComponentInChildren<Animator>();

        if (_anim != null)
        {
            _hasSpeed   = HasParam(speedParam,   AnimatorControllerParameterType.Float);
            _hasSuction = HasParam(suctionParam, AnimatorControllerParameterType.Bool);
        }
    }

    void Update()
    {
        if (_anim == null) return;

        Vector3 v = _cc.velocity; v.y = 0f;     // 수평 이동 속도
        float speed = v.magnitude;
        if (normalizeSpeed && _stats != null && _stats.MoveSpeed > 0.01f)
            speed = Mathf.Clamp01(speed / _stats.MoveSpeed);

        if (_hasSpeed)   _anim.SetFloat(speedParam, speed, damp, Time.deltaTime);
        // 사격 중 여부를 기존 "Suctioning" 파라미터로 전달(애니 컨트롤러 무수정 재사용)
        if (_hasSuction) _anim.SetBool(suctionParam, _vacuum != null && _vacuum.IsFiring);
    }

    // 컨트롤러에 해당 파라미터가 있을 때만 세팅(없으면 경고 방지)
    bool HasParam(string name, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var p in _anim.parameters)
            if (p.type == type && p.name == name) return true;
        return false;
    }
}
