# Western-Lemegeton 현재 프로젝트 구조 분석

- 분석 기준일: 2026-10-02 (Asia/Seoul)
- 대상: `E:\GitHub\Western-Lemegeton`
- 기준 Git HEAD: `d8eed843f7917ac88e6462cfb4ea426b7ab26b47` — “아스트라 + URP 작업 올립니다”
- 조사 방식: 파일 내용, C# 호출, Unity YAML의 fileID/GUID, Prefab instance override, Package/ProjectSettings, 프로젝트 내부 Git 기록 및 백업 ZIP을 읽기 전용으로 대조.
- Unity Editor/Play Mode/빌드/테스트/프로파일링/패키지 작업은 실행하지 않았다. 코드·Scene·Prefab·Material·Shader·SO는 변경하지 않았다. 생성물은 이 Markdown 한 파일이다.
- 시작 시 이미 `ProjectSettings/ShaderGraphSettings.asset`에 Git 수정 표시가 있었다. 이는 분석 이전 상태이며 본 작업에서 수정하거나 복원하지 않는다.
- 주요 분석 파일: **197개**. C# 58, Scene 1, Prefab 15, .asset 104(Assets 82 + ProjectSettings 22), Material .mat 1, Shader 1, Timeline .playable 3, JSON 4, Markdown 9, ProjectVersion .txt 1. .meta는 별도 참조 해석에 사용했으며 이 수에는 포함하지 않는다. PNG/WAV의 실제 재생·시각적 품질은 조사 대상에 포함하지 않는다.
- “현재 실제 사용 중”은 저장된 Main의 참조 및 그 참조에서 도달하는 코드 경로가 확인되었다는 뜻이다. 실행 성공 또는 모든 게임 기능의 완성도를 보증하는 표현은 아니다.
- 문서의 코드 경로는 프로젝트 루트 기준이다. `파일:행`은 분석 시점의 근거 위치다. 방법 이름도 함께 기재하여 이후 행 번호가 달라져도 추적할 수 있도록 했다.
- “발견되지 않음”은 현재 Assets/Packages/ProjectSettings에 해당 구현 또는 참조가 없다는 뜻이다. 외부 개발 브랜치·다른 프로젝트의 존재까지 부정하지 않는다.

## 1. 프로젝트 기본 정보

| 항목 | 확인 결과 | 실제 근거 |
|---|---|---|
| Unity | 6000.3.17f1, revision cf0352b38e81 | ProjectSettings/ProjectVersion.txt |
| Render Pipeline | URP 설정이 활성 연결됨 | GraphicsSettings.m_CustomRenderPipeline → Assets/Settings/PC.asset |
| URP | 17.3.0 | Packages/manifest.json 및 packages-lock.json |
| URP Core / ShaderGraph | 모두 17.3.0 | packages-lock.json |
| Universal config | 17.0.3 | packages-lock.json |
| Timeline | 1.8.12, 실제 컷신 3개 연결 | manifest 및 CinematicDirector.assets/Main YAML |
| Unity UI | com.unity.ugui 2.0.0, Canvas 기반 | manifest 및 GameUI.prefab |
| Input | 기존 UnityEngine.Input/KeyCode, Active Input Handling=0 | ProjectSettings.asset:688, Player.Update, StandaloneInputModule |
| New Input System | 패키지·.inputactions·PlayerInput 발견되지 않음 | manifest/lock 및 Assets 전체 검색 |
| Cinemachine | 패키지·Scene/Prefab 컴포넌트·사용 코드 발견되지 않음 | manifest/lock, Dungeon.LateUpdate |
| Spine | 실제 runtime/DLL/데이터 발견되지 않음. 조건부 어댑터만 존재 | SpineAnimationDriver.cs, scriptingDefineSymbols={} |
| Addressables | 패키지·설정·호출 발견되지 않음 | manifest/lock 및 Assets |
| NavMesh | ai.navigation/NavMeshAgent/Surface/베이크 데이터 발견되지 않음 | Assets 코드/YAML, manifest/lock |
| NavMesh 기본 설정 | NavMeshAreas.asset의 Walkable/Not Walkable/Jump 등 기본 영역은 존재 | ProjectSettings/NavMeshAreas.asset |
| DOTS/ECS | Entities 패키지·ECS gameplay 구현 발견되지 않음 | manifest/lock, C# 전체 |
| Burst/Collections/Mathematics | 1.8.29 / 2.6.6 / 1.3.3, URP의 간접 의존성 | lock depth=2; gameplay ECS 사용과 구분 |
| 기타 간접 패키지 | Searcher 4.9.4, Test Framework 1.6.0, Performance 3.5.0, NUnit 2.0.5, Mono.Cecil 1.11.6 | packages-lock.json |
| Unity 모듈 | animation/audio/director/imageconversion/imgui/jsonserialize/physics2d/screencapture 직접 의존; physics/terrain/UI/particlesystem 일부 간접 의존 | manifest/lock |

ProjectSettings.m_ActiveColorSpace=0(Gamma)이다.

Physics2D/3D 모듈이 설치되어 있다는 사실과 게임에서 물리를 사용하는 사실은 다르다. 현재 이동·전투는 Unity Physics API 대신 Vector2/Rect 계산을 사용하며, 조사한 모든 Scene/Prefab에서 Rigidbody/Rigidbody2D/Collider/Collider2D가 발견되지 않았다.

EditorSettings.m_SerializationMode=2로 텍스트 직렬화를 사용하므로 Scene/Prefab을 YAML로 분석할 수 있었다. 자체 asmdef·외부 gameplay DLL은 발견되지 않았다. 일반 코드는 Assembly-CSharp, Editor 폴더 코드는 Editor 영역에 속한다.

## 2. 전체 폴더 구조

```text
Western-Lemegeton
├─ Assets
│  ├─ Branding                         게임 로고 PNG
│  ├─ Scenes
│  │  └─ Main.unity                     유일한 활성 게임 Scene
│  ├─ Scripts
│  │  ├─ Dungeon / ExpeditionProgress / ExplorationStage / RoomDoor
│  │  ├─ Player / HunterLocomotion / HunterMotion
│  │  ├─ Enemy.cs                       Enemy + Bullet
│  │  ├─ Raven / RunBuild / CombatRules
│  │  ├─ GameHUD                        UI 상태와 빌드 이벤트 구독
│  │  ├─ PaperWorld / PaperCard / WorldDepth
│  │  ├─ CombatArt / WesternEnvironment / FrontierDressing
│  │  ├─ Ink / SkillEffects / PropGrounding
│  │  ├─ WesternAtmosphere / SceneryOccluder / LandscapeParallax
│  │  ├─ *Smoke.cs                      명시적 실행 인자용 검사 코드
│  │  ├─ BuildHUD / CinematicHUD / ExplorationHUD
│  │  │                                 현재 주석만 남은 레거시 파일
│  │  ├─ Authoring
│  │  │  ├─ GameSceneBindings / RoomAuthoring / WorldFootprint
│  │  │  ├─ WorldProp / AuthoredAtmosphere
│  │  │  ├─ ActorPresentation / ActorAnimationDriver / SpineAnimationDriver
│  │  │  └─ GameUIView / UICommand / PassiveSlotView / VisualCatalog
│  │  └─ Cinematics
│  │     └─ CinematicDirector / CinematicClip / CinematicTrack
│  ├─ Editable
│  │  ├─ Actors                         Hunter/Raven/Enemy_0~2/Boss Prefab
│  │  ├─ Rooms                          Hirva/Room_01~06 Prefab
│  │  ├─ UI                             GameUI/PassiveSlot/VisualCatalog/UI_Default
│  │  └─ Shared                         직렬화된 Sprite/Texture/Material
│  ├─ Resources
│  │  ├─ Combat                         캐릭터·아이콘·타이틀 PNG 25개
│  │  ├─ Motion                         HunterSkills/HunterLocomotion, CartoonAtlas
│  │  ├─ Western                        오브젝트 PNG 41개, bounds.json
│  │  └─ Cinematics                     입장/보스 등장/플레이어 사망 Timeline+WAV
│  ├─ Settings                          PC URP Asset/Renderer/GlobalSettings
│  ├─ URPDefaultResources               품질별 URP Asset 5개/Forward Renderer
│  └─ DefaultVolumeProfile.asset
├─ Packages                             manifest + lock
├─ ProjectSettings                      24개 설정 파일
├─ Docs                                 기존 연결·모션·레벨 문서 6개 + 본 보고서
├─ Backups
│  ├─ BeforeEditableScene-20260922.zip   이전 프로토 스크립트·Scene 보관
│  └─ IncompleteEditableMigration       이전 Shared 에셋 보관
├─ Previews / Verification / Logs        기존 시각 자료·검증 산출물 영역
└─ Library / UserSettings                로컬 캐시·에디터 상태
```

Player/Enemy/Boss/Raven/Combat/Weapon/Passive/Stigma/Dungeon/Stage/Room/Reward/Data/Animation/VFX라는 독립 시스템 폴더 대부분은 없다. 관련 책임이 Scripts의 단일 클래스와 Editable/Resources로 분산되어 있다. 특히 무기·피해·입력·체력은 Player.cs, 보스는 Enemy.cs, 패시브/성흔은 RunBuild.cs에 있다. Save/Reward/Currency 전용 폴더와 StreamingAssets는 현재 프로젝트에서 발견되지 않았다.

Assets에는 Scene 1개, Prefab 15개, C# 58개, PNG 69개, WAV 3개, Shader 1개가 있다. Shared의 .asset 대부분은 gameplay SO가 아니라 Unity Sprite/Texture/Material이다.

## 3. Scene 구조 분석

### 3.1 실제 메인 Scene 판정

`ProjectSettings/EditorBuildSettings.asset`에 enabled=1인 `Assets/Scenes/Main.unity` 한 개만 등록되어 있다. Assets의 .unity 전수 조사도 Main 한 개다. `ProjectSetup.CreateScene`은 같은 경로를 빌드 등록하고, `Dungeon.Boot`는 저장된 Dungeon이 없는 경우 Main을 열라는 오류만 기록한다. 따라서 Main은 Build Settings와 코드 참조로 확인된 유일한 플레이 진입 Scene이다.

“타이틀 Scene”, “타운 Scene”, “보스 Scene”은 별도 파일이 아니라 Main 안의 RunState와 오브젝트 활성화 상태이다.

### 3.2 저장된 Root 및 주요 배치

```text
Main
├─ Main Camera                 Camera + AudioListener
├─ Game_Systems                Dungeon, GameSceneBindings, RunBuild, GameHUD,
│                              CinematicDirector, PlayableDirector, AudioSource
├─ World
│  ├─ Hirva                    마을 Prefab instance
│  └─ Room_01 ~ Room_06         6개 방 Prefab instance
├─ Actors
│  ├─ Hunter                   Player Prefab instance
│  ├─ Raven                    Raven Prefab instance
│  └─ Spawned_Enemies          실행 중 적 생성 부모
├─ GameUI                      GameUI Prefab instance / Canvas
├─ EventSystem                 EventSystem + StandaloneInputModule
└─ Viewport_Backdrop            검은 배경 Camera
```

Main YAML에는 직접 저장된 GameObject 7개와 PrefabInstance 10개가 있다. 10개는 Hirva+6방+Hunter+Raven+GameUI이며, 실제 전체 배치는 연결된 Prefab YAML까지 읽어야 확인된다. Enemy/Boss는 Main에 미리 배치된 instance가 없고 GameSceneBindings.EnemyPrefabs에 에셋 참조 4개가 있다.

Game_Systems의 Scene 바인딩은 다음과 같다.

| 연결 | Main YAML 값 |
|---|---|
| Dungeon.Scene | fileID 1954788953(GameSceneBindings) |
| GameSceneBindings.Hunter | fileID 1276339837, Hunter prefab의 Player |
| Raven | fileID 1112908258 |
| GameCamera | fileID 1330023418 |
| EnemyContainer | fileID 217853482 |
| UI / GameHUD.View | fileID 2088121561 |
| EnemyPrefabs 순서 | Enemy_0, Enemy_1, Enemy_2, Boss |
| Rooms 순서 | Room_01, Room_02, Room_03, Room_04, Room_05, Room_06 |
| CinematicDirector.assets 순서 | DungeonEntrance, PlayerDeath, BossEntrance |

근거: Main.unity의 Game_Systems 컴포넌트 블록(약 1020~1227행), prefab m_SourcePrefab 및 stripped MonoBehaviour fileID.

### 3.3 각 영역의 목적과 실행 상태

| 영역 | 목적 / 주요 객체 | Player·적·UI 및 상태 |
|---|---|---|
| Hirva | Ground_and_Paths, Scenery, Doors, Gameplay_Markers, Atmosphere | Hunter/Raven은 Actors에 유지. 적 없음. Title→Town 흐름의 마을 |
| Room_01 | 붉은 바위 입구, RoomIndex=0, 02번 연결문 | 전투방. StartRun 진입 대상 |
| Room_02 | 마차 잔해 전투지, Index=1, 01/03 연결문 | 전투방 |
| Room_03 | 성흔 제단, Index=2, 02/04/05 연결문 | 전투 웨이브 없음. ServicePoint 상호작용→SigilChoice |
| Room_04 | 악마카드 역참, Index=3, 03 연결문 | 카드 무료 선택→CardChoice |
| Room_05 | 풍차 언덕, Index=4, 03/06 연결문 | 전투방 |
| Room_06 | 스테이지 출구, Index=5, 05 연결문+StageExit | 일반 스테이지는 전투방; 최종 스테이지 마지막 웨이브만 보스 |
| GameUI | Title/HUD/Route/Sigil/Card/Pause/Settings/Results/Cinematic/Toast | RunState와 GameHUD 상태에 따른 패널 표시 |
| Spawned_Enemies | 비어 있는 생성 부모 | SpawnWave에서 Enemy prefab Instantiate |
| Custom_Visual_Spine_Or_Animator | 캐릭터별 비활성 자식 | custom visual용 슬롯. 현재 비어 있는 기반 |

Room Scene instance의 XZ 중심은 각각 (0,0), (60,0), (60,44), (120,44), (60,88), (120,88)이며 RoomGraph.Centers와 일치한다. 마을 중심은 (0,0)으로 첫 방과 겹치지만 동시에 활성화하지 않는다.

Main과 모든 Prefab에서 Directional/Point/Spot Light, Light2D, Volume, NavMesh/물리 컴포넌트는 발견되지 않았다. 테스트 MonoBehaviour는 저장된 Scene/Prefab에 붙어 있지 않다. Smoke 코드는 명시적 실행 인자가 있을 때 Dungeon.Awake에서 추가된다. “임시”라고 이름 붙은 별도 Scene 객체는 발견되지 않았으며 custom visual 슬롯과 procedural fallback은 개발 기반으로 구분한다.

