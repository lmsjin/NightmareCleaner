# Nightmare Cleaner — 프로젝트 설계 문서

> 이 문서가 프로젝트 설계·계약의 **단일 출처(single source of truth)** 입니다.
> `AGENTS.md`는 이 문서를 가리키는 짧은 포인터로 축소되어 있습니다.
> 최종 최신화: 2026-07-09 — 실제 코드(`Assets/Scripts/`) 기준으로 전면 갱신.

## 게임 개요

| 항목 | 내용 |
|------|------|
| 장르 | 로그라이크 액션 |
| 시점 | 3D 쿼터뷰 |
| 플랫폼 | PC |
| 플레이타임 | 최대 10분 / 판 |
| 핵심 루프 | 유령 사격 처치 → 아이 구출(미니게임) → XP 레벨업 → 능력치 강화 → 기록 갱신 |

> **공격 방식 변경(실험)**: 초기 '흡입(청소기)' 방식에서 **사격(총)** 으로 전환됨.
> 좌클릭 홀드 연사 + 마우스 조준(상시 조준 레이저) + 히트스캔 판정 + 트레이서 탄환,
> 명중 시 유령 넉백, HP 0 시 제자리 소멸. 탄약/재장전(R키) 사용.
> 씬 재배선을 피하려 컴포넌트/클래스명(`VacuumController`/`SuctionCharge` 등)과 내부 스탯 이름
> (`SuctionPower`=공격력, `SuctionRange`=사거리)은 **유지**하고 내부 동작만 사격으로 바꿈.

---

## 팀 역할 분담 & 모듈 경계

> **핵심 원칙**: 모듈 간 직접 참조 금지. 반드시 `GameEvents` 이벤트 또는 인터페이스를 통해 통신.
> `GameEvents.cs` / `IPlayerStats.cs` 는 모듈 A 담당자만 수정한다(나머지는 읽기·구독만).
>
> **모듈 B(유령/아이)·C(성장/점수)는 이제 모두 구현 완료**되어 모듈 A와 연결돼 동작한다.
> (문서 초기 버전의 "미구현" 기술은 옛 내용이었으며 본 문서에서 정정됨)

### 모듈 A — 프레임워크 + 플레이어

> 상태: ✅ 완료 / 🟡 임시(추후 보강) / ⬜ 미구현

