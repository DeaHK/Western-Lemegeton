# 클로드 맥스용 씬 분리 작업 지시문

> 2026-11-25: 이 문서 작성 이후 코덱스에서 구현을 시작했다. [CONTINUE_HERE.md](CONTINUE_HERE.md)의 변경 파일·현재 설계·남은 작업을 우선하여 이어간다. 기존 구현을 다시 만들거나 초기화하지 않는다.

아래 내용 전체를 새 작업의 지시문으로 사용한다. 파일 접근이 가능한 클로드 환경에서 `E:\GitHub\Western-Lemegeton`을 작업 폴더로 연다. 파일 접근이 없는 채팅 환경에서는 이 문서만으로 프로젝트를 검수했다고 주장하지 말고 실제 저장소를 연결한다.

## 목표

Western-Lemegeton의 기존 기능을 유지하면서 Unity 씬을 MainMenu, Lobby, Game, Debug로 분류하고, GameManager를 통한 상태 전달과 비동기 로딩 화면을 구현한다. 사용자는 코드 초보자다. 결과는 플레이어가 보는 동작과 필요한 Unity 조작을 중심으로 설명한다.

최초 작성 시점에는 씬 분리를 구현하지 않았다. 현재는 라우팅·로딩·추출 코드가 작성 중이며, 새 씬 파일과 공용 프리팹은 아직 없다. 빌드 진입점은 여전히 Assets/Scenes/Main.unity다. 아래 설계와 실제 구현 방향의 차이는 CONTINUE_HERE.md를 따른다.

## 읽기 순서와 사실 판단

1. AGENTS.md.
2. Docs/SceneReview/PROJECT_REVIEW.md와 CODEX_HARNESS.md.
3. ProjectSettings/ProjectVersion.txt, EditorBuildSettings.asset, Packages/manifest.json.
4. Assets/Scenes/Main.unity와 GameSceneBindings, Dungeon.cs, DungeonEntrance.cs, DungeonBossTrial.cs, DungeonTrainingStations.cs.
5. Room/Runtime의 StageDungeonBuilder, ExpeditionStageCoordinator, StageLayoutProvider, RunStageSelectionController.
6. Player, Raven, GameUIView, GameHUD, StigmaManager, PassiveManager, PassiveRuntimeContext, CinematicDirector.
7. PaperWorld, PaperCard, RoomAuthoring, Rules, CombatBody, ProjectileSpace와 보스 정리 코드.
8. Assets/Editor/ProjectSetup, EditableValidation, 기존 Smoke/Checks, 관련 프리팹과 Resources.

기준 스냅샷은 Docs/SceneReview/baseline.json이며 174개 C# 파일의 분류와 씬 참조가 들어 있다. 이후 변경이 있을 수 있으므로 현재 코드를 우선한다. 기존 AI_HANDOFF_CURRENT, README, 성흔 문서는 과거 작업도 포함한다. RunBuild 제거 기록과 현재 소스 존재처럼 불일치가 있으면 현재 씬·프리팹·호출자를 확인한다. Verification의 복제 프로젝트와 Assets/_Recovery를 현재 게임으로 오인하지 않는다.

## 씬별 책임

| 씬 | 구현할 공간과 동작 |
| --- | --- |
| MainMenu | 타이틀, 시작, 설정, 게임 종료. 로비 진입 전에는 전투를 시작하지 않는다. |
| Lobby | 히르바, NPC 대화, 집, 던전과 디버그 입구. 귀환 이유에 맞춰 런 상태를 처리한다. |
| Game | 플레이 공간, 고정/생성형 던전 방, 서비스방, 웨이브와 전투 HUD. 생성된 방은 이 씬 소유다. |
| Debug | 무기 훈련·몬스터 테스트·보스 선택실·보스전과 테스트 장치. 기존 마을 진입 및 보스 재도전 동선을 연결한다. |

GameManager와 로딩 Overlay는 콘텐츠 씬 교체에도 살아 있는 서비스 루트로 만든다. 별도 Bootstrap 씬은 필요할 때만 사용하며, 사용자에게 보이는 공간 분류는 위 네 가지다. 로딩은 전환 동안 진행 상황을 표시하고 준비가 끝난 다음 새 공간을 보여 준다.

정상 새 런의 무기 연습·최초 성흔/인장 선택도 현재 존재한다. Debug 씬에 훈련실을 옮기는 경우 진입 목적을 전달해 정상 시작 절차와 자유 테스트의 출구를 구분한다. 분류 때문에 정상 입장 절차를 생략하지 않는다.

## 상태 전달과 초기화

먼저 전환 표를 작성한다. 각 전환에 출발/목적지, 사유, 유지할 데이터, 초기화할 데이터, 목적지 스폰, 성공/실패 동작을 적는다. 최소 새 게임, 로비 귀환, 같은 런 방/스테이지 이동, Debug 진입, 시험실 귀환, 보스 재도전, 사망, 메인 귀환을 포함한다.

GameManager는 전환과 세션 데이터의 단일 소유자를 정한다. 기존 PassiveManager와 StigmaManager를 재사용할 수 있으며, 데이터를 옮기기 위해 별도 사본을 동시에 갱신하는 구조는 피한다. 상태 복원 시 중첩 효과나 장착 보너스를 두 번 적용하지 않는다.

