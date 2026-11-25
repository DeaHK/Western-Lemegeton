# 씬 분리 구현 결과

작성일: 2026-11-25 (Asia/Seoul). 기준 커밋 `69e070e`, 결과는 브랜치 `scene-split`의 작업 트리(커밋 전)다. 이 문서가 `CONTINUE_HERE.md`의 "구현 중" 설명을 대체한다.

## 구현한 씬과 진입점

| 씬 | 내용 | 들어가는 방법 |
| --- | --- | --- |
| `Assets/Scenes/MainMenu.unity` | 타이틀 그림과 버튼(공용 UI가 표시). 전투 없음 | 빌드 시작 씬. 일시정지·결과 창의 `메인 화면으로` |
| `Assets/Scenes/Lobby.unity` | 히르바, NPC, 집, 동쪽 원정 관문, 서쪽 시험실 관문 | 타이틀의 Start / New_Game, `히르바로 돌아가기`, 시험실 남쪽 포탈 |
| `Assets/Scenes/Game.unity` | Room_01~06, 무기 연습실, 생성형 던전의 방, 적 | 히르바 동쪽 관문에서 F |
| `Assets/Scenes/Debug.unity` | 보스 선택실과 보스 전투장(실행 중 생성), 테스트 장치 | 히르바 서쪽 관문 |
| `Assets/Resources/Session/GameSession.prefab` | 공용 세션. 씬이 아니라 프리팹이며 첫 씬이 열릴 때 한 번 생성되어 계속 유지 | 자동 |

어느 씬을 열고 Play를 눌러도 `SceneContent.Awake`가 공용 세션을 만들고 그 씬에 맞는 진입(타이틀 / 마을 / 새 원정 / 보스 선택실)을 실행한다. 빌드 설정의 씬 순서는 MainMenu, Lobby, Game, Debug다. 기존 `Assets/Scenes/Main.unity`는 내용을 바꾸지 않고 남겼으며 빌드에는 포함하지 않는다.

## 구조를 이렇게 정한 이유

`PROJECT_REVIEW.md`와 `CLAUDE_TASK.md`는 카메라·헌터·HUD를 씬마다 두는 구성을 제안했다. 실제 구현은 **공용 세션 하나를 유지하고 콘텐츠만 씬으로 교체**한다. 이유는 다음과 같다.

- `Dungeon.I`를 쓰는 곳이 47개 파일 157군데다. 씬마다 Dungeon을 바꾸면 전부 다시 연결해야 한다.
- 헌터·인장·성흔·동전 객체가 파괴되지 않으므로 상태를 복사·복원할 필요가 없고, 보너스가 두 번 적용될 경로가 없다. 상태의 원본은 기존 그대로 Dungeon / PassiveManager / StigmaManager 하나씩이다.
- 카메라·AudioListener·EventSystem이 세션에만 있어 중복이 생기지 않는다.

## 소유권

| GameSession(유지) | 콘텐츠 씬(교체) |
| --- | --- |
| GameManager, 로딩 화면 | 히르바와 NPC(Lobby) |
| Game_Systems: Dungeon, GameSceneBindings, GameHUD, CinematicDirector, 패시브 4종, StigmaManager, StageDungeonBuilder | Room_01~06, 연습실, 생성형 방(Game) |
| RunMapManager, StageLayoutManager, StageRoomManager, 지도 UI | 보스 선택실·전투장·시험 보스(Debug) |
| Hunter, Raven, Main Camera(+AudioListener), Viewport_Backdrop, EventSystem, GameUI | 적, 탄환, 장판, 집 내부, 관문 표식, 각 씬의 `Spawned_Enemies` |

실행 중 부모 없이 만드는 표시용 카드(`PaperCard`)와 지면 장식(`PropGrounding`)은 `SceneOwnership.Follow`로 원본과 같은 씬에 둔다.

## 전환 순서 (GameManager.Transition)