| 파일/클래스 | 상태 | 역할 |
|---|---|---|
| `Core/GameManager.cs` | ✅ | 게임 상태 기계(InGame/Paused/GameOver/Result), 타이머, 게임 오버/클리어 판정. 씬 진입 시 자동 시작(3-씬 구조) |
| `Core/GameEvents.cs` | ✅ | 전역 이벤트 버스 — 모든 모듈 간 통신 창구. `Raise*()` 메서드로만 발행, `ResetStatics()`로 Play 시작 시 초기화 |
| `Core/IPlayerStats.cs` | ✅ | 외부 모듈이 플레이어 스탯에 접근하는 인터페이스 |
| `Core/IDamageable.cs` | ✅ | **피해 계약** — VacuumController가 이걸로 유령에 데미지. 유령(GhostBase)이 구현 |
| `Core/ISuctionable.cs` | ✅ | **넉백 통지 계약**(`IKnockbackable`, 파일명만 유지) — 사격 명중 대상에 `ApplyKnockback(dir, force)` 1회 통지. 유령이 구현해 명중 시 뒤로 밀림 |
| `Core/IAbilityStatsReadOnly.cs` | ✅ | 능력 선택 UI가 현재 스탯(공격력/사거리/이속)을 **읽기 전용**으로 조회하는 계약. PlayerStats 구현, AbilitySystem 소비 |
| `Core/AbilityType.cs` | ✅ | 능력 enum(SuctionPower/SuctionRange/MoveSpeed). GameEvents가 의존하므로 Core에 위치 |
| `Core/PlayerController.cs` | ✅ | WASD 이동, 마우스 방향 전환, 미니게임 중 이동 잠금. 피격 시 넉백 + 깜빡임 연출(`OnHit`) |
| `Core/VacuumController.cs` | ✅ | **사격 컨트롤러**(클래스명 유지). 좌클릭 홀드 연사 + 마우스 조준. 히트스캔 `IDamageable` 데미지 + `IKnockbackable` 넉백. 상시 조준 레이저/트레이서/총구강조 자체 생성. 탄약은 SuctionCharge로 소모. R키 재장전. `IsFiring`/`NozzlePosition` 노출 |
| `Core/VacuumRangeVisualizer.cs` | 🟡 | **무력화됨**(옛 흡입 부채꼴). 사격은 조준 레이저를 쓰므로 아무것도 안 그림. 클래스만 유지(추후 제거) |
| `Core/SuctionVortexVFX.cs` | 🟡 | **무력화됨**(옛 흡입 소용돌이). 사격 연출은 VacuumController가 처리. 클래스만 유지(추후 제거) |
| `Core/SuctionCharge.cs` | ✅ | **탄약/탄창/재장전 관리**(클래스명·프로퍼티 유지). `TryConsume()` 발당 소모, 소진 시 자동/`R키` 수동 재장전(`reloadSeconds`). **아이 구출 시 탄창 상한↑**. HUD 호환 위해 `Ratio`/`IsOverheated`(재장전중) 유지 |
| `Core/PlayerStats.cs` | ✅ | 런타임 HP·이동속도·공격력(SuctionPower)·사거리(SuctionRange) 관리. 기본값은 BalanceConfig에서 읽음. 피격 시 무적(i-frame) + 넉백/깜빡임 요청. IPlayerStats·IAbilityStatsReadOnly 구현. 외부 직접 필드 수정 금지 — 메서드 사용 |
| `Core/PlayerAnimatorBridge.cs` | ✅ | 이동속도/사격 상태를 Animator 파라미터(`Speed` float, `Suctioning` bool=발사중)로 전달. 파라미터 없으면 무시 |
| `Core/CameraController.cs` | ✅ | 쿼터뷰 카메라 추적 + LookAt |
| `Core/SpawnPointRegistry.cs` | ✅ | 창문·벽장·변기 등 스폰 포인트 등록 — 모듈 B가 읽어감 |
| `Core/BgmManager.cs` | ✅ | 씬 이름별 BGM 자동 로드/크로스페이드. 자동 생성 + DontDestroyOnLoad. 레벨업 스팅어 덕킹, `PlayUiClick()` UI 효과음. `OnAbilityTrigger` 구독 |
| `Core/GameSystems.cs` | ✅ | **부트스트래퍼** — 빈 GameObject에 붙이면 씬에 없는 필수 시스템(SpawnPointRegistry/ScoreManager/GhostSpawnManager/AbilitySystem/AbilityApplier)을 자동 생성 |
| `Core/RoomZoneManager.cs` | ⬜ | 구역 태그 관리 — **미구현**(SpawnPoint/GhostZone/`SpawnZone` 태그로 대체 중) |
| `UI/HUDController.cs` | ✅ | (SampleScene) HP 하트·타이머·점수·구출수·흡입게이지·경험치바 표시. **점수는 계산하지 않고 ScoreManager 값을 읽어 표시만 함**. 게임 종료 시 `GameResult`에 담아 end 씬 전환 |
| `UI/StartMenuController.cs` | ✅ | (start 씬) 시작화면 메인/옵션/조작법 팝업 + 설정 저장(PlayerPrefs) |
| `UI/PauseMenuController.cs` | ✅ | (SampleScene) ESC 일시정지 + 옵션(사운드/해상도/조작) 탭. `IsPaused` 노출. 구출 미니게임 중이면 재개해도 timeScale 유지 |
| `UI/EndMenuController.cs` | ✅ | (end 씬) 결과 화면 — `GameResult` 읽어 클리어/게임오버 + 점수 표시, 재시작/메인/종료 |
| `UI/MinimapController.cs` | ✅ | 좌상단 원형 미니맵(탑다운 RenderTexture + 적/아이 블립). `M` 키로 전체지도 토글(일시정지) |
| `UI/UIRealTheme.cs` | ✅ | `Resources/UI/UI_REAL/` 실제 아트 스프라이트를 버튼·이미지·토글에 입히는 스킨 유틸 |
| `UI/OptionToggleVisual.cs` | ✅ | 토글(ON/OFF) 스위치 핸들을 좌우로 이동시키는 시각 컴포넌트 |
| `UI/UiClickSfx.cs` | ✅ | 클릭 UI에 붙여 `BgmManager.PlayUiClick()` 재생 |
| `UI/EditableMiniGameUI.cs` | ✅ | (에디터 전용) 미니게임용 Canvas/Image/Dim/Text를 코드로 생성하는 정적 헬퍼 |
| `Core/GameResult.cs` | ✅ | 씬 전환을 넘어 결과(Win/Score) 전달하는 static 홀더 |
| `Editor/UISetupTool.cs` | ✅ | (에디터 전용) 한글 폰트(맑은 고딕) TMP 세팅 + EventSystem 생성. 메뉴: NightmareCleaner > Setup Korean Font |
| `Energy.cs`(EnergyShooter) | 🟡 | 루트 위치. F키 발사 테스트 스크립트(구형 Input Manager) |
| `particle.cs`(VacuumParticleAttractor) | 🟡 | 루트 위치. 파티클을 청소기 입구로 끌어당겨 소멸시키는 흡입 연출 |

