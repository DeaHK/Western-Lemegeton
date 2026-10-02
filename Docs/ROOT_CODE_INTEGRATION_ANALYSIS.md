# Root Code Integration Analysis

## 1. 분석 기준

- 분석일: 2026-10-03 (Asia/Seoul).
- 프로젝트: `E:\GitHub\Western-Lemegeton`.
- 재확인 HEAD: `c218280dbb80d83b03e17b05ad92f8bd50df86a3` — `Add files via upload`.
- parent: `d8eed843f7917ac88e6462cfb4ea426b7ab26b47`.
- 기준 문서: `Docs/WESTERN_LEMEGETON_PROJECT_ANALYSIS.md`, `Docs/LATEST_CHANGE_ANALYSIS.md`. 둘 다 수정하지 않았다.
- 시작 working tree: 기존 `ProjectSettings/ShaderGraphSettings.asset` 수정, 위 두 분석서 untracked. 이번 대상 소스·Scene·Prefab은 HEAD와 동일하다.
- 5쌍의 공백 차이를 제외한 1:1 diff, 필요한 Raven/Locomotion/Presentation/Camera/UI 호출 및 YAML만 확인했다. 전체 프로젝트 재분석·다른 branch 추정은 하지 않았다.
- 읽은 주변 코드: `Raven`, `HunterLocomotion`, `VisualCatalog`, `WorldDepth`, `PaperCard`, `PaperWorld`, `ActorAnimationDriver`, `CinematicDirector` 관련 부분, `RuntimeSmoke`/`MotionSmoke`의 Raven 계약, `EditableUIBaker`의 해당 UI 생성 부분. YAML은 Main/Hunter/Raven/GameUI 및 UI VisualCatalog·sorting layer 관련 부분이다.
- `[확인]`: 실제 코드/diff/YAML 근거. `[추론]`: 의도/원본 누락 추정. `[통합 후보]`: 향후 선택적으로 이식할 범위이며 구현 완료 또는 승인된 기능 변경을 뜻하지 않는다. `[사용자 Unity 테스트 필요]`: 통합 후 사람이 볼 사항.

현재 runtime 판단은 **Assets판 우선**이다. Unity Editor, Play Mode, 빌드, Smoke 실행, 소스 복사/교체, Scene/Prefab 변경, commit/push를 하지 않았다. 이후 절의 수정·배치·테스트는 모두 미래 작업 분리안이다.

## 2. 루트 신규 5파일의 정체

| 이번 Git 추가 경로 | 실제 Unity 소스 |
|---|---|
| `ActorPresentation.cs` | `Assets/Scripts/Authoring/ActorPresentation.cs` |
| `Dungeon.cs` | `Assets/Scripts/Dungeon.cs` |
| `GameUIView.cs` | `Assets/Scripts/Authoring/GameUIView.cs` |
| `MovementSmoke.cs` | `Assets/Scripts/MovementSmoke.cs` |
| `Player.cs` | `Assets/Scripts/Player.cs` |

[확인] commit은 루트 파일 5개/676행 추가다. `Assets`, `Packages`, `ProjectSettings`의 parent→HEAD diff는 없다. 루트 파일은 Assets 밖에 있고 같은 namespace/class 이름을 가진 사본이며, 현재 Prefab의 MonoScript 참조는 기존 Assets `.meta` GUID다.

[추론] 기존 클래스와 함수 대부분이 같고 서로 맞물리는 입력·UI 변경을 담아 교체 후보로 보인다. 그러나 작성자의 원본 branch/전체 패치·완성 여부는 알 수 없다. Raven·프레임 애니메이션·카메라 의존 소스가 별도로 존재했을 가능성은 있으나 현재 Git에 있다는 증거는 없다.

**결론: 5개 일괄 교체 불가.** 현재 없는 타입/멤버로 컴파일 계약이 충족되지 않고, 이를 해결해도 걷기·Command 입력·HUD 피드백 손실이 있다. 일부 독립 diff만 통합 후보로 삼는다. 파일을 Assets에 별도 추가하여 기존 파일과 공존시키는 방식 역시 같은 namespace/type 중복 문제를 만든다.

## 3. 파일별 Assets판 vs 루트판 비교

각 표의 “루트판”은 실행 중인 변경이 아니라 소스에 적힌 계약이다. 표에 없는 기존 함수/필드가 전부 새 기능인 것은 아니다.

### ActorPresentation.cs

| 항목 | Assets판에만 있거나 기존 방식 | 루트판의 추가·변경 |
|---|---|---|
| Player 표현 | `WalkHeld`로 Walk/Run, Speed/2.8 또는 /5.6 | 이동 시 Run, Speed/5.6. Walk 요청 경로 삭제; `Animations`의 Walk 항목은 남음 |
| Raven 상태 | `CurrentState.ToString()` | `AnimationName`(없으면 Idle), `IsVisible`, `FacingLeft`, `PresentationSequence` 조회 |
| Sprite | Raven.cs가 정한 Sprite/flip/rotation 사용 | 기존 SpriteFrames 유지. Raven 전용 `ApplyRavenFrames`로 clip 프레임·scale·회전·offset, 그림자 footprint/ground offset 적용 |
| CustomVisual | 고정 Pitch 회전, 기존 WorldDepth.Order×10+12 | `VisualScale`, collection offset, WorldDepth.Modern/SpriteBillboard/RenderOrder 경로 |
| 요청 상태 | motion+Player sequence 비교; custom 사용 중에만 캐시 갱신 | motion+Player/Raven sequence+UseCustomVisual 변경 비교; custom 미사용 중에도 캐시 갱신 |
| 이벤트 | 매칭 MotionBinding 기반 Animator/driver/event | null 바인딩 방어, Raven 이름 fallback, clip Loop→binding Loop 덮어쓰기. 빈 animation 문자열이면 반환 |
| 함수 | Awake, LateUpdate | 두 함수 변경, `ApplyRavenFrames(SpriteAnimationCollection,string,long,float)` 신규. 삭제 함수 없음 |
| 공개/직렬화 | 기존 참조/Animations/VisualOffset 유지 | public `SpriteAnimationCatalog`, `VisualScale=1`, 읽기 전용 `FramePlayer`; `[SerializeField] spriteAnimator=new SpriteAnimator()` |
| 내부 필드 | customScale/actor refs/last/lastSequence 유지 | baseRenderOffset, baseSpriteRotation, lastCustom, shadowCard, shadowSprite |

[확인] 입력/UI key/검사 코드는 추가되지 않았다. Camera 참조는 custom 표시 회전·정렬용이며 카메라를 움직이지 않는다. Animator와 AnimationDriver는 여전히 선택 참조다. CustomVisual 사용 조건 안에서만 `AnimationRequested`를 호출하는 경계는 유지한다. 기존 필드나 함수 삭제는 없으며 변경된 Player 데이터 조회는 WalkHeld 제거뿐이다.

### Dungeon.cs

