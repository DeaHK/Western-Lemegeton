# Western-Lemegeton Latest Change Analysis

## 1. 분석 기준

- 분석 날짜: 2026-10-03 (Asia/Seoul).
- 프로젝트: `E:\GitHub\Western-Lemegeton`.
- 현재 HEAD: `c218280dbb80d83b03e17b05ad92f8bd50df86a3`.
- parent commit: `d8eed843f7917ac88e6462cfb4ea426b7ab26b47` (단일 parent).
- commit message: `Add files via upload`; commit 시각: 2026-10-03 01:02:35 +09:00.
- 기존 기준 문서: `Docs/WESTERN_LEMEGETON_PROJECT_ANALYSIS.md` (기준 HEAD가 이번 parent와 일치). 기존 문서는 수정하지 않았다.
- 분석 시작 working tree: ` M ProjectSettings/ShaderGraphSettings.asset`, `?? Docs/WESTERN_LEMEGETON_PROJECT_ANALYSIS.md`. 둘 다 기존 로컬 상태이며 이번 분석에서 수정하지 않았다.
- 범위: 로컬 현재 HEAD와 parent. 원격 fetch/pull, 다른 branch·백업 프로젝트 분석, Unity Editor/Play Mode/빌드/실행 테스트, commit/push를 하지 않았다.

**[확인] 실제 commit은 아래 5개를 프로젝트 루트에 추가했다. 요청에 제시된 `Assets/Scripts/...` 파일의 수정 commit이 아니다.**

| Git 상태 | 실제 변경 경로 (프로젝트 루트 기준) | 추가 행 | 비교한 기존 Unity 소스 |
|---|---|---:|---|
| A | `ActorPresentation.cs` | 101 | `Assets/Scripts/Authoring/ActorPresentation.cs` |
| A | `Dungeon.cs` | 171 | `Assets/Scripts/Dungeon.cs` |
| A | `GameUIView.cs` | 158 | `Assets/Scripts/Authoring/GameUIView.cs` |
| A | `MovementSmoke.cs` | 74 | `Assets/Scripts/MovementSmoke.cs` |
| A | `Player.cs` | 172 | `Assets/Scripts/Player.cs` |

[확인] `git show --stat HEAD` 및 `git diff-tree --no-commit-id --name-status -r HEAD` 결과는 5개 추가, 676행 추가/0행 삭제다. `git diff HEAD^ HEAD -- Assets Packages ProjectSettings`는 비어 있다. 조사한 소스·Scene·Prefab은 working tree와 HEAD 사이의 변경도 없다. 새 파일의 `.meta`, Scene/Prefab/asset, 의존 소스는 이 commit에 없다.

이하 **“Assets판”은 현재 Unity 프로젝트에 연결된 기존 소스**, **“루트판”은 이번 commit에 추가된 Assets 밖 소스**를 뜻한다. 파일별 “변경 전→후”는 동일 이름 Assets판과 루트판을 추가 비교한 결과이지, Git이 기존 소스를 수정/이동했다고 판정한 결과가 아니다. 루트판을 Assets로 가져오는 기존 Editor/runtime 경로도 조사한 코드에서 확인되지 않았다. 따라서 루트 파일 추가를 실제 게임 기능 적용으로 간주하지 않는다.

표기: `[확인]`은 코드/diff/YAML 사실, `[추론]`은 의도 추정, `[사용자 Unity 테스트 필요]`는 실행 미검증이다. 아래 파일별 비교 내용은 별도 표시가 없는 한 정적 확인 사실이다.

## 2. 이번 변경 한 줄 요약

[확인] 같은 이름의 C# 5개가 루트에 추가되었고, 현재 Assets의 플레이 동작과 저장 연결은 그대로다.  
[확인] 루트판에는 걷기 제거·입력 재배치·단검 콤보 입력 간격 연장, Raven 소환 API 호출·프레임 표시·연계 말풍선, 선택적 카메라 위임 코드가 담겼다.  
[확인] 필요한 Raven API, 프레임 애니메이션 타입, 카메라/검사 타입이 현재 Assets에 없어 이 5개만 교체하면 의존성이 충족되지 않는다.  
[확인] Prefab에는 새 필드와 말풍선 아트 연결이 없고 기존 키 안내도 남아 있으므로, 통합 완료 또는 새 기능의 실행 성공으로 보고할 수 없다.

## 3. 변경 파일별 분석

### ActorPresentation.cs

**변경 전:** Assets판은 Player의 skill/basic/dodge/locomotion을 읽고 `WalkHeld`에 따라 `Walk`/`Run`과 속도 기준 2.8/5.6을 선택한다. Raven은 `CurrentState.ToString()`을 사용한다. CustomVisual이 있고 `UseCustomVisual=true`일 때만 Animator/driver/`AnimationRequested`를 구동한다.

