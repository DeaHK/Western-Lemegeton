# 씬 분리 구현 이어가기

> **2026-11-25 갱신: 이 체크포인트의 작업은 완료됐다.** 현재 상태와 검증 결과는 [IMPLEMENTATION_RESULT.md](IMPLEMENTATION_RESULT.md)를 본다. 아래 내용은 중간 시점의 기록이다.

기록일: **2026-11-25 (사용자 시간대 Asia/Seoul)**. 현재 상태는 **구현 중, 컴파일·실행 검증 전**이다. 사용자 요청으로 이어가기 지점을 기록했다. 이 문서가 이전 리뷰 문서의 현재 상태 설명보다 우선한다.

## 다음 대화에 붙여 넣을 요청

```text
E:\GitHub\Western-Lemegeton에서 씬 분리 작업 이어서 진행해.
AGENTS.md와 Docs/SceneReview/CONTINUE_HERE.md부터 읽고 현재 diff를 확인해.
이미 작성한 구현은 보존하면서 MainMenu / Lobby / Game / Debug와 로딩·상태 전달을 완성해.
씬 파일과 GameSession 프리팹은 아직 생성되지 않았고 컴파일도 검증하지 못했어.
문서에 적힌 실패 경로와 생성 던전 연결부터 리뷰하고 Unity 마이그레이션을 완료해.
검증한 것과 미검증을 구분하고, 몬스터 관련 변경은 도감도 함께 갱신해.
```

## 목표와 승인 범위

- 사용자는 프로젝트 리뷰, 코덱스 검수 하네스, 클로드 맥스가 읽을 지시문을 요청했다. 이후 “다음것도 작업해”로 실제 씬 분리 진행을 요청했다.
- 목표: 메인 화면 / 로비·마을 / 게임방 / 디버그 공간 분류, 로딩 화면, GameManager를 통한 상태 관리. 씬 이동 후 기존 기능을 유지한다.
- 과거 보스 패턴 조정 대화는 배경이다. 이번 작업에서 새로운 밸런스 변경이나 아트 재생성을 하지 않는다.
- 과거 특정 보스 변경에는 플레이테스트 제외 요청이 있었다. 현재 씬 분리의 검증 범위를 명확히 기록하고, 미실행 검사를 통과로 표현하지 않는다.
- 도감 갱신은 매번 재확인하지 않는다. AGENTS.md에 따라 같은 작업에서 갱신한다.
- 현재 체크포인트는 커밋하지 않았다. 다른 변경을 통째로 초기화하거나 baseline.json을 덮어쓰지 않는다.

## 실제 확인된 상태

| 항목 | 현재 사실 |
| --- | --- |
| 작업 폴더 | E:/GitHub/Western-Lemegeton |
| Unity | 6000.3.17f1 |
| 원본 리뷰 기준 HEAD | 69e070e6f5e3f6fc1ae91e35a4932d501c840c30 (재개 시 현재 HEAD도 확인) |
| 활성 빌드 씬 | 여전히 Assets/Scenes/Main.unity 하나 |
| 새 씬 파일 | MainMenu/Lobby/Game/Debug **아직 없음** |
| 공용 프리팹 | Assets/Resources/Session/GameSession.prefab **아직 없음** |
| 코드 | 아래 신규·수정 파일이 작업 트리에 있음 |
| 새 파일 meta | Unity import 미완료, 아직 생성 확인 안 됨 |
| Unity 컴파일 | 성공/실패 결과를 확보하지 못함 |
| 플레이·빌드 | 이번 씬 분리 구현에서는 실행 결과 없음 |
| 마지막 실행 로그 | Logs/SceneMigration.log, 마지막 메시지 Licensing is not yet initialized |
| 실행 프로세스 | 체크포인트 확인 시 Unity 프로세스 조회 결과 없음 |

로그의 라이선스 초기화 메시지만으로 라이선스 만료나 권한 부족을 단정하지 않는다. 시작 부분에 WMI 관련 assertion도 있다. 실제 C# 오류와 마이그레이션 성공 표시는 없다. PowerShell의 시작 명령 종료 코드 0을 Unity 작업 성공으로 해석하면 안 된다.