> **씬 흐름**: `start`(시작) → `SampleScene`(인게임) → `end`(결과) → 다시 SampleScene/start.
> 3개 씬 모두 Build Settings에 등록 필요.

### 모듈 B — 적 + 아이 시스템 (구현 완료)

**유령 (`Assets/Scripts/Ghost/`)**

| 파일/클래스 | 역할 |
|---|---|
| `GhostBase.cs` | 유령 공통 abstract 베이스. **IDamageable + IKnockbackable 구현**, HP/이동/접촉공격/NavMesh 추격, 명중 시 넉백, HP 0 시 **제자리 소멸**(스케일 축소·회전 후 `RaiseGhostKilled` + 파괴). `Create<T>()` 정적 팩토리, `XpReward` 노출 |
| `GhostJelly.cs` / `GhostChubby.cs` / `GhostSlim.cs` | 기본 유령 — 색/스케일/비주얼/스탯만 오버라이드, 베이스 추격·접촉 AI 사용 |
| `GhostRepair.cs` | 뚝딱이(특별★) — **돌진 상태머신**(Chasing→Dashing→Stunned). `IsSpecial=true`. 아이템 드랍은 `OnConsumeBegin`에 TODO만(미구현) |
| `GhostChef.cs` | 요리사(특별★) — **국물(장판) 상태머신**. `HotSoupPuddle`(트리거 장판, 밟으면 지속 피해) 포함. `IsSpecial=true` |
| `GhostSpawnManager.cs` | **시간 진행 기반 스폰** — 시간대별 스폰 간격·최대 유령 수 계단식 증가, zone별(Kitchen→Chef/Storage→Repair/Living→기본3종) 확률 스폰. `OnGameOver`/`OnTimerEnd`에 Stop 구독 |
| `GhostVisualSettings.cs` | ScriptableObject — id(Jelly/Chubby/Slim/Chef/Repair)별 프리팹/트랜스폼/콜라이더/틴트. `Resources/Configs/GhostVisualSettings` |
| `GhostZone.cs` | enum `GhostZone { Living, Kitchen, Storage }` — 스폰 존 분류 |
| `SpawnPoint.cs` | 씬 배치 스폰 마커. `zone` 지정, Start/OnDestroy에서 SpawnPointRegistry에 등록/해제, 기즈모 표시 |

**아이 구출 (`Assets/Scripts/Child/`)** — 두 경로가 공존 (통합 시 정리 필요, 미구현 섹션 참고)

| 파일/클래스 | 역할 |
|---|---|
| `Child/Cleaner/ChildRescueSystem.cs` | **정식 경로**. `SpawnZone` 태그/`ChildSpawnPoint`에 아이 스폰·보충, 최근접 아이 `E키` 상호작용, 결과 판정 → 이벤트 발행 |
| `Child/Cleaner/RescueMiniGameManager.cs` | 미니게임 2종 중 랜덤 선택 + 카운트다운 후 구동. `IsActive` 정적 노출(유령 등이 참조해 정지) |
| `Child/Cleaner/IRescueMiniGame.cs` | 미니게임 계약 — `Begin(Action<int> onComplete)`(클리어 단계 수, 0=실패), `Cancel()` |
| `Child/Cleaner/MiniGameSequence.cs` | 미니게임: 방향키(WASD) **순서입력(시퀀스)**, 다단계 |
| `Child/Cleaner/MiniGameGaugeStop.cs` | 미니게임: 좌우 왕복 마커를 중앙 성공 구간에서 E로 **타이밍 스톱**, 다단계 |
| `Child/Cleaner/RescueChild.cs` | 아이 인스턴스. `Create()` 팩토리(트리거 콜라이더 + 비주얼), `FailCount` 보유 |
| `Child/Cleaner/ChildSpawnPoint.cs` | 아이 스폰 위치 마커(빈 컴포넌트) |
| `Child/Cleaner/RescueBootstrap.cs` | `[ExecuteAlways]` — 참조 확인 + 에디터 컨텍스트 메뉴로 컴포넌트/UI 셋업 |
| `Child/ChildrenText.cs` + `Child/MiniGameController.cs` | **구(舊) 경로** — 아이 머리 위 `P키` 안내 UI + 자체 TAP/HOLD QTE 1종. `IRescueMiniGame` 미구현, 정식 매니저에 미등록 |

