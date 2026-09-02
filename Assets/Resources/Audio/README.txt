BGM 음원을 이 폴더에 아래 이름으로 넣으세요 (확장자는 .mp3/.wav/.ogg 무관, 이름만 일치).

  bgm_title    → start 씬(시작화면)
  bgm_ingame   → SampleScene(인게임)
  bgm_clear    → end 씬 — 클리어(승리)
  bgm_gameover → end 씬 — 게임오버(패배)

예) bgm_title.mp3, bgm_ingame.wav, bgm_clear.ogg, bgm_gameover.mp3

- 파일이 없으면 해당 화면은 조용히 무음 처리됩니다(에러 없음).
- 볼륨은 옵션 메뉴의 마스터 볼륨 슬라이더로 자동 조절됩니다.
- BGM 기본 볼륨/페이드 시간은 Resources/Configs/BalanceConfig.asset 의
  bgmVolume / bgmFadeSeconds 에서 조정합니다.

재생 로직은 Assets/Scripts/Core/BgmManager.cs 가 자동 처리합니다(씬 편집 불필요).
이 README.txt 는 지워도 됩니다.