## 현재 구현 방향

처음 리뷰에서는 플레이어·카메라·UI도 씬별로 재생성하는 구성을 제안했지만, 실제 작업에서는 **첫 분리의 상태 손실 위험을 줄이기 위해 공용 세션을 유지**하는 방향을 택했다. 이 선택을 사용자에게 진행 설명했다. 이전 문서와 다른 부분은 최종 문서에서 반드시 정리한다.

- `GameSession` 루트: GameManager, 기존 Dungeon/Game_Systems, Hunter/Raven, 카메라, 공용 HUD, EventSystem, 성흔·인장 시스템, 생성 맵 관리자와 지도 UI를 함께 유지한다. 루트 하나에 DontDestroyOnLoad를 적용한다.
- 콘텐츠 씬: 마을/방/적 컨테이너/일시 이펙트 소유. GameManager가 additive 로드 → 유효성 확인 → 기존 콘텐츠 정리 → 새 바인딩과 진입 처리 → 기존 씬 unload 순서를 맡는다.
- `MainMenu`: 콘텐츠 바인딩, 공용 UI의 타이틀 표시.
- `Lobby`: 기존 Hirva 배치 복제. NPC/집/동쪽 원정 관문/서쪽 시험실 관문 유지.
- `Game`: 기존 6개 방 배치 복제, 생성 던전 인스턴스도 이 씬에 소유시킨다. 기존 신규 원정 무기 연습 동선 보존.
- `Debug`: 선택실과 보스 전투장은 기존 코드로 런타임 생성한다. 선택실↔전투장 이동은 동일 Debug 씬 내부 전환이다.
- 기존 `Main.unity`는 복구·레거시 검사용으로 저장 내용을 바꾸지 않고 남긴다. 정상 시작점은 마이그레이션 완료 후 MainMenu로 바꾼다.
- 기존 `ResetRun`, `EnterTown`이 초기화하던 런 데이터는 유지하는 것이 아니라 원래 동작대로 초기화한다. 씬 전환 자체가 별도 초기화를 추가하지 않는 것이 목표다. 모든 이동에서 인벤토리를 보존한다고 설명하면 안 된다.

## 신규 파일

| 파일 | 구현한 내용 / 아직 검증 전 |
| --- | --- |
| Assets/Scripts/GameManager.cs | 공용 세션, 로딩 오버레이, 전환 잠금, 콘텐츠 바인딩, 오류 시 재시도/메인 버튼, 생성 던전 준비 중 로딩 유지 |
| Assets/Scripts/SceneContent.cs | GameSpace enum, 콘텐츠별 RoomAuthoring/적 컨테이너 참조, 직접 씬 실행 시 세션 bootstrap, 바인딩 검증 |
| Assets/Scripts/DungeonSceneRouting.cs | ManagedSession 플래그, BindContent/ReleaseContent/ShowMainMenu, 종료 시 Dungeon.I/장애물 정리 |
| Assets/Editor/Authoring/SceneSeparationSetup.cs | 원본 Main에서 4개 콘텐츠 씬과 GameSession 프리팹을 추출하는 일회성 Build, 씬/프리팹 참조 Validate |

## 수정한 기존 파일

