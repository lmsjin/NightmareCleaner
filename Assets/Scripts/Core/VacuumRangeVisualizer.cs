using UnityEngine;

// [사격 전환으로 무력화됨] 옛 흡입 부채꼴 표시기.
// 사격은 부채꼴이 아니라 조준 레이저(VacuumController가 생성)를 쓰므로 아무것도 그리지 않는다.
// 씬 Player에 남아있는 컴포넌트 참조를 깨지 않기 위해 클래스만 유지 — 추후 완전 제거 가능.
[RequireComponent(typeof(PlayerStats))]
public class VacuumRangeVisualizer : MonoBehaviour
{
    void Awake()
    {
        enabled = false; // 아무 동작도 하지 않음
    }
}