## 4. 전체 시스템 아키텍처

핵심은 하나의 Scene 안에서 Dungeon이 상태를 관리하고, 나머지 runtime 시스템이 Dungeon.I로 상태·Player·Raven·Enemies·Build·Camera에 접근하는 구조다. 입력·이동·무기·전투·HP가 세분화된 Manager 클래스 대신 Player에 통합되어 있다.

| 시스템 / 핵심 클래스 | 역할 | 관련 Prefab / SO | 실제 연결·사용 / 테스트 구분 |
|---|---|---|---|
| 게임 총괄: Dungeon | Title/Town/Combat/Exit/Route/서비스/사망/승리 상태, 생성·진행·오디오·카메라 | Main.Game_Systems; gameplay SO 없음 | Main에 1개. Scene 바인딩과 같은 객체의 컴포넌트 참조 |
| 바인딩: GameSceneBindings | Scene 객체·적 Prefab 배열 제공 | Main; 6 actors/7 rooms/GameUI 참조 | Dungeon.Scene 필수, 실제 직렬화 사용 |
| Player | 입력, 이동, 공격, 무기 태그, HP·무적·대시·스킬 | Hunter.prefab; 데이터 SO 없음 | Dungeon.Hero, Enemies, Crow, Build, Rules 직접 연결 |
| Movement: HunterLocomotion/Rules | 실제 이동 거리 기반 모션, Rect 충돌·방 경계 | Hunter + WorldFootprint를 갖는 Rooms | Player가 일반 C# 객체/정적 함수 호출 |
| Input | Input.GetKey/GetMouseButtonDown | Player/Dungeon/EventSystem | 기존 키 입력. 별도 InputController 없음 |
| Weapon/Attack | Weapon 정수 0/1, 기본 4타, 스킬 1~4, 탄약 | Hunter + atlas PNG; 무기 Prefab/SO 없음 | Player 내부 분기. Gun/Dagger 별도 클래스 없음 |
| Combat/Damage/Health | 거리·원뿔·선분 판정, HP 감소 | Enemy prefabs/Hunter | Player.Hit→Enemy.TakeDamage, 적→Player.Hurt |
| ComboTracker | 대상 ID별 1~4단계와 attackId/4초 제한 | 없음 | Player.tracker; 런·안전 진입 시 Clear |
| Bullet | 직접 좌표 이동·substep 탄환 판정 | 전용 Prefab 없음 | Enemy.cs의 추가 클래스; Bullet.Spawn이 runtime 생성 |
| Enemy/AI/Boss | Kind별 근접/돌진/원거리/보스 패턴 | Enemy_0~2/Boss.prefab; SO 없음 | Dungeon.Enemies 리스트 및 Hero 추적. 공용 Enemy 클래스 |
| Raven | 추적·명령·연계·콤보 돌진 | Raven.prefab; SO 없음 | Player/Crow 직접 호출, Dungeon.Nearest/Build 사용 |
| Passive/Inventory: RunBuild | 정의 catalog, 획득 목록, 등급·출처, bonus 합산 | PassiveSlot/GameUI; PassiveSO 없음 | Dungeon.Build, Player/Raven 계산, GameHUD 이벤트 |
| Stigma/성흔: RunBuild | Fire/Nature/Butterfly stack/threshold/effect level | GameUI의 Stigma_Build_Tree; SO 없음 | ClaimSigil→stack→Player/Enemy/Raven 효과 |
| Dungeon/Stage: ExpeditionProgress | 5 segment, 6 map, 웨이브·방문·완료 | Main의 6개 방 재사용 | Dungeon.Progress 일반 C# 인스턴스 |
| Room: RoomAuthoring/RoomDoor/WorldFootprint | XZ marker→논리 좌표 변환, 문·spawn·장애물 바인딩 | Hirva/Room_01~06 | Scene 참조 사용. RoomGraph가 논리 인접성 확인 |
| Reward | 성흔·카드 서비스 선택, 전투방 HP+18 | GameUI 선택 패널 | Dungeon.ClaimSigil/ClaimCard 구현. 보스 drop/spawner 없음 |
| Currency | Supplies 필드·표시 | GameUI Supplies text | reset/표시만 확인. 경제 시스템 발견되지 않음 |
| UI: GameHUD/GameUIView/UICommand/PassiveSlotView | 이벤트 알림과 패널·HUD·버튼·툴팁 | GameUI/PassiveSlot/VisualCatalog SO | 이벤트+프레임 polling+직접 함수 호출 혼합 |
| Save | 저장/로드 구현 | 없음 | PlayerPrefs/저장 파일 gameplay 경로 발견되지 않음 |
| Data | 코드 상수·직렬화 marker·일반 C# 상태·비주얼 catalog | VisualCatalog.asset, bounds.json, Shared assets | Gameplay balance는 코드. JSON은 아트 crop 데이터 |
| SceneManager 계열 | gameplay Scene 전환 | 없음 | runtime LoadScene 발견되지 않음. EditorSceneManager는 편집 도구에만 |
| Audio | 합성 타격/총격, Timeline WAV | Main.AudioSource, Resources/Cinematics | Dungeon.Sound와 CinematicDirector가 같은 AudioSource 사용 |
| Animation | cel atlas+거리 gait+상태 어댑터 | HunterSkills/HunterLocomotion PNG, ActorPresentation | Sprite 실제 사용. Animator/Spine 연결은 비어 있음 |
| Camera | 고정 pitch+위치 smooth follow+Timeline zoom | Main의 카메라 2개 | Dungeon/PaperWorld/CinematicDirector |
| VFX | 선·링·스파크·스킬·먼지·접지 shadow | Rooms 내 sprite 및 runtime 객체 | Ink/Fade/SkillEffects/AuthoredAtmosphere/WorldProp |
| URP | 품질별 Pipeline/Renderer 설정 | PC/PC_Renderer/GlobalSettings | 설정 참조 있음. 실제 조명 gameplay 없음 |

시스템별 데이터 SO는 대부분 존재하지 않는다. 현재 자체 ScriptableObject 클래스는 VisualCatalog이며, TimelineAsset/PlayableAsset 계열은 컷신 데이터이고 Passive/Weapon/Enemy 설정 SO가 아니다.

## 5. 클래스 의존관계 및 연결 방식

```text
Main.Game_Systems
├─ Dungeon.I                           static 공유 접근점
│  ├─ Scene → GameSceneBindings         Inspector/fileID
│  │  ├─ Hunter → Player               Hunter Prefab instance
│  │  ├─ Raven → Raven                 Raven Prefab instance
│  │  ├─ GameCamera → Camera
│  │  ├─ Hirva / Rooms[] → RoomAuthoring
│  │  ├─ EnemyPrefabs[0..3] → Enemy
│  │  ├─ EnemyContainer → Transform
│  │  └─ UI → GameUIView
│  ├─ Build → RunBuild                  GetComponent(Awake)
│  ├─ hud → GameHUD                     GetComponent(Awake)
│  ├─ Cinematics → CinematicDirector    GetComponent(Awake)
│  ├─ AudioSource                      GetComponent(Awake)
│  ├─ Progress → ExpeditionProgress    new / MonoBehaviour 아님
│  └─ Enemies[]                        Instantiate 결과 리스트
├─ RunBuild
│  ├─ PassiveDefinition catalog        Dictionary<string,...>
│  ├─ OwnedPassive inventory           List
│  └─ StackChanged / PassiveAcquired / Reset → GameHUD
├─ GameHUD
│  ├─ observed → Dungeon.I.Build        C# event 구독/해제
│  └─ View → GameUIView                Inspector
└─ CinematicDirector
   ├─ PlayableDirector/AudioSource      GetComponent
   ├─ assets[enum index] → TimelineAsset
   └─ PlayableDirector.stopped → OnStopped

Player
├─ art → SpriteRenderer                SerializeField
├─ HunterLocomotion                    readonly 일반 C# 객체
├─ ComboTracker                        readonly 일반 C# 객체
├─ Dungeon.I.Build                     공격/이동 bonus
├─ Dungeon.I.Enemies                   직접 순회
├─ Dungeon.I.Crow                      Mark/OfferLink/ComboFollowup/Command/TryLink
├─ Rules                               Rect/거리/원뿔 정적 계산
├─ HunterAtlas/HunterLocomotionAtlas    프레임·Material 정적 cache
└─ Ink/SkillEffects/Bullet              정적 생성 함수

Enemy / Raven / Bullet
└─ Dungeon.I → Player, Enemies, Build, RunState, RoomOrigin

GameUIView
├─ References[key] → GameObject         Inspector 목록→Dictionary
├─ Element<T> → GetComponent<T>        키별 UI 컴포넌트 조회
├─ Dungeon.I → Player/Crow/Progress/Build
├─ GameHUD                             매 프레임 GetComponent
├─ Art → VisualCatalog                 SO 참조
└─ PassivePrefab → PassiveSlotView     Inspector Prefab→Instantiate

Button.onClick / Slider.onValueChanged
└─ UICommand.Execute / SetVolume       serialized UnityEvent
   └─ Dungeon.I / GameHUD 함수 직접 호출
```

모든 연결은 현재 C#/YAML 근거이며, PlayerController/PlayerCombat/WeaponManager/PlayerHealth/PassiveManager로 나뉜 구성은 발견되지 않았다.

GetComponent는 동일 GameObject의 시스템 결합과 UI 요소 조회에 사용된다. Scene 연결은 GameObject 이름 검색보다 GameSceneBindings의 직렬화 참조가 중심이다. Runtime 글로벌 검색은 Boot 확인 및 transient cleanup에 남아 있다. Transform.Find/계층 이름을 이용한 gameplay 연결은 발견되지 않았다.

ActorPresentation은 root의 Player/Raven/Enemy를 Awake GetComponent로 확인하고, Inspector의 SpriteFrames/SpriteCard/CustomVisual을 연결한다. AnimationRequested는 UnityEvent이고 실제 Prefab의 persistent listener는 비어 있다. VisualCatalog는 Inspector SO 참조, 아트 atlas는 Resources.Load와 static cache를 사용한다.

## 6. 하드코딩 구조 전수 조사

조사 범위는 Assets의 C# 58개 전체다. 항목은 코드가 갖는 고정 의존성을 기록하며, 문제 여부를 자동 판정하지 않는다. 반복되는 개별 숫자는 기능별 위치와 의미로 묶었다.

### 6.1 탐색·문자열·계층 의존

| 분류 | 위치 | 현재 고정 의존 / 실행 시점 |
|---|---|---|
| [위험한 런타임 탐색] | Dungeon.cs:30~31 Boot | FindAnyObjectByType<Dungeon>. Scene 로드 후 1회 존재 확인. 객체 생성 없음 |
| [위험한 런타임 탐색] | Dungeon.cs:49~50 ClearTransient | FindObjectsByType<Bullet/Fade/SkillEffects> 전역 검색→Destroy. 방 진입·웨이브 clear·reset·컷신 시작 |
| [임시/테스트 코드] | RuntimeSmoke.cs:213 | GameObject.Find("Crow combo indicator")로 생성 결과 검사. 일반 플레이 경로 아님 |
| [Scene 의존] | Dungeon.cs:31,34 | Main 경로 안내, GameSceneBindings 필수, 없어도 새 게임 root를 자동 생성하지 않음 |
| [Scene 의존] | Editor/ProjectSetup.cs:12~18 | Main 파일 경로·활성 Scene 경로·FindFirstObjectByType 바인딩 확인. Editor 명령 |
| [Scene 의존] | Editor/Authoring/EditableValidation.cs:14~20 | Main 열기/전역 Sprite 검색/연결 검사. 검사 실행 안 함 |
| [Hierarchy 의존] | Dungeon.cs:35~38; CinematicDirector.cs:30~35 | Dungeon/Build/HUD/Director/AudioSource가 같은 Game_Systems에 있어야 함 |
| [Hierarchy 의존] | WorldDepth.cs:9; PaperCard.cs:18 | root/조상 SortingGroup에 의해 렌더 정렬 결정 |
| [Hierarchy 의존] | ActorPresentation.cs:24 | 같은 root의 Player/Raven/Enemy로 presentation 종류 판정 |
| [Hierarchy 의존] | GameUIView.cs:99~104 | 획득 순서와 slots 순서 대응. Prefab의 Icon/Border/Tooltip 참조 필요 |
| [Hierarchy 의존] [임시/테스트 코드] | AuthoringSmoke.cs:34 | PassiveContent.GetChild(i)가 획득 순서와 같다는 검사 |
| [콘텐츠 하드코딩] | GameUIView.cs:19~22 및 39~104 | "HP","BossHP","SkillIcon0","SigilNode0" 등 UI 키 문자열 계약. GameObject.Find가 아니라 저장된 Dictionary 키 |
| [콘텐츠 하드코딩] | Ink.cs:40 | 이름 Shadow/Trace/Attack warning/bullet와 sortingOrder<0으로 flat card 판정 |
| [콘텐츠 하드코딩] | PaperCard.cs:20~24 | sprite 이름 Wnn/OBJ/Deco/Fence 접두어로 scenery/grounding 판정 |
| [콘텐츠 하드코딩] | WesternEnvironment.cs:57~99 | Wnn1/Wnn8/OBJ10/Deco/Fence 등 에셋 ID별 크기 switch 및 floor 특별 분기 |
| [콘텐츠 하드코딩] | PropGrounding.cs:30~36,54~103 | Wnn1/OBJ10/Deco4/Deco9/Fence별 painted contact 좌표·그림자 조합 |
| [설계상 의도된 고정값] | GameUIView.cs:13,23 | 시스템 폰트 Malgun Gothic/Arial, FontOverride 우선 |
| [콘텐츠 하드코딩] | ActorPresentation.cs:20,29~43 | motion state→animation name 배열, skill 번호→이름 배열, Animator layer 0 |

GameObject.Find는 현재 정상 gameplay 코드에서 발견되지 않았고 Smoke 검사 1곳만 확인되었다. Transform.Find/FindObjectOfType/FindFirstObjectByType의 일반 runtime gameplay 호출은 발견되지 않았다. `List.Find(predicate)`와 Spine `FindAnimation`은 GameObject 전역 탐색이 아니므로 구분했다.

### 6.2 Resources 경로 전체

