# Mansion Clash — 프로젝트 설계 문서

> **이어서 작업할 때는 [`진행상황.md`](./진행상황.md)를 먼저 읽는다.** (완료·미확인·남은 일)

> 설계·계약의 **단일 출처**. 최종 갱신: 2026-11-27 (연회장 옆문 막음, 캠프 거리 대칭).
> 규칙 원본은 `Mansion_Clash_기획서.pdf`. 아래 "기획서와 다른 결정"이 기획서보다 우선한다.

## 개요
- PC 1:1 실시간 점령 슈팅, 쿼터뷰. Unity 6000.3.12f1, URP, Input System, Photon Fusion 2(Host 모드, 2인).
- 중앙홀을 혼자 점령하면 지배율이 초당 1%p 오른다. 100% 즉시 승리, 5분이 끝나면 지배율 우세 승, 같으면 무승부.
- **실제 게임은 `Assets/NC`다.** 옛 싱글 게임(코드·씬·리소스)은 2026-11-26에 삭제했다. 나중에 쓸 캐릭터·유령 모델과 BGM은 `Assets/Art_Reserve`, `Assets/UnityChan`에 보관한다(빌드에 들어가지 않도록 `Resources` 밖에 둔다).
- 씬: `NC_Title` → `NC_Game`(접속·대기실·경기·결과가 모두 이 씬)

## 기획서와 다른 결정
| 항목 | 기획서 | 현재 |
|---|---|---|
| 캠프 | 3곳 | 연회장 1곳 |
| 유령 | 캠프마다 고정 종류 | 등장할 때마다 기본·빠른·탱커 중 랜덤 1마리, 체력 모두 480. 맞으면 쏜 사람을 추적하고 이때는 영역 밖 4유닛까지 쫓음. 속도 3.2 / 4.5 / 2.5 |
| 보상 | 3종 교체 | 막타 과충전으로 통일 |
| 과충전 | 발사 간격만 감소 | Q 사용 시 탄창을 채우고 재장전 취소, 8초간 탄약 무제한 + 간격 0.2초 |
| 대쉬 | 없음 | 마우스 우클릭, 4유닛/0.2초, 대기 4초. 무적 없음, 대쉬 중 사격 불가, 부활 보호 유지, 벽·가구 통과 불가 |
| 스폰 | 미정 | 1P 서쪽 갤러리, 2P 동쪽 응접실. 중앙까지 이동 거리를 같게 유지(현재 25.07) |
| 점령 구역 | 미정 | 중앙홀 `StartZone` 원, 반경 4.0(`captureRadius`) |
| 엄폐물 | 없음 | 점령지 밖에만 소파 4개(좌우 각 위아래 2개). 점령지 안은 노출. 탄환을 막으려면 콜라이더 상단 1.2 이상 |
| 유령 출현 | 미정 | 1마리라 늘 `SpawnPoint_1`에서 나오고 그 자리를 집으로 삼는다. 연회장 남쪽 문 안쪽 가운데 (0, 16.5): 점령지에서 15.45, 양쪽 스폰에서 31.87로 같음 |
| 캠프 진입 | 미정 | 연회장 옆문 2개(동쪽 주방·서쪽 Room_B, z 15.75~19.5)를 서랍장 3개씩으로 막음(`Prop_DrawerCabinet_DoorBlock_East/West_1~3`). 캠프에는 중앙홀 → 연회장 남쪽 문으로만 들어가고, 양쪽 경로가 좌우 대칭(실제 거리 35.51). NavMesh 측정은 1P가 0.45 길게 나오지만 길찾기 오차다 |

방 구조가 바뀌면 `NC_Game` 씬에서 다음을 옮긴다: `EntrySpawn_1P/2P`, `StartZone`, `Camp_연회장`(자식 `Area`=활동 영역, `SpawnPoint_*`=출현 위치), `Map/Cover_중앙홀`(엄폐물), `Map/Carpet_중앙홀`(카펫), 옆문 막이 `ArtMap/Kitchen`·`ArtMap/Room_B`의 `Prop_DrawerCabinet_DoorBlock_*`.

씬 확인·수정은 Unity CLI로 열린 에디터를 직접 조작한다(`unity status`, `unity command script eval --file <cs>`). 프로젝트에 `com.unity.pipeline`이 있어 MCP 연결은 필요 없다.

`captureRadius`를 바꾸면 `Carpet_중앙홀`과 `StartZone`의 `Glow`·`Ring` 크기도 직접 맞춘다(자동 연동 안 됨).

