using UnityEngine;

// 사격 명중 시 유령에게 넉백을 통지하는 계약.
// VacuumController(사격 컨트롤러)가 히트스캔으로 맞춘 대상에 ApplyKnockback을 1회 호출한다.
// 모듈 B 유령(GhostBase)이 구현해 명중 순간 뒤로 밀리는 타격감을 연출한다.
// (구 흡입 계약 ISuctionable.OnSuctioned를 대체 — 파일명은 유지)
public interface IKnockbackable
{
    void ApplyKnockback(Vector3 dir, float force);
}