1. 로딩 화면 표시, `IsLoading = true`. 이 동안 `Dungeon.Running`은 false이고 `Dungeon.Update/LateUpdate`, UI 명령은 실행되지 않는다. 중복 요청은 무시된다.
2. 목적 씬을 Additive로 로드하고, 이번 요청으로 열린 씬을 handle로 식별해 `SceneContent.Validate`를 통과시킨다. 여기까지 실패하면 이전 공간이 그대로이므로 `다시 시도 / 취소`를 보여 준다.
3. `Dungeon.ReleaseContent`(컷신 취소, UI 닫기, 적·임시 이펙트 정리, 집·연습실·시험실 정리, 생성 스테이지 해제) → 새 씬을 활성 씬으로 지정 → `BindContent` → 진입 콜백 → 이전 씬 unload.
4. 한 프레임 뒤 로딩 화면을 닫는다. 3번에서 실패하면 `다시 시도 / 메인으로`를 보여 준다.

생성형 던전은 `StageDungeonBuilder.Build`가 `GameManager.PrepareGameScene`으로 Game 씬을 먼저 준비하고, 방 6개가 만들어져 `EnterGeneratedStage`가 끝날 때까지 로딩 화면을 유지한다. 같은 Game 씬 안의 다음 스테이지 이동은 로딩 화면 없이 기존 진행 표시를 쓴다.

## 전환 사유별 유지·초기화

씬 전환 자체는 초기화를 추가하지 않는다. 아래 초기화는 모두 분리 전부터 있던 `ResetRun` / `ResetAfterPractice` 호출 그대로다.

| 전환 | 로딩 | 유지 | 초기화 |
| --- | --- | --- | --- |
| 메인 → 로비 (Start / New_Game) | 있음 | 설정·볼륨 | 런 전체(`EnterTown`→`ResetRun`) |
| 로비 → 게임 (동쪽 관문) | 있음 | 설정 | 런 전체, 이후 무기 연습 → 첫 성흔 → 인장 선택 → 첫 방 |
| 같은 런의 방·스테이지 이동 | 없음 | 인장·성흔·동전·HP·탄약·쿨다운·진행 | 적·탄환·장판 |
| 로비 → 디버그 (서쪽 관문) | 있음 | 무기, 피해 무적 | HP·탄약·쿨다운(`ResetAfterPractice`) |
| 선택실 ↔ 전투장, 재도전 | 없음 | 선택 보스, 무기, 인장·성흔·동전, 무적 | 보스·암전·파괴 바닥·추적검·음악 |
| 디버그 → 로비 (남쪽 포탈) | 있음 | 설정 | 런 전체(시험실에서 얻은 인장·동전 포함, 분리 전과 동일) |
| 사망·일시정지 → 로비 | 있음 | 설정 | 런 전체 |
| 어디서든 → 메인 | 있음 | 설정·볼륨 | 런 전체, 헌터·까마귀 숨김 |

런을 유지한 채 마을에 다녀오는 기능은 분리 전에도 없었고 이번에도 추가하지 않았다.

## 이전 중간 구현에서 고친 결함