### 모듈 C — 성장 + 점수 + 밸런스 (구현 완료)

| 파일/클래스 | 역할 |
|---|---|
| `Ability/ScoreManager.cs` | **점수 + XP 권한 보유자**(싱글턴). `OnGhostKilled`/`OnChildRescued` 구독 → 점수·XP 누적, XP 임계값 도달 시 레벨업 + `RaiseAbilityTrigger` 발행. `CurrentScore/ChildCount/GhostKills/Level/XpRatio` 노출 |
| `Ability/AbilitySystem.cs` | `OnAbilityTrigger` 수신 → 능력 선택 카드 UI 표시(패널 열 때 timeScale=0, 연속 레벨업 큐잉). `IAbilityStatsReadOnly`로 현재값 미리보기. 선택 시 `RaiseAbilitySelected` 발행 |
| `Ability/AbilityApplier.cs` | `OnAbilitySelected` 구독 → `BalanceConfig.GetAbilityBonus`만큼 `IPlayerStats.AddSuctionPower/AddSuctionRange/AddMoveSpeed` 호출 |
| `Ability/BalanceConfig.cs` | ScriptableObject — 모든 수치 여기서 관리. `Resolve(assigned)` 로더 제공 |

---

## 이벤트 버스 계약 (GameEvents.cs)

> 코드와 문서 계약이 **완전히 일치**한다(변경 없음). 이벤트 9개, Raise 메서드 9개.

```csharp
// Assets/Scripts/Core/GameEvents.cs — 읽기·구독만, 직접 Invoke 금지
public static class GameEvents
{
    // 모듈 A 발행
    public static event Action OnGameStart;
    public static event Action OnGameOver;
    public static event Action OnTimerEnd;

    // 모듈 B 발행
    public static event Action<GhostBase> OnGhostKilled;    // 모듈 C 구독(점수/XP)
    public static event Action<int>       OnChildRescued;   // 파라미터: 최종 획득 점수
    public static event Action            OnMiniGameStart;  // 모듈 A 구독(이동 잠금)
    public static event Action<bool>      OnMiniGameEnd;    // true=성공, false=실패

    // 모듈 C 발행
    public static event Action              OnAbilityTrigger;   // 능력 선택 UI 열기
    public static event Action<AbilityType> OnAbilitySelected;  // 모듈 A(PlayerStats) 반영

    // RaiseXxx() 정적 메서드로만 발행. [RuntimeInitializeOnLoadMethod] ResetStatics()가
    // Play 시작 시 모든 이벤트를 null로 초기화(도메인 리로드 OFF 환경에서 죽은 구독자 누적 방지).
}
```

---

## 플레이어 스탯 인터페이스 (IPlayerStats.cs)

```csharp
// Assets/Scripts/Core/IPlayerStats.cs — 모듈 B(유령 공격)·C(능력 강화)가 사용
public interface IPlayerStats
{
    void AddSuctionPower(float amount); // 능력 A
    void AddSuctionRange(float amount); // 능력 B
    void AddMoveSpeed(float amount);    // 능력 C
    void TakeDamage(int amount);        // 모듈 B 유령 공격 시 호출
    int CurrentHP { get; }
    int MaxHP { get; }                  // HUD HP바 비율 계산용
}
```

> 실제 `PlayerStats`는 위 외에 `MoveSpeed / SuctionPower(=공격력) / SuctionRange(=사거리)`,
> 그리고 능력 UI용 `IAbilityStatsReadOnly`(SuctionPower/SuctionRange/MoveSpeed getter)를 함께 공개한다.
> (`SuctionAngle / SuctionPullSpeed / …` 프로퍼티는 남아있으나 사격에서 미사용)

### 피해 계약 (IDamageable.cs)

```csharp
// VacuumController(사격) → 유령. 모듈 B의 GhostBase가 구현.
public interface IDamageable { void TakeDamage(float amount); }
```
> 주의: `IPlayerStats.TakeDamage(int)`(유령→플레이어)와 `IDamageable.TakeDamage(float)`
> (플레이어→유령)는 **방향이 다른 별개 계약**이다. 사격은 명중 시 `TakeDamage(공격력)`를 **1회** 호출.