| 항목 | Assets판 | 루트판 |
|---|---|---|
| 입력/안내 | Update에서 E→TryTravel; E 안내 | 우클릭(index 1)→TryTravel; EnterTown/ActivateRoom/Update 안내 우클릭 |
| 검사 부착 | authoring→level→movement→motion→smoke | 맨 앞에 presentation-test/presentation-before→PresentationSmoke; 마지막 smoke 분기에 crow-test 별칭 |
| ResetCamera | PaperWorld.CameraPosition, cameraVelocity=0 | enabled CameraFollow2_5D가 있으면 Step(Pos,RoomCenter,IsTown,true) 후 반환, 없으면 기존 |
| LateUpdate | 컷신 ApplyCamera 또는 기존 FOV/SmoothDamp | 컷신 ApplyCamera 후 선택적 ClampCurrentView; 평상시 enabled follow.Step 후 반환; 없으면 기존 |

[확인] 새/삭제 함수·필드·public API·SerializeField·UI key·RunState enum 변경 없음. `Awake`, `EnterTown`, `ActivateRoom`, `Update`, `ResetCamera`, `LateUpdate` 내부만 다르다. Hero 초기화·SafeEntry·Crow.ResetAt·Spawn·진행 조건·Boss/Reward 계산은 같다. 새 animation 데이터 연결은 없고 카메라 위임이 컷신 경로와 만난다.

### GameUIView.cs

| 항목 | Assets판 | 루트판 |
|---|---|---|
| Skill4 | Crow.Cooldown, Active 기반 공격시간/명령 | cooldown=0; IsSummoned/SummonRemaining으로 회수/소환 60초 |
| CrowPrompt | LinkWindow 또는 ComboFlash; R·남은 시간/콤보 Text 갱신 | LinkWindow+Running+Hero.Hp>0만; 머리 위 투영/pop/tilt/화면 여백. ComboFlash 단독 표시와 Text 갱신 삭제 |
| 키 안내 | Prefab 저장 문구 | Awake에서 SkillKey0=Q, Controls=새 ControlHelp. SkillKey2..5 및 CrowPromptText는 갱신하지 않음 |
| 이동 안내 | Prompt의 E | 우클릭 |
| Butterfly 설명 | 명령 공격시간 연장+연계 강화 | 일반 공격+연계 강화라는 문구. 실제 효과 계산 변경 없음 |
| 함수 | Awake/LateUpdate/Prompt | 세 함수 변경. CrowLinkPrompt/HideCrowPrompt/KeepCrowPromptOnScreen 신규. 삭제 함수 없음 |
| API/직렬화 | 기존 References/Art/panels 등 유지 | public CrowPromptHeight=1.45, CrowPromptOffset=(18,-6), CrowPromptPop curve, const ControlHelp |
| 내부 상태 | 기존 refs/slots 등 유지 | canvas, crowPromptAge, crowPromptOffered, crowPromptTarget, crowPromptCorners[4] |

[확인] 새 UIReference key는 없다. `CrowPromptText` 사용 제거는 key/serialized reference 삭제가 아니다. Player에 새 UI 상태 API를 요구하지 않으며 위치·HP는 기존 값이다. 카메라는 투영에만 사용한다. 검사 코드 없음.

### MovementSmoke.cs

| 항목 | Assets판 | 루트판 |
|---|---|---|
| Reset | WalkHeld=false 명시 | 이 설정 삭제 |
| Start | Shift 절반속도, 걷기 gait 검사 | 해당 Check 두 개와 walk 계산 삭제 |
| 유지 | 8방향/flip/동일 대각속도, six-frame gait, settle/idle, pause/벽/액션/태그/조준/입장/접지, screenshots | 그대로 유지 |
| 잔존 | run/runCycle 및 walk 포함 성공 요약 | walk 검사가 사라져도 run/runCycle 계산과 성공 요약의 walk 문구가 남음 |

[확인] 추가 기능·새/삭제 함수·필드·API·SerializeField·UI key·Camera·Raven gameplay 변경 없음. 일반 플레이 코드가 아닌 `--movement-test` opt-in 검사다. Player.Update를 끄고 AdvanceCombat을 호출하므로 새 키/소환/UI를 검증하지 않는다. 검사 실행 자체는 이번 작업에서 하지 않았다.

### Player.cs

| 항목 | Assets판 | 루트판 |
|---|---|---|
| 삭제 API | `public bool WalkHeld {get;set;}` | 제거. ResetForRun 초기화/Update 읽기도 제거 |
| 이동 | 일반 5.6, Shift 2.8, 난사 3.1; bonus 적용 | 걷기 2.8 분기만 제거. 일반 5.6/난사 3.1 및 bonus 유지 |
| Raven 입력 | 1→Command, R→TryLink, ActionLocked 이후 | LeftShift→ToggleSummon, F→TryLink, ActionLocked 이전 |
| 스킬 입력 | 2/3 | E/R |
| TryAttack | 단검 comboTime=1.2, 총=4 | 단검=2, 총=4 |
| 표현/상태 | LastMove/Facing/MotionDirection, Locomotion/Animate, PresentationSequence | 같은 계산·증가 지점. Walker 모드만 제거 |

[확인] 변경 함수는 `ResetForRun`, `Update`, `AdvanceActions`, `TryAttack`. 새/삭제 함수·추가 필드·SerializeField·UI key·Camera 제어·검사 코드 없음. `WalkHeld`는 필드명이 아니라 자동 프로퍼티다. 좌클릭·Q·Space·공격 피해/판정·스킬 종류·대시·XY Rect 이동은 유지한다. 루트 Player.Hit의 Mark/OfferLink/ComboFollowup 호출도 그대로다.

## 4. 그대로 덮어쓸 경우 손실/위험

우선 컴파일 결손 때문에 새 동작을 실행할 수 없다. 아래 동작 손실은 그 결손만 해결하고 나머지를 그대로 이식했을 때도 남는 차이다.

| 파일 | A. 기존 기능 손실 | B. 컴파일 계약 위험 | C. Scene/Prefab 참조 위험 |
|---|---|---|---|
| ActorPresentation | Walk 요청·걷기 속도 기준; Raven.CurrentState 직접 매핑을 잃음 | SpriteAnimator/collection, Raven 새 멤버, VisualCatalog/PaperCard/WorldDepth 새 API, SpriteBillboard 부재 | 기존 필드 이름/타입 유지. `.meta` 보존 시 기존 참조가 자동 끊긴다는 근거 없음. 새 catalog/clip 연결은 별도 필요 |
| Dungeon | E 상호작용; enabled follow 사용 시 기존 카메라 FOV/SmoothDamp 경로 우회 | CameraFollow2_5D/PresentationSmoke 부재 | Scene 바인딩 필드 유지. Main Camera에 follow 없음. Scene 참조 파손보다 필요한 선택 컴포넌트 미배치 문제 |
| GameUIView | Command cooldown/Active 표시, ComboFlash 배너, 말풍선 남은 시간 및 R 텍스트 동기화 | Raven.IsSummoned/SummonRemaining 부재 | 기존 GameUIView/패널/References 유지. 새 조정 필드 및 말풍선 레이아웃/아트 미배치 |
| MovementSmoke | 걷기 검사 두 개 및 WalkHeld 초기화 | 단독 교체 시 신규 결손 없음 | 저장 Scene 부착 필요 없음. Assets Player와 조합하면 이전 WalkHeld 상태를 Reset에서 해제하지 않게 됨 |
| Player | 걷기 조작/속도/API, 1의 Command 입력, 기존 2/3/R 조작. Command 구현 자체는 Raven에 남음 | ToggleSummon 부재. Player만 교체하면 기존 ActorPresentation/MovementSmoke의 WalkHeld 참조도 깨짐 | art 등 기존 필드 유지. WalkHeld는 직렬화 참조가 아님. 기존 `.meta` 보존 필요 |