**변경 후:** 루트판의 의미상 변경은 다음과 같다.

- 추가 직렬화 필드: `public VisualCatalog SpriteAnimationCatalog`, `[SerializeField] SpriteAnimator spriteAnimator=new SpriteAnimator()`, `[Min(.01f)] public float VisualScale=1`. 추가 공개 API: 읽기 전용 `FramePlayer`.
- 추가 내부 필드: `baseRenderOffset`, `baseSpriteRotation`, `lastCustom`, `shadowCard`, `shadowSprite`. 기존 `customScale` 등은 유지하며 삭제 필드는 없다.
- 변경 함수 `Awake`: 기존 크기 외에 Sprite 회전·RenderOffset을 보관하고, 비활성 자식을 포함한 PaperCard 중 `Source.name=="Shadow"`인 첫 항목을 찾는다.
- 변경 함수 `LateUpdate`: Player 이동은 `Run`만 요청하고 속도 비율은 `Locomotion.Speed/5.6f`. Player 상태·이동 값을 새로 읽는 추가 계약은 없으며 `WalkHeld` 읽기를 제거한다. 기존 `Animations` 배열의 `Walk` 바인딩 자체는 남는다.
- Raven에는 `AnimationName`(비어 있으면 `Idle`), `IsVisible`, `FacingLeft`, `PresentationSequence` 읽기가 추가된다. 표시와 그림자를 숨기되 Raven root/FSM을 비활성화하지 않는다.
- 새 함수 `ApplyRavenFrames(SpriteAnimationCollection, string, long, float)`: collection의 `Get(state)` clip을 `Play(clip,sequence)`/`Tick(Time.deltaTime,playback)`로 재생하고 Sprite·고정 scale·회전·RenderOffset·그림자 footprint/ground offset을 적용한다. custom 표시 중에는 프레임 Sprite 교체를 건너뛴다. Player atlas를 이 프레임 재생기로 교체하는 코드는 아니다.
- CustomVisual은 `UseCustomVisual && visible`로 활성화하고 `VisualScale` 및 Raven collection offset을 반영한다. 회전/정렬은 `WorldDepth.Modern`, `SpriteBillboard.Rotation`, `WorldDepth.Objects`, `WorldDepth.RenderOrder`를 새로 참조한다.
- 요청 판정에 motion/sequence 외 `UseCustomVisual` 전환을 추가하고 캐시를 custom 사용 여부와 무관하게 갱신한다. Raven도 sequence로 같은 motion을 다시 요청할 수 있다. `AnimationRequested`의 이벤트 형식은 유지한다.
- Animator/driver/event 호출은 여전히 CustomVisual 존재 및 사용 조건 안에 있다. `Animations` null/항목 null을 허용하며, Raven은 매칭 바인딩이 없어도 state 이름을 fallback으로 사용한다. loop는 `Idle`/`Move` 기본값→frame clip→일치하는 `MotionBinding` 순으로 결정한다. 숨은 Raven은 playback=0이다. 삭제 함수는 없다.

**실제 동작 영향:** 현재 Assets판은 변경되지 않았다. 루트판은 Raven 데이터 기반 프레임 표시와 custom 표시 재요청 경로를 추가하지만 §7의 타입/API 없이는 통합할 수 없다. `[추론]` collection 단위 scale/anchor 및 custom 토글 감지는 포즈별 크기·접지 일관성과 전환 시 재생 요청을 다루려는 것으로 보인다.

**연결 시스템:** Player, Raven, Enemy, Dungeon/Cinematics, PaperCard, VisualCatalog, Animator/ActorAnimationDriver. UI 이벤트를 직접 호출하지 않는다.

**Scene/Prefab 추가 연결 필요 여부:** 현재 변경 없음. Hunter/Raven의 저장 `UseCustomVisual=0`, custom child 비활성, Animator 참조 0, `AnimationRequested` persistent call 비어 있음. Hunter의 AnimationDriver=0, Raven에는 해당 직렬화 항목 자체가 없다. 새 catalog/프레임 필드/VisualScale 직렬화도 없다. 기존 inactive slot이 이번 commit으로 채워지거나 활성화된 것은 아니다. 프레임/CustomVisual을 실제 사용하려면 각각 데이터 및 표시 연결이 필요하다(§7).

### Dungeon.cs

**변경 전:** Assets판은 `Update`의 E 입력으로 `TryTravel`을 호출하며 마을 출발·문·서비스·출구 안내도 E다. 카메라는 자체 `ResetCamera`/`LateUpdate`의 PaperWorld 위치 계산과 SmoothDamp, 컷신의 `ApplyCamera`를 사용한다.