| 위치 | 고정 경로 / 콘텐츠 | 사용 구분 |
|---|---|---|
| CombatArt.cs:9 | Combat/ + name | Texture 호출 때 load. Get(Sprite)는 Dictionary cache |
| WesternEnvironment.cs:20,23 | Western/bounds, Western/ + name | bounds/sprite 첫 load cache |
| HunterMotion.cs:60~64 | Motion/HunterSkills, Motion/CartoonAtlas | atlas/material 첫 load cache |
| HunterLocomotion.cs:55 | Motion/HunterLocomotion | atlas 첫 load cache |
| CinematicDirector.cs:35 | Cinematics/DungeonEntrance, PlayerDeath, BossEntrance | Inspector assets 누락 시 Awake fallback |
| RunBuild.cs:47 | definition.Icon→CombatArt.Texture | 등록 시 아이콘 존재 검증 |
| EditableProjectSetup.cs | Cinematics 3개, Combat LoadAll, Assets/Editable/... | migration/export tool |
| MotionSetup.cs | Motion 2개 atlas, Previews/... | 검증·가이드 산출 도구 |
| ProjectSetup.cs | Western LoadAll, Combat.Get ID 목록, Assets/Branding/WnnLogo.png, Builds/... | Editor 빌드/검증 도구 |
| TimelineSetup.cs | Assets/Resources/Cinematics/{이름}.playable/.wav | Editor 컷신 생성/검증 |
| EditableUIBaker.cs | LegacyRuntime.ttf, Title/아이콘 IDs, Editable/UI/PassiveSlot.prefab | Canvas migration |
| AuthoringFinalization.cs | Shared/Sprite_0005, SolidWhite*, UI_Default, UI/Default | 에셋 마무리 도구 |

앞 6개 행은 [콘텐츠 하드코딩], 뒤 Editor 도구는 [설계상 의도된 고정값] 또는 [임시/테스트 코드]이다. 매 프레임 Resources.Load 경로는 발견되지 않았다. Resources.Texture 자체에는 load 호출이 있지만 Sprite/atlas 캐시가 반복 읽기를 제한한다.

### 6.3 인덱스·게임 규칙·수치 고정

| 분류 | 위치 | 내용 |
|---|---|---|
| [정상적인 상수] | PaperWorld.cs:11~13 | LogicLayer=30, Pitch=47, XY→XZ 좌표 계약 |
| [정상적인 상수] | HunterMotion.cs:10~17; HunterLocomotion.cs:9,48 | 공통 타격 시간, atlas 6×4/8×4, stride/settle 시간 |
| [설계상 의도된 고정값] | Rules.cs:9~15,25~29 | 탄약 1/2 교대, 방 ±15/±9, 충돌 substep, 경계 반경 |
| [콘텐츠 하드코딩] | ExpeditionProgress.cs:7~9,17~34 | 6방 좌표·링크·서비스방 index 2/3, 5스테이지, 보스 segment=4/map=5/last wave |
| [Hierarchy 의존] [콘텐츠 하드코딩] | Dungeon.cs:66,125~127 | Rooms 배열 순서=MapIndex; EnemyPrefabs[kind], 0~2 일반/3 보스; 보스 Enemies[0] |
| [콘텐츠 하드코딩] | Dungeon.cs:20~21,117~120 | MapNames/RoomNames, 카드 3 ID, 카드 등급 Room/2 |
| [콘텐츠 하드코딩] | RunBuild.cs:22,31~33,53~56 | 3성흔 배열, 3패시브 등록, ID별 +8%/+8%/+12%, rarity 배수 |
| [콘텐츠 하드코딩] | Player.cs:6~8,98~158 | HP100/탄약6, Weapon 0/1, cds[2,2], 4 combo, 스킬 1~4 분기·피해·범위·재사용 |
| [콘텐츠 하드코딩] | Enemy.cs:8,18,35,50~75 | Kind별 HP/공격력/속도/패턴, 보스650/절반 HP12→20 탄환 |
| [콘텐츠 하드코딩] | Raven.cs:15,20~22,35,83~108 | 활동7/복귀9, 명령5초+stack/.75, CD10, 링크 피해·범위·CD8, 최대2 콤보 대상 |
| [설계상 의도된 고정값] | Dungeon.cs:81~108,124~156 | 문2.1/서비스2.4 거리, 웨이브 수/적 수, 웨이브 휴식2초, 회복18, follow 계수·clamp·FOV |
| [설계상 의도된 고정값] | Player.cs:40~52; Dungeon.cs:137~144 | WASD/Shift/Space/Q/1/R/2/3/좌클릭/E/M/Esc/P 키 직접 지정 |
| [콘텐츠 하드코딩] | WesternEnvironment.cs:157~251; ExplorationStage.cs:15~35; FrontierDressing.cs | town/room 템플릿·좌표·오브젝트 ID·색·방 번호별 배치. 현재 주로 migration/Editor용 |
| [설계상 의도된 고정값] | PropGrounding.cs; AuthoredAtmosphere.cs; SkillEffects.cs; Ink.cs | 접지 anchor, 먼지 wrap 범위, 링/선 개수, 색과 수명 등 표현 수치 |
| [임시/테스트 코드] | *Smoke.cs | 고정 진행 경로, HP500/5000/100000 피해, 적 위치 강제 변경, 테스트 seed·로그 경로 |
| [임시/테스트 코드] | Dungeon.cs:40~44 | --authoring-test/--level-test/--movement-test/--motion-test/--smoke-test |
| [임시/테스트 코드] | Dungeon.cs:110 | ChooseEvent(int)=>false. 현재 호출하는 gameplay/UI 경로 발견되지 않음 |
| [설계상 의도된 고정값] | UICommand.cs:11~26 | UIAction switch. 콘텐츠 효과 registry가 아니라 버튼 명령 dispatch |
| [콘텐츠 하드코딩] | CinematicDirector.cs; CinematicClip.cs | 컷신 enum 순서/이름/제목, zoom 27/30, 고정 cue channel |
| [정상적인 상수] | Ink.cs:8~10 | Gold/Cyan/Red 공통 색 |
| [임시/테스트 코드] | Dungeon.Tone, TimelineSetup.WriteAudio | 파형 기반 임시 타격/컷신 사운드 생성 |

무기 이름 문자열 비교는 발견되지 않고 정수 Weapon 분기다. 캐릭터 이름으로 Player 종류를 찾는 코드는 없지만 CombatArt의 "Hunter"/"Raven"/"Enemy" 문자열은 아트 ID다. 특정 Scene별 gameplay 분기는 없고 RunState/RoomKind/인덱스별 분기가 있다.

Dictionary는 RunBuild.Awake에서 3개 콘텐츠를 직접 Register하는 방식이며, 별도의 effect registry는 없다. 현재 새로운 passive ID를 등록해도 Bonus에 이름별 적용 코드가 없으면 해당 ID의 추가 효과는 발생하지 않는 구조다. 이는 현재 연결 사실이며 변경 제안이 아니다.

## 7. 2.5D 구조 집중 분석

### 7.1 논리와 표시의 서로 다른 좌표계

| 항목 | 현재 구조 / 근거 |
|---|---|
| Player 이동 | transform.position의 x/y를 Vector2로 사용. z는 논리 높이나 전투 축으로 사용하지 않음. Player:62~75 |
| Enemy 이동 | XY Vector2+Rules.ResolveObstacles. Enemy:37~62 |
| Raven/Bullet 이동 | XY Vector2 보간/직접 위치 누적. Raven.Update/Bullet.Update |
| 게임의 충돌 | 수동 Rect 확장·점 포함·거리·원뿔·선 폭 판정. Physics2D/Physics simulation 호출 없음 |
| Rigidbody/Collider | 모든 저장 Scene/Prefab에서 2D/3D 양쪽 모두 발견되지 않음 |
| 표시 지면 | PaperWorld.Point(p,h)=(p.x,h,p.y), 즉 XZ |
| Room/prop 저장 위치 | 실제 편집용 World와 marker는 XZ. RoomAuthoring.Point/Center는 x/z를 논리 x/y로 변환 |
| Y 높이 | 표시 세계의 Y는 ground offset/RenderOffset 등. gameplay root의 y는 평면 세로 방향 |
| Mouse 조준 | Camera ray와 y=0 XZ plane 교점→(x,z)→논리 Vector2 |

따라서 “3D XZ 이동 + 3D Physics”로 정의하면 현재 코드와 다르다. 3D 좌표는 화면 표현과 편집용 배치에 사용되고, 이동·전투 평면 계산은 XY이다.

### 7.2 root/visual 분리

```text
Hunter / Raven                          논리 XY root
├─ Player 또는 Raven + WorldDepth + SortingGroup + ActorPresentation
├─ Shadow                               논리 Source SpriteRenderer / layer 30
│  └─ PaperCard → View_Shadow
├─ Sprite_Frames                        논리 Source SpriteRenderer / layer 30
│  └─ PaperCard → View_Sprite_Frames
├─ Rendered_Sprites_2_5D                 렌더 자식 폴더
│  ├─ View_Shadow                       XZ 지면, pitch90, offset Y .015
│  └─ View_Sprite_Frames                 XZ 변환 위치, pitch47
└─ Custom_Visual_Spine_Or_Animator       비활성 SortingGroup, 현재 custom visual 미사용
```

PaperCard.Visible는 Prefab에서 명시적으로 연결되어 있다. 실행 중 root가 XY로 이동해도 Visible의 월드 위치는 LateUpdate에서 XY→XZ로 다시 설정한다. Source SpriteRenderer는 Layer30이고 Main Camera cullingMask에서 제외된다. 표시 Renderer가 gameplay 위치·공격 계산을 대체하지 않는다.

Enemy/Boss는 root 아래 Enemy art wrapper 및 실제 Enemy art source, HP background/HP, Attack warning, Rendered_Sprites_2_5D, custom slot이 있는 동일 계열이다. Boss는 crown source/view가 추가되고 art scale이 1.9다. Rigidbody/Collider 자식은 없다.

### 7.3 렌더러·billboard·방향

현재 SpriteRenderer를 사용한다. MeshRenderer/SkeletonAnimation/SkeletonMecanim/Animator 직렬화 컴포넌트는 조사한 Scene/Prefab에서 발견되지 않았다. SpineAnimationDriver는 조건부 연결 기반이다.

PaperCard:41과 ActorPresentation:36은 `Quaternion.Euler(PaperWorld.Pitch,0,0)`에 논리 z 회전을 곱한다. 카메라의 실제 look direction을 매 프레임 읽어 LookAt하는 billboard가 아니라 **같은 고정 pitch47을 공유하는 카드 회전**이다. flat sprite는 pitch90으로 지면에 놓는다.

| 방향 | 실제 처리 |
|---|---|
| Player 이동 표시 | HunterLocomotion의 8방향 Sector→4행 atlas+좌우 flipX |
| 기본 단검/스킬 방향 | LastMove/Facing 또는 시전 시 캡처한 basicDirection/skillDirection |
| 총 조준 | Mouse ray 결과의 aim.normalized. 이동 모션의 flip과 독립 |
| 액션 표시 | MotionDirection.x<-.05로 flipX, lean 로컬 z 회전 |
| Enemy | 논리 aim으로 attack warning 회전. 적 이동에 따른 sprite flip/방향 atlas는 발견되지 않음 |
| Raven | goal/end.x와 현재 x 비교로 flipX, sin 기반 날개 느낌 z rotation |
| Custom visual | SpriteFrames.flipX에 맞춰 localScale.x 부호 적용 |
| 실제 Collider 방향 | Collider 자체가 없어 해당 방향 분리 항목은 적용되지 않음 |

Player 이동 시 총의 조준 Facing은 이동 animation 방향과 분리될 수 있다. 공격 판정은 renderer flip이 아니라 캡처된 Vector2 방향이다. Root Transform 전체를 바라보는 방향으로 회전하는 구현은 없다.

### 7.4 sorting/depth/높낮이/그림자

- TagManager의 Sorting Layer는 Default 하나다. 사용자 정의 Layer30 이름은 비어 있지만 PaperWorld.LogicLayer=30/culling bit로 쓰인다.
- WorldDepth.Order(feetY)=-RoundToInt(feetY×30). root SortingGroup은 현재 RoomOrigin 기준 발 y로 정렬한다.
- PaperCard는 조상 SortingGroup.order×10+source.order 또는 source.order×10을 Visible.sortingOrder에 넣는다. Visible SortingGroup.sortAtRoot=true를 갱신한다.
- 저장된 XZ prop는 WorldProp.LateUpdate에서 transform.z-current RoomCenter.y로 order를 계산한다.
- GraphicsSettings.TransparencySortMode=0, axis=(0,0,1). 사용자 정의 투명 정렬 mode 설정은 없다. 실제 앞뒤 표현에 SortingGroup/order와 XZ 위치를 함께 사용한다.
- CartoonAtlas.shader는 Queue Transparent, Cull Off, Lighting Off, ZWrite Off, SrcAlpha/OneMinusSrcAlpha blend. ZTest 명시 없음. 명시적 Alpha Clip/clip()는 없고 마젠타 제거는 alpha를 smoothstep으로 감소시킨다.
- 일반 sprite는 builtin material reference fileID10754. 해당 내장 shader의 모든 pass 상태는 프로젝트 Assets에 소스가 없어 본 분석만으로 확정할 수 없다.
- Player의 내려찍기 lift는 source art.localPosition.y에 추가된다(Player:95). PaperCard가 source world y를 렌더 Z로 옮기므로 이 lift는 현재 변환상 지면 깊이 방향 offset이고, gameplay height나 3D 점프 물리와 동일하지 않다.
- MuzzleOffset=(0,.95,.65), slash RenderOffset=(0,.45,.25)는 실제 표시 Y 높이를 사용하는 별도 오프셋이다.
- 캐릭터 그림자는 flat Sprite blob이다. 환경의 PropGrounding.Decal도 SpriteRenderer 기반 접촉 그림자/soil/노을 그림자/작은 돌이며 URP DecalProjector가 아니다.
- PropGrounding.Update는 painted anchor를 카메라 ray로 y=.017 plane에 투영한다. 실제 Light shadow, Projector component, ShadowCaster2D는 발견되지 않았다.
- 오브젝트 가림은 WorldProp/SceneryOccluder가 player 또는 enemy의 논리 위치 관계로 alpha를 낮추는 방식이다. authored path의 WorldProp은 Player 기준이고 legacy SceneryOccluder는 Enemy도 검사한다.

**한 문장 정의:** “XY 논리 좌표와 수동 Rect·거리 전투 판정 + XZ 표시 세계 + pitch47 원근 카메라 + 고정 기울기 Sprite card 복제 + order 기반 깊이 정렬”이다.

## 8. URP 구조 분석

### 8.1 활성 참조와 품질 설정