| 파일 | D. UI reference/표시 | E. 기존 2.5D 구조 | F. Dungeon 진행 |
|---|---|---|---|
| ActorPresentation | UI 참조 변경 없음 | 논리 XY→PaperWorld.Point 유지. custom 정렬은 modern=false여도 RenderOrder 사용하므로 기존 Order와 동등한지 알 수 없음; 실제 SpriteFrames/Visible과 정렬 불일치 위험 | 직접 변경 없음 |
| Dungeon | 안내는 우클릭으로 바뀌나 Assets Player와 섞으면 E의 의미 불일치 | Step에 Pos/RoomCenter라는 논리 Vector2 전달. 새 controller가 이를 어떤 좌표로 해석할지 미확인 | TryTravel/문/clear 조건은 유지. 입력 혼용 시 E가 스킬과 여행을 함께 시도할 수 있음; 우클릭에 UI-overlap 필터 없음 |
| GameUIView | key/Target 단절은 없음. 옛 2/3/1/R과 R 배너가 남아 의미가 틀림 | 투영은 PaperWorld.Point 사용. 좌표계 변경은 없지만 새 화면 배치/pivot 확인 필요 | state 변경 호출 없음. 잘못된 안내로 진행 조작 혼동 가능 |
| MovementSmoke | UI 영향 없음 | 이동 엔진 동일; 걷기 회귀 탐지 범위 감소 | opt-in 종료형 검사이며 일반 진행 변경 없음 |
| Player | 화면의 기존 키·Command 안내와 실제 입력 불일치 | Rigidbody/Collider/NavMesh 도입 없음. 걷기 제거는 좌표계 변경과 다름 | Dungeon.Running/IsTown 계약 유지. 입력 파일들을 따로 적용하면 서비스·문 앞에서 충돌 가능 |

**[확인] 가장 큰 기존 gameplay 손실은 걷기와 Raven.Command를 호출하는 일반 입력 경로다.** 후자는 현 Raven의 일반 공격을 켜는 `Active` 타이머 시작점이다. ToggleSummon을 단순 가시성 토글로만 구현하면 명령 공격을 대체하지 못한다. Combo/Link는 별도 경로이므로 이를 일반 공격과 함께 제거하면 안 된다.

소스 내용 교체와 `.meta` 삭제/재생성은 구분한다. 현재 GUID를 보존하는 코드 패치라면 같은 class/serialized field 참조가 통째로 끊긴다고 단정할 수 없다. 반대로 새 GUID 파일로 교체하거나 동명 class를 추가하는 방식은 통합 후보에서 제외한다.

## 5. 누락된 타입/API/Asset/Prefab/UI 연결

아래 “호출 계약”은 루트의 사용식으로 확인한 요구사항이다. 정의가 없는 타입의 정확한 원본 선언·직렬화 형식·내부 알고리즘은 복원할 수 없다.

| 루트가 요구하는 항목 | 분류 | 현재 근거 / 필요한 확인 |
|---|---|---|
| Raven.ToggleSummon() | [현재 없음] | 인자 없이 호출. 기존 Command()와 기능이 다르며 이름만 바꿔 연결할 수 없음 |
| Raven.IsSummoned / SummonRemaining | [현재 없음] | bool 조건/남은 시간 수치 요구. Active는 공격 명령 시간이라 대용 불가 |
| Raven.IsVisible | [현재 없음] | renderer 표시 조건. GameObject.activeSelf나 Active>0과 동등하다는 근거 없음 |
| Raven.AnimationName | [이름/시그니처 다름] | 현재 CurrentState:RavenState와 ToString() 경로. 새 string property는 없음. 이름 매핑·animation 재시작 계약 필요 |
| Raven.FacingLeft | [현재 없음] | 기존 private art.flipX를 Raven.Update/TickCombo가 직접 씀. 공개 bool API 없음 |
| Raven.PresentationSequence | [현재 없음] | 같은 이름은 Player에만 long으로 있음. Raven action 재시작 카운터 계약 별도 필요 |
| SpriteAnimator 및 new SpriteAnimator() | [현재 없음] | Play(clip,sequence), Tick(deltaTime,playback), CurrentFrame의 Sprite 사용 요구. default 생성·Unity 직렬화 가능 여부 모두 정의 필요 |
| SpriteAnimationCollection | [현재 없음] | Get(state), ShadowGroundOffset, Footprint.x/y, Scale, VisualOffset 요구 |
| collection.Get(state)의 clip API | [현재 없음] | null 검사, IsValid, Loop 및 SpriteAnimator.Play 인자 계약 요구. clip의 원본 타입명은 이 5개에 없음 |
| VisualCatalog.FrameAnimations | [현재 없음] | VisualCatalog에는 Icons/NamedSprite/Get(string)만 있음. Get(icon)의 Sprite 반환과 clip Get은 다른 API |
| PaperCard.ShadowGroundOffset | [현재 없음] | 기존 Flat/RenderOffset은 있으나 같은 의미라는 근거 없음. 그림자 높이/접지 단위를 추정해 대입하면 안 됨 |
| WorldDepth.Modern(camera) | [현재 없음] | 반환을 bool로 사용. modern 판정 기준 소스 없음 |
| WorldDepth.Objects | [현재 없음] | SortingGroup.sortingLayerName에 대입. 문자열 값 자체는 알 수 없음 |
| WorldDepth.RenderOrder(feetY,roomCenterY) | [이름/시그니처 다름] | 기존 Order(float feetY) 한 인자 함수만 있음. 기존 식 Order(y-center-.35)*10+12와 동등성 미확인 |
| SpriteBillboard.Rotation(camera,false,zAngle) | [현재 없음] | Quaternion 회전 결과 필요. 기존 Pitch 회전의 단순 별칭인지 미확인 |
| CameraFollow2_5D | [현재 없음] | Component/GetComponent 및 enabled 사용, Step(Pos,RoomCenter,IsTown[,true]), ClampCurrentView() 요구. Step가 overload인지 optional 인자인지도 정의 없음 |
| PresentationSmoke | [현재 없음] | AddComponent 대상 타입. argument가 없어도 컴파일 시 정의 필요 |
| Raven.Command/Mark/OfferLink/ComboFollowup/TryLink/ResetAt/ResetForRun | [현재 존재] | 소환으로 대체되지 않은 현재 전투·초기화 API. 기존 호출자 보존 대상 |
| Raven.LinkWindow/LinkCooldown/LinkTarget/ComboFlash/CurrentState | [현재 존재] | 기존 연계 제안/콤보/상태 자료. 새 Frame/소환 상태와 혼동 금지 |
| Player.PresentationSequence 및 Locomotion/Presentation | [현재 존재] | 새 Player API가 아님. WalkHeld만 루트에서 제거 |
| RuntimeSmoke 및 MovementSmoke 등 기존 검사 | [현재 존재] | --crow-test는 RuntimeSmoke 부착 별칭일 뿐 별도 Raven 전용 검사 구현은 없음 |
| ActorPresentation.SpriteAnimationCatalog | [Prefab 연결 필요] | public VisualCatalog 참조. Hunter/Raven 저장값 없음. 루트 Tooltip은 이 catalog가 frame collection을 담는다고 기대함 |
| ActorPresentation.spriteAnimator / VisualScale | [Prefab 연결 필요] | 코드 초기값은 있으나 저장 항목 없음. spriteAnimator는 object reference라고 단정 불가; 타입 정의 후 직렬화 방식 확인 |
| GameUIView.CrowPromptHeight/Offset/Pop | [UI 연결 필요] | 코드 초기값만 있음. GameUI 저장 항목 없음. 자동으로 말풍선 아트가 생기는 필드는 아님 |
| CrowPrompt/Controls/SkillKey0..5/CrowPromptText 및 Skill4/5 표시 keys | [현재 존재] [UI 연결 필요] | References에 존재. 새 key 누락은 없으나 아트·문구·pivot 내용이 새 계약과 불일치 |
| frame collection asset/상태별 clips/frames | [현재 없음] [Prefab 연결 필요] | 현재 VisualCatalog는 아이콘 집합. 루트 Tooltip의 SpriteAnimationCollection.asset은 실제 asset 경로나 로딩 코드가 아님 |
| RavenBubble/RavenPortrait 아이콘 | [현재 존재] [UI 연결 필요] | 기존 VisualCatalog의 Sprite refs 존재. 루트 코드가 RavenBubble을 CrowPrompt에 대입하지 않음. 이미지 내부 F 표시 여부/최종 디자인은 이번 정적 텍스트 분석으로 확인하지 않음 |
| enabled CameraFollow2_5D on Cam | [Prefab 연결 필요] | 실제 배치 대상은 Prefab이 아니라 Main의 Main Camera(Scene). 현재 Transform/Camera/AudioListener만 있음 |

