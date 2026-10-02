# Dungeon Block Refactor

- 작업일: 2026-10-03 (Asia/Seoul).
- 대상: `E:\GitHub\Western-Lemegeton\Assets\Scripts\Dungeon.cs`.
- 시작 HEAD: `c218280dbb80d83b03e17b05ad92f8bd50df86a3`.
- 기준: 작업 시작 시 Assets Dungeon.cs. 루트 Dungeon.cs는 가져오거나 수정하지 않았다.
- 참고: `Docs/WESTERN_LEMEGETON_PROJECT_ANALYSIS.md`, `Docs/PLAYER_BLOCK_REFACTOR.md`.
- 이전 작업의 Player.cs 수정과 기존 ShaderGraphSettings.asset 로컬 상태는 보존했다. 이번 변경은 Dungeon.cs 내부 정리와 이 문서 생성뿐이다.
- Unity Editor/Play Mode/빌드/Smoke 실행, 새 클래스·Manager·MonoBehaviour·ScriptableObject, BossController·Reward 구현, commit/push 없음.

## 1. 변경 전 Dungeon 책임

Dungeon은 단일 Main Scene의 공유 접근점 `Dungeon.I`이면서 Scene 바인딩, 런 초기화, 마을/6개 방 활성화, 이동·서비스·지도 입력, 5개 스테이지 진행, 웨이브/적 생성·전멸, Player 사망/승리, 카메라, 사운드, Timeline, opt-in 검사 부착을 함께 담당했다.

- `Awake`: singleton 설정→필수 Scene 검사→컴포넌트/배우 바인딩→카메라 설정→합성 오디오→컷신/적 부모 참조→마을 배치→검사 부착.
- `Update`: modal 상태별 입력·조기 반환, pause/notice, 여행/마을 처리, RunTime, WaveBreak, 적 목록 정리 및 전멸 완료.
- `ResetRun`: 컷신 취소, 적·일시 효과 정리, Hero/Crow/Build reset, 진행/seed/random 초기화.
- `ActivateRoom`: root 활성화·footprint capture, 도착 문/스폰 배치, SafeEntry·Crow·카메라, 완료방/전투방/서비스방 시작.
- `SpawnWave`: 상태·적 수, spawn 위치/종류/Prefab 생성·목록 등록·효과, 안내, 보스 컷신.
- `LateUpdate`: 컷신 카메라 우선 또는 FOV/일반 추적.

여러 책임이 같은 필드 선언문과 긴 한 줄 함수에 섞여 있었다. 책임 자체나 공개 경계는 바꾸지 않고 필드 그룹, region, private 함수로 읽기 순서를 정리했다.

## 2. 생성한 책임 블럭

상단 `Fields and state by responsibility`는 공유 상태 선언용이며 아래 동작 블럭은 **17개(요청한 16개+보스 기존 코드 1개)**다.

| 블럭 | 주요 함수/역할 |
|---|---|
| Singleton / Scene Binding / Awake | Boot, Awake, BindSceneActors |
| Run State | Update, HandleSettingsInput, HandlePauseInput; modal 조건/return의 원래 순서 유지 |
| Run Reset / Start | ResetRun, ResetRunProgress, StartRun |
| Town / Stage Entry | ClearEnvironment, BuildTown, EnterTown, BuildStage |
| Room Activation / Travel | ActivateRoom, ActivateRoomLayout, PlaceActorsAtRoomEntry, BeginRoomContent, ApplyFootprints, NearbyDoor, TryDoor, TryTravel |
| Service Room Interaction | HandleServiceInput, CloseService, ClaimSigil, ClaimCard, Upgrade, CardRarity |
| Wave Lifecycle | SpawnWave, BeginWaveBreak, TickWaveBreak |
| Enemy Spawn / Enemy Tracking | SpawnWaveEnemies, DestroyTrackedEnemies, RemoveDefeatedEnemies, SpawnSafe, SpawnPoint, Nearest |
| Room Clear / Completion | CompleteCombatRoom; 호출 전 조건과 CompleteWave 검사는 Update에 유지 |
| Route / Stage Progression | HandleRouteInput, AtStageExit, OpenRoute, CloseRoute, ChooseRoom, ChooseEvent, NextRoom |
| Player Death / Victory | BeginDeath, FinishExpedition |
| Boss-related existing code | IsBossWave, GetWaveEnemyCount, GetWaveEnemyKind, PlayBossEntrance |
| Camera | ResetCamera, LateUpdate, UpdateFollowCamera |
| Audio | InitializeAudio, Tone, Sound |
| Cinematic | HandleCinematicInput; 다른 Timeline 호출은 원래 수명주기의 위치 유지 |
| Utility / Query | BurnLevel/FuryLevel/RavenLevel, ApplySeongheunStack, AcquirePassive, ClearTransient, Tell, TickNotice |
| Test / Smoke Entry Points | AttachSmokeTest |

