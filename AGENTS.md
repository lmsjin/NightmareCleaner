# Nightmare Cleaner — AGENTS

> **이 프로젝트의 설계·계약·구조 문서는 [`CLAUDE.md`](./CLAUDE.md)가 단일 출처(single source of truth)입니다.**
> AGENTS.md는 중복을 피하기 위해 포인터로만 유지합니다. 모든 상세(모듈 경계, 이벤트 버스,
> 인터페이스 계약, BalanceConfig 필드, 폴더 구조, 미구현/TODO)는 `CLAUDE.md`를 참고하세요.

## 에이전트가 꼭 지킬 핵심 규칙

1. **계약 파일 불변** — `Assets/Scripts/Core/GameEvents.cs`, `IPlayerStats.cs`는 모듈 A 담당자만
   수정한다. 나머지는 **읽기·구독만**.
2. **직접 참조 금지** — 다른 모듈 클래스를 직접 참조하지 말고 반드시 `GameEvents`(이벤트) 또는
   인터페이스(`IPlayerStats`/`IDamageable`/`ISuctionable`/`IAbilityStatsReadOnly`)로 통신한다.
3. **수치 하드코딩 금지** — 모든 수치는 `Assets/Resources/Configs/BalanceConfig.asset`에서 읽는다.
   새 수치가 필요하면 `BalanceConfig.cs`에 필드를 추가하고 거기서 읽어 쓴다.
4. **폴더 규칙** — 새 스크립트는 소속 모듈 폴더(Core/Ghost/Child/Ability/UI)에 배치한다.
5. **씬 구성** — `start` → `SampleScene` → `end` 3개 씬이 모두 Build Settings에 등록돼 있어야 한다.
6. 한글이 □로 보이면 상단 메뉴 **NightmareCleaner → Setup Korean Font**를 1회 실행한다.

> 전체 계약과 최신 구조는 반드시 [`CLAUDE.md`](./CLAUDE.md)에서 확인하세요.