[확인] 새 enum/struct 선언, 새 UIReference key, 새 Resources.Load 경로, 새 입력 enum 타입은 없다. E/F/R/LeftShift 및 GetMouseButtonDown(1)은 기존 Unity Input API다. RunState 변경도 없다. 새로 요구하는 animation 문자열 `Move`는 현 RavenState에 없고 기존 Follow/Return→fly와 자동으로 같아지지 않는다.

[확인] TagManager의 sorting layer는 Default만 확인된다. WorldDepth.Objects 값이 없으므로 “Objects라는 layer를 추가하면 해결”이라고 단정할 수 없다. 프레임 소스 및 렌더 정책 확인이 먼저다.

[추론] **[추가 파일이 Git에 누락됐을 가능성]** Raven 수정본, 프레임 재생기/collection/clip, VisualCatalog·PaperCard·WorldDepth 수정본, SpriteBillboard, CameraFollow2_5D, PresentationSmoke, 관련 asset/UI 저장본이 함께 작성됐을 가능성이 있다. 이것은 누락된 계약 목록이지 외부 파일의 실재 확인이 아니다. 원본을 확보하지 못하면 현재 코드와 호환되도록 선택한 기능만 별도 구현할 범위를 정해야 한다. 이름만 맞춘 빈 stub으로 통합 완료 처리하지 않는다.

## 6. 입력 변경

| 기능 | 현재 키 | 루트 신규 키 | 충돌/손실 |
|---|---|---|---|
| 이동 | WASD | WASD | 없음. normalized 및 MoveSpeedBonus 유지 |
| 걷기 | LeftShift 또는 RightShift hold | 없음 | 양쪽 Shift 걷기 기능 제거 |
| 소환/회수 | 없음 | LeftShift down | 걷기를 유지하며 그대로 더하면 한 번의 입력이 감속+소환을 함께 수행. RightShift 소환 없음 |
| 까마귀 명령 공격 | 1 | 입력 없음 | Command API가 남아도 일반 입력 경로 소실 |
| 연계 기술 | R | F | 기존 R은 루트에서 스킬2 슬롯으로 바뀜. 표기의 “R Link”는 같은 TryLink 기능을 뜻함 |
| 무기별 스킬 slot0 | 2 | E | Dungeon을 함께 바꾸지 않으면 E 여행/서비스와 충돌. 실행순서/RunState에 따라 한쪽이 먼저 처리될 수 있음 |
| 무기별 스킬 slot1 | 3 | R | 기존 연계 입력과 의미 충돌. old R alias를 무조건 유지하면 스킬/연계 동시 요청 |
| 출발/문/서비스/출구 | E | 우클릭 | TryTravel의 조건은 유지. 우클릭에는 UI 포인터 가드가 없음 |
| 기본 공격 | 좌클릭 | 좌클릭 | 기존 buffer/EventSystem 가드 유지 |
| 태그 / 회피 | Q / Space | 동일 | 변경 없음 |
| 지도 / pause / 컷신 | M / Esc / Space·Esc skip·P pause | 동일 | 이번 키 변경 범위 밖 |

[확인] **걷기 제거는 animation만의 변경이 아니다.** HunterLocomotion.State는 원래 `Idle, Moving, Settling` 3개이고 Walk/Run enum은 없다. 기존 WalkHeld가 2.8/5.6 속도를 선택하고 같은 거리 기반 gait를 사용하며 ActorPresentation이 Walk/Run 문자열을 선택한다. 루트판은 WalkHeld API, 2.8 속도 분기, Walk 요청을 모두 제거한다. 그러나 HunterLocomotion/atlas·Moving/Settling·걷기에 쓰던 frame 자체와 Animations의 Walk 항목은 삭제하지 않는다.

[확인] root의 F/LeftShift는 ActionLocked 검사 전이다. 현재 R/1은 검사 후라 내려찍기(skill2) 중 입력이 무시된다. 단순 키 이름 교체뿐 아니라 허용 타이밍도 달라진다. TryLink에는 Player.ActionLocked 조건이 없으므로 현 Raven으로도 이 위치 변경은 의미가 있다.

[통합 후보] E/R/F/우클릭과 모든 안내는 하나의 단위로 맞추되, 걷기·Command 제거는 포함하지 않는다. Shift를 소환에 쓰려면 걷기 유지/새 binding 정책을 먼저 정해야 한다. 이 문서에서 임의의 대체 키를 확정하지 않는다. ActionLocked 중 Raven 호출 허용도 독립 동작 변경으로 분리하여 기존 제약을 기본 보존한다.