### 넉백 통지 계약 (ISuctionable.cs — `IKnockbackable`)

```csharp
// 사격 명중 시 VacuumController가 맞춘 유령에 1회 호출. GhostBase가 구현.
public interface IKnockbackable { void ApplyKnockback(Vector3 dir, float force); }
```
> `GhostBase.ApplyKnockback()`는 NavMeshAgent를 잠깐 끄고 Rigidbody에 순간 속도를 줘 뒤로 밀린 뒤
> 짧은 시간(0.18s) 후 NavMesh로 복귀한다. (파일명은 `ISuctionable.cs` 유지, 내용만 넉백 계약)

### 처치 연출 — GhostBase에 구현됨 (제자리 소멸)
- HP 0 도달 시 즉시 파괴하지 않고 **그 자리에서 스케일 축소 + 회전하며 소멸("펑")** 후
  `RaiseGhostKilled` + 파괴. (더 이상 청소기 입구로 빨려들지 않음)
- Rigidbody `isKinematic=true` + agent off로 고정 후 스케일 0으로 감소.

---

## BalanceConfig 구조

> 실제 `Assets/Scripts/Ability/BalanceConfig.cs`의 전체 필드(헤더 그룹 단위 요약).
> **코드에 수치 하드코딩 금지 — 반드시 이 asset에서 읽는다.**

```csharp
[CreateAssetMenu(menuName = "NightmareCleaner/BalanceConfig")]
public class BalanceConfig : ScriptableObject
{
    // 게임 흐름
    gameDurationSeconds = 600

    // 플레이어 기본 스탯
    playerMaxHP = 5,  playerMoveSpeed = 5

    // 사격 — 기본 스탯(스탯 재사용)
    suctionPower = 10(발당 데미지),  suctionRange = 12(사거리),  suctionAngle = 55(미사용)

    // 사격 — 발사/탄약
    fireInterval = 0.15(연사 간격),  magazineSize = 12(탄창),  reloadSeconds = 1.2,
    bulletTracerSpeed = 60(트레이서 속도),  ghostKnockbackForce = 6(명중 넉백)

    // (사격 미사용) 옛 흡입 당김/게이지 — 필드는 남겨둠
    suctionPullSpeed, suctionPullResponsiveness, suctionSettleRadius,
    suctionMaxSeconds, suctionRegenPerSec, suctionRegenDelay, suctionOverheatSeconds,
    suctionRescueBonus = 1(구출당 탄창 상한↑)

    // 피격 반응
    invincibleSeconds = 1,  knockbackForce = 6

    // 능력치 증가폭(1회 선택당)
    suctionPowerBonus = 3,  suctionRangeBonus = 1,  moveSpeedBonus = 1
    // → GetAbilityBonus(AbilityType) 로 조회

    // 점수
    scorePerGhostKill = 10,  scoreChildRescue = 100,
    scoreChildBonus = 50,    retryPenaltyRate = 0.3   // ※ 뒤 둘은 현재 코드 미사용(미구현 섹션 참고)

    // 미니게임(공통 + 게이지스톱)
    miniGameTimeLimit = 5,  keyMashCount = 10,  miniGameCountdownSeconds = 1.5,
    gaugeStopSuccessWidth = 0.18,  gaugeStopSpeed = 1.2,  gaugeStopTimeLimit = 15,
    gaugeStopSpeedStep = 0.15,  gaugeStopWidthShrink = 0.88,  gaugeStopMinWidth = 0.05

    // 미니게임 — 순서입력(시퀀스)
    sequenceTimeLimit = 15,  sequenceStartLength = 4,  sequenceLengthStep = 2,
    scorePerSequenceStage = 30   // 클리어 단계 1개당 추가 구출 점수

    // 유령 스탯 (BalanceConfig에서 직접 관리 — 유령 스탯표 참고)
    jelly*/chubby*/slim*/repair*(+repairDash*)/chef* HP·Speed·Damage

    // 요리사 — 뜨거운 국물
    chefSoupRadius/GroundOffset/Lifetime/SpawnInterval/Count/PostDelay/DamageInterval

    // 스폰(시간 진행 스케일링)
    spawnIntervalSeconds = 5,  spawnIntervalMin = 1,  spawnCountEachTime = 1,
    maxGhostCountStart = 3,  maxGhostCountMax = 8,  spawnRadius = 10,  stageStepSeconds = 60,
    kitchen/storageSpawnChanceStart·End

    // 아이 / 능력 트리거
    childCount = 3,  childSpawnInterval = 10,  childInteractRange = 2.5,
    childSpawn(Search/Clearance/Ground)*,  abilityTriggerKillCount(미사용)

    // 오디오(BGM)
    bgmVolume = 0.6,  bgmFadeSeconds = 0.5

    // 미니맵
    minimapWorldRadius = 18,  minimapCameraHeight = 40

    // 능력 강화 — 경험치(XP)
    xpBasicGhost = 1,  xpSpecialGhost = 5,  xpChildRescue = 10,
    xpFirstThreshold = 8,  xpThresholdGrowth = 1.4

    public static BalanceConfig Resolve(assigned)  // Inspector 연결값 우선, 없으면 Resources 자동 로드
}
```