GraphicsSettings.m_CustomRenderPipeline GUID `2d9eb20227d024349a4b44108914cc81`은 PC.asset이다. QualitySettings.m_CurrentQuality=5(Ultra), Standalone 기본도 5이며 Ultra의 customRenderPipeline 역시 PC.asset이다. 현재 설정상 PC가 사용된다. 실행 플랫폼·사용자가 런타임에 바꾼 품질은 실행하지 않았으므로 확인 불가다.

PC의 RendererDataList[0] GUID `eb55e939749358c49a43e19af71e1b12`는 PC_Renderer.asset이다. m_EditorClassIdentifier=UniversalRendererData, m_RenderingMode=0(Forward). 2D Renderer/Forward+/Deferred가 아니다. enum 수치는 로컬 URP 17.3.0 package source의 UniversalRenderer.cs와 UniversalRenderPipelineAsset.cs로 대조했다.

| PC 항목 | 저장 값 |
|---|---|
| Render Scale / Upscaling | 1 / 0 |
| MSAA | 1 = Disabled(1 sample), camera AllowMSAA=1과 구분 |
| HDR | SupportsHDR=1, Camera HDR=1 |
| Depth / Opaque Texture | 둘 다 0 |
| Main Light | PerPixel=1, shadow 지원1, map2048 |
| Additional Light | PerPixel=1, object limit4, shadow 지원0 |
| Shadow | distance50, cascade1, depth/normal bias1/1, soft0 |
| Renderer Feature | PC_Renderer.m_RendererFeatures=[] |
| Depth Priming | 0 |
| Native Render Pass | 0 |
| Intermediate Texture Mode | 1 |
| SRP Batcher / Dynamic Batching | 1 / 0 |
| Color Grading | mode0, LUT32 |
| Light Layers | 지원0 |

품질별 별도 Pipeline도 저장되어 있다.

| 품질 | Asset | Renderer | Depth/Opaque | MSAA | Main shadow / 거리 / cascade | Additional lights |
|---|---|---|---|---|---|---|
| Very Low | URPDefaultResources/Very Low | Default_Forward_Renderer | 0/0 | 1 | off /15 /1 | PerVertex, limit4, shadow off |
| Low | Low | 동일 | 0/0 | 1 | off /20 /1 | PerVertex, limit4, shadow off |
| Medium | Medium | 동일 | 0/0 | 1 | on1024 /20 /1 | PerPixel, limit1, shadow on |
| High | High | 동일 | 0/0 | 1 | on2048 /40 /2, soft on | PerPixel, limit2, shadow on |
| Very High | Very High | 동일 | 1/0 | 2 | on4096 /70 /2, soft on | PerPixel, limit3, shadow on |
| Ultra | Settings/PC | PC_Renderer | 0/0 | 1 | on2048 /50 /1 | PerPixel, limit4, shadow off |

Default_Forward_Renderer도 UniversalRendererData/Forward, feature 배열이 비어 있다. 위 모든 Pipeline은 RenderScale1/HDR1이다. 품질별 설정 차이는 저장값이지 현재 Scene에 Light가 있다는 뜻이 아니다.

### 8.2 feature/Volume/Post Processing

PC/Default renderer에 Decal, SSAO, ScreenSpaceShadows, 기타 custom RendererFeature가 발견되지 않았다. GlobalSettings의 URP 기본 shader/resource 목록에 2D light/shadow shader가 존재하는 것은 패키지 공용 리소스 등록이며 실제 2D Renderer 사용 증거가 아니다.

GlobalSettings GUID `ce505d59d2aabe0469f2d4300358c972`이 GraphicsSettings의 pipeline global map에 연결된다. GlobalSettings 내부 DefaultVolumeProfile 참조는 `Assets/DefaultVolumeProfile.asset`이다.

Profile에는 Bloom, ColorAdjustments, ColorCurves, Tonemapping, Vignette, ChromaticAberration, LensDistortion, FilmGrain, ScreenSpaceLensFlare, ColorLookup, MotionBlur, DepthOfField, PaniniProjection, WhiteBalance, ShadowsMidtonesHighlights, LiftGammaGain, SplitToning, ChannelMixer, ProbeVolumesOptions 19개가 있다. active=1인 컴포넌트라도 Bloom/Grain/ChromaticAberration/LensFlare/MotionBlur intensity=0, Tonemapping/DepthOfField mode=0 등의 기본값이다. 항목 존재만으로 눈에 보이는 효과 적용을 주장할 수 없다.

Main/Prefab에는 Volume component와 UniversalAdditionalCameraData가 저장되어 있지 않으며 renderPostProcessing=1이라는 Scene 설정도 발견되지 않았다. Default profile 등록은 확인되지만 실제 post-processing 화면 결과는 **확인 불가**다.

### 8.3 캐릭터/환경 재질과 Light

일반 Enemy/Raven/Shadow와 모든 authored room sprite는 builtin material fileID10754, GUID `0000000000000000f000000000000000`를 참조한다. Hunter 본체 Source/View 2개만 Material_0046.asset(`Hunter cel animation`)을 참조하고, 이 material은 CartoonAtlas.shader를 연결한다. UI는 UI_Default.mat의 builtin UI shader fileID10770을 사용한다.

CartoonAtlas는 light 계산 없이 texture×vertex color로 출력하는 shader이고 URP 전용 LightMode/URP lighting 계산이 없다. 일반 sprite를 URP Sprite-Lit 또는 Spine URP Lit로 바꾼 직렬화 참조는 발견되지 않았다.

따라서 확인된 병합 범위는 **URP package 및 pipeline 설정 도입 + 기존 Sprite 표현 유지**이다. 실제 Scene의 Directional/Point/Spot/2D Light는 각각 0개다. 노을·lantern pool·그림자·모래는 tint/alpha Sprite 합성이다. URP 적용 후 shader 호환·핑크 material 여부·Camera clear 및 backdrop 합성은 실행 없이 확인할 수 없으며, 이전 문서의 “Built-in 기준” 안내가 현재 설정과 불일치한다.

## 9. Camera 구조

| 항목 | 현재 코드 및 Scene 값 |
|---|---|
| Main projection | Perspective, orthographic=0. orthographic size5는 미사용 필드 |
| FOV | 저장38, Configure38, Dungeon.LateUpdate에서 aspect<1.5이면48, 아니면38 |
| 회전 | quaternion(.39874908,0,0,.9170601), 코드 pitch47 / yaw0 / roll0 |
| 저장 위치 | (0,20.515198,-17.731958) |
| runtime 위치 | Point(focus,1.5)-pitch47 forward×26 |
| clip | near .1, far130 |
| culling | 3221225471 = bit30 제외 |
| Follow | Dungeon.ResetCamera/LateUpdate, Vector3.SmoothDamp, .24초, unscaled delta |
| focus 경계 | room center + x clamp[-6,6], y clamp[-2.5,2.5] |
| 추적 계수 | x .7(마을 .42), y .45 |
| 컷신 Camera | CinematicDirector.ApplyCamera, 저장 위치↔focus 보간, FOV death27/기타30 |
| Boss Camera | 별도 Camera 없음. BossEntrance focus가 target의 논리 위치 |
| Camera shake | 발견되지 않음 |
| 수동 zoom | wheel/zoom input 발견되지 않음. aspect/Timeline FOV만 존재 |
| Cinemachine | 없음 |
| Target texture | Main/Backdrop 모두 fileID0 |
| AudioListener | Main Camera에1개 |
| Viewport_Backdrop | 별도 Camera, Perspective/FOV60, depth -100, mask0, black, near .3/far1000 |

Scene Inspector의 camera position/FOV는 존재하지만 Awake.Configure와 ResetCamera, 매 프레임 follow/FOV가 덮어쓴다. pitch/거리/FOV/경계/추적 계수는 코드 고정값이고 전용 Inspector CameraController 설정은 발견되지 않았다. 카메라가 방 전체 경계에 맞춰 동적으로 viewport 크기를 계산하는 호출은 없으며 Rules.CameraTarget은 Editor 검증에서 쓰는 별도 함수다.

## 10. Player 시스템

핵심 파일은 Player.cs(174행), HunterLocomotion.cs(84), HunterMotion.cs(84), Rules.cs(61), CombatRules.cs(21)이다.

| 기능 | 실제 구현 |
|---|---|
| 이동 | WASD normalized, run5.6/walk2.8, RunBuild.MoveSpeedBonus 적용. Rect 장애물·경계 해결 |
| 대시/회피 | Space+방향, 15속도×.19초, CD2.5, 무적2초. 건물 SolidObstacles 사용 |
| 공격 | 좌클릭 .18초 buffer, 4stage, animation duration26% 시점 타격 |
| 콤보 | 입력 Combo0~3 + 실제 적중 ComboTracker 별도. 같은 적/attackId/4초 기준 |
| 무기 | Weapon0 단검, 1 리볼버. Q로1-Weapon, TagCD.5, Fury 부여 |
| 총 | 탄약6, 1/2/1/2 소비, Ammo<=3 자동2초 reload, 잔탄 사용 가능 |
| 스킬 | 각 무기2개, 숫자2/3. 스킬1~4, 종료 후 각 슬롯CD8 |
| HP/피격 | MaxHp100, Hurt가 상태/무적 검사→HP감소→무적1초→Ink/Sound |
| 사망 | Hp0→Dungeon.BeginDeath→PlayerDeath Timeline→RunState.Dead |
| 상태 | ActionLocked/SkillBusy/BasicBusy/Dodging/Invulnerable 등 내부 타이머 |
| 애니메이션 | idle/run/walk/settle atlas, 액션 atlas, Sprite flip/lean/lift/tint |
| 패시브 | 공격/이동 bonus를 계산 시 RunBuild에서 읽음 |
| 성흔 | Fire는 ignite, Nature는 tag Fury 강화, Butterfly는 Raven 쪽 효과 |
| 입력 억제 | Dungeon.Running=false이면 Update 반환. 기본 클릭은 EventSystem UI overlap도 검사 |
| 런 초기화 | ResetForRun→HP/탄약/무기/타이머/콤보 초기화. SafeEntry는 무적1초·pending action 제거 |

스킬 수치:

| 스킬 | 실제 피해/시간 계약 |
|---|---|
| 단검 베어 가르기(skill1) | .11~.30초 active, 속도15, 범위1.6, 피해36, target HashSet 중복 방지, duration.48 |
| 내려찍기(skill2) | .8초 radius2.3/20, 1.05초 radius3.2/34, duration2초, ActionLocked |
| 원 샷(skill3) | .12초, 전방 길이4/폭 기준.8, 피해42, duration.45 |
| 난사(skill4) | .2초 간격5회 radius5/11, duration2초, 이동속도3.1 |

Prefab은 7 GameObject, 4 SpriteRenderer(Source2+View2), 4 SortingGroup, Player/ActorPresentation/WorldDepth/PaperCard2개다. art SerializeField가 Sprite_Frames를 가리킨다. UseCustomVisual=0, Animator/AnimationDriver=fileID0, custom child 비활성이다. PlayerController/PlayerHealth/PlayerCombat 별도 클래스는 없다.

## 11. 전투 시스템과 데이터 흐름

```text
Input / UI에 가리지 않은 좌클릭
→ Player.TryAttack
→ basicStage/basicId/basicWeapon/basicDirection 캡처
→ TickBasic(기본 duration의 .26 경과)
   ├─ 단검: Enemies 순회→Rules.InCone
   └─ 리볼버: Bullet.Spawn→Bullet.Update substeps→거리 적중
→ Player.Hit
   ├─ ComboTracker.Hit(Enemy instanceID, stage, attackId, RunTime)
   ├─ Crow.Mark
   ├─ Enemy.TakeDamage(damage×DamageScale, ignite)
   ├─ DamageEvents / HitCombo
   └─ skill 또는 4타→OfferLink, 실제 4단계 완성→ComboFollowup
→ Enemy.Hp / Dead
→ Kills++ / warning off / Ink / Destroy
→ Dungeon.Update Enemies.RemoveAll
→ 전체 목록0→Progress.CompleteWave
→ 다음 웨이브 또는 방 완료
```

| 항목 | 실제 확인 |
|---|---|
| Damage 전달 | float damage + bool ignite 직접 메서드. 공용 DamageInfo/IDamageable 없음 |
| Hitbox/Hurtbox | 별도 Collider/Prefab 없음. range/cone/rect/거리 계산 |
| melee | InCone range2.25, dot threshold-.1, 피해17/마지막30 |
| player gun | speed21, life.23, enemy radius.55/보스1, 마지막24/기타20 |
| 적 피격 | Enemy.TakeDamage 및 burn tick, flash scale 증가 |
| player 피격 | Enemy.Execute/charge 또는 hostile Bullet→Player.Hurt |
| 무적 | Player 타이머; Cinematic 중 enemy damage는 별도 차단 |
| Knockback | 강제 피격 knockback 발견되지 않음. Enemy charge/Player dash는 공격·이동 기능 |
| Critical | 확률/치명타 판정 발견되지 않음 |
| Status effect | ignite→burnTime3, .5초 tick, 3+FireLevel×2. 일반 status stack framework 없음 |
| Cooldown | Player 배열·타이머, Enemy cooldown/windup/chargeTime, Raven timers |
| Combo | Player Combo는 공격 stage, tracker는 실제 대상별 적중 stage. 중복 pellet 같은 attackId 무시 |
| Animation Event | Unity AnimationEvent 기반 타격 없음. HunterMotion 시간 상수를 Player가 직접 읽음 |
| Enemy attack | Kind0 범위 피해, Kind1 charge, Kind2 부채꼴3탄, Kind3 방사탄/주기 돌진 |
| Raven attack | Enemy.TakeDamage 직접 호출. Player.Hit를 통과하지 않음 |
| 전투 pause | Running gate로 대부분 gameplay timer/move 중단. Timeline은 unscaled 사용 |

Player health를 직접 수정하는 곳도 있다(Dungeon 방 완료 회복). HP 변경/죽음의 C# event는 없고 UI는 매 프레임 값을 읽는다.

## 12. 무기 시스템

실제 무기는 **단검과 리볼버 2개**다. Scythe/낫 또는 별도 Gun/Dagger/WeaponManager/WeaponSO/AttackSO는 발견되지 않았다. Hunter atlas의 무기 포즈가 표시를 담당하며 무기 전용 Prefab/손 attach transform 데이터는 없다.

Weapon은 Player의 정수이며 기본0 단검/1 총이다. TryTag가 스킬·기본공격을 취소하고 1-Weapon으로 전환한다. 기본 타격 수치·스킬 슬롯·공격 거리·탄약·시전 시간은 Player/Rules/HunterMotion 코드에 직접 있다. cds[2,2]가 무기별 쿨다운을 분리한다.