**변경 후:** 추가/삭제 필드와 새/삭제 함수는 없다. 다음 함수 내부가 바뀐다.

- `Awake`: `--presentation-test` 또는 `--presentation-before`가 있으면 `PresentationSmoke`를 먼저 추가하는 최우선 분기. 기존 마지막 RuntimeSmoke 분기에 `--crow-test`를 `--smoke-test`의 별칭으로 추가. 다른 검사와는 `else if`라 한 종류만 선택된다.
- `Update`: E 대신 `Input.GetMouseButtonDown(1)`으로 기존 `TryTravel()` 호출. 새 우클릭 처리에 `IsPointerOverGameObject()` 필터는 없다.
- `EnterTown`, `ActivateRoom`, `Update`: 안내를 마우스 우클릭으로 변경. `ActivateRoom`은 서비스방 안내 문자열만 변경하며 진입 위치·SafeEntry·Crow.ResetAt 순서는 유지한다.
- `ResetCamera`: Cam의 enabled `CameraFollow2_5D`가 있으면 `Step(Pos,RoomCenter,IsTown,true)`로 즉시 배치하고 반환. 없으면 기존 로직.
- `LateUpdate`: 컷신 중에는 `ApplyCamera()` 뒤 enabled follow의 `ClampCurrentView()`. 평상시 enabled follow가 있으면 `Step(Pos,RoomCenter,IsTown)`로 위임하고 기존 FOV/SmoothDamp 분기를 건너뛴다. 없으면 기존 카메라 동작.

**실제 동작 영향:** 현재는 E와 기존 카메라가 유지된다. 루트판에서도 `RunState` enum/`Running` 정의, Hero 바인딩·초기화/Spawn·방 입장, `TryTravel`/`TryDoor` 조건, 웨이브·전멸·HP+18·Exit, BossEntrance·Reward 서비스 흐름은 변경하지 않는다. 컷신 카메라 후처리 연결만 추가되며 카메라 컴포넌트의 내부 알고리즘은 소스 부재로 알 수 없다. 단일 Main 및 Dungeon.I 구조 유지.

**연결 시스템:** 입력→Dungeon.TryTravel→RunState/Notice→GameUIView polling; Dungeon→Cam의 CameraFollow2_5D(루트판에만 호출); Dungeon.Awake→opt-in 검사.

**Scene/Prefab 추가 연결 필요 여부:** 현재 Main Camera는 Transform/Camera/AudioListener만 저장되어 있고 새 follow 연결은 없다. follow는 동작상 선택 사항이지만 C# 타입 정의는 필요하다. 테스트 컴포넌트는 Awake에서 조건부 추가하므로 저장 Scene에 붙일 전제는 없다. 새 UI/animation Inspector 필드는 없다.

### GameUIView.cs

**변경 전:** Assets판은 Skill4에 Raven.Command cooldown/Active 시간을 표시하고, `CrowPrompt`를 `LinkWindow>0 || ComboFlash>0`에 표시하면서 `CrowPromptText`에 R 안내·남은 시간 또는 콤보 문구를 쓴다.

**변경 후:**

- 추가 직렬화 필드: `CrowPromptHeight=1.45f`, `CrowPromptOffset=(18,-6)`, `CrowPromptPop` AnimationCurve. 추가 상수 `ControlHelp`, 내부 필드 `canvas`, `crowPromptAge`, `crowPromptOffered`, `crowPromptTarget`, `crowPromptCorners[4]`. 제거 필드는 없다.
- `Awake`: 상위 Canvas를 캐시하고 기존 `SkillKey0`에 Q, `Controls`에 새 안내를 쓰고 `CrowPrompt`를 숨긴다. 나머지 SkillKey를 갱신하는 코드는 추가되지 않았다.
- `LateUpdate`: Skill4 cooldown 표시를 0으로 고정하고 `IsSummoned`/`SummonRemaining`에 따라 `회수 N초` 또는 `소환 60초` 표시. Player의 새 상태 표시 필드는 없으며 Raven 소환 표시를 새로 요구한다.
- 새 함수 `CrowLinkPrompt`: `LinkWindow>0 && g.Running && Hero.Hp>0`일 때 Hero 논리 위치를 `PaperWorld.Point`로 변환하고 카메라 up×height를 더해 화면·UI 좌표로 투영한다. 카메라 뒤/화면 밖/연결 부재이면 숨긴다. 제안 시작 또는 `LinkTarget` ID 변경 시 pop 시간을 초기화하고 unscaled time으로 scale/tilt를 갱신한다.
- 새 함수 `HideCrowPrompt`: 표시·제안 캐시·scale/rotation 초기화. 새 함수 `KeepCrowPromptOnScreen`: 네 모서리 기준 camera.pixelRect의 12px 안쪽으로 보정. parent는 RectTransform이어야 한다.
- 자동 4타 ComboFlash만으로 이 UI를 켜는 경로와 `Text("CrowPromptText",...)` 호출을 제거한다. 기존 자동 콤보 월드 표시를 새로 구현한 것은 아니다.
- `Prompt`: 이동·출발·서비스 안내를 우클릭으로 변경. 기존 Butterfly 효과 설명을 “까마귀의 일반 공격과 연계 공격을 강화”로 변경했으나 RunBuild/Raven 효과 계산 변경은 이 commit에 없다. 삭제 함수는 없다.