> `PlayerStats`·`GhostBase`·매니저들이 `BalanceConfig.Resolve(...)`로 값을 읽는다.
> 밸런스 담당은 `Assets/Resources/Configs/BalanceConfig.asset`만 열어 Inspector에서 수치 조정.

---

## 폴더 구조 (실제 `Assets/Scripts/`)

```
Assets/
├── Scenes/           start.unity / SampleScene.unity / end.unity  (3개 모두 Build Settings 등록)
├── Editor/           UISetupTool.cs  (한글 폰트 세팅 메뉴, 빌드 미포함)
├── Fonts/            malgun.ttf + malgun_TMP.asset
├── Scripts/
│   ├── Core/         GameManager, GameEvents, GameResult, GameSystems(부트스트래퍼),
│   │                 IPlayerStats, IDamageable, ISuctionable, IAbilityStatsReadOnly, AbilityType,
│   │                 PlayerController, PlayerStats, PlayerAnimatorBridge,
│   │                 VacuumController, VacuumRangeVisualizer, SuctionVortexVFX🟡, SuctionCharge,
│   │                 CameraController, SpawnPointRegistry, BgmManager, RoomZoneManager⬜
│   ├── Ghost/        GhostBase, GhostJelly/Chubby/Slim, GhostRepair, GhostChef,
│   │                 GhostSpawnManager, GhostVisualSettings, GhostZone, SpawnPoint
│   ├── Child/        ChildrenText, MiniGameController        (구 P키 경로)
│   │   └── Cleaner/  ChildRescueSystem, RescueMiniGameManager, IRescueMiniGame,
│   │                 MiniGameSequence, MiniGameGaugeStop, RescueChild, ChildSpawnPoint, RescueBootstrap
│   ├── Ability/      BalanceConfig, ScoreManager, AbilitySystem, AbilityApplier
│   ├── UI/           HUDController, StartMenuController, PauseMenuController, EndMenuController,
│   │                 MinimapController, UIRealTheme, OptionToggleVisual, UiClickSfx, EditableMiniGameUI
│   ├── Energy.cs     🟡 (EnergyShooter — 발사 테스트)
│   └── particle.cs   🟡 (VacuumParticleAttractor — 흡입 파티클)
├── Prefabs/
└── Resources/
    ├── map/          NighMare_Cleaner_Room_test_02.fbx
    ├── Ghost/        G_TypeA / fat / FLY 등 유령 비주얼
    └── Configs/      BalanceConfig.asset (밸런스 조정), GhostVisualSettings.asset
```

---

## 유령 스탯표 (BalanceConfig 필드 기준)

| 유령 | 타입 | HP | SPD | DMG | BalanceConfig 필드 | 특이 |
|------|------|----|-----|-----|--------------------|------|
| 젤리 | 일반 | 10 | 3 | 1 | `jelly*` | — |
| 통통 | 탱커 | 20 | 1 | 3 | `chubby*` | — |
| 홀쭉 | 속공 | 5 | 5 | 2 | `slim*` | — |
| 뚝딱이 | 특별★ | 50 | 4 | 5 | `repair*` (+`repairDash*`) | 돌진 상태머신 / 아이템 드랍 미구현 |
| 요리사 | 특별★ | 40 | 2 | 4 | `chef*` (+`chefSoup*`) | 국물 장판(지속 피해) |

---

## 구역별 스폰 규칙

| 구역 | 유령 종류 | 근거 |
|------|-----------|------|
| 거실(Living) | 젤리·통통·홀쭉 랜덤 | `SpawnPoint.zone = Living` |
| 주방(Kitchen) | 요리사★ | `kitchenSpawnChance*` 확률로 스폰 |
| 창고(Storage) | 뚝딱이★ | `storageSpawnChance*` 확률로 스폰 |
| 폴백 | 젤리 | SpawnPoint 없으면 플레이어 주변 `spawnRadius` 원형 랜덤 |