## 7. Raven 변경

| 루트가 기대하는 기능 | 현재 Raven.cs 지원 여부 | 현재 API/실제 동작 | 필요한 추가 작업 |
|---|---|---|---|
| Summon | 없음 | Main에 저장된 Raven이 계속 동행 | ToggleSummon/IsSummoned/SummonRemaining 의미와 최초 상태·시간·마을/컷신 정책 확인. UI의 60초만으로 구현을 추정하지 않음 |
| Recall | 소환 해제는 없음 | EndCombo/returning/거리초과의 Return은 플레이어 근처 복귀 | 복귀 이동과 “회수되어 보이지 않음”을 구별. 기존 Return 제거 금지 |
| Active/Inactive | 소환 상태 없음 | Active는 Command 공격시간. Command=5+RavenLevel×.75초, Cooldown=10 | Active를 bool 소환이나 60초 타이머로 재해석하지 않음. Command 일반 공격과 소환 기능의 관계 확정 필요 |
| visible | 없음 | renderer/root 상시 연결, art는 private | IsVisible/FacingLeft 제공 및 renderer 가시성 연결. root SetActive(false)는 Update·타이머·FSM도 멈추므로 루트 주석의 전제와 다름 |
| Frame/Sprite Animation | clip 재생기 없음 | CombatArt.Pose(Raven/RavenLink), flipX·rotation 직접 갱신 | AnimationName/sequence 및 데이터 clip 매핑. 기존 Update와 LateUpdate가 같은 Sprite를 쓰는 소유권/우선순위 확인 |
| Combo | 있음 | ComboFollowup→queue→windup .18→dash .24→도착 피해→최대 2대상, 교체 타깃 반경7 | 큐/피해/target 교체/중복 타격 방지 유지. 회수 중 진행/보류/완료 정책은 루트 5개로 알 수 없음 |
| R Link (신규 키 F) | 있음 | OfferLink: window6; TryLink: .6초 이동 후 radius2.5 피해, CD8 | key만 재배치 가능. ComboBusy/queue/Linking/CD/거리/town/running 조건 유지 |
| 말풍선 조건 | 기초 데이터 있음 | LinkWindow, LinkTarget, ComboFlash | root 표시 조건은 제안창의 존재일 뿐 TryLink 성공 가능성 전체와 같지 않음. ComboBusy 중 F가 거절될 수 있음 |
| Player와 거리 | 있음 | 일반 탐색·combo·link 반경7, follow에서 거리9 초과 순간 복귀, 근접공격1.3 | 새 소환 의미로 거리 제한/교체 타깃을 지우지 않음. 루트에 새 거리 알고리즘 없음 |
| Idle/Follow | 있음 | CurrentState Idle/Follow/Return, 목표=Player+(-.95,1.15+sin×.15), 속도10 | 프레임 이름 Move와 mapping 필요. 새로운 배회/랜덤 waypoint는 루트에도 없음 |

[확인] `Player.Hit`은 `Crow.Mark` 후 피해를 주고, 스킬 또는 basic stage4이면 `OfferLink`, 적별 유효 4단 combo이면 `ComboFollowup`을 호출한다. 단검 입력 간격 2초 변경은 이 적중 콤보 시스템/4초 제한을 바꾸지 않는다.

[확인] 현재 Raven.Update는 Running이 아니면 반환하고, 타이머 후 Combo→대기 queue→Link→일반 follow/attack 순으로 처리한다. 소환 추가가 이 우선순위를 어떻게 보존할지는 별도 확인 대상이다. 프레임 표시를 늘리는 이유로 combat damage를 animation event로 옮길 근거도 없다.

[확인] 기존 `RuntimeSmoke.BuildAndComboChecks`는 timed dash, pause freeze, 서로 다른 두 대상 피해, 죽은 타깃 교체, 타깃 없는 경우 PNG만 표시되는 조건을 검사한다. `MotionSmoke`도 실제 4타 후 RavenState.ComboWindup을 확인한다. 향후 상태명을 바꾸거나 CurrentState를 없애면 기존 검사 계약에도 영향이 있다. 해당 테스트를 이번에 실행하지 않았다.

## 8. Presentation/Animation 변경

**실제 표시 경로 유지:** `Raven.Update/Player.Animate → SpriteFrames(Source) → ActorPresentation.LateUpdate(order250) → PaperCard.LateUpdate(order300) → Visible Sprite(XZ)`. root도 기존 SpriteFrames를 필요로 하며 없으면 시작부터 반환한다. GameUIView는 order500에서 최종 카메라/actor 위치를 UI에 투영한다.

**프레임 추가 범위:** root의 collection 재생은 `if(raven)` 내부다. Player는 계속 HunterLocomotionAtlas/HunterAtlas를 쓰며 frame API를 새로 요구하지 않는다. Raven은 매 frame Play(clip,sequence)를 호출하므로 새 SpriteAnimator가 동일 clip/sequence 호출마다 재시작하는 구현이면 animation이 진행하지 못한다. 이는 사용식에서 확인되는 요구사항이며 실제 재생기 구현은 없다.

**Animator/CustomVisual 요구:** Sprite frame 경로는 UseCustomVisual=false에서 작동하며 Animator가 필요 없다. CustomVisual 경로는 기존처럼 opt-in이고 Animator 또는 ActorAnimationDriver 또는 event listener를 선택 연결한다. root가 현재 inactive slot 활성화를 자동 보장하지 않는다. 프레임 clip이 invalid/null이면 교체 없이 반환하므로 기존 Raven.Pose가 남는 경로도 있다.

**AnimationRequested:** UnityEvent<string,bool,float> 형태 유지. 상태/sequence/custom 토글에 반응하며 state→binding animation 이름으로 요청한다. custom=false이면 발행하지 않는다. Raven은 binding이 없으면 state 이름을 쓰고 Idle/Move loop, clip Loop, binding Loop 순으로 결정한다. visible=false에서도 custom 사용 조건과 요청 변화가 있으면 speed0 요청은 가능하다. “숨으면 event가 반드시 안 온다”는 계약이 아니다.

**Prefab 필드별 미래 연결:**