**실제 동작 영향:** 현재 UI는 기존 동작이다. 루트판의 `60초`는 UI 문구이며 소환 수명 구현의 증거가 아니다. `[추론]` F 연계 제안을 자동 콤보 표시와 구별하여 플레이어 머리 위에서 보여주려는 코드다. 실제 초상/말풍선/키 아트는 이 코드가 생성하지 않는다.

**연결 시스템:** Dungeon/Player 위치·HP·Running·Camera, Raven.LinkWindow/LinkTarget 및 추가 소환 API, Canvas/RectTransform. 기존 매 프레임 조회 구조 유지.

**Scene/Prefab 추가 연결 필요 여부:** 새 UIReference key 추가는 없고 기존 `Controls`, `SkillKey0`, `CrowPrompt`를 재사용한다. `CrowPromptText`는 호출만 제거되고 Prefab key/객체는 남는다. 저장된 `SkillKey0..5=Q/SPACE/2/3/1/R`; 루트판을 적용해도 2/3/1/R은 자동 변경되지 않는다. CrowPrompt는 Sprite 없는 320×38 Image와 “R · 까마귀 연계” Text, pivot(0,1)의 기존 배너다. 새 주석의 초상·꼬리 pivot·F 키가 포함된 말풍선 연결은 없다. 새 필드 직렬화 및 디자인 연결도 없음(§7).

### MovementSmoke.cs

**변경 전:** Assets판의 `Reset`은 `hero.WalkHeld=false`. `Start`에는 Shift 걷기 속도=달리기의 절반 및 걷기 이동거리 기반 gait 검사 2개가 있다.

**변경 후:** 루트판은 위 WalkHeld 설정과 걷기 검사 2개만 제거한다. 추가/삭제 필드, 새/삭제 함수, 새 테스트는 없다. 수정 함수는 `Reset`, `Start`. 삭제된 검사 전에 계산하던 `run`/`runCycle` 지역 변수는 남아 후속 참조가 없고, 성공 요약 문자열의 `walk`도 그대로 남는다.

**실제 동작 영향:** Assets판 테스트는 여전히 걷기를 검사한다. 루트판은 WalkHeld 제거에 대응하여 검증 범위를 줄인 것이다. 8방향·대각선 동일 속도 5.6, 6프레임 gait, 정지/idle, pause, 벽, 스킬·회피·태그 후 복귀, 총 조준과 이동 표현 분리, SafeEntry, atlas 접지/크기 검사는 유지한다. 새 입력·소환·2초 콤보·말풍선은 검사하지 않는다.

**연결 시스템:** Dungeon.Awake의 `--movement-test` opt-in→MovementSmoke→Player.AdvanceCombat/Locomotion. Player.Update를 비활성화하고 직접 stepping하므로 실제 키 입력 검사는 아니다. 일반 플레이 로직이 아닌 종료형 검사로, screenshots/로그를 쓰고 `Application.Quit`한다. 이번 작업에서는 실행하지 않았다.

**Scene/Prefab 추가 연결 필요 여부:** 없음. 기존/루트 Dungeon 모두 인자로 런타임 부착하는 방식이다.

### Player.cs

**변경 전:** Assets판은 양쪽 Shift로 걷기, 1로 까마귀 Command, R로 연계, 2/3으로 스킬을 요청한다. 단검 기본 공격의 `comboTime`은 1.2초다.

**변경 후:** 루트판에서 삭제된 것은 공개 자동 프로퍼티 `WalkHeld`이며 새로운 필드/프로퍼티·함수 또는 삭제 함수는 없다. `ResetForRun`의 WalkHeld 초기화, `Update`의 Shift hold 읽기, `AdvanceActions`의 걷기 속도 분기를 제거한다.