> `RoomZoneManager`(태그 기반 구역 관리)는 미구현. 현재는 `SpawnPoint`+`GhostZone` 컴포넌트와
> 아이 스폰용 `SpawnZone` 태그 콜라이더 스캔으로 대체한다.

---

## 아이 구출 미니게임 (정식 경로: `Child/Cleaner/`)

### 흐름
1. `ChildRescueSystem`이 최근접 아이가 `childInteractRange`(기본 2.5) 안이면 `[E] 아이 구출` 프롬프트.
2. **E키** → `Time.timeScale=0`(월드 정지) + `RaiseMiniGameStart()`(플레이어 이동 잠금) → `RescueMiniGameManager.Play()`.
3. 미니게임 2종 중 랜덤 선택 → 카운트다운(`miniGameCountdownSeconds`) → `Begin()`. 미니게임은 `unscaledDeltaTime`로 진행(월드는 멈춰도 실시간).
4. 종료 시 클리어 단계 수(`cleared`, 0=실패)를 콜백 → `RaiseMiniGameEnd(cleared>0)`.
   - **성공**: 점수 계산 후 `RaiseChildRescued(score)` → 아이 제거·방 등록. (점수/게이지 상한 자동 반영은 모듈 A가 처리)
   - **실패**: `FailCount++`, 재도전 가능.

### 미니게임 타입 (`IRescueMiniGame` 구현)

| 클래스 | 설명 |
|---|---|
| `MiniGameSequence` | 랜덤 WASD **순서입력**. 완성 시 단계↑ 후 시퀀스 길이 증가. `sequenceTimeLimit` 내 클리어 단계 수 반환 |
| `MiniGameGaugeStop` | 좌우 왕복 마커를 **중앙 성공 구간에서 E로 스톱**. 단계↑ 시 속도↑·성공폭↓. `gaugeStopTimeLimit` 내 클리어 단계 수 반환 |

> 새 미니게임 추가 시 `IRescueMiniGame` 구현 후 `RescueMiniGameManager.games` 배열에 등록.
> 구(舊) `Child/MiniGameController`(P키 TAP/HOLD QTE)는 별개 독립 경로이며 정식 매니저에 미등록.

### 점수 계산 (실제 코드)
```
구출 점수 = scoreChildRescue + (클리어 단계 수 × scorePerSequenceStage)
          = 100 + cleared × 30
```
> ⚠️ 문서 초기 버전의 공식(`scoreChildBonus`, `retryPenaltyRate` 사용)은 **코드에 미반영**이다.
> 두 필드는 BalanceConfig에 존재하지만 어디서도 참조되지 않는다(미구현 섹션 참고).

---

## 능력치 선택 시스템 (XP 레벨업)

- 트리거: 유령 처치/아이 구출로 **XP 누적** → 임계값(`xpFirstThreshold × xpThresholdGrowth^level`)
  도달 시 `ScoreManager`가 레벨업하며 `RaiseAbilityTrigger()` 발행.
- `AbilitySystem`이 능력 카드 UI 표시(연속 레벨업은 큐잉) → 선택 시 `RaiseAbilitySelected(type)`.
- `AbilityApplier`가 `BalanceConfig.GetAbilityBonus`만큼 `IPlayerStats`에 반영.

| AbilityType | 이름 | 효과 |
|---|---|---|
| `SuctionPower` | 공격력 증가 | 발당 데미지 ↑ |
| `SuctionRange` | 사거리 증가 | 사격 사거리 ↑ |
| `MoveSpeed` | 이동속도 증가 | 이동속도 ↑ |

---

## 조작 키 매핑

| 입력 | 행동 |
|------|------|
| WASD | 이동 |
| 마우스 이동 | 조준(캐릭터가 커서 방향 회전, 상시 조준 레이저) |
| 마우스 좌클릭(홀드) | 사격 (히트스캔 연사) |
| R | 재장전 (탄창 소진 시 자동) |
| E | 아이 근처에서 구출 미니게임 시작 (정식 경로) |
| P | 아이 근처에서 구출 (구 경로, `MiniGameController`) |
| M | 미니맵 전체지도 토글(일시정지) |
| ESC | 일시정지 메뉴 |

---

## 미구현 / TODO (통합·마무리 전 확인)