Weapon 표시와 cooldown UI는 GameUIView.Skill에서 Weapon==0 분기로 icon/한국어 이름을 고른다. 입력 방향도 단검 LastMove vs 총 Mouse aim으로 달라진다. WeaponType enum이나 패시브의 무기 조건은 발견되지 않았다.

## 13. 패시브 시스템

현재 시스템은 요청에 예시로 든 PassiveSO/PassiveManager 구조가 아니라 RunBuild의 일반 C# 정의+획득 객체+코드 bonus 구조다.

| 기능 | 상태 | 근거 및 현재 범위 |
|---|---|---|
| PassiveSO | [발견되지 않음] | VisualCatalog는 아이콘용 SO |
| PassiveInstance | [부분 구현] | 이름 대신 OwnedPassive(definition,rarity,source) 객체가 획득마다 생성 |
| PassiveManager | [부분 구현] | 같은 역할 일부를 RunBuild가 담당. 동명의 클래스 없음 |
| PassiveRuntimeContext | [발견되지 않음] | Dungeon.I/Build 직접 접근 |
| Effect Registry | [발견되지 않음] | ID별 Bonus 함수 hardcoding |
| 정의 등록 | [구현 완료] | RegisterPassive validates id/name/icon then catalog[id]=definition |
| Apply | [부분 구현] | AcquirePassive→목록 추가; 실제 효과는 Bonus property pull 방식 |
| Remove | [발견되지 않음] | 개별 제거 API 없음. ResetRun 전체 clear만 존재 |
| Level | [발견되지 않음] | 패시브 level 없음. rarity가 배수 |
| Stack | [부분 구현] | 동일 ID 획득이 여러 OwnedPassive로 저장되고 Bonus 합산. 독립 stack 필드 없음 |
| IsActive | [발견되지 않음] | 패시브 활성 필드 없음. RunBuild.IsActive는 성흔용 |
| Enable/Disable | [발견되지 않음] | 보유 passive 효과 일시 해제 API 없음 |
| WeaponType 제한 | [발견되지 않음] | 공격 bonus가 Player 전체에 적용 |
| Run Reset | [구현 완료] | inventory.Clear/Reset event |
| Excel/JSON 데이터 | [발견되지 않음] | 패시브 데이터는 Awake의3개 정의. bounds.json은 아트용 |
| 획득 | [구현 완료] | AcquirePassive가 id/enum 검사→OwnedPassive append→PassiveAcquired |
| 삭제 | [발견되지 않음] | 런 초기화 제외 |
| 강화 | [부분 구현] | rarity 배수와 중복 합산 존재. 기존 item upgrade API 없음 |
| UI | [구현 완료] | slots, rarity border, tooltip, 획득 queue/toast |
| MonsterDrop/ShopPurchase | [구조만 존재] | PassiveSource enum/API 경로만 존재. 실제 drop/shop 없고 Smoke에서 호출 |
| EventReward | [구현 완료] | ClaimCard가 실제 gameplay에서 호출 |

catalog의 콘텐츠는 spent_bullet(공격+.08), blue_charm(이동+.08), raven_seal(까마귀+.12)이다. 배수는 1+.5×rarity(0/1/2), 같은 ID 합산이다.

Passives property는 inventory.AsReadOnly()를 호출하여 read-only wrapper를 반환한다. 정의/런타임 획득은 일반 객체로 구분되지만 catalog·inventory·효과 계산이 같은 RunBuild MonoBehaviour에 함께 있다. PassiveAcquired는 GameHUD가 구독하여2.4초 순차 toast를 표시하고, 실제 효과를 event listener가 적용하는 구조는 아니다.

## 14. 성흔 / 인장 시스템

코드 이름은 SeongheunType(Fire/Nature/Butterfly), UI/방 이름은 Sigil/Stigma/성흔이다. 별도의 “인장” 아이템 시스템을 추가로 찾을 수 없으며 현재 문맥의 각인은 RunBuild.stacks이다.

| 항목 | 현재 구현 |
|---|---|
| 데이터 | int stacks[3], thresholds[3]={1,1,1}, enum3종 |
| UI | GameUI.Stigma_Build_Tree, 6 node(속성당2), SelectedSigil, HUD3아이콘 |
| 노드 | threshold와 threshold+2 달성 상태 표시. 독립 노드 ownership/skill tree topology 없음 |
| 선택 | UICommand.SelectSigil→GameHUD.SelectedSigil clamp0~2 |
| 획득 | Room03 중앙E→SigilChoice→ClaimSigil(type) |
| 적용 | ApplySeongheunStack→SetStack→StackChanged, 최소0/상한int |
| runtime | EffectLevel=IsActive?Stack:0; 임계값 미만0 |
| Player 연결 | Nature→Fury/tag duration+damage, Fire→Enemy ignite |
| Raven 연결 | Butterfly→활동 시간/피해, Fire도 Raven ignite |
| 전투 연결 | Player.DamageScale/Hit, Enemy burn, Raven damage 수식 |
| 초기화 | RunBuild.ResetRun stacks clear. threshold 설정은 유지 |
| 별도 SO/Prefab | 성흔 SO/노드 Prefab 없음; GameUI 내부와 Room03 오브젝트 사용 |

ClaimSigil은 올바른 State/RoomKind/미완료를 검증하고 +1 뒤 CompleteService/Exit로 전환한다. 다른 방에서 동일 수치 API를 호출하는 것이 서비스방 완료를 자동 처리하지는 않는다. UI의 두 번째 노드는 강화 이정표이고, 별도 능력 해금 함수를 호출하는 구현은 발견되지 않았다.

## 15. 까마귀 / Raven / Crow 시스템

현재 관련 구현·참조 파일은 Raven.cs, Raven.prefab, Player.cs의 Crow 호출, Dungeon.cs의 Crow 바인딩/reset, GameUIView의 skill4/5·CrowPrompt, ActorPresentation, CinematicDirector의 까마귀 연출, CombatArt/RunBuild/CombatRules이다. 관련 아트는 Resources/Combat/Raven.png, RavenLink.png, RavenPortrait.png, RavenBubble.png 및 대응 Shared sprite/VisualCatalog entries다. RavenBubble은 catalog 콘텐츠는 있으나 현재 gameplay에서 쓰는 경로는 발견되지 않았다.

| 기능 | 현재 구조 |
|---|---|
| Summon | 새 까마귀 소환/해제 없음. Main에 저장된 Raven이 항상 동행 |
| Recall | 별도 Recall API/입력 없음. combo 종료·거리초과 시 Return/follow 복귀 |
| Follow/Idle | player offset+sin 위치에 MoveTowards |
| Attack 명령 | 키1→Command, Active=5+RavenLevel×.75, CD10 |
| Combo | 실제4단 적중→Queue→.18 windup→.24 dash→도착 피해→최대2대상 |
| Link skill/R | OfferLink가6초 window, R→TryLink, .6초 추적→radius2.5 area45+level×10, CD8 |
| Target Search | marked target 또는 Dungeon.Nearest. 반경7. 콤보는 별도 Replacement |
| AI | enum 상태+comboPhase/returning/linkTime/Active의 자체 분기 |
| NavMesh | 없음 |
| 이동 | XY 직접 MoveTowards/Lerp; Rules 장애물 충돌 적용 없음 |
| Animation | Sprite 교체 Raven/RavenLink, flipX/수동 rotation. Spine/Animator 없음 |
| Damage | Enemy.TakeDamage 직접, RavenDamageBonus 및 성흔 적용 |
| Player 연결 | Mark/OfferLink/ComboFollowup/Command/TryLink, reset 시 Dungeon.Pos |

기본 대기 목표는 `g.Pos+(-.95,1.15+sin(Time.time×3)×.15)`이고 속도10으로 이동한다(Raven:101~107). root가 player의 자식으로 딱 붙는 구조는 아니지만 **고정 side offset을 중심으로 추적하는 구조**다. 거리가9 초과면 follow 위치로 teleport한다. 무작위 주변 waypoint·체류시간·주변 배회·장애물 회피는 현재 발견되지 않았다.

따라서 자연스러운 주변 대기에 관한 현재 기반은 **Idle/Follow/Return 상태, 별도 root/visual, 독립 목표점과 이동 처리, 공격/복귀 전환**까지다. 주변을 여러 지점으로 선택해 자연스럽게 대기하는 동작은 구현되어 있다고 판단할 수 없다. 이 절은 현재 상태 분석이며 구현안을 제시하지 않는다.

Raven prefab은 Hunter와 같은7객체/Source2+View2 구조다. art가 Inspector로 연결되고 UseCustomVisual=0, custom slot 비활성, AnimationDriver/Animator0이다.

## 16. Enemy / AI 시스템

| 기능 | 현재 구현 |
|---|---|
| Spawn | Dungeon.SpawnWave→EnemyPrefabs[kind] Instantiate→Init→Enemies.Add |
| spawn point | RoomAuthoring의6 marker 중 random+SpawnRadius2, 180회 안전 검사, 고정 grid fallback |
| Targeting | 모든 적은 Dungeon.I.Hero/Pos를 대상으로 delta 계산 |
| Movement | Rules.Rect collision, enemy separation, 거리 조건에 따른 접근/후퇴 |
| NavMesh | 없음 |
| 상태 | windup/cooldown/chargeTime/burnTime/flash/cycle 필드. 별도 FSM class 없음 |
| Attack | Kind0 melee/1 charge/2 triple bullets/3 boss radial/charge |
| Damage | TakeDamage/ignite/burn ticks |
| Death | HP<=0→Kills++→warning off→Destroy |
| Drop | 현재 일반적/보스 Drop 또는 currency drop 없음 |
| Animation | authored Enemy sprite tint/scale flash. 체력 바 scale. custom adapter만 준비 |
| Presentation | AuthoredArt/AuthoredSprite/HealthFill/AuthoredWarning Inspector 참조 |

Enemy_0/1/2가 각각 같은 Enemy 클래스를 다른 Kind 값으로 저장한다. 각각 prefab HP는55/80/55이고 실제 Init(room)이 room stage 증가에 따라 +13×Room을 반영한다. 보스는650 고정이다.

외부 에셋 패키지의 독립 monster AI와 게임용 AI가 두 세트로 있는 증거는 없다. 실제 사용 몬스터도 단일 Enemy PNG를 재사용한 프로토 표현이다. `Enemy.Init`의 Inspector 참조가 모두 있으면 authored branch로 즉시 반환하고, 없으면 Enemy art/HP/warning을 코드 생성한다. 저장된4개 Enemy Prefab은 모두 참조가 채워져 있어 정상 Main 생성 경로는 authored branch다. RuntimeSmoke의 review boss는 코드 생성 fallback을 이용하지만 일반 플레이와 구분한다.

적 separation은 매 Enemy가 전체 Enemies를 순회하여 가까운 다른 적의 away vector를 더한다. 경로 탐색이 아니므로 장애물 주위를 목적지까지 탐색하는 알고리즘은 발견되지 않았다.

## 17. Boss 시스템과 사망 감지

Boss.prefab의 root 이름은 Boss이며 Enemy.Kind=3/HP650, AuthoredArt/HealthFill/AuthoredSprite/AuthoredWarning이 연결된다. 별도 BossController/BossAI/BossHealth/BossState/BossDeath 클래스는 없다.

Boss Spawn 조건은 ExpeditionProgress.FinalBossWave: Segment==4 && Map==5 && Wave==WaveCount-1이다. Dungeon.SpawnWave는 이 때 count1/kind3을 선택하고 Enemies[0]로 BossEntrance Timeline을 재생한다. 최종 스테이지 이외 Room06은 일반 전투방이다.

보스 패턴은 Enemy.Execute:71~75에 있다. cycle 증가, 체력 절반 아래에서는 탄환12→20/속도4.5→6, 3회마다 돌진, 나머지 방사탄이다. 돌진 접촉 피해는 공통 charge branch의22이며 AttackPower property의 보스25와 동일한 소비 경로가 아님에 유의한다. AttackPower는 Raven의 link target 선택 비교에 쓰인다.

**“보스가 죽었다”는 것을 최초 처리하는 클래스는 Enemy이다.** TakeDamage:80에서 Hp를0까지 감소시키고 Dead=>Hp<=0을 즉시 평가하여82행에서 Kills++, warning off, VFX, Destroy를 수행한다. BossKilled event 또는 Boss 전용 clear callback은 없다.

```text
Enemy.TakeDamage (Kind3도 같은 함수)
→ Hp<=0 / Dead=true
→ Kills++ / Destroy(gameObject)
→ Dungeon.Update:146 Enemies.RemoveAll(!e || e.Dead)
→ State==Combat && Enemies.Count==0
→ Progress.CompleteWave
   ├─ 아직 중간 wave: WaveBreak, 2초 후 SpawnWave
   └─ 마지막 wave: cleared[Map]=true, Hero.Hp+18, State=Exit
→ 자동 reward spawn/선택 없음
→ 모든6방 완료 + Room06 출구 근접E
→ Route / FinishExpedition → Victory
```

위 흐름은 event chain이 아니라 Enemies 목록 polling이다. Boss Death→Room Clear는 공통 전투방 루프로 연결되어 있고, Room Clear→Boss Reward는 발견되지 않았다. Boss Death Timeline도 없으며 현재 Timeline은 BossEntrance/PlayerDeath/DungeonEntrance뿐이다. 저장된 Boss prefab과 문 상태·HP HUD는 연결되어 있으나 사망 애니메이션 완료를 기다리는 단계는 없다.

## 18. Room / Dungeon / Stage 구조

현재 gameplay는 미리 저장된 Hirva+6방을 활성화하여 사용한다. 런 시작 시 절차적으로 새 방 Prefab을 Instantiate하거나 room generator를 호출하지 않는다. `ExplorationStage.Create/WesternEnvironment.Build/FrontierDressing`은 Editor migration/기존 검증용 템플릿 경로에 남아 있다.

- Dungeon.BuildTown: Scene.Hirva 활성화/Capture/ApplyFootprints.
- Dungeon.BuildStage: Scene.Rooms[i].Bind로 ExplorationRoom6개 데이터를 구성.
- ActivateRoom: 현재 map의 root만 활성화, footprint 다시 Capture, 도착 문/InitialSpawn으로 player 이동, crow reset/camera reset.
- ExpeditionProgress: segment/map/visited/cleared/waves 유지. 전체5스테이지, 각6방.
- 현재 graph는 고정 양방향 트리. 0↔1↔2, 2↔3, 2↔4↔5.
- 전투방0/1/4/5는 seed+segment+room 식으로2~3wave, 서비스방2/3은0wave.
- 전투방은 clear 전 다른 방 이동 불가. 서비스방은 선택 전에도 이동 가능하지만 complete로 계산되지 않음.
- 열린 방 재방문은 spawn/service 재보상 없음.
- 다음 segment는6방 모두 완료 후 Room06 east StageExit 근접E→RouteTravel→ChooseRoom.
- 최종 segment 완료는 UICommand.NextStage→FinishExpedition→Victory.
- 분기 Room 존재는 있지만 random branch/route choice generation은 없다.
- Dungeon.Room은 **스테이지(segment)** 번호다. MapIndex가 실제6개 방 index이므로 이름을 혼동하면 안 된다.