| 동작 | Assets판 / 현재 적용 코드 | 루트판 |
|---|---|---|
| 이동 | WASD, 기본 5.6 / Shift 2.8 | WASD 5.6, 걷기 없음 |
| Raven 입력 | 1→Command, R→TryLink | LeftShift key-down→ToggleSummon, F→TryLink |
| 스킬 | 2→TrySkill(0), 3→TrySkill(1) | E→TrySkill(0), R→TrySkill(1) |
| 상호작용 (Dungeon) | E→TryTravel | 우클릭→TryTravel |
| 단검 입력 콤보 만료 | TryAttack에서 1.2초 설정 | 2초 설정; 총은 4초 유지 |

**실제 동작 영향:** 현재 게임에는 위 교체가 적용되지 않았다. 루트판은 다음 계약을 바꾼다.

- 소환/연계 입력은 `ActionLocked` 조기 반환보다 앞에 있어 내려찍기 중에도 호출을 시도한다. Running 검사는 그대로이고 실제 허용 여부는 Raven API가 판단한다. RightShift 소환 처리는 없다.
- 일반 이동·대시는 여전히 Vector2/Transform과 Rules의 Rect 충돌 계산이다. 난사 중 3.1 속도 및 MoveSpeedBonus 적용 유지. Rigidbody/Collider/NavMesh 전환 없음.
- `LastMove`/`Facing`/`MotionDirection`, `Locomotion.Advance`, `Animate`의 상태 우선순위·atlas·flip 및 `PresentationSequence` 증가 지점은 유지한다. 단검 이동방향/총 마우스 조준 분리, 논리 XY→표시 XZ 구조 유지.
- 스킬 종류/피해·기본 공격 타격시점·Space 회피·Q 태그·좌클릭 buffer/UI 필터는 그대로다. 2초는 입력 Combo 만료값이며 적별 `ComboTracker`의 4초 계약을 바꾼 것이 아니다.

**연결 시스템:** Player→Dungeon.I.Crow(소환/연계), Dungeon.Build/Rules(이동·피해), ActorPresentation(Player 상태 읽기), MovementSmoke(동일 stepping API). Player가 GameUIView를 직접 호출하지 않는다.

**Scene/Prefab 추가 연결 필요 여부:** 새 SerializeField/Inspector 연결 없음. `art` 연결은 기존 그대로다. 단, 새 `Raven.ToggleSummon`은 현재 Raven에 없다. `[추론]` Shift를 소환에 사용하며 걷기를 제거하고 E/R을 스킬로 재배치한 흐름이며, E와 겹치던 상호작용을 우클릭으로 옮긴 것으로 보인다.

공통 formatting 구분: 파일 전체가 신규 추가라 Git의 676행을 676행의 신규 gameplay 구현으로 계산하면 안 된다. 동일 이름 비교에서 삼항 연산자 주위 공백·줄 분할·주석 등은 동작 변경에서 제외했다. 함수 분할, 조건·입력·수치·API 호출 변경은 위에 별도로 기록했다.

## 4. 시스템 연결 변화

[확인] 현재 Unity에서 새로 활성화된 연결은 없다. 아래는 **루트판에 쓰인 호출/조회 관계**이며 `미연결`은 현재 Assets 쪽 정의/데이터가 충족되지 않음을 뜻한다.

```text
Player.Update → Dungeon.I.Crow → ToggleSummon [미연결] / TryLink
Player 상태 ← ActorPresentation.LateUpdate
                 ├─ Player Run + 기존 액션 / PresentationSequence
                 ├─ Raven 새 표시 API [미연결]
                 ├─ VisualCatalog.FrameAnimations → SpriteAnimator [미연결]
                 └─ CustomVisual → Animator / Driver / AnimationRequested

Dungeon.Update(우클릭) → TryTravel → 기존 RunState/Notice
                                          ↓ 조회
GameUIView.LateUpdate ← Dungeon.Hero 위치·HP / Cam / Running
                     ← Dungeon.Crow 소환 상태 [미연결] / LinkWindow / LinkTarget
                     → CrowLinkPrompt → 기존 CrowPrompt RectTransform

Dungeon.ResetCamera/LateUpdate → CameraFollow2_5D [미연결]
Dungeon.Awake(--movement-test) → MovementSmoke → Player.AdvanceCombat
```

`Player → Dungeon → GameUIView`는 Player를 보관하는 Dungeon을 UI가 조회하는 관계다. 새 Player→UI 이벤트를 추가한 것이 아니다. `Dungeon.I` 중심 단일 Main, 수동 논리 XY와 표시 World XZ 분리는 루트판에서도 유지된다.

## 5. 기존 프로젝트 분석서에서 달라진 내용