| 항목 | 상태 | 비고 |
|------|------|------|
| **RecordManager** (최고기록 PlayerPrefs 저장/로드) | ⬜ 미구현 | 파일 없음. 문서·주석에만 언급. `GameResult`로 결과 전달까지만 됨 |
| **RoomZoneManager** (태그 기반 구역 관리) | ⬜ 미구현 | `SpawnPoint`+`GhostZone`+`SpawnZone` 태그로 대체 중 |
| **특별 유령 아이템 드랍** | ⬜ 미구현 | `GhostRepair.OnConsumeBegin`에 `// TODO`만. 요리사도 드랍 없음 |
| **아이 점수 공식**(`scoreChildBonus`/`retryPenaltyRate`) | 🟡 필드만 존재 | 코드 미참조. 현재는 `scoreChildRescue + 단계수×scorePerSequenceStage` |
| **아이 구출 구/신 두 경로 공존** | 🟡 정리 필요 | 정식=`Child/Cleaner/`(E키), 구=`Child/`(P키). 통합 시 한쪽 제거 권장 |
| **일부 전투 수치 하드코딩** | 🟡 이관 필요 | `_attackRange`/`_attackInterval`/`_consumeDuration`, 국물 데미지, 돌진 거리 등 → BalanceConfig 이관 |
| **SuctionVortexVFX / VacuumRangeVisualizer** | 🟡 무력화 | 사격 전환으로 빈 스텁. 씬에서 컴포넌트 제거 후 파일 삭제 가능 |
| **Energy.cs / particle.cs** | 🟡 미사용 | 옛 흡입/발사 테스트. 정리 대상 |
| **사격 컴포넌트 클린 리네이밍** | 🟡 후속 | `VacuumController→ShootController`, `SuctionCharge→AmmoCharge` 등은 씬 재배선 유발 → 실험 후 별도 진행 |
| **트레이서/조준 레이저 정식 아트** | 🟡 후속 | 현재 코드 생성 LineRenderer. 프리팹/머티리얼 교체 예정 |

> **해소된 임시 처리**: 초기 문서의 "HUD가 점수를 직접 계산해 ScoreManager와 충돌" 이슈는
> `ScoreManager`가 점수·XP 권한을 가져가고 HUD는 값을 읽어 표시만 하도록 정리되어 **해결됨**.
> `Testing/TestDummyGhost`는 **삭제됨**(모듈 B 정식 유령으로 대체).

---

## 작업 지침

- 스크립트 생성 시 반드시 소속 모듈 폴더(Core/Ghost/Child/Ability/UI)에 배치.
- 다른 모듈 클래스 직접 참조 금지 — `GameEvents` 또는 인터페이스 사용.
- 수치 하드코딩 금지 — 반드시 `BalanceConfig`에서 읽어올 것(새 수치는 필드 추가 후 사용).
- 새 유령 추가 시 `GhostBase` 상속 후 `BalanceConfig`에 스탯 추가, `GhostVisualSettings`에 비주얼 등록.
- `GameEvents.cs`, `IPlayerStats.cs`는 모듈 A 담당자만 수정(나머지는 읽기·구독만).
- 한글이 □로 보이면 상단 메뉴 **NightmareCleaner → Setup Korean Font** 1회 실행.

### 유령 만들 때 체크리스트 (`GhostBase` 상속)
1. `IDamageable.TakeDamage(float)` — 사격 명중 시 체력을 깎음(베이스가 처리, `Configure`에서 스탯만 세팅).
2. `IKnockbackable.ApplyKnockback(dir, force)` — 명중 시 뒤로 밀림(베이스가 처리).
3. 사망 시 제자리 소멸 연출 후 `GameEvents.RaiseGhostKilled(this)` — 베이스가 처리(점수/XP 자동 반영).
4. 플레이어 공격은 `IPlayerStats.TakeDamage(int)`만 호출(무적·넉백·깜빡임은 모듈 A가 처리).
5. 오브젝트 세팅: **Ghost 레이어 + Rigidbody(비키네마틱) + Collider** 필수(`Create<T>()` 팩토리가 처리).

### 아이/점수/능력 담당 체크리스트
- 미니게임: `RaiseMiniGameStart()` → `RaiseMiniGameEnd(성공여부)`, 성공 시 `RaiseChildRescued(점수)`.
- 점수/XP 권한은 `ScoreManager`(싱글턴)가 소유 — HUD는 읽어서 표시만.
- 능력: `RaiseAbilityTrigger()`(UI) → `RaiseAbilitySelected(AbilityType)`(적용).
- 밸런스: 모든 수치는 `Assets/Resources/Configs/BalanceConfig.asset` 한 파일에서 Inspector로 조정.