| 필드/참조 | 현재 | 이식 시 확인 |
|---|---|---|
| SpriteFrames / SpriteCard / CustomVisual | Hunter/Raven 기존 연결 | 기존 Source→Visible 구조와 GUID 유지 |
| UseCustomVisual | 0, custom child inactive | Sprite 프레임만 적용할 때 활성화 불필요 |
| Animator / AnimationDriver / AnimationRequested | Animator=0, Hunter driver=0, Raven driver 저장 항목 없음, persistent calls 비어 있음 | custom 기능을 선택할 때만 controller/driver/listener 배치 |
| SpriteAnimationCatalog | 없음 | Raven용 VisualCatalog 참조와 FrameAnimations/clip 데이터 필요 |
| spriteAnimator | 없음 | missing type의 serializable 데이터 계약 확인. MonoBehaviour 추가 슬롯으로 단정하지 않음 |
| VisualScale / VisualOffset | 새 scale 저장 없음 / offset은 기존 | VisualScale은 custom 크기, collection.Scale은 Sprite frame 크기. 서로 대체 불가 |
| Animations | 기존 Idle/Follow/Return/ComboWindup/ComboDash/LinkAttack 등 | AnimationName 및 clip Get(state) 이름과 맞는지 확인. Move에 자동 Follow mapping 없음 |
| 자식 PaperCard.Source 이름 Shadow | 실제 존재 | 첫 이름 일치 항목 선택. footprint/ground offset 적용 시 renderer 가시성과 접지 함께 확인 |

[확인] 아직 구현되지 않은 modern billboard/정렬은 Raven Sprite 재생의 필수 gameplay 기능이 아니다. custom 표시 개선과 섞지 않고 분리 후보로 남긴다. 현재 PaperCard/WorldDepth를 전체 교체해야 한다는 결론도 내리지 않는다.

## 9. Camera 변경

[확인] **Player로 카메라 책임을 넘기지 않는다.** Dungeon이 Cam.GetComponent<CameraFollow2_5D>()로 별도 컴포넌트를 찾아 호출하는 경로다. Scene의 Cam binding과 Hero 위치 조회 책임은 Dungeon에 남는다.

| 상황 | 루트 Dungeon 호출 | 남아 있는 기존 동작 |
|---|---|---|
| Awake/방 입장의 ResetCamera | enabled follow이면 Step(Pos,RoomCenter,IsTown,true) 후 return | 없거나 disabled이면 기존 즉시 배치와 velocity reset |
| 일반 LateUpdate | enabled follow이면 Step(Pos,RoomCenter,IsTown) 후 return | 없거나 disabled이면 기존 aspect FOV+SmoothDamp |
| 컷신 | Cinematics.ApplyCamera(), 이어서 선택적 ClampCurrentView(), return | CinematicDirector가 위치/FOV 적용 및 Restore 담당 |

[확인] 같은 Dungeon.LateUpdate 안에서 follow.Step와 기존 SmoothDamp가 동시에 실행되는 구조는 아니다. 다만 누락된 컴포넌트가 자기 Update/LateUpdate에서 또 카메라를 쓰는지는 알 수 없다. 그런 구현을 추가하면 이중 갱신/컷신 덮어쓰기 위험이 생긴다. “이미 충돌한다”거나 “충돌이 없다” 둘 다 확정할 수 없다.

[확인] Dungeon.Awake의 PaperWorld.Configure는 여전히 먼저 perspective/FOV38/pitch47/culling/viewport를 설정한다. Step 인자의 Pos/RoomCenter는 논리 XY다. 새로운 컴포넌트가 world XZ를 기대하는지, FOV/rotation도 담당하는지, ClampCurrentView가 위치와 화면경계를 어떻게 계산하는지 소스 부재로 불명이다. CinematicDirector.Restore는 기존 위치/FOV를 복원하므로 follow의 다음 Step와 상태 동기화도 확인해야 한다. enabled→disabled 시 예전 cameraVelocity가 남을 가능성도 실제 구현/전환 정책과 함께 확인한다.

[통합 후보] 현재 카메라 유지가 기본이다. 원본 CameraFollow2_5D 계약을 확인한 뒤 선택 컴포넌트 배치·분기만 별도 이식한다. Player, RoomGraph, 전투 좌표, 전체 PaperWorld 설계를 바꿀 근거는 없다.

## 10. UI 변경

| 대상 | 현재 GameUI.prefab의 실제 연결 | 루트 코드가 하는 일 / 남는 문제 |
|---|---|---|
| SkillKey0..5 | Q / SPACE / 2 / 3 / 1 / R, 모두 References 존재 | Awake는 0만 Q로 덮어씀. 2/3/1/R은 새 키와 불일치 |
| Controls | 기존 WASD/SHIFT 걷기/2·3/1/R 안내 | 새 ControlHelp로 덮어씀. SkillKey와 서로 다른 안내가 될 수 있음 |
| Skill4 | 기존 SkillIcon/Label/Cooldown/Text keys | 소환/회수 남은 시간, cooldown0. Command의 cooldown/Active 정보를 잃음 |
| Skill5 | 기존 연계 window/CD 표시 | 기존 표시 유지. 새로운 key 불필요 |
| CrowPrompt | Target=8330236839596222084, RectTransform 320×38/pivot(0,1), Image Sprite=0 | 위치/scale/tilt 변경만 함. 말풍선 Sprite·꼬리 anchor·F 표시를 만들지 않음 |
| CrowPromptText | Target=2237614347639480065, “R · 까마귀 연계” Text | root의 Text 호출 삭제로 저장 R 문구가 남음. root는 이 key를 제거하지 않음 |
| PromptText | 기존 key 존재 | 여행/서비스 안내 우클릭으로 변경 |

**[코드는 있으나 GameUI Prefab 연결 없음]** 새 말풍선 의도에 맞는 이미지·꼬리 pivot·F 표시 레이아웃 및 CrowPromptHeight/Offset/Pop 저장 설정. 이는 “CrowPrompt key 자체가 없다”는 뜻이 아니다. RavenBubble/RavenPortrait assets는 기존 catalog에 있으나 root가 이를 이 말풍선에 연결하지 않는다.

[확인] pop curve는 (0,.28)/(.11,1.17)/(.19,.94)/(.26,1), 이후 소폭 sin scale; tilt와 화면12px 여백 보정이 있다. canvas renderMode에 따라 UI camera를 선택하고 parent RectTransform 좌표로 변환한다. offscreen/behind camera/Running=false/Hp<=0이면 숨기며 제안 또는 target 변경 시 age가 초기화된다. 이 표시가 target 생존·LinkCooldown·ComboBusy 조건 전체를 검사하는 것은 아니다.

[확인] 자동 콤보의 월드 “Crow combo indicator”는 Raven.ComboFollowup에 이미 있고 삭제되지 않았다. UI의 ComboFlash 배너 제거와 이 PNG 제거는 다른 작업이다. 남은 시간은 Skill5에 계속 표시되지만 CrowPromptText에서는 사라진다.

[통합 후보] 말풍선은 기존 LinkWindow/LinkTarget만으로 별도 이식 가능하다. 소환이 완성될 때까지 전체 GameUIView를 바꿀 필요가 없다. 기존 Command HUD는 소환 정책이 확정될 때까지 유지하고, 바뀐 입력 안내·Prefab Text·EditableUIBaker 생성 문자열을 함께 맞춘다. 생성 도구를 실행하는 것은 이번 분석에 포함하지 않는다.

## 11. 단계별 통합 단위