현재 saved geometry는 스테이지마다 동일6방을 재사용한다. stage별로 적 수/HP/웨이브/카드 등급은 달라지지만 BuildStage는 stage별 배경 템플릿을 다시 생성하지 않는다. 이전 문서에서 “Create가 여섯 환경 생성”이라고 설명한 부분은 현재 runtime에 그대로 적용되지 않는다.

Room prefab 구조는 Ground_and_Paths/Scenery/Doors/Gameplay_Markers/Atmosphere다. 각 방 marker6개, SpawnRadius2, ServicePoint/InitialSpawn/TownExit를 저장한다. 문 Owner/Arrival/Marker도 fileID가 모두 있다.

| Prefab | WorldProp | Footprint | Doors | SpriteRenderer | GameObject |
|---|---:|---:|---:|---:|---:|
| Hirva | 51 | 15 | 0 | 892 | 992 |
| Room01 | 180 | 10 | 1 | 2002 | 2265 |
| Room02 | 187 | 12 | 2 | 2291 | 2573 |
| Room03 | 239 | 10 | 3 | 2268 | 2606 |
| Room04 | 157 | 11 | 1 | 2135 | 2380 |
| Room05 | 189 | 11 | 2 | 2125 | 2404 |
| Room06 | 202 | 10 | 2 | 2135 | 2430 |

이는 YAML 컴포넌트 수이지 동시 활성 renderer/실제 draw call 측정값이 아니다. 많은 renderer는 접지 그림자·돌 조각·모래·장식이다. 비활성 방도 Scene 참조로 저장되어 있다.

## 19. Reward 시스템

| 요청 항목 | 현재 결과 |
|---|---|
| RewardManager/RewardSpawner | 발견되지 않음 |
| Item Drop/Monster Drop | gameplay 실행 경로 발견되지 않음 |
| Currency Drop | 발견되지 않음 |
| Passive Reward | 카드방 ClaimCard 선택·획득 존재 |
| Sigil Reward | 제단 ClaimSigil +1 존재 |
| Reward UI | CardsPanel/SigilPanel은 존재. 전용 보스 RewardPanel/RewardSelection class 없음 |
| Reward Selection | 카드3개 선택/성흔3종 선택은 존재 |
| Boss Reward | 발견되지 않음 |
| 전투 clear 보상 | 공통 HP+18 존재. 별도 reward object 없음 |
| RunState.Reward | enum 선언만 존재. gameplay로 진입·사용하는 branch 발견되지 않음 |

기존 연결 가능한 지점은 Enemy.TakeDamage, Dungeon의 Enemies.Count==0/Progress.CompleteWave, Dungeon.AcquirePassive/RunBuild.AcquirePassive, ClaimCard/ClaimSigil, GameUI의 existing panel/command, 문 진행 함수이다. 단, ClaimCard/ClaimSigil은 기존 서비스 RoomKind와 RunState 검증이 있으므로 보스 사망에서 자동으로 호출되는 범용 보상 함수로 해석할 수 없다.

Supplies는 Dungeon의 int 필드이며 ResetRun에서0, UI에서 읽기만 한다. 지급/사용/가격/구매/저장/통화 event가 발견되지 않아 CurrencyManager가 존재한다고 표현하지 않는다.

Boss Death→Reward 생성→Player 획득이라는 월드 pickup chain은 현재 없다. 카드/성흔은 world object pickup이 아니라 서비스 위치에서E→UI 선택→즉시 런 상태에 반영된다.

## 20. UI 구조

실제 UI prefab root 이름은 GameUI다. 이전 문서/생성 도구의 UI_Canvas 이름과 구분한다. Canvas는 ScreenSpaceOverlay, sortingOrder100, CanvasScaler reference1280×720/ScaleWithScreenSize/match.5다. EventSystem은 기존 StandaloneInputModule이며 UI Text/Image/Button/Slider/ScrollRect를 사용한다. TMP package/component는 발견되지 않았다.

| 영역 | 실제 기능 |
|---|---|
| Title | Start/New_Game→EnterTown, Settings |
| HUD | HP/Ammo/위치/Notice/Supplies/Wave/Combo/스킬6개/성흔3종 |
| Weapon | Skill0 태그 상태·현재단검/리볼버, 2/3 skill icon |
| Raven | Command/Link cooldown, CrowPrompt |
| Passive | 가로 inventory slots, tooltip, count, acquisition toast |
| Stigma | 원형6노드/선택/detail/claim, HUD stack pulse |
| Dungeon | mini nodes/Route map/방문·완료·다음 진행 |
| Boss | BossHP, IsBossWave 및 Enemies[0] |
| Reward | CardsPanel/SigilPanel 서비스 선택 |
| Result | Dead/Victory 결과·도달/처치/시간·Town 복귀 |
| Settings | AudioListener.volume slider, fullscreen toggle |
| Cinematic | letterbox/title/shade/hint |

GameUI.prefab에는 GameUIView1개, UICommand27개, Button26개, 등록 UIReference327개가 있다. GameUIView가 직렬화 목록을 Awake Dictionary로 구성한다. UI는 이름 Find 대신 key→Target→GetComponent로 조회한다. key는 문자열 고정 계약이다.

동기화 방식은 혼합이다. RunBuild 이벤트는 GameHUD toast/pulse에 사용되지만 HP/skill/room/boss/state는 GameUIView.LateUpdate polling이다. UI button은 serialized UnityEvent→UICommand.Execute→Dungeon/GameHUD 직접 메서드다. ActorPresentation.AnimationRequested는 animation slot용 event이며 현재 UI 이벤트와 별개다.

특이한 현재 연결: GameUIView는 panel 활성 여부와 관계없이 Map/Sigil/Passives 등의 update 함수를 매 LateUpdate 호출한다. GameHUD.View는 Inspector에 저장되지만 GameHUD 자체가 View rendering을 호출하지 않으며 GameUIView가 독립적으로 Dungeon.I를 읽는다.

## 21. 데이터 구조

| 저장 위치/형태 | 실제 내용 | runtime과의 관계 |
|---|---|---|
| ScriptableObject | VisualCatalog.Icons25개, TimelineAsset 및 custom clip/track 데이터 | UI 아이콘 조회, 컷신 입력 |
| JSON | Resources/Western/bounds.json의41개 crop/source size entries | WesternArt 첫 호출에 JsonUtility로 읽음 |
| Excel/변환 table | 발견되지 않음 | passive/weapon/enemy 데이터 외부 테이블 없음 |
| Resources | PNG/Shader/Timeline/WAV/bounds | 동기 Resources.Load/정적 cache |
| SerializeField/public Inspector | art renderer, Scene bindings, anchors/footprints, UI references, prefab | authoring 데이터와 초기 runtime 값 |
| 일반 C# 객체 | ExpeditionProgress, PassiveDefinition, OwnedPassive, HunterLocomotion, ComboTracker | MonoBehaviour 내부 소유 |
| static | Dungeon.I/Obstacles/SolidObstacles, Rules.RoomOrigin, art/atlas/sprite/material cache | 전역 공유 runtime 상태/표현 cache |
| PlayerPrefs | 사용 발견되지 않음 | 설정/진행 영속 저장 없음 |
| Save File | gameplay save/load 발견되지 않음 | Smoke File.WriteAllText/PNG는 검증 로그이며 save game이 아님 |

데이터와 runtime이 함께 있는 부분: Dungeon의 public State/Kills/Seed/Supplies 등은 Scene 직렬화 초기값이자 실행 상태이고, Player/Enemy/Raven의 public HP/타이머도 Prefab 초기값과 runtime을 겸한다. RunBuild는 코드 정의 catalog와 한 런의 inventory/stacks/threshold/bonus를 동시에 가진다. RoomAuthoring은 편집 배치이며 Capture로 runtime Rect snapshot이 된다.

ScriptableObject를 런 중 수정해 passive 상태를 저장하는 구조는 없다. thresholds는 ResetRun에서 초기화하지 않는다. settings의 볼륨/fullscreen도 현재 UI 실행 값만 바꾸며 영속 저장이 없다.

## 22. Manager / Singleton 구조

이름에 Manager가 붙은 자체 runtime class는 발견되지 않았다. 역할상 관리 시스템은 아래와 같다.

| 클래스 | 생성 위치 | Scene 종속 / DDOL | 접근 | 관리 의존 |
|---|---|---|---|---|
| Dungeon | 저장 Main.Game_Systems | Scene 종속, DontDestroyOnLoad 없음 | public static I | Scene/Build/HUD/Cinematics/Audio/Player/Raven |
| RunBuild | 같은 Game_Systems | Scene 종속 | Dungeon.I.Build / GetComponent | CombatArt(등록 검증), 이벤트 observer |
| GameHUD | 같은 Game_Systems | Scene 종속 | GetComponent<GameHUD> | Dungeon/RunBuild |
| CinematicDirector | 같은 Game_Systems | Scene 종속 | Dungeon.Cinematics | Dungeon/PlayableDirector/AudioSource/actors/camera |
| GameSceneBindings | 같은 Game_Systems | Scene 종속 | Dungeon.Scene | Scene·Prefab 참조 제공 |
| GameUIView | 저장 GameUI | Scene 종속 | Scene.UI/Inspector, 자체 LateUpdate | Dungeon/HUD/Build/VisualCatalog |

Dungeon.I는 Awake에서 단순 대입한다. 중복 instance 제거/생성 guard/DontDestroyOnLoad/OnDestroy에서 I null 처리하는 전형적 singleton framework는 없다. Boot는 Scene 로드 후 존재 확인만 한다. 나머지 singleton은 발견되지 않았다.

구조상 Dungeon↔GameHUD, Dungeon↔CinematicDirector의 양방향 접근, Dungeon→Player/Raven/Enemies와 이들의 Dungeon.I 역참조, UI→Dungeon→Build/HUD의 연결이 중심이다. 이는 참조 결합 관계를 기록한 것이며 과도함에 대한 개선 판단은 하지 않는다.

## 23. 이벤트 구조

| 이벤트 종류 | 선언 / 발행 / 구독 |
|---|---|
| C# event Action<SeongheunType,int,int> | RunBuild.StackChanged: SetStack/SetThreshold→GameHUD.StackChanged |
| C# event Action<OwnedPassive> | RunBuild.PassiveAcquired: AcquirePassive→GameHUD.Acquired |
| C# event Action | RunBuild.Reset: ResetRun→GameHUD.ResetBuild |
| UnityEvent<string,bool,float> | AnimationRequest/ActorPresentation.AnimationRequested.Invoke |
| Unity UI event | Button.onClick→UICommand.Execute; Slider.onValueChanged→SetVolume |
| PlayableDirector event | Director.stopped→CinematicDirector.OnStopped; OnDestroy 해제 |
| interface callback | PassiveSlotView pointer enter/exit→Tooltip |
| 직접 함수 호출 | Player.Hit/Enemy.TakeDamage/Hurt, Crow methods, Dungeon travel/claim |
| polling | Dungeon enemy 목록/Room clear, GameUIView gameplay HUD |

EnemyKilled/BossKilled/RoomCleared/WeaponChanged라는 C# gameplay event는 현재 발견되지 않았다. PassiveAdded라는 이름 대신 PassiveAcquired가 있다. 무기변경은 Player.TryTag가 필드를 바꾸며 HUD polling이 읽는다.

ScriptableObject Event channel 또는 일반 delegate 별도 선언은 발견되지 않았다. CinematicMixer.ProcessFrame은 Playable callback에서 ApplyCue를 직접 호출하는 구조이고 gameplay event bus가 아니다. GameHUD는 OnEnable/Connect에서 구독, OnDisable에서 해제하며 Awake 순서상 아직 Build가 없으면 Update.Connect로 연결을 다시 시도한다.

## 24. 현재 미완성 / 임시 구현

TODO/FIXME/TEMP/Mock/Placeholder/Sample/NotImplementedException 표식은 자체 C#에서 발견되지 않았다. 표식이 없다는 것과 미완성이 없다는 것은 다르다.

| 항목 | 현재 실제 상태 / 사용 여부 |
|---|---|
| SpineAnimationDriver의 #else 빈 Play/SetPlaybackSpeed | WNN_SPINE 미정의. 연결도 없고 실제 Spine 재생 기능 없음 |
| ActorAnimationDriver | 추상 연결 기반. 직접 사용 instance 없음 |
| Custom_Visual_Spine_Or_Animator | 모든6 actor에 비활성 슬롯. UseCustomVisual0/driver0/animator0 |
| ChooseEvent(int)=>false | Dungeon:110, legacy API stub. 실제 호출 경로 발견되지 않음 |
| RunState.Reward/Reward 관련 전용 UI | enum만 남음; reward spawn/use 없음 |
| Dungeon.Events[5]/Supplies | 선언/reset, Supplies 표시. 이벤트 보상/경제 실행 미발견 |
| environment Transform 필드 | Dungeon:29 선언, 현재 gameplay에서 할당·사용되지 않음 |
| VisualCatalog.Get return null | 찾지 못한 아이콘 정상 조회 실패 경로. 미구현 stub와 구분 |
| Dungeon.NearbyDoor/CurrentRoom null | town/미바인딩/근접문 없음 정상 조건 반환 |
| MonsterDrop/ShopPurchase | enum/API는 있고 실제 시스템은 없음. Smoke에서는 경로 테스트 |
| 카드·성흔 효과 | 현재 gameplay 실제 적용. 코드 고정 프로토 규칙 |
| Enemy/보스 표현 | 같은 PNG/수치·패턴 분기로 actual prefab 사용. 별도 완성 animation 없음 |
| Audio | Dungeon.Tone/Timeline WAV 생성의 합성 임시 오디오 |
| Skills/VFX | runtime sprite line/ring burst actual 사용. 전용 VFX prefab/graph 미발견 |
| *Smoke 5개 | 명시 실행 인자에서만 추가. 강제HP/이동/진행 변경, 캡처·로그 작성·Quit |
| Editor 검증/생성 도구 | 프로젝트 내 존재. 이번 작업에서 호출하지 않음 |
| BuildHUD/CinematicHUD/ExplorationHUD | 코드가 아닌 legacy 안내 주석 한 줄 |