필드는 Scene/runtime 참조, 적 목록, Progress, RunState/카운터, 방/논리 footprint, wave, camera, audio/cinematic, notice로 모았다. 기존 `environment` 필드도 삭제하지 않았다. 테스트 전용 새 저장 필드는 만들지 않았다. Singleton 접근, static/readonly, public 이름·초기식·타입을 유지했다.

`TryTravel`의 서비스 선택 분기는 문 처리 다음이라는 우선순위와 반환을 보존하기 위해 travel 함수 안에 남겼다. 컷신 시작/취소/카메라 적용도 실행 순서를 유지한 채 각 수명주기에서 호출한다. 관련 코드가 반드시 한 블럭에 전부 있어야 한다는 이유로 흐름을 재설계하지 않았다.

## 3. 추출한 private method

23개 모두 기존 코드의 문장 묶음 또는 조건식을 이동했다. 기존 메서드의 이름/시그니처는 유지했다.

| 새 private 함수 | 원래 위치 | 옮긴 내용 |
|---|---|---|
| BindSceneActors() | Awake | Hero/Crow/Cam 바인딩 및 PaperWorld.Configure |
| InitializeAudio() | Awake | AudioSource 획득, hitClip→shotClip 순서의 Tone 생성 |
| AttachSmokeTest() | Awake 마지막 | 기존 5개 인자 if/else-if 검사와 AddComponent |
| ResetRunProgress() | ResetRun 후반 | Kills/Supplies/RunTime/seed/Progress/WaveCountdown/Events/random 초기화 |
| DestroyTrackedEnemies() | ResetRun | 유효 Enemy Destroy 후 Enemies.Clear |
| ActivateRoomLayout() | ActivateRoom | map root 활성화→Capture→ApplyFootprints |
| PlaceActorsAtRoomEntry(int from) | ActivateRoom | 도착 문 탐색/entry 계산→Hero 위치/SafeEntry→Crow.ResetAt→ResetCamera |
| BeginRoomContent(int from) | ActivateRoom 마지막 | 완료방/전투방/서비스방 기존 분기 |
| HandleCinematicInput() | Update의 Cinematic 분기 | Space/Esc skip, else-if P pause |
| HandleSettingsInput() | Update의 SettingsOpen 분기 | Esc로 설정 닫기 |
| HandleRouteInput() | Update의 Route 분기 | Esc/M으로 지도 닫기 |
| HandleServiceInput() | Update의 SigilChoice/CardChoice 분기 | Esc로 서비스 닫기 |
| HandlePauseInput() | Update | Esc 조건, BeforePause/State 처리 |
| TickNotice() | Update | unscaled 시간 감소 또는 Notice 비우기 |
| TickWaveBreak() | Update의 WaveBreak 분기 | countdown 감소 및 0 이하 SpawnWave |
| RemoveDefeatedEnemies() | Update | null/Dead 항목 RemoveAll |
| BeginWaveBreak() | Update의 CompleteWave=false 분기 | State.WaveBreak→countdown2→안내 |
| CompleteCombatRoom() | Update의 CompleteWave=true 이후 | HP+18 clamp→State.Exit→완료 안내 |
| SpawnWaveEnemies(int count) | SpawnWave | 기존 for loop, 위치/종류/Instantiate/Init/Add/Ring |
| GetWaveEnemyCount() | SpawnWave의 count 초기식 | IsBossWave ? 1 : 기존 일반 적 수식 |
| GetWaveEnemyKind(int i) | SpawnWave의 kind 초기식 | IsBossWave ? 3 : 기존 일반 kind 순환식 |
| PlayBossEntrance() | SpawnWave 마지막 | 기존 보스 판정과 Enemies[0] 컷신 호출 |
| UpdateFollowCamera() | LateUpdate의 일반 카메라 분기 | aspect FOV→focus 계산→SmoothDamp |

