using System;

// 내 버전(팀원판과 공존) — 구출 미니게임 공통 계약.
// Begin: 시작하며 끝나면 onComplete(클리어한 단계 수) 콜백. 0이면 실패.
// (게이지스톱처럼 단일 판정은 성공=1 / 실패=0으로 보고)
// Cancel: 콜백 없이 중단.
public interface IRescueMiniGame
{
    void Begin(Action<int> onComplete);
    void Cancel();
}
