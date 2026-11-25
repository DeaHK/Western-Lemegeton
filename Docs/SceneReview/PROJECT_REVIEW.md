# 씬 분리를 위한 프로젝트 리뷰

> 2026-11-25 이어가기: 실제 구현이 시작되었다. 아래는 작업 전 리뷰이며, 현재 코드·미완료 사항은 [CONTINUE_HERE.md](CONTINUE_HERE.md)를 먼저 읽는다. 새 씬 생성과 Unity 검증은 아직 완료되지 않았다.

2026-11-24 작업 트리를 기준으로 작성했다. 기준 커밋은 `69e070e`이며 전체 SHA와 파일별 SHA256은 [기준 스냅샷](baseline.json)에 있다. 조사 시작 시 추적 파일의 수정 사항은 없었다. 현재 코드·Main YAML·프리팹 참조·빌드 설정·기존 문서·최근 커밋을 대조한 정적 리뷰다. 모든 기능의 실행 정상 여부는 이번 조사로 확정할 수 없으며 Unity 실행과 플레이 검사는 수행하지 않았다.

사용자가 원하는 분류는 메인 화면, 로비 마을, 게임방, 디버그다. 이 문서는 그 분류를 구현할 다음 작업자의 기준이며, 아래 제안 씬과 GameManager는 아직 구현되지 않았다.

## 현재 구조에서 확인한 사실

- Unity 버전은 `6000.3.17f1`, URP는 `17.3.0`, Timeline은 `1.8.12`, UGUI는 `2.0.0`이다.
- 빌드 등록 씬은 `Assets/Scenes/Main.unity` 하나다. Assets 안에는 이외에 `_Recovery/0.unity`, `RunMapUIPreview.unity`, `RunStageEntryPreview.unity`가 있다. 뒤의 두 씬은 `Assets/Scripts/Room/Tests_Optional/` 아래의 미리보기다.
- Main의 직접 직렬화 루트로 `Game_Systems`, `StageRoomManager`, `World`, `Actors`, `Main Camera`, `Viewport_Backdrop`, `EventSystem`을 확인했다. 프리팹 인스턴스 내부와 루트는 스냅샷의 외부 참조 및 Unity Hierarchy를 함께 확인해야 한다.
- Main은 히르바와 Room_01~06, Hunter, Raven, GameUI, RunMapCanvas, RobeMan, Cowboy, HauntedBull, Logan 프리팹을 참조한다. 심판관은 Resources 경로를 통해 등록되므로 Main의 프리팹 목록만으로 몬스터를 조사하면 빠진다.
- `Game_Systems`에는 Dungeon, GameSceneBindings, GameHUD, CinematicDirector, 패시브 관련 컴포넌트 네 개, StigmaManager, StigmaDebugMenu, StageDungeonBuilder가 연결돼 있다.
- `RunMapManager`에는 `ExpeditionStageCoordinator`와 `Tests_Optional/RunIntegrationTest`가 연결돼 있다. RunIntegrationTest의 검사는 ContextMenu로 실행되며 자동 Start 호출은 없다. 폴더 이름만으로 해당 컴포넌트가 실제 씬에서 제외된 것으로 판단하면 안 된다.
- `Dungeon.StartRun()`은 ResetRun → BuildStage → BeginPractice로 이어진다. 메인 시작 버튼을 곧바로 새 전투방으로 연결하면 무기 연습과 초기 선택 흐름이 누락된다.
- `Dungeon.EnterTown()`도 ResetRun을 호출한다. 마을 귀환을 상태 유지 이동으로 바꿀지, 기존 런 종료로 처리할지는 전환 사유로 구분해야 한다.
- 현재 집은 약 (-350,-350), 시험실은 (-180,-180), 보스 전투장은 (-240,-180)에 생성된다. 씬 분리 후 원점을 바꾼다면 출입구·장판·카메라·탄환·경계도 같이 이관해야 한다.

## 전체 기능 분류

174개 C# 파일의 개별 분류와 경로는 baseline.json의 scripts 배열에 전부 기록했다. 아래 표는 기능 기준 리뷰다. 파일 수는 Editor 및 검증 코드를 먼저 분류한 수치다.