GetWaveEnemyCount/GetWaveEnemyKind만 식을 반환하는 private query다. 다른 새 helper에는 조기 반환을 옮겨 넣지 않았다. **Update의 기존 return은 Update의 원래 분기 안에 그대로 남겼다.** 기존 GetComponent 호출도 캐시로 바꾸지 않았고, 비슷해 보이는 상태/거리 조건을 합치지 않았다.

## 4. 유지된 public API

| 종류 | 보존 항목 |
|---|---|
| public 필드 21개 | I, Obstacles, SolidObstacles, Enemies, Progress, Scene, Hero, Crow, Cam, State, BeforePause, BeforeRoute, Kills, Seed, Supplies, RunTime, RouteTravel, Events, MapNames, RoomNames, Notice |
| public 프로퍼티 22개 | Room, MapIndex, WaveIndex, WavesInMap, MapsCleared, IsBossWave, WaveCountdown, IsTown, Running, Pos, RoomCenter, LocalPos, Rooms, CurrentRoom, CurrentKind, Build, Cinematics, BurnLevel, FuryLevel, RavenLevel, NextRoom, CardRarity |
| public 메서드 20개 | AcquirePassive, ApplySeongheunStack, BeginDeath, ChooseEvent, ChooseRoom, ClaimCard, ClaimSigil, CloseRoute, CloseService, EnterTown, FinishExpedition, NearbyDoor, Nearest, OpenRoute, Sound, StartRun, Tell, TryDoor, TryTravel, Upgrade |
| internal API | ClearTransient() |
| Unity/직렬화 | 기존 public 필드 이름/타입/접근성·static/readonly·초기식, 프로퍼티 접근자 유지. 추가/변경 SerializeField 없음. Dungeon.cs.meta 및 Scene의 GUID/fileID 미변경. Boot의 RuntimeInitializeOnLoadMethod(AfterSceneLoad) 유지 |

`OpenRoute(bool travel=false)`, `Sound(bool gun=false)` 기본 인자, 나머지 인자명/순서/타입/반환형도 유지했다. RunState는 `Title,Town,Combat,Reward,Exit,Paused,Dead,Victory,Route,WaveBreak,SigilChoice,CardChoice,Cinematic`의 이름과 순서/암시적 값 모두 동일하다. 사용하지 않는 Reward 상태를 제거하거나 구현하지 않았다.

외부 호출 확인:

| 호출/조회 코드 | 유지한 주요 계약 |
|---|---|
| Player | Running, IsTown, Cam/Crow/Enemies/Build, BurnLevel/FuryLevel, RunTime, Sound, BeginDeath |
| Raven | Pos/Enemies, Running/IsTown, Build/성흔 level, Nearest, Sound |
| Enemy/Bullet | Hero, Pos, Enemies, State, Running, Kills, BurnLevel |
| GameUIView | State/진행/방/웨이브/보스/서비스/Notice 및 Hero/Crow/Build/Scene 조회 |
| GameHUD | State/BeforePause/Running/Build |
| RoomDoor | Dungeon.I.Progress의 SegmentComplete/CanLeave 조회 |
| CinematicDirector | Hero/Crow/Cam, Pos/Room/State, internal ClearTransient |
| UICommand | EnterTown, 지도/서비스/진행·승리 메서드 및 상태/NextRoom 조회 |
| RunBuild | 현재 RunBuild.cs의 직접 Dungeon 참조는 확인되지 않음. 반대 방향의 Dungeon.Build 및 획득/성흔 위임 API 유지 |