Smoke 목록: RuntimeSmoke, MotionSmoke, MovementSmoke, LevelSmoke, Authoring/AuthoringSmoke. 일반 실행에서 활성화된 component가 아니며 테스트 결과 파일의 과거 PASS를 현재 URP 실행 검증으로 간주하지 않았다.

빈 animation adapter 외에 NotImplementedException을 던지는 함수는 발견되지 않았다. 임시 Prefab/Material이 이름상 따로 분리된 자료는 없지만 generated Shared와 Enemy sprite/VFX/audio/카드 수치는 프로토타입 표현이다.

## 25. 중복 / 레거시 코드

### 25.1 현재 Assets와 백업 구분

`Backups/BeforeEditableScene-20260922.zip`은72 entries, C#34개와 이전 Scenes/Main.unity를 포함한다. 압축을 해제하지 않고 ZIP 안의 이름·핵심 source를 읽었다. Assets 밖 ZIP의 C#은 Unity의 현재 runtime source와 구분한다. 줄바꿈·앞뒤 공백을 정규화한 현재파일 대조에서34개 중16개는 같고18개는 내용이 다르다. 동일한 프로토 핵심과 authored migration으로 바뀐 부분이 함께 존재한다.

확인된 이전 형태:
- backup Dungeon.Boot: Dungeon이 없으면 new GameObject+AddComponent로 게임 생성.
- backup Dungeon.Awake: camera/player/crow/HUD/환경을 코드로 조립.
- backup GameHUD: OnGUI 기반.
- backup BuildHUD/CinematicHUD/ExplorationHUD: GameHUD의 분할 UI source.
- backup PaperWorld.cs: PaperWorld와 PaperCard가 한 파일 안.
- backup Main Scene은218행의 초기 camera 중심 Scene. 현재 Main은1515행과10 PrefabInstance.

현재:
- Boot는 오류 안내, Awake는 GameSceneBindings 필수.
- 현재 HUD3파일은1행 주석만 있고 중복 UI class를 실행하지 않는다.
- PaperCard는 별도 파일로 분리, Visible 직렬화와 ExecuteAlways 지원.
- 이전 procedural environment builder는 Editor migration/검증에 남고 runtime은 authored room 사용.

### 25.2 기능 중복·유사 역할

| 조합 | 현재 판정 |
|---|---|
| WesternAtmosphere vs AuthoredAtmosphere | procedural 생성용 legacy와 saved room용. Main 방에는 AuthoredAtmosphere7개 |
| SceneryOccluder vs WorldProp.FadeWhenOccluding | legacy 논리 XY Sprite path vs authored XZ prop path |
| ExplorationStage.Create vs RoomAuthoring.Bind | 기존 생성 path vs 현재 Scene 참조 bind |
| Enemy.Init fallback vs authored refs | fallback 존재, 실제4 prefab은 authored refs로 반환 |
| ActorPresentation vs Player.Animate | 대체 visual 어댑터와 실제 sprite 모션. 현재 어댑터 custom 출력 미사용 |
| GameHUD vs GameUIView | 상태/event와 실제 Canvas 표시로 역할 분리. 동일 renderer 중복 실행 아님 |
| Built-in sprite/CartoonAtlas vs URP config | 이전 표현과 새 pipeline 설정 공존 |

`Backups/IncompleteEditableMigration/Shared`에는 Sprite/Texture 복사본과 Assets와 중복되는 GUID도 있다. 파일 이름/GUID가 같더라도 현재 Unity 에셋 해석은 Assets/Packages를 기준으로 조사했으며 Backups 파일로 잘못 연결하지 않았다. Backup 폴더는 현재 Assets 밖이다.

Old/New/Legacy/Backup/Copy라는 별도 runtime 시스템 세트 또는 같은 이름의 active duplicate gameplay class는 발견되지 않았다. 문서/툴/backup의 legacy 표기는 실제 사용 경로와 분리했다.

## 26. 기존 프로토 → 신규 URP 프로젝트 병합 상태

현재 Git에는 Initial commit(fcdb6ba)와 “아스트라 + URP 작업 올립니다”(d8eed84)가 보인다. 뒤 commit에 Scripts/Editable/Main/URP/Packages/ProjectSettings가 함께 추가되어 있다. 따라서 이 commit과 백업은 내부 근거지만, 각 개별 파일을 어떤 외부 Astra 작업에서 작성했는지/원래 다른 repository와 얼마나 동일한지는 확인 불가다.

| 영역 | 병합 판정 | 근거 |
|---|---|---|
| Player/Enemy/Raven/전투/RunBuild | [현재 실제 사용 중] | Main/Prefabs/script 호출 |
| 6방·서비스·진행 상태 | [현재 실제 사용 중] | Scene.Rooms 및 Runtime ActivateRoom |
| authored Canvas/아이콘/actor card | [현재 실제 사용 중] | GameUI/Hunter/Raven/Enemy prefab refs |
| Timeline3개 | [현재 실제 사용 중] | Main assets array+Play 호출 |
| URP package/PC renderer/global settings | [현재 실제 사용 중] (설정) | Graphics/Quality GUID 연결 |
| URP Light/Volume gameplay | [확인 불가]는 화면 결과; 실제 component는 발견되지 않음 | Scene/Prefab YAML |
| Spine/custom Animator | [병합되어 있으나 미사용] | adapter source/slot 존재, refs0 |
| procedural room builder/WesternAtmosphere/SceneryOccluder | [레거시] | Editor migration/검증 호출, Main component 없음 |
| old OnGUI HUD | [레거시] | ZIP 보관 및 현재 stub comments |
| Smoke scripts | [테스트] | explicit CLI branch only |
| incomplete Shared backup | [중복] [레거시] | Assets 밖 복사본/동일GUID |
| PassiveSO/context/registry 구조 | 현재 프로젝트에서 발견되지 않음 | 전체 C#/SO 검색 |
| 기존 외부 proto 전체와 동일성 | [확인 불가] | 비교 대상 원본 repository/branch 없음 |

중요한 문서 불일치: Assets/Editable/README.md는 Built-in 프로젝트 기준으로 가져오기를 안내하지만 현재 GraphicsSettings는 URP다. 일부 기존 Docs는 ExplorationStage.Create가 런 생성이라고 설명하지만 현재 Dungeon.BuildStage는 Scene.Rooms.Bind를 사용한다. 보고서의 판단은 문서보다 현재 코드/YAML을 우선한다.

## 27. 성능상 구조 확인 — 실행·프로파일링 없음

| 위치 | 구조상 확인된 작업 | 실제 사용 범위 |
|---|---|---|
| Dungeon.Update:138 | 매 프레임 GameHUD GetComponent, settings close 시  재조회 | Main |
| GameUIView.LateUpdate:34 및 Element:22 | GameHUD/각 Text/Image/Graphic GetComponent 반복 | 저장된 Canvas |
| GameUIView:39~104 | interpolated string/ToString/키 연결/text 갱신, panel 비활성여도 계산 | 매 LateUpdate |
| RunBuild:25 | Passives 접근마다 AsReadOnly wrapper 생성 가능 | GameUIView 및 map/기타 호출 |
| ActorPresentation:29 | skill 활성 시  new string[]; enum.ToString/Attack stage ToString | actor들 |
| PaperCard.LateUpdate:38 | Visible.GetComponent<SortingGroup> 반복 | actor Source 및 runtime FX |
| PropGrounding.Update:141~155 | decal마다 Transform/ray-plane/scale/color 갱신 | authored prop contacts |
| Room prefab 다수 sprite | 수천 GameObject/renderer 직렬화, contacts 분산 | 현재 방만 활성. 수량은 §18 |
| Enemy.Update:61 | 적별 전체 Enemies 순회 separation, 구조상 O(N²) | 매 gameplay frame |
| Player.Area/스킬·총알 | 여러 공격에서 Enemies 전체 순회, bullet별 substep | 활성 전투 |
| Ink.Ring/Burst/SkillEffects | 링24 line·스킬 stroke/스파크 반복 생성·수명 Destroy | 자주 발생하는 전투 표현 |
| Bullet.Spawn | Sprite/GameObject/PaperCard/Bullet 생성 및 Destroy | 총·적탄 |
| ClearTransient | 글로벌 타입 검색3회 및 객체 정리 | reset/room/wave/cinematic 전환 |
| GameUIView.Passives | 새 슬롯 Instantiate, reset시 전체 슬롯 Destroy | 획득/런 초기화 |
| ActorPresentation.CustomVisual sorting | GetComponent<SortingGroup> 반복 | 현재 custom 미사용 |
| CinematicDirector.ApplyCue Seal | GetComponentsInChildren<SpriteRenderer> 배열 반복 | 컷신 cue 적용 중 |
| atlas 첫 Load | GetPixels32/픽셀 순회/Sprite.Create/Material 생성 | 최초 로딩, static cache |
| FrontierDressing/EditableWorldBaker/Validation | ToArray/LINQ/Where/OrderBy/Distinct/All | Editor/legacy 생성·검증 |

Update에서 GameObject.Find/FindObject 전역 탐색, 매 프레임 LINQ, 매 프레임 Resources.Load는 정상 gameplay 코드에서 발견되지 않았다. GameHUD.Connect는 매 Update 호출되지만 observed가 있으면 즉시 반환한다. RoomDoor.Update는 marker 색을 매 프레임 설정하고 state 변경 event 기반은 아니다.

Update가 빈 채로 존재하는 MonoBehaviour는 발견되지 않았다. Spine의 빈 virtual override는 조건부 adapter stub이다. static 구조와 개별 반복에 대해 allocation/프레임 비용 가능성을 기록했으며 실제 GC양·FPS·drawcall·메모리는 **확인 불가**다. 이 절은 최적화/수정 제안이 아니다.

## 28. 현재 프로젝트 구현 현황표

상태 “완료”는 확인된 좁은 기능의 구현·연결 완료를 의미한다. 전체 제품 완성·URP 실행 검증을 뜻하지 않는다.

| 시스템 | 상태 | 핵심 파일 | 비고 |
|---|---|---|---|
| Player | 부분 구현 | Player.cs / Hunter.prefab | 현재 입력·액션·HP loop 구현, 통합 클래스/2무기 |
| Movement | 완료 | Player / Rules / HunterLocomotion | XY 수동 충돌·dash·walk/run·gait 연결 |
| Combat | 부분 구현 | Player / Enemy / CombatRules | 직접 피해·burn·combo, critical/knockback framework 없음 |
| Weapon | 부분 구현 | Player / HunterMotion / GameUIView | 단검/리볼버 코드 고정. SO/무기prefab 없음 |
| Passive | 부분 구현 | RunBuild / PassiveSlotView | 획득·합산·UI, 개별삭제/level/context/registry 없음 |
| Stigma | 부분 구현 | RunBuild / Dungeon / GameUIView | 3속성각인/6표시노드, 독립 노드 능력 없음 |
| Raven | 부분 구현 | Raven.cs / Raven.prefab | follow/attack/combo/link, 주변배회/소환해제 없음 |
| Enemy | 부분 구현 | Enemy.cs / Enemy_0~2 | 3kind, 수동이동·패턴, NavMesh/drop 없음 |
| Boss | 부분 구현 | Enemy.cs / Boss.prefab | Kind3·최종wave·등장·공통clear, 보스reward 없음 |
| Dungeon | 부분 구현 | Dungeon / ExpeditionProgress | 고정5stage6room flow, random generation 없음 |
| Room | 완료 | RoomAuthoring / RoomDoor / WorldFootprint | 저장6방/양방향문/완료상태/재방문 |
| Reward | 부분 구현 | Dungeon.ClaimCard/ClaimSigil | 서비스보상+회복18. Boss reward spawn 발견되지 않음 |
| UI | 부분 구현 | GameHUD / GameUIView / GameUI | 주요패널연결, 고정키·polling, economy/save 없음 |
| Save | 발견되지 않음 | 없음 | 로그 파일은 save가 아님 |
| Camera | 완료 | PaperWorld / Dungeon / CinematicDirector | perspective/follow/컷신zoom, shake 없음 |
| URP | 부분 구현 | PC / PC_Renderer / GraphicsSettings | 설정연결, 실제Light/Volume 및 shader 결과 미검증 |
| 2.5D | 완료 | PaperWorld / PaperCard / WorldDepth | 논리XY→표시XZ, 수동판정, Sprite card |
| Animation | 부분 구현 | HunterMotion / HunterLocomotion / ActorPresentation | Player atlas, Spine/Animator 기반 미사용, 적전용frame 없음 |
| VFX | 임시 구현 | Ink / SkillEffects / PropGrounding / AuthoredAtmosphere | 실제loop에 Sprite 생성·접지·먼지 연결 |

## 29. 현재 프로젝트 핵심 관계도

```text
Main (단일 Scene)
├─ Game_Systems
│  ├─ Dungeon.I ────────────── 전체 RunState/진행/참조 중심
│  │  ├─ ExpeditionProgress ─ Segment/Map/Wave/Visited/Cleared
│  │  ├─ GameSceneBindings ── 저장된 객체/Prefab 명시 참조
│  │  ├─ Enemies ──────────── 생성·순회·전멸 polling
│  │  ├─ Rules/Footprints ─── XY Rect 이동/스폰 기준
│  │  └─ Camera/Audio ─────── follow·합성Sound
│  ├─ RunBuild
│  │  ├─ Seongheun stacks ─── Player Fury/Enemy burn/Raven 강화
│  │  ├─ Passive inventory ── 공격/이동/까마귀 bonus
│  │  └─ C# events ───────── GameHUD의 pulse/toast
│  ├─ GameHUD ─────────────── UI 상태·알림 queue
│  └─ CinematicDirector ───── PlayableDirector/Timeline→camera/actor/UI/audio
├─ World (XZ로 저장)
│  ├─ Hirva
│  └─ Room_01~06
│     ├─ RoomAuthoring ────── marker/문/스폰/footprint capture
│     ├─ RoomDoor ─────────── E근접→Dungeon.TryDoor
│     ├─ WorldProp ────────── 정렬·가림·접지그림자
│     └─ AuthoredAtmosphere ─ 모래·노을 sprite
├─ Actors (논리 XY)
│  ├─ Hunter / Player
│  │  ├─ Input/Movement/Health/Weapon/Skills 통합
│  │  ├─ HunterLocomotion + HunterAtlas
│  │  ├─ ComboTracker→Raven 콤보
│  │  └─ Source Sprite→PaperCard→XZ View
│  ├─ Raven
│  │  ├─ Idle/Follow/Attack/Return/Combo/Link
│  │  └─ Source Sprite→PaperCard→XZ View
│  └─ Spawned_Enemies
│     └─ Enemy_0/1/2 또는 Boss(Enemy.Kind3)
│        ├─ 직접AI/HP/공격/죽음
│        └─ Source Sprite→PaperCard→XZ View
├─ GameUI
│  ├─ GameUIView ───────────── Dungeon/Player/Crow/Build 매프레임 조회
│  ├─ VisualCatalog ────────── 아이콘 SO
│  ├─ UICommand ────────────── Button event→기존 함수 호출
│  └─ PassiveSlotView ──────── 획득Prefab/tooltip
└─ Main Camera / Viewport_Backdrop / EventSystem

전투: Player/Enemy/Raven/Bullet→직접 HP 함수→Enemy Dead→목록0→wave/room 완료
서비스: 중앙E→UI 선택→Build 추가→서비스방 완료
진행: 6방완료→출구E→Route 선택→다음segment 또는Victory
보스전용 월드보상: 현재 연결 없음
```