**[확인] 이번 commit 때문에 기존 분석서의 실행 시스템 설명이 outdated 된 항목은 확인되지 않았다.** `Assets` 전체가 parent와 같기 때문이다. 루트판의 의도를 적용된 기능으로 취급해 기존 설명을 고쳐 읽으면 안 된다.

기존: 분석서 머리말의 기준 HEAD는 `d8eed843...`, §2 폴더 구조와 §25 중복 설명은 당시 저장소 기준이다.  
현재: HEAD는 `c218280d...`이며 Assets 밖에 같은 namespace/class 이름의 C# 사본 5개가 새로 존재한다.  
근거: `git rev-list --parents -n 1 HEAD`, `git diff-tree ... HEAD`. 소스 트리 추가 사실만 증분 보완한다.

향후 루트판이 의존성과 함께 실제 통합될 경우에만 재검토할 설명은 §6.3 입력, §9 카메라, §10 걷기·스킬, §15 소환/Command/R 연계, §20 UI, §24 opt-in 검사다. 이는 **현재 outdated 판정이 아니다**. 특히 기존 §15의 “소환/해제 없음”은 현재 `Assets/Scripts/Raven.cs`에 여전히 맞는다.

## 6. 새로 생긴 하드코딩 / 의존성

아래는 **루트판과 기존 Assets판 사이의 신규·변경 계약만** 기록한다.

| 구분 | 추가/변경 내용 |
|---|---|
| 입력 | LeftShift의 역할을 hold 걷기→key-down 소환으로 변경, F 연계 신규, E/R 스킬 재할당, 우클릭 index 1 상호작용. 숫자1/2/3 및 RightShift 경로 제거 |
| 시간/표시 | 단검 comboTime=2; UI “소환 60초”/“최대 60초” 고정 문구. 60초 수명 구현은 이 commit에 없음 |
| Inspector/API | ActorPresentation의 SpriteAnimationCatalog/spriteAnimator/VisualScale 및 FramePlayer; GameUIView의 CrowPromptHeight/Offset/Pop |
| animation/계층 | Raven.AnimationName 및 sequence; Raven fallback loop의 `Move` 상태 문자열; 자식 PaperCard.Source 이름 `Shadow`에 의존. 기존 `Animations` 이름 목록은 신규 목록이 아님 |
| 타입/멤버 | SpriteAnimator, SpriteAnimationCollection, VisualCatalog.FrameAnimations, Raven 새 소환·표시 API, PaperCard.ShadowGroundOffset, WorldDepth.Modern/Objects/RenderOrder, SpriteBillboard.Rotation, CameraFollow2_5D, PresentationSmoke |
| UI 배치 | height 1.45, offset(18,-6), pop keyframe (0,.28)/(.11,1.17)/(.19,.94)/(.26,1), .26초 이후 흔들림, .4초 전후 tilt, 12px 화면 여백. Canvas 및 RectTransform parent 필요 |
| 검사 argument | --presentation-test / --presentation-before 최우선, --crow-test→RuntimeSmoke 별칭 |

새 UIReference key/enum 또는 Resources.Load 경로는 없다. Tooltip의 `SpriteAnimationCollection.asset` 언급은 실제 로딩 경로나 그 asset의 존재 증명이 아니다. `SkillKey0`/`Controls`는 새로 쓰는 기존 key이고 `CrowPromptText` key 자체는 삭제하지 않았다.

## 7. 아직 연결되지 않은 부분

**[확인] 가장 먼저 미연결인 것은 파일 위치다. 5개 루트 파일은 현재 Unity Assets 소스 교체가 아니다.** 아래는 그 루트판을 사용할 때 필요한 계약과 현재 HEAD의 차이다. 현 Assets가 이 commit 때문에 컴파일 오류가 났다는 뜻은 아니다.

| 요구 위치 | 현재 Assets에서 충족되지 않는 계약 |
|---|---|
| Player.Update | Raven.ToggleSummon 없음 |
| ActorPresentation.LateUpdate | Raven.AnimationName / IsVisible / FacingLeft / PresentationSequence 없음. 현재 CurrentState 기반 구현만 있음 |
| GameUIView.LateUpdate | Raven.IsSummoned / SummonRemaining 없음. 기존 Active/Cooldown 기반 |
| ActorPresentation 프레임 경로 | SpriteAnimator, SpriteAnimationCollection 타입 정의 없음; VisualCatalog에는 Icons/Get만 있고 FrameAnimations 없음 |
| ActorPresentation 그림자/정렬 | PaperCard.ShadowGroundOffset 없음; WorldDepth에는 기존 Order만 있고 Modern/Objects/RenderOrder 없음; SpriteBillboard 타입 없음 |
| Dungeon 카메라/검사 | CameraFollow2_5D, PresentationSmoke 타입 정의 없음. 조건부 사용이어도 타입 참조가 있어 단순 비활성화만으로 컴파일 의존성을 해소할 수 없음 |