정적 검증:

- Roslyn 문법 파싱 오류 0. 프로젝트 컴파일/assembly emit/빌드는 하지 않았다.
- 전체 필드 29개와 프로퍼티 22개의 이름·타입·접근성·속성·초기식/구현을 비교해 일치 확인.
- 기존 메서드 37개(private/Unity 콜백 포함)의 시그니처와 속성 유지. 추가된 메서드는 private 23개뿐이다.
- 새 helper를 원래 호출 위치에 재귀적으로 펼친 뒤 기존 C# token 순서와 대조했다. 문장 helper는 조기 반환이 없고 인자명 대응을 확인했으며, 두 expression helper는 기존 지역 변수 초기식 위치에 펼쳤다. 기존 모든 메서드 본문/식이 공백·주석을 제외하고 일치했다.
- 이 대조에 State 변경, 조건/return, Enemies 정리, Progress 호출, HP 수치/시점, random 호출, Camera 계산, 오디오·Timeline 호출, 입력 순서가 포함된다.
- instance 초기화의 Enemies→Progress→Events 생성 순서, static Obstacles/SolidObstacles→MapNames→RoomNames 순서와 기존 초기식을 유지했다.
- 시작 상태의 코드·Scene·Prefab·관련 meta·기존 문서/루트 파일 86개를 보존 기준으로 기록했다. Dungeon.cs 외 나머지 85개 내용과 Git 변경 범위를 대조했다. 이전 Player 리팩터링 상태도 포함해 보존한다.

이는 정적 동등성/호출 계약 검사이며 실제 Unity 컴파일 성공이나 게임 실행 테스트 통과를 뜻하지 않는다. 검증 스크립트는 프로젝트 밖 작업 폴더에만 있다.

## 5. 유지된 gameplay 흐름

**Update 순서와 조기 반환**

```text
Cinematic 입력 → return
SettingsOpen 입력 → return
Route 입력 → return
SigilChoice/CardChoice 입력 → return
M && Running: OpenRoute → return
Esc pause 처리
Notice 시간 처리
!Running → return
E && TryTravel 성공 → return
IsTown → return
RunTime 증가
WaveBreak: countdown/SpawnWave → return
RemoveDefeatedEnemies
Combat && Enemies.Count == 0:
  ClearTransient
  CompleteWave가 false:
    State.WaveBreak → countdown2 → 안내 → return
  CompleteWave가 true:
    HP = Min(MaxHp, HP+18) → State.Exit → 완료 안내
```

문자열/입력은 기존 E/M/Esc 및 컷신 Space/Esc/P 그대로다. 루트판의 우클릭 상호작용, CameraFollow2_5D, 새 smoke argument는 추가하지 않았다. 기존에도 WaveBreak 분기는 enemy cleanup 전에 반환하며 그 순서를 유지했다. Cleanup은 Combat 상태 안으로 옮기지 않았다.

**Run / Town / Room**

- ResetRun: Cinematics.Cancel→Enemy Destroy/Clear→ClearTransient→Hero.ResetForRun→Crow.ResetForRun→Build.ResetRun→카운터/seed/Progress/웨이브/Events/Random.InitState.
- StartRun: ResetRun→BuildStage→ActivateRoom(-1).
- EnterTown: ResetRun→BuildTown→Town 상태→Hero 위치→Crow.ResetAt→Hero.SafeEntry→카메라→안내. Awake의 초기 마을 배치에는 원래 없던 SafeEntry를 추가하지 않았다.
- ActivateRoom: ClearTransient→방 root 선택→Capture/footprints→entry 계산→Hero 위치/SafeEntry→Crow→카메라→방 내용 분기.
- 완료방 재방문은 Exit 안내만, 전투방은 SpawnWave 후 최초 입장 DungeonEntrance, 서비스방은 Exit 및 기존 안내다.
- TryTravel은 마을 출구 검사→Running/IsTown 검사→가까운 문 우선→서비스 거리2.4 및 미완료 검사다. TryDoor의 거리2.1·소유/목록·stage exit/인접 진행 조건은 같다.