유지 대상은 런 seed와 진행, 보상 획득 여부, 인장/패시브, 대상별 성흔 보유·레벨·장착, 동전·보급, 무기·탄약·HP·필요 쿨다운과 자원, 디버그 무적/보스 선택, 설정·볼륨이다. 전환별 적용 여부는 기존 코드와 사용자가 승인한 흐름을 따른다. GameManager가 모든 공간을 옮길 때 ResetRun을 호출하는 방식은 요구사항을 충족하지 못한다.

씬 로컬 대상은 카메라·표시 액터·방·문·적·탄환·장판·상호작용 객체·HUD다. 씬 해제 시 구속과 입력 잠금, Timeline, 음악, 이벤트 구독, 독·로프·까마귀 타겟, 보스 암전·지형파괴·추적검을 정리한다. 세션에는 파괴될 GameObject나 Transform을 보관하지 않는다.

현재 Dungeon.Awake의 GetComponent 요구와 마을 초기화, StigmaManager.Start의 전투 객체 생성, GameSceneBindings의 직접 참조를 새 생명주기에 맞게 변경한다. Dungeon.I를 유지한다면 씬 준비/해제 사이에서 접근을 막고 유효한 인스턴스로 바꾼다. GameManager를 추가하면서 기존 Dungeon이 계속 별도의 전환을 실행하는 이중 구조가 생기지 않게 한다.

## 구현 순서

1. git status와 정적 Compare로 현재 변경을 파악하고, 프로젝트 리뷰의 구조가 실제 파일과 일치하는지 확인한다.
2. 소유권과 전환 표를 만든 뒤 초기화·바인딩·정리 함수를 분리한다. 기존 동작과 public 진입점의 호출 순서를 보존한다.
3. GameManager, 영구 로딩 UI, 중복 요청 방지와 오류 복구를 구현한다. 시간 정지 상태에서도 로딩이 진행되고 재시도 시 상태가 중복 적용되지 않아야 한다.
4. MainMenu→Lobby부터 실제 씬 전환을 연결한다. 이어 Game과 생성형 던전, Debug와 기존 훈련/보스 동선을 연결한다. 현재 serialized prefab override를 보존한다.
5. StageDungeonBuilder의 목적 씬, 동적 렌더러와 효과의 부모/수명을 점검한다. 동일 콘텐츠가 두 번 활성화되는 프레임을 제거한다.
6. 빌드 설정뿐 아니라 ProjectSetup의 Main 하드코딩, EditableValidation의 단일 씬 전제, setup/test 도구의 시작 경로를 갱신한다. 편집용 월드를 재생성해 기존 수작업을 덮어쓰지 않는다.
7. CODEX_HARNESS.md의 회귀표에 따라 적절한 컴파일·Editor·플레이·빌드 검증을 진행한다. 사용자의 테스트 제외 지시가 있으면 해당 실행은 미검증으로 남긴다.
8. 현재 구조·사용 방법·전환 표·실제 검증 결과와 미해결 사항을 Docs/SceneReview/IMPLEMENTATION_RESULT.md에 기록하고 코덱스에 넘긴다.

각 단계는 구현을 끝내기 위한 작업 단위다. 사용자에게 반복 승인 요청을 하라는 의미가 아니다. 변경 범위 안에서 필요한 연결·복구는 계속 진행한다. 기존 변경을 reset하거나 작업 파일을 삭제해서 정리하지 않는다.

## 완료 조건

- 네 공간을 실제로 왕복할 수 있고 씬 전환마다 로딩 화면이 보인다.
- 씬 직접 실행 및 실제 Windows 빌드의 시작 경로가 명확하다.
- 런 초기화와 이동을 구분하고 모든 보유/진행 데이터가 전환 표와 맞게 유지된다.
- 마을·NPC·집·훈련·초기 선택·생성형 탐험·서비스방·전투·보스 재도전이 연결된다.
- 카메라·AudioListener·EventSystem·입력·전역 서비스가 중복되지 않는다.
- 2.5D XY/XZ 변환과 룸 경계, 피격 판정, 기존 전투 수치를 보존한다.
- 보스전 중 이탈/사망/재도전 후 암전·파괴 지형·구속·추적검·음악이 남지 않는다.
- 변경된 몬스터 코드·프리팹·연출은 같은 작업에서 Monsters.md에 반영한다.
- 빌드 메뉴가 새 씬 목록을 유지하며, 테스트를 제거하거나 성공 로그를 고정해 통과시키지 않는다.
- 보고서에 변경 파일, 구조 선택 이유, 실제 수행한 검증과 미검증 항목을 구분한다. 모든 기능 정상이라는 문장은 실제 회귀 검증 결과로 뒷받침한다.

최종 답변에는 사용자가 열 씬, 추가 Inspector 연결이 있다면 오브젝트·컴포넌트·필드·연결 순서, 검증 결과와 남은 문제를 간단히 적는다. 코덱스가 같은 프로젝트에서 변경을 재확인할 수 있게 결과 문서 경로도 제공한다.