범위: 현재 Assets C#의 해당 심볼 정의·사용, 관련 클래스 전체, HEAD의 Assets/Packages 경로를 확인했다. 다른 branch/외부 작업자의 파일로 보충해 판단하지 않았다. 기존 `Raven.LinkTarget`/`TryLink`와 **Player**의 `PresentationSequence`는 이미 있으므로 미구현 목록에서 구별했다.

[확인] 저장된 Scene/Prefab 연결은 기존 상태다.

- `Main.unity`: 기존 GameSceneBindings의 Hunter/Raven/Cam/UI, GameHUD.View 참조 유지. CameraFollow2_5D 컴포넌트 없음. 관련 새 필드 override 없음.
- `Hunter.prefab`/`Raven.prefab`: 새 SpriteAnimationCatalog, spriteAnimator, VisualScale 저장값 없음. 프레임 animation collection asset도 현재 Assets에서 확인되지 않는다. 새 직렬화 항목 부재가 기존 Assets 컴포넌트의 오류는 아니지만, 새 참조가 이미 연결되었다고 볼 수 없다.
- `GameUI.prefab`: 새 CrowPromptHeight/Offset/Pop 저장값 없음. 해당 값은 루트 코드에 초기값이 있지만 말풍선 아트·꼬리 pivot·F 표시를 제공하지 않는다. 기존 CrowPrompt의 Sprite=0, pivot(0,1), R Text와 SkillKey2..5=2/3/1/R이 유지되어 루트 입력 계약과 어긋난다. 루트판은 CrowPromptText를 더 이상 갱신하지 않는다.
- `Assets/Editor/Authoring/EditableUIBaker.cs`도 기존 R 배너와 이전 Controls 문구를 생성한다. 이번 commit에는 UI 재생성 도구 변경이 없다. 이를 실행하여 새 UI가 만들어질 것으로 가정하면 안 된다.

현재 확인된 타입/멤버 결손만으로도 5개 단독 교체는 완결된 통합이 아니다. 파일 복사·교체·중복 class 정리·Prefab 연결·migration은 이번 분석에서 수행하지 않았다.

## 8. 사용자 Unity 테스트 필요 항목

Unity Editor/Play Mode/빌드/Smoke를 실행하지 않았다. **현재 HEAD에 없는 기능을 테스트로 확인할 수는 없다.** §7의 코드·데이터 통합이 별도 작업으로 완료된 이후에만 다음 최소 항목을 확인한다. 현재 문서 작성 완료를 위해 즉시 Unity 실행이 필요한 것은 아니다.

1. **[사용자 Unity 테스트 필요] 입력·이동·콤보**  
   테스트 방법: 통합 후 전투방에서 WASD/LeftShift/F/E/R/우클릭을 확인하고, 단검 공격을 약 1.5초 간격으로 입력한다.  
   정상 결과: Shift가 걷기로 감속시키지 않고 소환 토글을 요청, E/R 스킬·F 유효 연계·우클릭 이동/서비스가 동작, 단검 입력 Combo가 2초 이내 이어짐. 방향/idle/액션 복귀는 기존 계약 유지.  
   실패 시 확인할 증상: 기존 키만 반응, 내려찍기 중 소환/연계 요청 누락, UI 위 우클릭 시 의도치 않은 여행, 방향/복귀 포즈 불일치. 현재 Assets에서 이전 키가 동작하는 것은 새 회귀가 아닌 미적용 상태다.

2. **[사용자 Unity 테스트 필요] Raven 표시·연계 UI**  
   테스트 방법: 관련 Raven 구현/collection/Prefab 통합 후 소환·회수, 연계 제안/수락/종료, 화면 가장자리 및 pause를 짧게 확인한다. custom visual을 실제 연결한 경우에만 토글 재요청도 확인한다.  
   정상 결과: 구현된 소환 수명과 HUD가 일치하고 Sprite/그림자 표시가 함께 전환. F 제안이 Hero 머리 위·viewport 안에 보이고 종료/pause 시 숨음. frame 접지/크기, sequence 재생, CustomVisual의 요청/속도 연계가 맞음.  
   실패 시 확인할 증상: R 또는 숫자 안내 잔존, 사각 배너만 확대·회전, 말풍선 잘림, 숨은 Raven 그림자 잔존, 프레임 미재생. 이는 현재 §7에서 미리 확인된 데이터/계약 누락과 구분하여 판단한다.