- `Dungeon.cs`: managed 세션에서는 기존 Awake 마을 생성 생략, StartRun→Game / EnterTown→Lobby 라우팅, 로딩 중 Update·카메라·전투 차단, 일부 null 처리.
- `DungeonBossTrial.cs`: OpenTrialGallery→Debug 라우팅, 비동기 Lobby 복귀 완료 후 관문 옆 배치하도록 ReturnFromTrial 분리.
- `TownNpcDialogueController.cs`: Initialize 때 NPC 목록을 비운다. 새 Lobby 바인딩에서 다시 초기화한다.
- `UICommand.cs`: 로딩 중 입력 차단, 기존 enum 값 유지하며 ReturnToMainMenu=14 추가.
- `GameUIView.cs`: managed 타이틀/로딩 중 콘텐츠 HUD 읽기를 건너뛴다.
- `PaperCard.cs`: 런타임 생성 Visible을 Source의 자식으로 둬 소유 객체/씬 수명에 함께 정리되게 한다. **모든 공용 렌더링에 영향이 있으므로 시각 확인 필요.**
- `StageDungeonBuilder.cs`: 생성 전에 Game 씬 확보, 생성 루트 소유 씬 수정, 성공/실패 시 준비 로딩 종료.
- `ProjectSetup.cs`: 세션 프리팹이 있으면 4씬 build settings 사용, BuildPlayer도 enabled 씬 목록 사용.
- `EditableValidation.cs`: 분리된 프리팹이 있으면 새 검증기로 연결.
- `EditableProjectSetup.cs`: Open 메뉴는 분리 완료 후 MainMenu 열기.

## 먼저 리뷰할 미해결 위험

아래는 코드 읽기로 찾은 점검 대상이며, 실행 재현 완료한 결함 목록이 아니다.

1. **GameManager 실패 복구:** `preparingGame`은 EnsureGameScene에서 true가 된다. 실패 후 메인 이동/재시도에서 확실히 false가 되는지 확인한다. 현재 Transition 완료 시 true면 오버레이를 닫지 않는다. 생성 던전 실패 뒤 재시도가 씬만 로드하고 방 생성을 재개하지 않거나 타이틀에서 입력이 잠길 수 있다. 실패 전이와 재시도 의도를 명시적으로 정리한다.
2. **로드한 씬 식별:** 현재 `GetSceneAt(sceneCount - 1)`로 새 씬을 찾는다. 다른 로더가 개입하지 않는 가정이다. 로드 전 handle 집합과 새 씬 path를 대조하거나 안전한 전환 계약으로 보강한다.
3. **전환 요청 충돌:** IsLoading 중 Route는 요청을 소비한다. EnsureGameScene은 이미 로딩 중이면 즉시 종료한다. builder가 잘못된 콘텐츠에 계속 생성하지 않는지 검증한다.
4. **ReleaseContent 순서:** 기존 생성 던전 root를 event로 파괴한 뒤 Rooms/Progress와 ResetRun이 파괴된 참조를 읽지 않는지 확인한다. Town/Debug/Game/MainMenu 왕복, 생성 던전 중단을 특히 확인한다.
5. **상태 보존:** 동일 Hero/Raven/Passive/Stigma 인스턴스 유지가 곧 모든 값 보존은 아니다. ResetAfterPractice, ResetRun, EnterGeneratedStage(newRun)가 언제 호출되는지 검증한다.
6. **UI 초기화:** 비활성화된 플레이어를 다시 활성화할 때 Awake/Start 순서, NPC overlay, 지도 버튼/일시정지/결과 버튼이 정상인지 확인한다. 추가한 메인 버튼은 기존 패널의 배치와 겹칠 수 있어 시각 확인이 필요하다.
7. **PaperCard:** 새 Visible 자식의 layer, world position/rotation, 부모 scale 보정과 Source 비활성화, 기존 authored Visible의 수명을 확인한다. 보스·일반 몬스터·투사체·지형까지 공용 영향이 있다.
8. **Editor 추출:** Main의 serialized override와 루트 내부 참조가 GameSession에 보존되는지, 제거한 World로 향하는 참조가 없는지 확인한다. CloneRoom의 씬 이동/Prefab unpack도 Unity에서 검증한다.
9. **마이그레이션 재실행:** Build는 새 씬 또는 세션 프리팹이 하나라도 있으면 덮어쓰기를 막는다. 중간 실패 시 전체 자동 삭제로 해결하지 말고 이번 실행이 만든 파일만 확인하여 복구한다.
10. **기존 편집 도구:** 일부 다른 Setup/Smoke는 아직 Main을 직접 연다. 정상 빌드/편집 도구와 레거시 검사의 구분을 문서화하고 필요한 연결을 바꾼다. 기존 검증 전체를 새 Validate로 우회하면서 검사 범위가 줄지 않았는지도 확인한다.
11. **프로젝트 설정:** 새 ProjectSetup 분기는 기존 PlayerSettings/스플래시 설정을 건너뛴다. 기존 값 유지와 일반 Configure 동작을 확인한다.

