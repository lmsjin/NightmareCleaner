# Mansion Clash — AGENTS

> 설계·계약·구조는 [`CLAUDE.md`](./CLAUDE.md)가 단일 출처입니다.

1. 실제 게임은 `Assets/NC`입니다. `Assets/Art_Reserve`와 `Assets/UnityChan`은 아직 쓰지 않는 보관용 아트입니다.
2. 판정은 Host만 확정합니다. 처리 순서는 `NCSession.TickMatch`를 따릅니다.
3. 수치는 `NCBalance`와 `Assets/NC/Resources/NCBalance.asset`에서 관리합니다.
4. 빌드 씬은 `NC_Title`, `NC_Game` 두 개입니다.
5. UI에 새 글자를 쓰면 `NCUi.Glyphs`에 추가합니다.
6. 사용자에게 하는 모든 대답은 한글로 씁니다(설명·질문·요약·최종 보고 포함). 코드·파일명·식별자만 원문 그대로 둡니다.