**Wave / Spawn / Progress**

- SpawnWave는 State.Combat→countdown0→적 수 결정→반복 생성→안내→보스 컷신이다.
- 일반 적 수 `3+Room+MapIndex/2+WaveIndex`, 일반 kind `(i+Room+MapIndex+WaveIndex)%3`, 보스 수1/kind3을 유지한다. 각 loop에서 SpawnPoint→kind 결정→Instantiate→Init→Enemies.Add→Ring 순서 유지.
- SpawnPoint의 180회 시도, authored marker/SpawnRadius, fallback 범위와 1.5 간격, 플레이어 거리4/적 거리1.4/장애물1 확장, random 호출 순서 그대로다.
- `Room`은 stage 번호, `MapIndex`는 방 번호다. RoomGraph/ExpeditionProgress 미수정: 6 Room/5 Stage, Room03(index2) Sigil, Room04(index3) DevilCards, 전투방 2~3wave 규칙 유지.
- 제단/카드 선택의 검증→획득→CompleteService→Exit 순서, 카드 ID 3개/등급 계산/성흔 0~2 제약 유지. 보스 reward 구현 없음.
- 다음 stage는 6방 완료·출구·RouteTravel·NextRoom 조건으로 진행하며, 마지막 stage에서 FinishExpedition의 기존 조건을 만족할 때 Victory다.
- 사망은 PlayerDeath Timeline 요청이 실패하면 Dead로 전환하는 기존 BeginDeath 그대로다.

**Camera / Audio / Timeline**

- ResetCamera의 X계수는 `.7`, Y계수 `.45`, clamp X[-6,6]/Y[-2.5,2.5], velocity zero를 그대로 사용한다.
- LateUpdate는 Hero가 없으면 반환, 컷신 중 ApplyCamera 후 반환, 그 외 FOV(aspect<1.5이면48/아니면38)→focus(X town .42/그 외 .7)→SmoothDamp(.24, unscaled dt) 순서다. Reset과 follow의 서로 다른 X계수를 합치지 않았다.
- PaperWorld.Configure/CameraPosition 및 논리 XY→표시 XZ 그대로다. 카메라 controller 위임이나 새 컴포넌트 없음.
- Awake의 hit Tone(130,.075)→shot Tone(65,.12), 44100Hz 계산·Random.Range·사운드 호출 유지. 음악/오디오 구현 변경 없음.
- Cinematics.Cancel, DungeonEntrance, BossEntrance, PlayerDeath, ApplyCamera 및 skip/pause 호출은 원래 조건과 시점 유지.
- smoke 부착 우선순위는 authoring→level→movement→motion→smoke의 기존 else-if다. 인자 없으면 부착하지 않는다.

## 6. Boss 관련 현재 격리 위치

`Boss-related existing code` region에 아래 기존 판단/호출을 모았다.

- `IsBossWave => Progress.FinalBossWave`.
- `GetWaveEnemyCount`: 보스1/일반 적 수 선택.
- `GetWaveEnemyKind`: 보스 EnemyPrefabs[3]/일반0~2 선택. 원래 SpawnPoint 호출 다음 위치에서 평가한다.
- `PlayBossEntrance`: IsBossWave일 때 Cinematics.Play(BossEntrance, Enemies[0]). SpawnWave 안내 이후 호출한다.