| 기능군 | C# 수 | 구현과 자료 | 씬 이관 책임 |
| --- | --- | --- | --- |
| Editor·검증 | 42 | Assets/Editor, 각 Editor 디렉터리, Smoke, Checks, Tests_Optional | 빌드 대상·진입 씬·검증 전제 함께 갱신 |
| 몬스터·보스 | 35 | Enemy 모듈, RobeMan, Cowboy, HauntedBull, Logan, Paladin/Unjudged | 전투 씬 소유 적·투사체·장판, 도감 번호 보존 |
| 생성형 던전·지도 | 18 | Room/Runtime, StageDungeonBuilder, Coordinator, RunMap/Stage 생성기와 검증기 | Game 씬에 방 생성, 런 진행·seed는 세션에 보존 |
| 인장·패시브 | 14 | Passives/Runtime, Excel→SO→DB/Registry 생성 경로 | 보유 ID·중첩·획득 순서·적용 스탯을 이전 |
| 플레이어·전투 | 8 | Player, Raven, CombatBody, ProjectileSpace, SkillEffects | 액터 상태와 화면 투영·명중 계약 보존 |
| 런·이동 규칙 | 5 | Dungeon, Rules, ExpeditionProgress, RoomDoor, RunBuild | 씬 이동과 런 초기화 구별, 정적 상태 정리 |
| 씬·프리팹 바인딩 | 10 | GameSceneBindings, RoomAuthoring, ActorPresentation, WorldFootprint 등 | 씬별 연결과 준비 완료 검증 |
| 성흔 | 15 | Stigmas/Runtime의 Data·Core·UI·Debug | 대상별 보유·레벨·장착·조합·전투 자원 이전 |
| Timeline | 3 | CinematicDirector, Clip, Track와 Resources/Cinematics | 전환 중 취소, 새 카메라·액터 연결 |
| 집·훈련·시험실 | 3 | DungeonEntrance, DungeonBossTrial, DungeonTrainingStations | Lobby 집, Debug 시험 공간과 정상 입장 연습 동선 |
| UI·NPC | 9 | GameUIView, GameHUD, UICommand, TownNpcDialogue 등 | 메뉴·HUD·로딩·대화·지도 표시 책임 |
| 월드 표현·모션 | 12 | PaperWorld/Card, WorldDepth, PropGrounding, 환경·헌터 모션 | 씬 언로드 시 별도 생성 렌더러와 연출 제거 |

아트·데이터도 작업 범위에 포함된다. `Assets/Editable`는 편집용 액터·방·UI·공통 자원, `Assets/Resources`는 런타임 로드 리소스, `Assets/Audio`는 음원, `Assets/Data`는 데이터 입력 자료다. `Assets/Settings`, `URPDefaultResources`, ProjectSettings와 Packages는 렌더링·프로젝트 설정이다. 씬별 파일 이동보다 참조·수명 분리가 먼저다. Resources 문자열을 쓰는 파일과 `.meta` GUID를 같이 확인한다.

`Docs`는 설계·작업 이력, `Previews`는 과거 미리보기, `Logs`는 과거 실행 기록이다. `Verification/EditablePackageImport-20260922`는 별도 복제 프로젝트이며 현재 Assets의 근거와 섞지 않는다. `Backups`와 `Assets/_Recovery`도 보관 자료로 취급한다. Library, Temp, UserSettings는 현재 구현 분류에서 제외한다.

## 지금까지 작업된 기능과 이관 회귀 범위

| 영역 | 현재 유지해야 할 기능 | 확인할 코드·자료 |
| --- | --- | --- |
| 타이틀·설정 | 시작, UI 조작, 일시정지·설정·키 설정, 로고와 배경 | GameUIView, UICommand, ChangeKeySettings.md, ProjectSetup |
| 히르바 | 마을 배치, NPC 대화, 집과 던전 입구, 왼쪽 시험실 포탈 | Dungeon, DungeonEntrance, TownNpcDialogueController/View |
| 훈련·입장 | 무기 선택·해제, 더미, 최초 성흔/인장 선택, 실제 던전 진입 | DungeonEntrance.BeginPractice/ChooseEntry 흐름 |
| 탐험 | 고정 6방과 생성형 6방, 연결문, 서비스방, 잠긴 출구, 웨이브, 재방문·중복 보상 방지 | ExpeditionProgress, ExplorationStage, Room/Runtime |
| 전투 | 단검·총, 탄약·장전·스킬·회피, 까마귀 명령·연계, 피격·넉백·구속, 2.5D 명중 | Player, Raven, Combat, Enemy, Rules |
| 성장 | 패시브/인장 중첩·스탯, 성흔 대상별 장착·레벨·조합·G 스킬, 동전·강화 | Passives, Stigmas, DungeonTrainingStations |
| 등록 몬스터 | 일반 001 RobeMan, 일반 002 Cowboy, 엘리트 001 HauntedBull, 보스 001 Logan, 보스 002 심판관 | Monsters.md 및 각 프리팹·모듈 |
| 보스 시험실 | 보스 프로필 변경, 무기·인장·강화·동전·무적 장치, 입장·귀환·재도전 | DungeonBossTrial, DungeonTrainingStations |
| 심판관 | 1페 신성 패턴, 60% 잔상·20% 암전, 갑옷 전환, 2페 검/맨손·지형파괴·추적검·최후 발악·퇴장 | Paladin/Unjudged, SanctuaryPaladin.prefab, Monsters.md |
| 연출 | 입장·사망·보스 Timeline, 2.5D 바닥·벽·그림자·정렬, 보스 음악·볼륨·전환 | Cinematics, PaperWorld/Card, BossTrialAudio, TrialInterior |