## 파일 (`Assets/NC/Scripts`)
| 파일 | 역할 |
|---|---|
| `Core/NCBalance` | 모든 수치. 에셋 `NC/Resources/NCBalance.asset`. `soloTest`를 켜면 2P 자리에 허수아비가 생긴다(행동은 `soloDummy`) |
| `Player/NCDummyBrain` | 솔로 테스트 허수아비의 입력 계산(Host 전용, Tick 기반이라 Random 안 씀). 허수아비는 조종자 없는 진짜 `NC_Player`(`NCPlayer.IsDummy`), 입력은 `NCPlayer.TryGetInput`으로 받는다 |
| `Core/NCSession` | 경기 단계, 점령, 지배율, 승패, 초기화, 결과 통계(판정에 쓰지 않음) |
| `Core/NCLauncher` | 방 생성·참가, 플레이어 생성, 입력 수집 |
| `Player/NCPlayer` | 슬롯, 이동, 대쉬, 조준, 스폰 이동 |
| `Player/NCHealth` | 체력, 자연 회복, 사망·부활, 부활 보호 |
| `Player/NCGun` | 탄창, 자동 재장전, 과충전, 발사 요청 |
| `World/NCBulletSystem` | 탄환 이동·명중(Host), 탄환 연출 RPC |
| `World/NCGhostCamp` | 캠프 생성·전멸·보상·재등장 |
| `Ghost/NCGhost` | 유령 FSM: 대기·추적·공격·복귀 |
| `UI/NCUi` | 화면 전환. HUD `NCHud`, 머리 위 체력·탄창·대쉬 대기 `NCOverhead`, 유령 체력바 `NCGhostBar`, 결과 `NCResultScreen`, 미니맵 `NCMinimap` |
| `Fx/NCFx` | 풀링 연출 |
| `UI/NCLobbyPreview` | 대기실 카드의 캐릭터 미리보기. `NC_Player/Body`만 복제해 지하(y -200)에 세우고 전용 카메라·조명으로 찍어 카드에 띄운다. 참가 대기=실루엣, 준비 중=회전, 준비 완료=정면·점프·발밑 녹색 빛(카드 녹색 테두리는 `NCLobbyScreen`). 대기실이 보일 때만 켜진다 |
| `UI/NCHitFeedback` | 명중 표시·피해 숫자·피격 번쩍임과 방향(내 화면만). `NCBulletSystem.RpcHit`이 호출. 조준이 흔들리므로 카메라는 흔들지 않는다 |
| `Fx/NCSfx` | 사격·명중·처치·피격·사망음(`NC/Resources/Audio`). 음량·거리 상수는 파일 상단 |

## 네트워크 규칙
- 피해·사망·회복·유령·보상·점령·승패는 **Host만** 확정한다. 화면은 `[Networked]` 값을 읽어 그린다.
- 한 Tick 순서는 `NCSession.TickMatch`가 정한다: 탄환·유령 공격 수집 → 피해·사망 → 부활·회복 → 캠프 보상 → 점령 인원 → 지배율 → 100% → 시간 종료. 이 순서를 바꾸지 않는다.
- 이름이 `Server`로 시작하는 메서드는 Host에서만 호출한다. 예측 실행되는 코드에서 부르면 내부에서 권한을 확인해야 한다.
- 유령 이동은 `NCGhost.MoveTo`가 Tick마다 직접 한다. `NavMeshAgent`는 길 찾기에만 쓴다(`updatePosition`을 켜면 호스트 FPS에 따라 속도가 달라진다).
- 플레이어는 레이어 10을 쓴다(서로 이동을 막지 않음). 이 레이어를 다른 용도로 쓰지 않는다.
- 미니맵에 상대 위치를 표시하지 않는다. 상대 머리 위 체력바도 벽에 가리면 숨긴다. 예외: 내가 쓰러져 있는 동안에는 `spectateDelaySeconds` 뒤부터 카메라가 상대를 따라간다(관전, `NCCameraRig`).
- 메뉴를 열어도 시간은 멈추지 않는다.

## 조작
WASD 이동 · 마우스 우클릭 대쉬(이동 키 방향, 없으면 마우스 방향) · 마우스 조준(커서 자리에 빨간 동그라미 조준점, 경기 중 OS 커서는 숨김) · 좌클릭 연사 · R 재장전 · Q 과충전 · H 조작키 표시(경기 중 켜기·끄기, 카운트다운에는 늘 보임) · Esc 메뉴 · F3 성능 표시

## 작업 지침
- **모든 대답은 한글로 쓴다.** 설명·질문·요약·최종 보고까지 전부 해당하고, 코드·파일명·식별자만 원문 그대로 둔다.
- 수치 하드코딩 금지. `NCBalance`에 필드를 추가한다. C# 기본값을 바꿔도 기존 에셋 값은 남으므로 에셋도 확인한다.
- 씬을 통째로 다시 만드는 도구는 제거했다(수작업 가구 배치 보호). 씬은 에디터에서 직접 고친다.
- 맵·가구 모델은 Read/Write가 켜져 있어야 한다(실행 중 NavMesh 굽기). `Assets/Map`, `NC/Resources/Asset/Meshes`는 `NCMapMeshImporter`가 자동으로 켠다. 다른 폴더에 두면 직접 켠다.
- UI에 새 글자를 쓰면 `NCUi.Glyphs`에 추가한다.
- 2인 테스트: Multiplayer Play Mode로 Host와 Client를 띄운다. MPPM은 `Library/VP` 아래 복제본을 만들어 경로가 길어지므로 프로젝트를 짧은 영문 경로에 둔다(길면 `TestNewCustomAssembly.asmdef`를 못 읽는다며 멈춘다).
- 혼자 테스트: `NCBalance.soloTest`를 켜고 방을 만들면 허수아비가 2P로 들어와 준비 완료 상태가 된다. 대기실에 사람이 들어오면 허수아비가 자리를 넘기고, 사람이 나가면 다시 선다.
- 플레이어 입력은 `GetInput` 대신 `NCPlayer.TryGetInput`으로 받는다(허수아비 입력 포함).

## 남은 작업
- 타이틀 설정 화면, 타이틀 이미지의 제목 교체, 정식 아트·이펙트.
- 이동속도·최대 체력 보상(기획서 7장)은 범위에서 제외된 상태.
