using UnityEngine;

// [사격 전환으로 무력화됨] 옛 흡입 소용돌이 파티클.
// 사격 연출(조준 레이저/트레이서/총구 강조)은 VacuumController가 직접 처리하므로 비활성.
// 씬 Player에 남아있는 컴포넌트 참조를 깨지 않기 위해 클래스만 유지 — 추후 완전 제거 가능.
[RequireComponent(typeof(VacuumController))]
public class SuctionVortexVFX : MonoBehaviour
{
    void Awake()
    {
        enabled = false; // 아무 동작도 하지 않음
    }
}