최근 커밋에서 확인되는 작업 축은 10월 기본 구조·패시브·성흔·키 설정, 이후 몬스터 3종·NPC·까마귀·조준·로건, 11월 시험실·심판관 2페·몬스터 코드 및 프리팹 조정이다. 과거 문서에 적힌 구현 시점과 현재 HEAD의 파일 존재 여부를 별도로 판단한다.

## 리뷰에서 찾은 주요 결합

| 우선순위 | 근거 | 씬 분리 시 문제와 조치 |
| --- | --- | --- |
| 높음 | Dungeon.Awake, GameSceneBindings | Awake에서 마을·액터·카메라·패시브 DB를 모두 요구한다. MainMenu에서 같은 코드를 바로 켜면 실패한다. 서비스 초기화와 씬 바인딩, 콘텐츠 시작을 나눈다. |
| 높음 | Dungeon.I, Rules.RoomOrigin, Dungeon.Obstacles | 참조와 경계가 전역이다. 이전 씬을 언로드할 때 초기화하고 새 바인딩이 끝날 때 한 번 설치한다. 전환 중 Update/입력/피격을 차단한다. |
| 높음 | Dungeon.ResetRun/EnterTown/EnterGeneratedStage | 씬 이동 과정에서 ResetRun을 재사용하면 인장·성흔·동전과 진행이 지워진다. 새 런·런 종료·디버그 귀환·같은 런 이동의 의도를 구분한다. |
| 높음 | StageDungeonBuilder.Build | 생성 루트를 dungeon.gameObject.scene으로 옮긴다. Dungeon을 영구 보존하면 생성 방까지 영구 씬에 붙을 수 있다. 목적 콘텐츠 씬을 명시한다. |
| 높음 | ProjectSetup.CreateScene/ValidateAndBuild | 빌드 설정과 BuildPlayer가 Main 한 씬으로 고정된다. 신규 씬 등록만 하고 기존 빌드 메뉴를 누르면 설정이 덮인다. 같은 작업에서 변경한다. |
| 높음 | EditableValidation.ValidateImportedProject | Main을 열고 히르바·6방·액터·UI가 모두 존재한다고 검사한다. 다중 씬 계약에 맞춘 검증으로 이관한다. |
| 높음 | Dungeon.ClearTransient, PaladinEncounter.OnDisable, PaperCard.OnDestroy | 장판·구속·추적검·독·탄환·별도 렌더러가 각각 다른 정리 경로를 쓴다. 씬 unload와 생성 부모 관계를 대조한다. 전역 Find 정리는 로딩 중 신·구 씬이 함께 있을 때 새 오브젝트까지 지울 수 있다. |
| 중간 | StageDungeonBuilder.OnEnable/OnDisable | 지도·생성 이벤트 연결과 입력 차단 해제가 묶여 있다. 재바인딩을 여러 번 해도 구독 수가 증가하지 않도록 한다. |
| 중간 | StigmaManager.Start, CinematicDirector.Awake | Dungeon과 동작 주체를 시작 시점에 캐시한다. 서비스가 먼저 생기는 구조에서는 명시적인 Bind/Unbind가 필요하다. |
| 중간 | GameSceneBindings.UI, GameHUD.View, CinematicDirector | 카메라·HUD를 씬마다 바꾸면 직접 참조가 남는다. 로딩 UI를 독립시키고 기존 뷰를 해제한 뒤 새 뷰를 연결한다. |
| 중간 | PaperWorld, RoomAuthoring.Point, ProjectileSpace | 논리 XY와 표시 XZ가 공존한다. 씬 원점 변경 후 월드 Transform만 보고 판정 좌표를 바꾸면 이동·탄환이 틀어진다. |
| 중간 | AI_HANDOFF_CURRENT와 RunBuild.cs | 옛 문서는 RunBuild 제거를 기록하지만 현재 소스는 존재한다. Main에서 해당 스크립트의 직접 연결은 이번 조사에서 확인되지 않았다. 프리팹·호출자를 더 조사한 뒤 처리하며 파일 존재만으로 활성 기능이라고 판단하지 않는다. |

이 표는 정적 근거가 있는 이관 위험이다. 현재 게임에서 각각 런타임 버그가 발생했다고 주장하는 표가 아니다.

## 권장 씬과 소유권

| 제안 씬 | 내용 | 수명 |
| --- | --- | --- |
| MainMenu | 타이틀 배경·메뉴·시작·설정 UI | 앱 시작/메뉴 귀환 |
| Lobby | 히르바·NPC·집·출입구 | 로비 방문 |
| Game | 전투·서비스 방과 생성형 스테이지 루트, 게임 HUD | 플레이 공간 방문 |
| Debug | 무기/몬스터 테스트, 보스 선택실, 보스 전투장 | 디버그 방문, 내부 공간 전환은 기존 흐름 재사용 가능 |