| 결함 | 수정 |
| --- | --- |
| `PaperCard`가 표시 카드를 원본의 자식으로 만들어, 눕히거나 기울인 카드의 크기와 정렬 그룹 순서가 달라짐 | 자식 배치를 되돌리고 원본과 같은 씬으로 옮기기만 함 (`PaperCard.cs`, `SceneContent.cs`의 `SceneOwnership`) |
| 추출 도구가 Hunter·Raven·GameUI 프리팹 연결을 풀고 방을 일반 복사본으로 만듦 | 원래 오브젝트를 그대로 새 씬으로 옮김. 프리팹 인스턴스 11개(방 6, 히르바, Hunter, Raven, GameUI, RunMapCanvas) 유지 확인 |
| 던전 준비 실패 뒤 재시도하면 빈 Game 씬에 타이틀이 뜸. 전환 코루틴이 던전 빌더에 얹혀 중간에 끊길 수 있음 | 전환 코루틴을 GameManager가 소유. 재시도는 방 만들기부터 다시. 이전 공간이 살아 있으면 `취소` 가능 |
| 새 씬을 "마지막 씬"으로 추정 | 로드 전 handle 집합과 경로로 식별 |
| 분리 후 `EditableValidation`의 기존 검사가 생략됨 | UI 버튼 수·바인딩 키·몬스터 프리팹 외형·방의 문/도착점/소품·스프라이트·누락 스크립트 검사를 `SceneSeparationSetup.Validate`로 옮겨 유지 |
| 빌드 설정 메뉴가 분리 후 PlayerSettings·스플래시를 건너뜀 | 두 경로 모두 `ApplyPlayerSettings` 실행 |
| 메인 메뉴에 종료 버튼 없음, Game 씬 직접 Play가 옛 고정 6방 런 | 타이틀에 `게임 종료`(`GameUIAction.QuitGame`) 추가, 직접 Play도 생성형 원정으로 시작 |
| NPC 대화 초기화가 씬 연결마다 글꼴을 새로 만듦 | `Rebind`로 NPC 목록만 갱신 |

## 변경 파일

신규: `Assets/Scripts/GameManager.cs`, `SceneContent.cs`, `DungeonSceneRouting.cs`, `SceneSplitCheck.cs`, `Assets/Editor/Authoring/SceneSeparationSetup.cs`, `Assets/Scenes/MainMenu|Lobby|Game|Debug.unity`, `Assets/Resources/Session/GameSession.prefab` 및 각 meta.

수정: `Dungeon.cs`, `DungeonBossTrial.cs`, `PaperCard.cs`, `Authoring/GameUIView.cs`, `Authoring/TownNpcDialogueController.cs`, `Authoring/UICommand.cs`, `Room/Runtime/StageDungeonBuilder.cs`, `Assets/Editor/ProjectSetup.cs`, `Editor/Authoring/EditableValidation.cs`, `Editor/Authoring/EditableProjectSetup.cs`, `ProjectSettings/EditorBuildSettings.asset`, `Docs/Monsters.md`.

몬스터 코드(`Assets/Scripts/Enemy/**`)와 몬스터 프리팹은 변경하지 않았다.

## 검증 결과

실행 환경: Unity 6000.3.17f1 배치 모드. 로그는 `Logs/SceneSplit-*.log`(Git 제외 폴더).

| 항목 | 결과 | 증거 |
| --- | --- | --- |
| C# 컴파일 | 통과 (오류 0, 기존 경고 CS0108 1건) | `SceneSplit-migrate-1.log` |
| 마이그레이션 + 정적 검증 | 통과 `WNN_SCENE_MIGRATION_PASSED`, `WNN_SEPARATED_SCENES_VALIDATED` | 같은 로그 |
| 원본 Main 보존 | `git diff Assets/Scenes/Main.unity` 변화 없음 | — |
| Play 모드 자동 검사 | 통과, 확인 303개, 오류·예외 로그 0 `WNN_SCENE_SPLIT_PLAYCHECK_PASSED` | `SceneSplit-play-1.log` |
| 빌드 메뉴 → Windows 빌드 | 통과 `WNN_BUILD_SUCCESS`, 4개 씬 포함, 기존 규칙·아트·모션 검사 통과 | `SceneSplit-build-2.log` |
| 기존 Main의 심판관 플레이 검사 | 통과, 230항목 | `SceneSplit-check-PaladinLegacy.log` |
| 기존 에디터 검사 7종 | ProjectileSpace·SlowMovement·RobeMan 통과. CrowdControl·EnemyModule·EnemyPair·Logan 실패 | `SceneSplit-legacy-2.log` |

