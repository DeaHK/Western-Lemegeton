# Timeline 연출 연결 안내

첨부 `초안 기획서 간단 개요 문서 ㅎㅎ.pptx`의 15쪽은 플레이어 사망 모션 후 화면을 어둡게 처리하고 결산 화면을 생성하는 흐름입니다. 이 흐름과 사용자가 요청한 던전 입장·보스 등장 연출을 Unity 게임에 연결했습니다. PPT 자체는 수정하지 않았습니다.

## 재생 시점과 조작

| 연출 | 발생 조건 | 기본 길이 | 종료 후 |
|---|---|---|---|
| 던전 입장 | 히르바 출발 또는 다음 스테이지 진입 | 2.8초 | 첫 전투방 조작 가능 |
| 플레이어 사망 | 실제 피격으로 HP가 0이 됨 | 2.7초 | 기존 결산 화면 |
| 보스 등장 | 마지막 스테이지의 마지막 전투 웨이브에서 보스 생성 | 3.4초 | 보스 전투 |

입장 연출은 한 스테이지 내부의 방 이동·재방문에는 반복되지 않습니다. 여섯 방을 완료하고 출구에서 E를 눌러 다음 스테이지를 선택하는 흐름은 유지됩니다.

연출 중 SPACE 또는 ESC로 건너뛸 수 있습니다. P는 연출 일시정지/계속입니다. 플레이어 이동·공격·피격·문 이동·지도 열기와 적 AI는 연출 중 멈춥니다. 화면은 별도의 `Cinematic` 상태에서 제어하므로 기존 전투 HUD와 메뉴가 겹치지 않습니다. 종료 시 카메라·캐릭터 모양·까마귀 위치를 복원하고, 생존 시 짧은 입장 보호 시간을 적용합니다. 새 런 시작이나 마을 복귀는 진행 중인 연출을 취소하고 예약된 종료 처리가 남지 않도록 정리합니다.

## Timeline 파일 편집

- `Assets/Resources/Cinematics/DungeonEntrance.playable`
- `Assets/Resources/Cinematics/PlayerDeath.playable`
- `Assets/Resources/Cinematics/BossEntrance.playable`

Unity Project 창에서 파일을 선택하고 Timeline 창에서 편집합니다. 게임 실행 중에는 `Western Lemegeton` 오브젝트의 `PlayableDirector`에 현재 연출과 트랙 연결이 표시됩니다. 메뉴 `Western Lemegeton > Timeline > Select Dungeon Entrance`로 입장 파일을 선택할 수도 있습니다.

각 파일에는 카메라, 배우 동작, 상하 검은 여백, 이름 표시, 암전, 바닥 효과, 오디오의 7개 트랙이 있습니다. 앞의 6개는 현재 게임의 2.5D 표시 방식에 연결하는 `CinematicTrack`이며 오디오는 Unity의 표준 `AudioTrack`입니다. 클립의 시작·길이와 From/To, Curve, FadeIn/FadeOut을 조절할 수 있습니다. 채널은 트랙의 Channel 속성이 결정합니다.

카메라는 값 0이 기존 전투 구도, 1이 주인공/보스 클로즈업입니다. 배우 동작은 0~1 진행 값으로 입장, 쓰러짐, 보스 실체화 동작을 평가합니다. 이름 표시·상하 여백·바닥 효과는 FadeIn/FadeOut으로 표시 시간을 조절합니다. Fade는 0이 투명, 1이 검은 화면입니다. Timeline의 고정 길이를 변경할 때는 마지막 카메라 복귀·암전 클립의 끝 시간도 맞춰 주세요.

`TimelineSetup.EnsureAssets`는 파일이 없을 때만 기본 파일을 생성합니다. 기존 Timeline을 사용자가 편집한 경우 다음 빌드에서 덮어쓰지 않습니다.

## 기존 아트 사용 범위

입장에서는 카메라가 가까운 구도에서 전투 구도로 물러나며 주인공과 까마귀가 등장합니다. 사망에서는 주인공 PNG를 회전·이동해 쓰러지는 동작을 만들고 암전합니다. 보스는 소환 문양 위에서 모습을 드러내며 카메라가 보스를 보여 준 뒤 복귀합니다.

현재 제공된 PNG를 시간에 따라 이동·회전·투명도 조절하는 연출입니다. 새 사망 프레임 애니메이션이나 Spine 리소스는 포함하지 않습니다. 오디오는 Timeline 연동 확인용으로 생성한 임시 효과음이며 각 AudioTrack의 클립을 정식 사운드로 교체할 수 있습니다.

## 진입점 및 검증

일반 게임 흐름은 `Dungeon.StartRun`, `Dungeon.ChooseRoom`, 보스 웨이브 생성, `Player.Hurt`에서 자동 연결합니다. 다른 이벤트에서 별도로 재생해야 할 때는 아래 진입점을 사용할 수 있습니다.

```csharp
Dungeon.I.Cinematics.Play(CinematicKind.DungeonEntrance);
Dungeon.I.Cinematics.Play(CinematicKind.BossEntrance, bossEnemy);
Dungeon.I.Cinematics.Skip();
Dungeon.I.Cinematics.SetPaused(true);
Dungeon.I.Cinematics.SetPaused(false);
```

`--smoke-test`는 기존 30방/50웨이브 진행과 새 연출 검사를 함께 수행합니다. `--smoke-test --cinematic-only`는 입장·사망·보스 등장만 집중 확인하고 자동 종료합니다. 후자는 빠른 검토를 위해 첫 전투방에 검사용 보스를 생성하며 일반 플레이에는 적용되지 않습니다.

Unity Timeline 안내: https://docs.unity3d.com/Packages/com.unity.timeline@1.8/manual/index.html

사망 Timeline은 처음 0.9초 동안 쓰러지는 동작을 완성하고, 1.25초부터 2.7초까지 암전한 뒤 결산 화면을 엽니다. 연출 시작 시 남아 있는 전투 탄환과 일회성 효과를 정리합니다.