권장 출발점은 MainMenu의 GameManager 프리팹이 영구 서비스 루트를 초기화하고 나머지 콘텐츠 씬을 비동기로 교체하는 구조다. 별도 Bootstrap 씬이 필요하면 숨은 시작 씬으로 추가할 수 있다. 로딩 화면은 영구 Overlay Canvas로 두어 콘텐츠가 없어도 진행률과 오류·재시도 버튼을 표시할 수 있게 한다.

GameManager는 전환 요청·세션 상태·현재 콘텐츠 씬·중복 전환 차단을 소유한다. 기존 Dungeon은 씬의 월드 진행자 역할로 정리하고 Dungeon.I의 사용자는 새 콘텐츠가 준비된 구간에서만 접근하게 한다. 기존 패시브/성흔 매니저를 영구 서비스로 재사용할 경우 GetComponent 결합을 명시적 주입으로 바꾸고, 전투 액터 참조는 각 입장마다 다시 연결한다. 동일 상태를 GameManager와 Dungeon 양쪽에서 독립적으로 갱신하지 않는다.

| 영구/세션 데이터 | 씬 로컬 오브젝트 |
| --- | --- |
| 설정·볼륨·입력 설정 | 카메라, AudioListener, 배경 카메라 |
| 런 seed, 층·노드·방·웨이브·방 보상/완료 상태 | 방 프리팹, 문, 장애물, NPC·대화 UI |
| 인장 ID·중첩·획득 순서·적용 스탯 | Hunter/Raven 표시 객체와 현재 적 |
| 성흔 대상별 보유·레벨·장착·전투 자원 | 공격 코루틴, 탄환·장판·추적검·구속·BGM 재생 객체 |
| 동전·보급·선택 무기·HP·탄약 등 전환 시 전달할 상태 | HUD·지도 View와 현재 씬 직렬화 바인딩 |
| 디버그 선택 보스·무적 설정·귀환 위치 의도 | 보스 전투의 현재 페이즈·파괴된 바닥·스포트라이트 |

런 중 이동은 스킬 쿨다운·버프/디버프·까마귀 자원도 기존 동작과 대조해서 이전한다. 진행 중 공격·묶인 대상·씬의 Transform을 세션 데이터로 보존하지 않는다. 재도전·새 런·로비 귀환의 초기화 범위는 실제 기존 흐름을 기준으로 전환 표에 기록한다. 무적의 HealthOnly는 피격·구속·넉백을 받되 피해만 0이라는 기존 계약을 보존한다.

## 로딩과 전환 계약

1. 목적지와 전환 사유를 받고 중복 요청을 막는다. 플레이 입력·상호작용·피격·컷신을 전환 상태로 묶는다.
2. 복구에 필요한 현재 세션 데이터를 캡처하고 로딩 Overlay를 표시한다. 메뉴 일시정지에서 들어와도 로딩 UI는 unscaled 시간으로 움직인다.
3. 목적 콘텐츠를 Additive로 준비한다. 준비 과정의 콘텐츠 Awake가 전투·음악·입력을 먼저 시작하지 않도록 한다. 새 씬 바인딩과 필수 리소스를 검증한다.
4. 이전 씬 공격·구속·음악·이벤트를 정리하고 목적 씬을 활성 씬으로 지정한다. 상태와 입장 위치를 한 번 복원한다. 렌더러·스폰 루트의 소유 씬을 확인하고 이전 콘텐츠를 unload한다.
5. 카메라·UI·성흔·패시브·까마귀·방 경계와 실제 준비 완료를 확인한 뒤 로딩을 닫고 입력을 복구한다. 프레임에서 검은 화면이나 이전 씬 피격이 남지 않아야 한다.
6. 실패 시 로딩 창에 실패 이유와 재시도/메뉴 복귀를 제공한다. 초기화를 두 번 적용하거나 이미 삭제한 씬을 참조하지 않는다. 재시도 정책에 맞춰 이전 세션 스냅샷을 유지한다.

Unity의 비동기 로딩 진행률은 활성화 전 준비 단계와 실제 준비 완료를 구분해서 표시한다. allowSceneActivation을 사용하는 경우 0.9 대기와 다음 비동기 작업의 정체를 고려한다. 데이터 복원과 바인딩까지 끝나야 완료로 표시한다.

각 씬 직접 Play도 검수한다. 영구 서비스가 없을 때 에디터 시작 도우미로 먼저 초기화하거나, 명확한 시작 경로를 제공해야 한다. 씬마다 GameManager·EventSystem·AudioListener가 중복 생성되는 상태는 허용하지 않는다.