**추천: 8개 검토/이식 단위. 전부 적용하라는 권고는 아니다.** Assets 코드를 출발점으로 필요한 diff만 가져오며 걷기 삭제, Command 폐기, 전체 Raven 교체는 기본 이식 목록에서 제외한다. 새 키와 기존 기능의 공존 정책이 정해지지 않은 단위는 구현하지 않는다.

| 단위 | 최소 범위 / 수정 필요 파일 | Prefab·Scene 변경 | 사용자 Unity 테스트 | 선행 조건·보존 계약 |
|---|---|---|---|---|
| Integration 1 — 입력·조작 안내 동기화 | Player.Update의 E/R/F, Dungeon.Update/Tell의 우클릭, GameUIView의 안내, EditableUIBaker 관련 문자열 | GameUI 기존 SkillKey/Controls/PromptText/CrowPromptText 내용 확인·동기화. 새 key 불필요 | 필요 | E 여행→우클릭과 E/R 스킬·F 연계를 함께 변경. Shift 걷기/1 Command/WalkHeld/걷기 검사 유지. ActionLocked 허용 시점은 기존 우선 |
| Integration 2 — 단검 콤보 입력 간격 | Player.TryAttack의 1.2→2만 선택 적용 | 없음 | 필요 | 별도 balance 후보. 공격 animation/피해/적별 ComboTracker·Raven 4타 연계 유지 |
| Integration 3 — Raven 소환·회수 및 HUD | 원본 Raven API 확보/현 Raven에 호환 추가, Player 입력, GameUIView Skill4와 안내. 초기화는 Dungeon.ResetRun/ActivateRoom의 기존 호출과 계약 확인 | 새 데이터가 필요하면 Raven/GameUI에 해당 필드만; root가 요구하는 새 필드 이름 외 임의 구조 확정 안 함 | 필요 | 시간/마을/전투/컷신/회수 중 Combo·Link 처리 정책 확인. 걷기와 충돌 없는 binding 결정 전 LeftShift 활성화 금지. Command 일반 공격과 HUD를 대안 없이 제거하지 않음 |
| Integration 4 — Raven 프레임·가시성 표현 | Raven 표시 API, ActorPresentation의 Raven Sprite 경로, VisualCatalog 및 누락 frame player/collection/clip. PaperCard의 그림자 계약은 원본 확인 후 필요한 범위만 | Raven.SpriteAnimationCatalog 및 frame 데이터·Shadow 확인. Animator/custom 활성화 불필요 | 필요 | 현재 combat/거리/FSM 유지. 초기 visibility 및 frame 이름·sequence 정의. Sprite write 우선순위와 접지 확인. 3의 소환 정책과 연결 시 의미를 일치시킴 |
| Integration 5 — F 연계 말풍선 | GameUIView의 CrowLinkPrompt/Hide/KeepOnScreen 부분, 필요 시 EditableUIBaker | GameUI.CrowPrompt 이미지·pivot·F/텍스트 배치, CrowPromptHeight/Offset/Pop. 기존 key 유지 | 필요 | 1의 키 안내 계약. 현 LinkWindow/LinkTarget로 가능하며 3/4 완료 불필요. 기존 자동 Combo PNG와 Link 피해 보존 |
| Integration 6 — CustomVisual 요청·크기 보완 | ActorPresentation의 lastCustom 감지/null 바인딩 처리/VisualScale. Raven sequence는 준비된 경우만 | 실제 custom 선택 actor에만 Animator/driver/event 연결 및 VisualScale; 현재 Sprite actor는 그대로 | 실제 custom 채택 시 필요 | 걷기 Walk 요청 보존. modern 정렬/billboard는 정의 확인 전 가져오지 않음. 비활성 slot을 강제로 켜지 않음 |
| Integration 7 — 선택적 카메라 위임 | CameraFollow2_5D 소스 확인, Dungeon.ResetCamera/LateUpdate 최소 분기. CinematicDirector는 계약 검토 대상 | Main Camera에 선택 컴포넌트 배치. Prefab 변경 필수 아님 | 필요 | 논리 XY 인자, PaperWorld.Configure, 컷신 Apply/Restore, 단일 갱신 주체 확인. 기존 fallback 유지 |
| Integration 8 — opt-in 검사 진입점 정리 | Dungeon.Awake, 실제 존재할 PresentationSmoke; MovementSmoke/RuntimeSmoke는 채택 기능과 검사 범위가 맞는지 확인 | 없음. 자동 runtime 부착 | 새 기능 증거가 부족할 때만 사용자 실행 | 누락 타입 확보 전 presentation 인자 추가 금지. --crow-test는 기존 전체 RuntimeSmoke 별칭. 걷기를 유지하면 두 검사도 유지; 로그의 walk 표현과 실제 검사를 일치시킴 |

권장 의존 순서: **1→5**, **3의 상태 계약→4의 가시성 연결**, **4의 Raven sequence 준비→6의 Raven 재요청 부분**. 2와 7은 독립 검토 가능하다. 8은 채택한 단위의 범위에 맞춰 마지막으로 정리하되 사람의 화면 확인을 대체하지 않는다. 6의 modern renderer 의존군은 원본 계약 미확인으로 보류하며 새 2.5D 렌더링 시스템을 설계하지 않는다.

걷기 제거까지 선택하려면 별도 명시적 기능 변경으로 다뤄야 한다. 그때도 Player.WalkHeld/속도, ActorPresentation.Walk/speed, UI 안내, MovementSmoke 두 검사를 함께 검토한다. 현재 요청의 “기존 기능 유지” 기준에서는 제거하지 않는 통합이 우선이다.

## 12. Astra 작업 / 사용자 Unity 테스트 분리

아래는 향후 구현 담당을 위한 분리표다. **이번 분석에서 Astra 작업도, 사용자 테스트도 실행하지 않았다.** Astra는 먼저 변경 diff/참조/조건을 기계적으로 검토하고 사람이 봐야 하는 결과를 넘긴다. Unity/Play Mode를 반복 실행하여 조작감·아트·카메라를 대신 승인하지 않는다.