최종 보스 조건은 기존 ExpeditionProgress의 `Segment==4 && Map==5 && Wave==WaveCount-1`이다. 해당 파일이나 조건식을 옮기거나 바꾸지 않았다.

보스 사망·Clear 전용 코드는 만들지 않았다. 최종 보스도 `RemoveDefeatedEnemies→Combat/Count0→CompleteWave→CompleteCombatRoom` 공통 경로를 사용한다. HP+18과 Exit 시점도 같다. BossController, death event, reward spawn/선택/완료 gate는 추가하지 않았다.

## 7. 향후 파일/클래스 분리 후보

제안만 하며 이번에는 Dungeon 한 클래스 안에 남겼다.

| 현재 블럭 | 추후 후보 | 분리 전 유지해야 할 경계 |
|---|---|---|
| Wave Lifecycle / Room Clear | WaveController 후보 | cleanup→Count0→CompleteWave→대기 또는 HP18/Exit, 보스 공통 처리 |
| Enemy Spawn / Tracking | 적 생성/목록 관리 후보 | Prefab 배열 순서, random 호출·Init/Add/효과 순서, 즉시 완료 판단과 목록 수명 |
| Route / Stage / Entry | DungeonFlow 후보 | BeforeRoute/RouteTravel/6방 완료, room index와 stage index 구분 |
| Camera | CameraController 후보 | Reset/follow 계수 차이, 컷신 우선 반환, Apply/Restore와 갱신 소유권 |
| Audio | 오디오 처리 후보 | 생성 순서, Tone의 random 소비, Sound public 호출 계약 |
| Service Interaction | 서비스 처리 후보 | 거리/상태/중복 획득 검사와 CompleteService 시점, 기존 RunBuild 위임 |
| Test / Smoke | opt-in 검사 bootstrap 후보 | 기존 argument 우선순위, 일반 gameplay 미부착, Awake 초기화 후 실행 |

Boss region은 향후 검토 지점일 뿐 BossController 설계가 아니다. Singleton/SceneBindings/Reset과 각 블럭의 연결을 먼저 검토해야 하며, 독립 클래스 분리를 전제로 public API를 미리 바꾸지 않았다.

## 8. 사용자 Unity 테스트가 필요한 부분

이번 작업은 정적 검사만 수행했다. 현재 요청 완료를 위해 Unity 실행을 요구하지 않는다. 추후 사용자가 실행 검증을 선택할 때 다음 기존 동작을 짧게 확인하면 된다. 새 기능 테스트가 아니다.

| [사용자 Unity 테스트 필요] | 방법 / 정상 결과 | 실패 시 확인할 증상 |
|---|---|---|
| Wave→Room Clear | 전투방에서 한 웨이브와 마지막 웨이브를 완료. 중간은 2초 대기 후 다음 적, 마지막은 HP 상한 내 +18/Exit | 빈 웨이브 정지, 중복 생성·회복, 너무 이른 문 개방 |
| Entry / Service / Route | 마을 E 출발, 방 이동/재방문, 제단·카드 각1회, 6방 완료 후 다음 stage | 잘못된 spawn/방 root, 서비스 재획득, 미완료 출구 통과 또는 진행 잠김 |
| Boss / Death / Victory | 기존 최종 보스 등장·사망과 Player 사망/최종 출구 흐름 확인 | BossEntrance 누락, 공통 clear 미처리, 사망/승리 상태 누락 |
| Camera / Modal / Audio | 마을/전투방 추적, 입장·보스·사망 컷신, 지도/서비스/설정/pause, 기존 타격/총 사운드 확인 | 카메라 튐·컷신 뒤 추적 이상, 메뉴 입력 누출, 사운드 누락 |

Smoke는 이번에 실행하지 않았고 전체 자동 검사 반복 실행을 추가하지 않았다. 향후 필요하면 기존 opt-in 범위에서 사용자가 선택한다. 문법 파싱과 token 동등성 확인을 Unity 컴파일/화면 검증으로 표현하지 않는다.