3. **[사용자 Unity 테스트 필요] 카메라 위임**  
   테스트 방법: CameraFollow2_5D 구현을 통합·연결한 경우 방 입장과 BossEntrance/PlayerDeath 컷신 종료를 확인하고 disabled 경우 기존 경로와 비교한다.  
   정상 결과: enabled는 Step/컷신 후 clamp 경로, disabled는 기존 PaperWorld/SmoothDamp 경로가 작동하며 화면이 튀거나 대상을 잃지 않음.  
   실패 시 확인할 증상: 방 입장 snap 누락, 컷신 clamp로 대상 잘림, 컷신 후 카메라 복귀 이상. 새 컴포넌트 내부 소스가 없어 정확한 화면 경계 정책은 이번 분석으로 검증하지 못했다.

MovementSmoke는 선택적 이동 회귀 검사이며 새 소환·키 입력·UI 검사를 대체하지 않는다. 걷기 검사 삭제 후에도 성공 요약에 `walk`가 남으므로 그 문자열을 걷기 검증 증거로 사용하지 않는다.

## 9. 기존 예정 작업에 미치는 영향

| 예정 작업 | 현재 HEAD 적용 상태 및 이후 주의점 |
|---|---|
| 1. BossController | 영향 없음. 새 controller/보스 AI 변경 없음. 향후 루트 Dungeon 통합 시 BossEntrance 카메라 후처리 계약만 함께 확인 |
| 2. Boss Death/Clear | 영향 없음. 전멸→CompleteWave→Exit/HP 회복 및 사망 컷신 경로 유지 |
| 3. Boss Reward | 영향 없음. 새 보상 생성·선택·진행 gate 없음. 루트판에서는 기존 서비스/출구 진입 입력만 우클릭으로 바뀜 |
| 4. Raven | 현재 적용 영향 없음. 다만 루트판을 통합하려면 §7의 소환/표시/프레임 API 계약을 먼저 확인해야 함. 60초 소환 수명·애니메이션 FSM 구현이 업로드되었다고 가정하면 안 됨. 기존 Mark/OfferLink/ComboFollowup/TryLink 연계도 유지 대상 |
| 5. Passive 시스템 이식 | 영향 없음. RunBuild/AcquirePassive/보너스 API와 구현 위치 유지. UI의 Butterfly 설명 변경만으로 효과 계산 변경을 추정하면 안 됨 |

이 문서는 위 작업의 구현·이식·설계를 시작하지 않았다.

## 10. 다음 AI가 알아야 할 핵심

- HEAD `c218280dbb80d83b03e17b05ad92f8bd50df86a3`는 루트 C# 5개 추가다. **Assets/Scripts를 수정한 commit이 아니다.**
- 현재 게임은 기존 Shift 걷기/숫자 스킬/E 상호작용 및 기존 Raven/카메라/UI를 유지한다. 최신 파일 이름만 보고 기능 적용 완료로 판단하지 않는다.
- 루트판 계약은 걷기 제거, LeftShift 소환·F 연계·E/R 스킬·우클릭 상호작용, 단검 입력 Combo 1.2→2초다. WalkHeld 공개 프로퍼티도 제거된다.
- Player의 수동 논리 XY/Rect 충돌, 표시 XZ, facing/atlas/PresentationSequence 및 Dungeon.I 단일 Main 구조는 유지한다.
- ActorPresentation은 catalog/프레임 재생기/VisualScale을 추가하지만 Player의 atlas 교체는 아니다. AnimationRequested는 여전히 활성 CustomVisual 경로이며 토글·Raven sequence 재요청이 추가된다.
- Raven.ToggleSummon·소환 상태·표시 상태, SpriteAnimator/collection 및 관련 WorldDepth/PaperCard API가 현재 Assets에 없다. 5개만 가져오면 의존성이 부족하다.
- GameUIView는 새 CrowPrompt 배치/animation 필드를 추가하나 새 UI key는 없다. Prefab의 2/3/1/R, R 배너, Sprite 없는 기존 패널은 새 조작/말풍선 계약과 맞지 않는다.
- CameraFollow2_5D/PresentationSmoke도 없다. --presentation-test/--presentation-before/--crow-test는 루트 Dungeon에만 추가된 opt-in 경로다.
- MovementSmoke 변경은 걷기 검사 2개 삭제이며 새 기능 검사가 아니다. Assets판 검사는 그대로이고 루트판 로그의 walk 표현은 잔존한다.
- 코드·Scene·Prefab·기존 분석서·ProjectSettings를 수정하지 않았고 Unity 실행/빌드/commit/push도 하지 않았다. 새 기능 화면·입력은 미검증이며 §8은 통합 후 조건부 사용자 테스트다.