## 30. 보스방 + 보상 작업을 위한 현재 연결 지점

### 30.1 기존 책임 클래스

| 확인 항목 | 실제 클래스 / 함수 |
|---|---|
| 1. Boss Death | Enemy.TakeDamage / Dead. Boss도 Enemy.Kind3 |
| 2. Room Clear | Dungeon.Update 전멸감지 + ExpeditionProgress.CompleteWave/CompleteService |
| 3. Dungeon 진행 | Dungeon.ActivateRoom/ChooseRoom/FinishExpedition, ExpeditionProgress |
| 4. Passive 획득 | Dungeon.AcquirePassive→RunBuild.AcquirePassive |
| 5. Currency 관리 | Currency class 없음. Dungeon.Supplies reset/display만 존재 |
| 6. Reward 관련 | 전용 class 없음. Dungeon.ClaimCard/ClaimSigil, HP+18 |
| 7. UI 호출 | State 변경→GameUIView polling, RunBuild event→GameHUD; button→UICommand |
| 8. 다음 방 이동 | Dungeon.TryTravel/TryDoor→Progress.TryVisit→ActivateRoom |
| 다음 stage | StageExit→OpenRoute(true)→UICommand.NextStage→ChooseRoom/FinishExpedition |

### 30.2 현재 존재하는 단계만 추적

| 단계 | 존재 여부 | 현재 연결점 / 실제 제약 |
|---|---|---|
| Boss Death | 존재 | Enemy.TakeDamage, 즉시 Dead/Kills/Destroy |
| Death→Room Clear | 존재 | Dungeon.Enemies 목록 정리/Count0→CompleteWave. 별도 boss death event 없음 |
| Room Clear 상태 | 존재 | cleared[Map]=true, State.Exit, HP+18, 문 표시 polling |
| Clear→Reward 생성 | 없음 | 일반적/보스 reward object 생성/테이블/spawner 없음 |
| Reward 선택/획득 | 서비스방에만 존재 | 카드3개/성흔3종 패널. 보스clear에서 여는 연결 없음 |
| Passive 반영 | 존재 | AcquirePassive API 및 Bonus. ClaimCard는 State.CardChoice/RoomKind.DevilCards 조건 |
| Stigma 반영 | 존재 | ApplySeongheunStack. ClaimSigil은 State.SigilChoice/RoomKind.Sigil 조건 |
| Currency 반영 | 없음 | Supplies 변경경제 API/실제 지급·구매 없음 |
| 획득 UI | 존재 | PassiveAcquired→GameHUD queue→Toast/슬롯 |
| 다음 방 | 존재 | 문 거리2.1/adjacency/canLeave 확인 뒤 ActivateRoom |
| 다음 진행 | 존재 | 모든6방 완료→stage exitE→RouteTravel→다음segment/Victory |
| Reward 완료 후 진행 gate | 없음 | reward completion flag와 기존 clear/travel 연결 없음 |

현재 Boss Room이라는 별도 Scene/전용 class도 없다. 최종 segment의 Room06 마지막 wave가 보스 역할을 한다. Boss clear가 전체6방완료와 동일하지 않으므로 제단·카드를 남겨 두면 최종출구는 계속 잠긴다. 보고서에는 기존 연결점과 없는 연결만 기록했으며 보상 시스템 구현안·새 클래스 설계는 제시하지 않는다.

## 31. 최종 요약

### 현재 프로젝트 한 줄 구조 정의

**단일 Main Scene의 Dungeon 중심 런 상태 관리에, 통합 Player·공용 Enemy·독립 Raven·RunBuild 성흔/패시브·고정6방 탐험·serialized Canvas를 연결한 서부 오컬트 프로토타입이다.**

### 현재 2.5D 구현 방식

논리 XY의 수동 이동·충돌·전투 계산과 표시 XZ를 분리하고, PaperCard가 Source Sprite를 Visible Sprite로 복사한다. pitch47 perspective와 SortingGroup/order로 카드형 2.5D 화면을 구성한다. Rigidbody/Collider/NavMesh 기반 게임은 아니다.

### 현재 URP 구현 방식

URP17.3.0과 PC UniversalRenderer/Forward가 Graphics/Ultra에 연결되어 있다. Renderer feature는 없고 저장 Scene Light/Volume도 없다. 기존 builtin sprite material과 custom unlit CartoonAtlas 표현이 남아 있다. Profile은 등록되지만 실제 URP rendering 결과는 이번 분석으로 확인하지 않았다.

### 가장 중심이 되는 Manager / 시스템

Dungeon.I, GameSceneBindings, ExpeditionProgress, RunBuild이다. Dungeon은 진행·적목록·카메라·오디오·서비스 상호작용을 관리하고 대부분 runtime 코드가 이를 역참조한다.

### 하드코딩 의존이 큰 영역

Weapon0/1·스킬1~4·Enemy Kind0~3, passive3ID/bonus, 성흔3속성 배열, RoomGraph6방/5stage/보스조건, UI327키와 index, Resources art ID, 기존 환경템플릿·접지 anchor, camera·전투 balance 수치다. 연결은 Inspector도 사용하며 모든 문자열이 GameObject.Find 의존은 아니다.

### 아직 프로토타입 성격이 강한 영역

고정 카드효과/무료서비스, 공용 Enemy PNG·boss branch, 합성사운드, procedural Sprite VFX, currency필드만 존재, 개별passive관리·저장·drop·보스reward 미구현, Spine/custom slot 기반만 존재한다.

### 실제 플레이 루프에서 현재 연결되어 있는 영역

Title→Town→출구E→Route→stage입장Timeline→6방 탐험(전투wave/제단/카드)→전투전멸clear 및 서비스 선택완료→6방완료→출구E→다음stage→최종bosswave→공통roomclear→출구/승리다. Player 죽음은 PlayerDeath Timeline 뒤 Results로 이어진다. 이 연결은 코드·저장참조 기준이다.

### 보스방/보상 작업 전에 반드시 이해해야 할 파일

| 우선 파일 | 이해해야 하는 현재 책임 |
|---|---|
| Assets/Scripts/Enemy.cs:78 | Boss도 공용 TakeDamage/Dead/Destroy, death event 없음 |
| Assets/Scripts/Dungeon.cs:122 | SpawnWave/최종보스/Timeline |
| Assets/Scripts/Dungeon.cs:135 | Update 전멸→wave/room clear, HP18, State.Exit |
| Assets/Scripts/ExpeditionProgress.cs:25 | 웨이브/FinalBossWave/CompleteWave/6방완료 조건 |
| Assets/Scripts/Dungeon.cs:80 | 문 이동/서비스/route/다음segment |
| Assets/Scripts/RunBuild.cs:48 | passive획득/정의catalog/bonus/event/reset |
| Assets/Scripts/Dungeon.cs:112 | ClaimSigil/ClaimCard의 서비스상태 검증 |
| Assets/Scripts/Authoring/GameUIView.cs | state-based panels/HP/boss/map/선택/획득UI |
| Assets/Scripts/Authoring/UICommand.cs | 실제 버튼 진입점 |
| Assets/Scripts/Authoring/GameSceneBindings.cs | Prefab 배열 순서·Scene 참조 |
| Assets/Scripts/Authoring/RoomAuthoring.cs | XZ marker↔논리Vector2/footprint/문 owner |
| Assets/Scripts/RoomDoor.cs | 문 위치/Arrival/StageExit 및 색상 |
| Assets/Scripts/Cinematics/CinematicDirector.cs | boss입장/사망 UI·조작/카메라 상태 복원 |
| Assets/Scenes/Main.unity | 실제 bindings/fileID/prefab instance |
| Assets/Editable/Actors/Boss.prefab | Kind3/HP650/Presentation 연결 |
| Assets/Editable/Rooms/Room_06.prefab | 실제출구/도착지점/스폰/footprint |
| Assets/Editable/UI/GameUI.prefab | 실제패널·button event·UI key |
| Assets/Settings/PC.asset 및 PC_Renderer.asset | 현재 rendering 설정, 실제Light가 없는 상태 |

### 분석 과정에서 확인이 불가능했던 부분

- Unity 실행·테스트·빌드를 금지했으므로 실제 실행 성공, 컴파일 결과, 화면·shader 호환/색·핑크 material 여부, post-processing 최종 결과는 확인 불가.
- 실제 FPS/GC/drawcall/메모리/로드시간/카메라 체감·까마귀 움직임의 자연스러움은 확인 불가.
- 외부 원래 proto/Astra repository 전체와의 비교, 개별파일의 작업자별 출처·개발의도는 확인 불가. 현재 Git의 묶음 commit과 내부backup 범위까지만 확인.
- builtin Sprite/UI shader 전체 구현은 Assets에 없으므로 내장shader 모든 pass의 상세 depth/lighting 동작은 확인 불가. custom CartoonAtlas는 실제 source를 확인.
- 과거 Docs/Logs/Verification의 PASS/완료 문구는 현재 URP 실행검증으로 간주하지 않았다.
- Resources/Western/ART_SOURCE.md에는 일부 문자의 손상/대체 문자로 인해 원문 문구를 정확히 복원하지 못한 부분이 있다. 아트 구조 판단은 bounds.json·실제 경로·코드를 근거로 했다.
- “발견되지 않음”으로 표시한 저장/통화/보스reward/패시브SO·context·registry/NavMesh/Cinemachine/실제Spine는 현재 프로젝트의 구현·패키지·참조 전수검색 결과다.

---

## 부록 A. C# 58개 조사 범위와 실제 사용 분류

정적 helper는 serialized m_Script 참조가0이어도 호출되어 사용될 수 있다. 아래 표는 코드 호출과 저장 연결을 함께 판정했다.

| 그룹 | 파일 | 현재 분류 |
|---|---|---|
| 핵심gameplay(10) | Dungeon, Player, Enemy, Raven, Rules, CombatRules, RunBuild, ExpeditionProgress, GameHUD, RoomDoor | Main 및 도달 가능한 runtime 호출 |
| 아트·2.5D·VFX(9) | CombatArt, HunterMotion, HunterLocomotion, Ink, PaperWorld, PaperCard, WorldDepth, PropGrounding, SkillEffects | 실제 actor/FX/world 호출 |
| 기존환경(5) | WesternEnvironment, ExplorationStage, FrontierDressing, WesternAtmosphere, SceneryOccluder | WesternArt/Layout/scale 일부공유; 절차생성·atmosphere·occluder는 legacy/Editor path |
| parallax(1) | LandscapeParallax | 저장room7개씩 총49개 연결 |
| 레거시HUD(3) | BuildHUD, CinematicHUD, ExplorationHUD | 주석만존재 |
| runtime검사(4) | RuntimeSmoke, MotionSmoke, MovementSmoke, LevelSmoke | 실행인자 opt-in; 실행안함 |
| Authoring(13) | ActorAnimationDriver, ActorPresentation, AuthoredAtmosphere, AuthoringSmoke, GameSceneBindings, GameUIView, PassiveSlotView, RoomAuthoring, SpineAnimationDriver, UICommand, VisualCatalog, WorldFootprint, WorldProp | 주adapter2개미사용/검사1개opt-in; 나머지저장참조 |
| Cinematics(3) | CinematicDirector, CinematicClip, CinematicTrack | Main 및Timeline3개연결 |
| Editor(10) | MotionSetup, ProjectSetup, TimelineSetup, WesternArtImporter, AuthoringFinalization, EditableAssetStore, EditableProjectSetup, EditableUIBaker, EditableValidation, EditableWorldBaker | import/menu/migration/export/검증/빌드 도구. 실행안함 |

부록의 파일명은 .cs를 생략했다. Enemy.cs에는 Bullet, Ink.cs에는 Fade, WesternEnvironment.cs에는 WesternArt, WesternAtmosphere.cs에는 SandWake, HunterMotion/Locomotion에는 Atlas helper, CinematicClip에는 Behaviour/Mixer도 포함된다. 따라서 파일수와 클래스수는 같지 않다.

## 부록 B. 재현 가능한 근거 범위

- 설정: Packages 2개, ProjectSettings 24개, Assets Settings/URPDefaultResources/DefaultVolumeProfile의 실제 YAML 및 .meta.
- Scene/Prefab: Main 1개, Actors6/Rooms7/UI2 총15개. 모든 block type과 m_Script GUID, renderer/material refs, 핵심component fields, PrefabInstance override 및 binding fileID를 대조했다.
- 데이터: VisualCatalog25entries, Western.bounds41entries, Shared Sprite/Texture/Material의 type/texture/shader GUID.
- animation: Timeline3개 각각7개 output track, 길이2.8/2.7/3.4초, audio refs; atlas용 shader/source와 importer.
- backup: ZIP을 해제하지 않은 목록/기존Dungeon·HUD·PaperWorld·Main 비교, IncompleteEditableMigration의 meta/GUID와 Assets 구분.
- 내부Git: HEAD와2개commit 기록, 대상폴더 변경목록. 새commit/index변경을 만들지 않았다.
- 원본파일 보존확인: 보고서를 제외한 Assets/Packages/ProjectSettings/Docs/Backups/Previews/Verification 및 README/.gitignore의 파일목록·내용을 SHA-256으로 전후 대조했다. 4,872개 원본 파일의 통합 해시가 분석 전후 동일했다(5e5fa7300477885675a27701d4afd0ff2f800d99dbc61c9ba48882254cb0b489). Git 상태에서도 기존 ShaderGraphSettings.asset 수정 표시와 새 보고서 한 파일만 확인되었다. 상세 실행로그·스크립트 같은 별도 분석파일은 생성하지 않는다. Assets 및 로컬 PackageCache의 .meta에서 참조한 외부 GUID를 해석했으며, 현재 조사한 Assets 텍스트 에셋에서 해석되지 않은 일반 외부 GUID는 발견되지 않았다. builtin GUID/fileID는 별도로 구분했다.