Play 모드 자동 검사가 확인한 범위: 타이틀 상태와 종료 버튼, 메인↔로비 3회 왕복, 로딩 중 연타 무시, NPC 대화 열기/닫기, 집 출입, 로비→디버그, 선택실→전투장(같은 씬), 재도전 시 보스 선택·동전 유지, 남쪽 포탈 귀환, 보스전 도중 메인 이탈 후 잔여물 없음, 동쪽 관문→Game(생성 방·연습실·적의 소속 씬), 연습실 통과→첫 방, 일시정지 상태에서 마을 귀환, 두 번째 원정, 메인 귀환. 매 공간에서 로드된 콘텐츠 씬 1개, EventSystem·AudioListener·GameManager·Dungeon 각 1개, timeScale 1, 헌터·까마귀 카드 유지, 카메라 원근 복귀, 남은 적·탄환·반복 음악 없음을 확인한다.

실패한 기존 검사 4종에 대해: EnemyModule("Chase respects obstacle resolution")과 EnemyPair(`Player.SafeEntry`의 NullReference)는 기준 커밋 `69e070e`만 담은 별도 복사본에서도 같은 지점에서 실패했다. CrowdControl("Shotgun prefab settings")과 Logan("Health remains 350 in later rooms")은 프리팹 값 검사이며, 기준 커밋에서 따로 돌려 보지는 않았다. 이번 작업의 diff에는 몬스터 코드·프리팹 변경이 없다.

## 미검증

- 사람이 직접 플레이하며 보는 화면: 그림 크기·정렬, 로딩 화면과 새 버튼 3개의 위치·모양, 타이틀 그림 위 `게임 종료` 버튼의 어울림.
- 실제 전투 진행: 방 클리어, 다음 스테이지·분기 선택, 서비스방, 사망 연출과 결과 창, 최종 보스 노드 클리어. 자동 검사는 첫 방 진입까지만 간다.
- 심판관 1→2페이즈·암전·지형파괴 도중 이탈. 자동 검사는 전투 시작 2초 뒤 이탈만 확인했다.
- 로딩 실패 화면(재시도/취소/메인으로). 코드로만 확인했고 실패를 일부러 일으켜 보지는 않았다.
- 빌드된 실행 파일: 창 없이 25초 실행해 오류 로그가 없음만 확인했다. 화면과 조작은 확인하지 않았다.
- 명령줄 스모크 테스트(`--smoke-test` 등)는 기존 Main 전용이다. 분리된 씬에서는 붙지 않고 오류 로그로 안내한다. 빌드에 Main이 없으므로 현재 빌드로는 실행할 수 없다.
- `Tools/SceneReview.ps1 -Mode Compare`: 이 PC에 PowerShell 7(`pwsh`)이 없어 실행하지 못했다.

## Unity에서 사용하는 방법

- 추가로 Inspector에서 연결할 것은 없다.
- 편집 위치: 마을은 `Lobby.unity`, 방 템플릿은 `Game.unity`, 헌터·까마귀·카메라·UI·몬스터 프리팹 목록은 `GameSession.prefab`. 방과 히르바, Hunter, Raven, GameUI는 여전히 `Assets/Editable`의 프리팹과 연결돼 있다.
- 새 버튼: GameSession 프리팹 → `GameUI` 아래 `Title/Title_Quit`, `Pause/Pause_MainMenu`, `Results/Results_MainMenu`. RectTransform으로 위치를 옮길 수 있다.
- 메뉴: `Western Lemegeton > Scenes > Validate Separated Scenes`, `Tests > Run Scene Split Check (Play Mode, from MainMenu)`, `Tests > Run Legacy Editor Checks (Main)`, `Build Windows Player`.
- 배치 재실행: `Unity -batchmode -projectPath <경로> -executeMethod WesternLemegeton.SceneSplitCheck.RunBatch -checkSceneSplit -logFile <로그>`.
- `Create Separated Scenes (Once)`는 이미 실행했다. 씬이나 프리팹이 하나라도 있으면 덮어쓰지 않고 중단한다.