| Integration | [Astra가 할 일] | [사용자가 Unity에서 할 일] |
|---|---|---|
| 1 입력·안내 | 조건/키 호출과 UI Text·Baker 목록 맞춤, E/R 구입력 잔존·이중호출 검색, 걷기/Command 코드 및 API 보존 확인. 기존 References target 유지 | 전투방/문/서비스에서 E/R/F/우클릭, UI 위 우클릭, Shift 걷기·1 명령 확인. 성공: 안내 일치·한 입력 한 동작. 실패: 스킬과 여행 동시 요청/옛 키 표시 |
| 2 콤보 간격 | 변경이 TryAttack 단검 시간 한 곳인지, ComboTracker·피해·PresentationSequence가 그대로인지 비교 | 단검 약1.5초 간격 이어치기 및 2초 초과 초기화, 총/4타 Raven 연계 확인. 성공: 새 입력시간만 반영 |
| 3 소환/HUD | 원본 API/수명 정책 확인, 필요한 코드·필드·HUD 연결, ResetAt/ResetForRun 및 Command/Combo/Link caller 추적. pending 큐·pause·town 조건 정적 확인 | 소환/회수·시간 만료·방 이동·pause·컷신과 진행 중 Combo/Link 조합 확인. 성공: 정한 정책대로 복귀/표시/공격 유지. 실패: 보이지 않는 공격·정지된 타이머·명령 손실 |
| 4 프레임/표시 | collection/clip/FramePlayer 타입·멤버·직렬화, Sprite/Shadow/GUID/상태명 매핑, 순서250→300, 동일 sequence Play의 재시작 조건 확인. 해당 Prefab field/data 배치 | Idle/Follow/Combo/Link의 방향·프레임·접지·회수 시 그림자 확인. 성공: 이동/타격 timing은 기존이고 포즈만 계약대로 표시. 실패: 프레임 정지·크기 튐·잔상 |
| 5 말풍선 | 기존 key 유지, F 표시·Sprite/pivot/curve 설정, 4개 hide 경로 및 화면 투영/target 변경 조건 검토. 자동 combo PNG 경로 보존 | 제안 시작/수락/만료·화면 가장자리·pause 확인. 성공: 읽을 수 있고 화면 안에 배치, 조건 종료 시 숨음. 실패: R 잔존·사각 배너 확대·잘림 |
| 6 custom | 선택한 actor의 필드/Animator state/driver/event mapping 확인, Sprite 모드 유지, 토글·sequence 조건 비교 | 실제 custom을 쓰는 actor만 토글 후 같은 motion 재생·좌우·속도/pause 확인. Sprite만 쓰면 이 테스트 생략 |
| 7 카메라 | Step/Clamp 시그니처·논리좌표 단위, enabled/fallback 상호배타 분기, controller 자체 Update 유무, Cinematic.Apply/Restore와 Scene field 배치 검토 | 방 입장, 추적, BossEntrance/PlayerDeath 및 enabled/disabled 전환 확인. 성공: 대상 유지·한번 갱신·컷신 복귀. 실패: 이중 follow·화면 튐·대상 잘림 |
| 8 검사 | 타입 존재, 인자 선택 우선순위, 종료/출력 부작용, 검사 이름과 실제 Check 내용 대조. 걷기/Combo 계약 검사 제거 금지 | 필요한 경우에만 합의한 opt-in 1회 실행 및 로그 공유. 일반 실행에서 검사·강제 종료가 활성화되지 않는지 확인. 시각 합격은 앞 단위 테스트 기준 |

실행 제한이 유지되는 동안 컴파일/Unity 실행 검증은 미완료로 표기한다. 소스에 API가 있다는 확인과 컴파일 성공, Prefab 참조 존재와 올바른 화면을 같은 결과로 취급하지 않는다. 새로운 사용자 승인 없이 이번 단계에서 실행 테스트를 시작하지 않는다.

## 13. 기존 예정 작업에 미치는 영향

| 예정 작업 | 이번 분석으로 확인한 영향 |
|---|---|
| BossController | 현재 변경 없음. root 5개가 새 Boss API를 제공하지 않는다. 추후 카메라 단위7의 BossEntrance 확인만 관련 |
| Boss Death/Clear | 현재 변경 없음. 공통 Enemy 사망/전멸/CompleteWave/Exit 전제 유지. 프레임 도입으로 피해·사망 판정을 animation event로 옮길 근거 없음 |
| Boss Reward | 현재 변경 없음. root에도 reward spawn/gate가 추가되지 않는다. 상호작용 입력 단위1은 향후 보상 UI의 안내와 일치시킬 사항 |
| Raven | 직접 영향 큼. 누락 소환/표현 계약 확보와 현 Command/Combo/Link 보존이 우선. 프레임·말풍선·소환을 분리하고 root를 새로운 완성 Raven 명세로 취급하지 않음 |
| Passive | RunBuild/AcquirePassive/PlayerDamageBonus/MoveSpeedBonus/RavenDamageBonus 전제 유지. root UI의 Butterfly 설명은 현재 Command의 RavenLevel 공격시간 증가를 제거하지 않으므로, 단순 이식하면 효과와 설명이 어긋남 |

위 예정 작업을 실제 구현·이식하지 않았다. 현재 시스템의 구현 위치를 바꿀 필요가 있다는 근거도 이번 루트 변경만으로는 없다.

## 14. 다음 AI가 알아야 할 핵심

- 현재 runtime은 Assets판이다. 루트 5개는 아직 교체할 수 없는 부분 패치 후보이며 HEAD는 c218280dbb80d83b03e17b05ad92f8bd50df86a3다.
- 일괄 덮어쓰기·Assets에 동명 class 추가를 하지 않는다. 기존 소스/.meta/serialized refs를 기준으로 선택한 기능만 이식한다.
- 단독 Player 교체는 ToggleSummon 결손 외에도 ActorPresentation/MovementSmoke의 WalkHeld 참조를 깨뜨린다.
- 걷기는 Locomotion enum이 아니다. root는 WalkHeld·2.8 속도·Walk 요청을 모두 제거하나 atlas/Moving/Settling은 유지한다. 보존 통합에서는 걷기와 두 검사도 유지한다.
- root의 1 Command 제거는 일반 공격 시작 입력의 손실이다. Active를 소환 여부로 바꿔 이름만 맞추면 안 된다.
- E/R/F/우클릭은 입력·안내를 함께 옮겨야 한다. LeftShift 소환은 걷기와 충돌하므로 별도 정책 확정 전 적용하지 않는다.
- root F/LeftShift는 ActionLocked보다 앞이다. 키 재배치와 action 중 허용 정책 변경을 섞지 않는다.
- Raven 소환·가시성·animation name/sequence API와 SpriteAnimator/collection/clip/FrameAnimations는 현재 없다. 원본 파일 누락은 추론이다.
- 기존 Raven Combo queue/2대상·거리7·pause, TryLink window6/CD8·거리·town 조건과 Mark/OfferLink/ComboFollowup 호출을 보존한다.
- Sprite frame 추가는 Raven 대상이며 Player atlas/논리 XY→표시 XZ는 유지한다. Animator/custom slot 활성화는 필수가 아니다.
- 새 UI key는 없다. 실제 문제는 옛 2/3/1/R 안내, Sprite 없는 R 배너, 새 말풍선 field/레이아웃 미연결이다. RavenBubble asset 자체는 이미 있다.
- 카메라는 Player로 이전되지 않는다. Dungeon의 선택적 controller 호출이며 해당 타입·구현이 없어 갱신/좌표/FOV 책임 확인이 필요하다.
- 추천 단위는 입력·안내, 단검 콤보 간격, 소환/HUD, Raven 프레임/가시성, F 말풍선, custom 요청/크기, 카메라 위임, opt-in 검사 8개다. modern 렌더 계약은 확인 전 보류한다.
- 현재 코드/Scene/Prefab/기존 문서 미수정. 컴파일·Unity·빌드·Smoke·commit/push 미실행이며 실제 조작/화면은 통합 후 사용자 확인 영역이다.
