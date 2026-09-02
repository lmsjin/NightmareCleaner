# 멀티 수직 슬라이스 — 유니티 세팅 가이드

> 목표: **2명 접속 → 둘 다 사격 → 서버 스폰 유령 1마리 죽음 → 공유 점수가 양쪽에서 오름.**
> 코드(`Assets/Scripts/Net/`)는 이미 다 만들어져 있고, 아래는 **유니티 에디터에서 손으로 조립**하는 부분.
> 기존 싱글 게임(`SampleScene`)은 건드리지 않는다. 전부 새 씬 `NetSlice`에서만 작업.

## 0. 준비 (프로젝트 처음 열 때)
1. NightmareCleaner 프로젝트를 **Unity 6000.3.12f1** 로 연다. → `manifest.json`에 NGO를 추가해놔서 **자동 설치**됨(잠깐 걸림).
2. "Burst 버전 변경… 재시작" 뜨면 재시작. **Vivox/Multiplayer Tools 관련 빨간 에러**가 뜨면 내 코드 문제 아님 →
   `Window > Package Manager`에서 해당 패키지 **Remove** 하거나 무시.
3. `Assets/Scripts/Net/` 의 스크립트들이 콘솔 에러 없이 컴파일되는지 확인(초록불).

## 1. 새 씬 + 바닥
1. `File > New Scene` → 저장 `Assets/Scenes/NetSlice.unity`.
2. `GameObject > 3D Object > Plane` (바닥). 위치 (0,0,0).
3. `Main Camera` 선택 → Transform을 대략 `Position (0, 14, -10)`, `Rotation (55, 0, 0)` — 바닥이 잘 보이게(위에서 비스듬히).

## 2. 관리자(NetworkManager)
1. `GameObject > Create Empty` → 이름 **NetworkManager**.
2. `Add Component` → **Network Manager**.
3. `Add Component` → **Unity Transport**. 그리고 Network Manager의 **Network Transport(Select transport) 칸에 UnityTransport 지정.**
4. `Add Component` → **NetConnectHud** (접속 버튼).

## 3. 플레이어 프리팹
1. `GameObject > 3D Object > Capsule` 생성(이게 플레이어).
2. `Add Component`로 아래 4개 추가:
   - **Network Object**
   - **Network Transform** → 컴포넌트에서 **Authority Mode = Owner** ⚠️ (안 하면 내가 못 움직임). Sync Position/Rotation 체크는 기본값 유지.
   - **NetPlayerController**
   - **NetShooter**
3. `Assets/Prefabs` 폴더에 캡슐을 **드래그해서 프리팹**으로 만들고, 씬의 캡슐은 삭제.
4. **NetworkManager 선택 → Network Manager의 `Player Prefab` 칸에 이 캡슐 프리팹을 드래그.**
   (→ 접속할 때마다 각 플레이어에게 자동 생성됨)

### ▶ 여기서 1차 테스트 (이동 동기화)
- `Window > Multiplayer > Multiplayer Play Mode` → Player 2 체크.
- ▶ Play → 한 창 **Host**, 다른 창 **Client** → WASD로 캡슐 2개가 각자 움직이고 서로 보이면 OK.
- (씬을 빌드에 추가할지 물으면 **Yes**.)

## 4. 유령 프리팹
1. `GameObject > 3D Object > Capsule`(또는 Sphere) 생성(이게 유령).
2. `Add Component`:
   - **Network Object**
   - **Network Transform** (Authority는 **기본값=Server** 그대로 — 유령은 서버가 움직임)
   - **Capsule Collider**(캡슐이면 이미 있음) — ⚠️ **총 맞음 판정에 콜라이더 필수**
   - **NetGhost**
3. 색을 빨갛게(선택): Material 하나 만들어 빨강으로.
4. `Assets/Prefabs`로 드래그해 **프리팹화**, 씬의 것은 삭제.
5. **이 유령 프리팹을 NetworkManager에 등록**: NetworkManager 선택 → Network Manager 컴포넌트의
   **Network Prefabs 리스트**(또는 Default Network Prefabs List 에셋)에 **유령 프리팹 추가**.
   (Player Prefab 칸이 아니라 별도의 프리팹 목록임 — 스폰하려면 반드시 등록해야 함)

## 5. 점수 + 스폰너 (씬 오브젝트)
1. **NetScore**: `Create Empty` → 이름 NetScore → `Add Component` **Network Object** + **NetScore**.
   (씬에 놓인 NetworkObject라 서버 시작 시 자동 활성)
2. **NetGhostSpawner**: `Create Empty` → 이름 GhostSpawner(위치는 맵 가운데쯤) → `Add Component` **NetGhostSpawner**
   → 인스펙터의 **Ghost Prefab 칸에 4번 유령 프리팹을 드래그**. Count=1, Radius=5 정도.

## 6. 최종 테스트 (슬라이스 완성 확인)
1. `Window > Multiplayer > Multiplayer Play Mode`에서 Player 2 켜기.
2. ▶ Play → 한 창 **Host** → 다른 창 **Client**.
3. 확인:
   - 유령 1마리가 두 화면에 **같은 위치**로 나타나 플레이어를 향해 천천히 움직인다.
   - **양쪽 다** 좌클릭으로 쏠 수 있고, 트레이서가 보인다.
   - 유령을 계속 맞히면 죽고, **왼쪽 화면의 SCORE(shared) 숫자가 양쪽에서 똑같이 오른다.** ← 성공! 🎉

## 막히면 (자주 나오는 것)
- **내 캐릭터가 안 움직임** → 플레이어 프리팹 `NetworkTransform`의 **Authority Mode = Owner** 인지.
- **Client 눌렀더니 NullReference** → NetworkManager의 **Transport 칸이 비어있음** → UnityTransport 지정.
- **유령이 안 생김** → (1) 유령 프리팹을 **Network Prefabs 목록에 등록**했는지, (2) GhostSpawner의 **Ghost Prefab 칸**을 채웠는지, (3) 프리팹에 **NetworkObject** 있는지.
- **총을 쏴도 유령이 안 죽음** → 유령 프리팹에 **Collider**가 있는지(판정에 필요). 사거리(NetShooter Range)·조준 방향 확인.
- **점수가 한쪽만 오름/안 오름** → NetScore 오브젝트에 **NetworkObject**가 붙어 있는지(씬 NetworkObject라야 동기화됨).

## 이번 슬라이스에서 확인되는 멀티 패턴 (팀 공유)
- **서버 권위**: 유령 체력·점수는 호스트(서버)만 바꾼다. 클라는 `NetworkVariable`로 구경만.
- **ServerRpc / ClientRpc**: 클라가 "쐈다"고 서버에 요청(ServerRpc) → 서버가 판정 → 전원에 연출(ClientRpc).
- **NetworkObject.Spawn / Despawn**: 유령 생성·제거는 서버가 하고 모두에게 자동 복제.
- **IsOwner / IsServer 게이팅**: 입력은 Owner만, 판정·AI는 서버만.

→ 다음 단계(기존 플레이어의 레이저·탄약·능력 이식, 유령 5종, 아이구출 재설계, Relay)는 이 패턴 위에 얹으면 됨.