## 이어서 진행할 순서

1. git status / diff와 위 신규 파일을 읽어 현재 작업을 확인한다. 다른 사용자의 후속 변경이 있으면 보존한다.
2. 위 실패 경로를 리뷰·수정하고 C# 컴파일을 확보한다. 아직 어떤 새 코드도 컴파일 통과로 기록하지 않았다.
3. Unity 프로세스와 편집 중 씬 여부를 확인하고 마이그레이션을 실행한다. 라이선스/실행 환경 문제는 실제 로그를 근거로 처리한다.
4. 새 씬 4개, Session 프리팹, meta, build settings를 확인한다. Validate의 성공 로그가 있어야 한다.
5. 실제 씬 전환과 상태 유지 검증을 수행한다. 실패가 있으면 해당 원인 수정 후 필요한 검사만 재실행한다.
6. Tools/SceneReview.ps1 Compare를 실행하여 원본 baseline 대비 변경·누락을 확인한다. 원본 스냅샷을 덮어쓰지 않는다.
7. Docs/Monsters.md의 공통 씬 수명 변경 기록을 실제 결과로 갱신한다. IMPLEMENTATION_RESULT.md를 만들고 PROJECT_REVIEW/CLAUDE_TASK/CODEX_HARNESS의 설계 차이와 검증 결과를 정리한다.

### Unity 실행 정보

실행 파일: `C:/Program Files/Unity/Hub/Editor/6000.3.17f1/Editor/Unity.exe`

마이그레이션 인수:

```text
-batchmode -nographics -projectPath E:/GitHub/Western-Lemegeton -executeMethod WesternLemegeton.EditorTools.SceneSeparationSetup.Build -quit -logFile E:/GitHub/Western-Lemegeton/Logs/SceneMigration.log
```

Windows에서 숨김 Start-Process로 시작했지만 성공 결과는 확보하지 못했다. 새 실행 전 기존 Unity가 같은 프로젝트를 사용 중인지 확인한다. 정상 완료 표시는 `WNN_SCENE_MIGRATION_PASSED`와 `WNN_SEPARATED_SCENES_VALIDATED`다.

### 핵심 검증 동선

- 각 씬을 직접 열어 시작해도 세션·EventSystem·AudioListener가 정확히 하나인지.
- 메인→마을→NPC 대화/집→시험실→심판관 1·2페→선택실→마을→메인.
- 시험실 인장/강화/동전/피해 무적, 보스 재도전, HP바·BGM·암전·장판 정리.
- 마을→새 원정 지도→Game 생성→무기 연습→방 전환→다음 스테이지→사망/클리어→마을.
- 동일 세션 객체 유지, 성흔/인장 중복 등록 없음, 신규 런에서만 의도한 초기화.
- 연속 클릭/일시정지 중 이동, 로드 실패·바인딩 실패 후 재시도/메인 복구, 전환 중 피격 없음.
- 이전 콘텐츠 씬의 적·투사체·장판·음악·구속·장애물이 다음 씬에 남지 않음.
- 최종 빌드 시작점 MainMenu와 로딩 화면, 메인 복귀 버튼 위치 확인.

## 기존 산출물

- PROJECT_REVIEW.md: 작업 전 전체 분류·의존성 리뷰. 역사적 기준으로 남긴다.
- CODEX_HARNESS.md: 코덱스 검수 절차와 회귀 검증표.
- CLAUDE_TASK.md: 맥스 인계 지시문. 현재 구현 방향은 이 체크포인트를 먼저 적용한다.
- baseline.json: 2026-11-24 작업 전 텍스트 자산/meta 스냅샷, 수정 금지.
- Tools/SceneReview.ps1: Snapshot/Compare 정적 조사. 이전 단계에서 도구 동작과 합성 diff를 검증했으나, 현재 구현의 Unity 성공을 뜻하지 않는다.
