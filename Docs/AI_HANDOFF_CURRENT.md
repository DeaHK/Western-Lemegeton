# Western-Lemegeton — Current AI Handoff

## 1. 문서 기준

- 작성 날짜: 2026-10-03.
- 프로젝트: `E:\GitHub\Western-Lemegeton`.
- 확인한 HEAD: `8208001d6dd271e099d488f40780411c680f5e45`.
- Git 상태: 구조 정리 이후의 패시브 코드·Scene·Excel·생성 데이터 변경이 작업 트리에 남아 있다. HEAD만으로 현재 구현을 판단하지 않는다. 이번 문서 통합은 commit/push를 수행하지 않았다.
- 이 문서는 현재 상태의 첫 인수인계 문서다. 판단 순서는 **현재 Assets 코드 → Main Scene 연결 → 실제 Database/생성 데이터 → 이 문서와 [Passive System Current](PASSIVE_SYSTEM_CURRENT.md) → 기존 전체 분석서**다. 이후 코드가 바뀌면 실제 코드를 우선하고 이 문서를 갱신한다.
- `[확인]`은 코드/YAML/데이터 정적 대조, `[사용자 확인]`은 사용자가 전달한 실행 결과, `[예정]`은 미구현 방향이다. 이번 Docs 작업에서는 Unity, Play Mode, Build 및 Editor 메뉴를 실행하지 않았다.

## 2. 프로젝트 한 줄 정의

단일 Main Scene의 `Dungeon.I`를 중심으로 수동 XY 이동·전투, XZ Sprite 표현, 고정 방 탐험, 성흔과 DB 기반 패시브를 연결한 서부 오컬트 액션 프로토타입이다.

## 3. 현재 Scene / 핵심 구조

[확인] `Assets/Scenes/Main.unity`의 기존 `Game_Systems`에 Dungeon, GameSceneBindings, RunBuild, GameHUD, CinematicDirector, AudioSource, PlayableDirector가 배치되어 있다. 신규 패시브 컴포넌트도 이 GameObject에 각각 **1개**씩 배치되어 있다.

| 컴포넌트 | 현재 연결 |
|---|---|
| PassiveManager | database → `Assets/Resources/Passive/PassiveDatabase.asset`; effectRegistryOverride=null; startingPassives=0; logChanges=false |
| PassiveRuntimeContext | defaultFlatStatTarget → 같은 Game_Systems의 StatModifierContainer |
| StatModifierContainer | 여러 StatType의 modifier를 key별로 보관하는 공통 Flat 대상 |
| PassiveOfferService | Awake에서 같은 GameObject의 PassiveManager 캐싱 |

GameSceneBindings가 Hunter, Raven, GameUI, Rooms/EnemyPrefabs 등의 authored 참조를 제공한다. Dungeon은 같은 GameObject의 패시브 컴포넌트를 캐싱하여 `Passives`, `PassiveOffers`, `PassiveStats`로 노출하며 누락된 필수 연결은 오류로 처리한다. 새 전역 Find나 별도 패시브 Scene은 없다. Camera follow는 계속 Dungeon이 담당하고 Timeline/CinematicDirector 연계가 유지된다.

현재 Hunter/Raven은 Sprite 표현 경로를 사용한다. `ActorPresentation`의 CustomVisual/Animator/AnimationDriver 슬롯은 준비된 확장 경로다. 활성 custom visual 경로의 `AnimationRequested`를 실제 Animator 콘텐츠 구현 완료로 해석하지 않는다. walk/run 및 기존 HunterLocomotion은 유지된다.

## 4. 2.5D 좌표 구조

[확인] 이동·공격·적 탐색의 gameplay 좌표는 `Vector2` XY다. `Rules`와 `CombatRules`의 Rect/거리/원뿔/선 판정 및 수동 장애물 해결을 사용한다. Rigidbody, Collider, NavMesh 기반 이동으로 전환하지 않았다.

표시 세계는 XZ다. `PaperWorld.Point(p, h)`는 `(p.x, h, p.y)`로 변환하고, `PaperCard`는 논리 Source Sprite와 Visible Sprite의 표시를 연결한다. authored Room/marker의 x/z를 논리 x/y로 읽는다. 화면의 Sprite 위치나 Transform.forward를 gameplay 기준으로 바꾸지 않는다. 고정 pitch 및 SortingGroup/WorldDepth의 표시 정렬 계약도 유지한다.

## 5. 현재 핵심 시스템 책임

| 시스템 | 현재 실제 책임 |
|---|---|
| Dungeon | singleton/Scene 바인딩, RunState, 런·방·Wave·Stage 진행, 적 목록, 서비스방, 카메라·오디오·컷신 연결, 패시브 컴포넌트 캐싱/Reset |
| Player | 입력, walk/run/수동 이동/Dash, 체력·피해, 무기·탄약·Reload, 기본공격·스킬·Combo, Raven 연계, animation 요청 |
| Enemy | Kind 0~3의 기존 이동·공격·Burn·피격·공통 사망·presentation; 같은 파일의 Bullet 이동/충돌/수명 |
| Raven | 기존 Command, Combo follow-up, R Link, 동행/추적/복귀와 직접 공격; CrowATK 및 기존 성흔 가산 |
| RunBuild | Fire/Nature/Butterfly 성흔 stack/threshold/active/effect level, 변경 event와 성흔 Reset |
| PassiveDatabaseSO | PassiveSO 정의 목록, ID 조회 cache, EffectRegistry 참조 |
| PassiveManager | ID별 보유 Instance, 획득/중복 Stack, Source, 효과 적용/제거, 획득 순서와 event/Reset |
| PassiveOfferService | 지원 DB 후보 필터, 결정적 3택1, 문맥별 선택지 유지, claim |
| StatModifierContainer | sourceKey별 Raw/Percent modifier 교체·제거·합산 및 Evaluate |
| GameHUD | 성흔 pulse/표시 상태, 신규 패시브 획득 Toast queue, UI 상태/시간 |
| GameUIView | UGUI 참조 Dictionary, LateUpdate polling, HUD/슬롯/Tooltip/Toast/선택창/지도/결과/컷신 표시 |
| ExpeditionProgress | 현재 고정 Stage/Room의 방문·완료·Wave·서비스·Stage 진행 상태 |

## 6. 2026-10-03 구조 정리 결과

| 파일 | 정리한 책임 경계 |
|---|---|
| Player.cs | Input / Movement / Dash / Health / Basic Attack / Weapon·Ammo·Reload / Skills / Combo / Raven / Presentation / Reset / Query |
| Dungeon.cs | Scene·RunState / Reset·Entry / Travel·Service / Wave·Enemy / Clear·Progression / Death·Victory / 기존 Boss 분기 / Camera·Audio·Cinematic / Query·Smoke |
| Enemy.cs | 초기화·Update·Target / Movement·Separation·Obstacle / 공통 공격 시간 / Kind별 공격 / Damage·Burn·Death / authored/fallback 표현; Bullet 책임은 같은 파일 안에서 별도 블럭 |
| GameUIView.cs | 참조·초기화·갱신 흐름 / Panel·Settings / Player·Skill·Raven HUD / Passive / Stigma / Map·Route / Boss prototype / Results·Cinematic / 조회·format |
| RunBuild.cs | 먼저 Passive/성흔 블럭을 정리한 뒤, 구 Passive 블럭을 제거하여 성흔 상태·event·조회·변경·Reset만 남김 |

블럭화 단계는 기존 실행 순서/public·직렬화 계약을 유지한 private method 추출이었다. 아직 PlayerMovement 등의 별도 클래스 분리는 하지 않았다. 이후 패시브 전환은 별도의 기능 변경이며 아래 현재 흐름이 기준이다. 향후 분리는 블럭 경계를 사용하되 필드 직렬화, 호출 순서, event 발행 시점부터 확인한다.

이전 문서에서 이어받을 절단선: Player는 action 상태 캡처 → Skill → Basic → Movement → Locomotion → Animate 순서, Mark → 피해 → Raven 후속 연계를 유지한다. Enemy는 Running/Dead 검사 → Burn(사망 시 return) → Flash/HP → target → charge 또는 windup의 조기 return → cooldown/일반 이동·공격 순서를 유지한다. authored Art/Sprite/HealthFill/Warning이 갖춰진 경로와 fallback 생성 조건을 바꾸지 않는다. Bullet.Spawn signature와 수동 substep 충돌/Destroy 계약도 유지한다.

GameUIView의 실제 LateUpdate는 Panel → Location/Supplies → Core HUD → Health/Ammo → Player skills → Raven skills → Stigma HUD → Prompt → Map → Sigil → Passives → Results → Boss prototype → Raven prompt → Cinematic 조건부 순서다. 비활성 Panel이라는 이유로 기존 무조건 갱신을 생략하지 않는다. Dungeon은 문 처리 뒤 서비스 상호작용, WaveBreak의 기존 조기 return, enemy cleanup/clear 순서 및 Cinematic 우선 Camera 반환을 유지한다. Camera Reset과 follow의 서로 다른 계산 계수를 합치지 않는다.

## 7. 현재 Passive 시스템

```text
Assets/Data/Passive/패시브.xlsx (Passive Sheet)
  → Parse/Validate → 수동 Import
  → PassiveSO → PassiveDatabase + EffectRegistry
  → PassiveOfferService → 카드방 3택1
  → PassiveManager.AcquirePassiveById → Added / Stacked
  → StatModifierContainer (Flat) / EffectScript (framework)
  → GameHUD → GameUIView Slot / Tooltip / Toast / RouteBuild
```

Runtime namespace는 `WesternLemegeton.Passives`, Editor는 `WesternLemegeton.Passives.Editor`다. 자동 Registry 생성은 차단되어 있다. Editor 로드만으로 Import/Rebuild가 실행되지 않는다.

[확인] 실제 Database는 null 없는 ID 1~18의 **18 definitions**와 Registry 참조를 갖는다. Registry entry는 **0개**이며 구체 효과 미구현에 맞는 현재 상태다.

| 데이터 분류 | 수 |
|---|---:|
| Flat / Ability / Shift | 11 / 6 / 1 |
| Common / Uncommon / Rare / Legendary | 10 / 4 / 3 / 1 |

현재 gameplay에 연결된 Stat은 **ATK / Move / CrowATK**다. 지원 카드 후보는 ID **1, 4, 5, 6, 7**의 5개이며 모두 Common이다. 4단계 UI 지원과 현재 카드 후보의 등급 분포는 구분한다. 나머지 Flat Stat 및 Ability/Shift는 데이터로 보존되지만 현재 카드 후보에서 제외된다. 상세 API·원본 데이터 주의점은 [Passive System Current](PASSIVE_SYSTEM_CURRENT.md)를 읽는다.

## 8. 현재 카드방 동작

04 악마카드 역참에서 `Dungeon.TryTravel`이 `PrepareOffer(EventReward, Seed, Room, MapIndex, 3)`를 성공시킨 뒤 CardChoice에 진입한다. 여기서 `Room`은 Progress의 Segment/Stage 값이며 `MapIndex`가 Stage 내부 방 문맥이다. 후보를 ID 순으로 정렬하고 로컬 System.Random으로 shuffle하여 서로 다른 ID 3개를 만든다. 전투/Spawn의 UnityEngine.Random 상태는 건드리지 않는다.

같은 Seed/Stage/Map/source/count로 ESC 닫기 → E 다시 열기를 하면 선택지가 유지된다. 기존 보유 ID도 후보이며 재획득은 Stack+1이다. `Dungeon.ClaimCard(index)` → Offer.TryClaim → Manager.AcquirePassiveById(EventReward)의 성공 뒤에만 `Progress.CompleteService` → `State.Exit`가 진행된다. 실패는 선택지/미완료 상태를 유지한다. 성공한 claim은 목록을 비우되 완료 문맥을 기억하며 같은 문맥을 reroll하지 않는다. 새 런/다른 문맥에서 새 offer를 준비한다.

현재 이동 입력 WASD, Shift 걷기, Space Dash, Q 무기 전환, 2/3 무기 스킬, 1 Raven Command, R Link, E 문/서비스 상호작용, M 지도 및 ESC 흐름을 유지한다. 루트 후보의 새 키 배치를 적용하지 않았다.

## 9. RunBuild 현재 상태

**RunBuild 패시브 책임 제거 완료.** 구 `PassiveRarity`, `PassiveSource`, `PassiveDefinition`, `OwnedPassive`, catalog/inventory/Passives, AcquirePassive/PassiveAcquired, 3개 bonus 및 등록 Awake가 제거되었다. Dungeon의 legacy AcquirePassive wrapper와 CardRarity property도 제거되었다.

현재 `SeongheunType { Fire, Nature, Butterfly }`, stacks/thresholds, Stack/Threshold/IsActive/EffectLevel, SetThreshold/SetStack/ApplySeongheunStack, StackChanged/Reset, ResetRun/Index만 담당한다. ResetRun은 stack을 비운 뒤 Reset을 발행하며 threshold는 유지한다. 성흔 HUD·효과·event 연결은 계속 사용 중이다.

enum/index는 Fire0/Nature1/Butterfly2, 기본 threshold는 각각1이다. SetThreshold는 최소1이며 같은 stack을 old/new로 통지한다. SetStack은 최소0으로 clamp하고 값이 달라질 때 통지한다. 증가 계산은 long을 사용해 int.MaxValue 상한을 처리한다. active는 stack≥threshold, effect level은 active일 때 stack/아니면0이다. UI의 6개 성흔 표시 node는 독립 skill tree 구현으로 해석하지 않는다.

**RunBuild 자체도 최종 구조에 남길 예정이 아니며 향후 StigmaManager 이관 후 삭제 예정.** 상태/event를 이관하고 모든 `g.Build` 및 Dungeon의 성흔 접근/observer를 전환한 뒤 컴포넌트·Scene 계약을 정리한다. 현재 패시브를 RunBuild에 다시 넣지 않는다.

## 10. Dungeon 현재 구조와 미래 목표

[확인] 현재 고정 **5 Stage × 6 Room**, fixed graph/RoomGraph와 Rooms 배열 계약을 사용한다. 03 성흔, 04 카드, 06 출구/마지막 Stage의 기존 Kind3 분기가 있다. 실제 방 이동은 RoomDoor까지 걸어가 E를 누르는 방식이다. M 지도는 상태 표시이며 Route는 Town→Stage/Stage→다음 Stage 진행용이다.

적 사망 → Enemy.Dead → Dungeon의 목록 정리 → Enemies.Count==0 → Progress.CompleteWave → 다음 Wave 또는 Room Clear → 기존 HP+18 → State.Exit 흐름을 유지한다. Kind3도 이 공통 사망/전멸 흐름을 사용하며 BossKilled 전용 event나 보상 gate가 없다.

향후 Boss 이전 후보는 Enemy의 `ExecuteBossPattern`, `BeginBossCharge`, `FireBossRadialPattern`, `CreateBossFallbackPresentation`이다. 공통 charge/target/damage/death 및 Dungeon의 기존 Boss spawn/Timeline 분기도 함께 의존한다. prototype은 HP가 엄격히 절반 미만일 때 강화, 3회 주기 charge를 사용한다. AttackPower query와 실제 charge 피해값이 같은 계약이라고 가정하지 않는다. Boss HUD는 IsBossWave/Enemies[0] 경로 그대로이며 현재 정식 콘텐츠 검증 완료 대상으로 보지 않는다.

[예정] topology를 랜덤 가지치기 `RunMap / Node / Edge`로 바꾸고 Layout/Encounter를 구분한다. 현재 Room layout은 layout pool 후보, 고정 6 node Map/Route UI 블럭은 동적 graph View 교체 후보다. **실제 문 E 이동은 유지하면서 topology를 랜덤화할 예정**이며 지도 클릭 텔레포트 계획으로 해석하지 않는다. Enemy에 특정 Room 번호/문 목적지 의존성을 새로 추가하지 않는다.

## 11. 아직 구현되지 않은 시스템

- PassivePickup, WorldRewardSpawner/RewardSpawner, Spawned_Rewards, 공통 획득·월드 아이템 interaction 우선순위.
- 실질 Currency, Shop runtime, Boss Reward, Save 시스템.
- 정식 BossController 및 실제 보스 콘텐츠. Enemy Kind3/IsBossWave/Boss HP·Timeline 경로는 **prototype/legacy branch**다. 기존 spawn/패턴/UI 분기가 있다는 사실과 정식 콘텐츠 완성을 구분한다.
- Raven 정식 Summon/Recall 및 자연스러운 Idle 표현. 기존 Command/Combo/R Link/follow 기능은 존재하며 유지 대상이다.
- Ability/Shift 구체 PassiveEffectScript. ScriptName은 기획 데이터와 미래 구현 연결점이다.
- HP, StigmaATK, CrowRange, CritDamage, CritRate, StigmaDamage의 실제 gameplay Stat bridge.

Dungeon의 기존 Supplies/서비스 소비 표시는 존재하지만 이를 완성된 화폐·상점 경제로 해석하지 않는다.

## 12. 다음 권장 작업 순서

1. StigmaManager 이관, 모든 Build 성흔 참조 전환, RunBuild 제거.
2. 책임 블럭을 기준으로 실제 클래스 분리 2차.
3. Passive field item foundation: Pickup/WorldRewardSpawner, 공통 Acquire, Spawned_Rewards, E Interaction 우선순위.
4. Dungeon random branching 및 Node/Edge/Layout/Encounter, 동적 지도 UI.
5. Reward / Currency / Shop.
6. Raven Summon/Recall/Idle 및 기존 연계 보존.
7. 정식 BossController / Death·Clear / Reward.
8. 일반 Enemy 콘텐츠·데이터/Encounter 연결.
9. 실제 측정에 따른 최적화.
10. URP visual 작업.

각 항목은 향후 작업 방향이며 이번 Docs 작업의 구현 범위가 아니다.

## 13. 작업 원칙

- 현재 기능과 public/serialized API를 먼저 확인하고 단계적으로 교체한다. 범위 밖 리팩터링, 전체 재분석 반복, UI/Scene/Prefab의 불필요한 재구축을 하지 않는다.
- 컴파일 Error와 작업으로 생긴 Runtime Exception은 즉시 해결한다. 기존 gameplay 품질 문제·기술 부채는 별도 작업에서 다룬다.
- gameplay XY / display XZ, Dungeon.I, Scene bindings, 이동/전투 수치와 명시된 event/Reset 순서를 유지한다.
- 기능 이전 후 기존 경로 참조 0개를 확인하고 제거한다. RunBuild 패시브 제거는 완료되었고 성흔 제거는 아직 아니다.
- 사람이 직접 보는 Play Mode/조작/화면 테스트는 사용자에게 맡긴다. 정적 검증, 사용자 보고, 미실행 검사를 구분한다.

[사용자 확인] Excel Import/Verify 및 DB 카드방 3택1, ATK/Move/CrowATK, Slot/Toast/RouteBuild, 신규 패시브 Reset 정상 동작이 보고되었다. 최근 legacy 제거 문서는 Roslyn 정적 오류 0개를 기록하지만, cleanup 이후의 전체 Unity 회귀 테스트 완료를 대신하지 않는다. 이번 문서 작업은 Scene YAML/연결, DB18/후보5/분포, legacy 참조를 읽기 전용으로 다시 확인했다.

Smoke는 opt-in 상태다. `RuntimeSmoke`/`AuthoringSmoke`의 구 패시브 assertion은 신규 API/Stack/Source/SO rarity 기준으로 전환되었다. ID1/4/5와 ID1 Stack2=ATK Percent30을 전제로 하는 fixture이므로 데이터셋 변경 때 검토한다. Smoke 전체 및 다른 검사 흐름은 유지되며 이 문서 작업에서는 실행하지 않았다.

## 14. 반드시 읽을 파일

먼저 이 문서와 [Passive System Current](PASSIVE_SYSTEM_CURRENT.md)를 읽고 작업 관련 파일만 추가로 확인한다.

| 범위 | 파일 |
|---|---|
| 런/성흔 | `Assets/Scripts/Dungeon.cs`, `ExpeditionProgress.cs`, `RunBuild.cs` |
| 전투/좌표 | `Assets/Scripts/Player.cs`, `Enemy.cs`, `Raven.cs`, `Rules.cs`, `CombatRules.cs`, `PaperWorld.cs`, `PaperCard.cs` |
| 표현 | `Assets/Scripts/Authoring/ActorPresentation.cs`, `Assets/Scripts/HunterLocomotion.cs`, `HunterMotion.cs`, `WorldDepth.cs` |
| 패시브 | `Assets/Scripts/Passives/Runtime/Core/PassiveManager.cs`, `PassiveOfferService.cs`, `PassiveInstance.cs`, `PassiveAcquisition.cs`, `PassiveRuntimeContext.cs` |
| 정의/계산 | `Assets/Scripts/Passives/Runtime/Data/PassiveSO.cs`, `PassiveDatabaseSO.cs`, `PassiveEnums.cs`; `Runtime/Stats/StatModifierContainer.cs`, `WesternPassiveStatKeys.cs` |
| 제작 | `Assets/Scripts/Passives/Editor/WesternPassiveDataGeneration.cs`, `PassiveExcelImporter.cs`, `SimpleXlsxReader.cs`, `PassiveEffectRegistryGenerator.cs` |
| UI/연결 | `Assets/Scripts/GameHUD.cs`, `Assets/Scripts/Authoring/GameUIView.cs`, `PassiveSlotView.cs`, `UICommand.cs`, `GameSceneBindings.cs` |
| 실제 저장 상태 | `Assets/Scenes/Main.unity`, `Assets/Editable/UI/GameUI.prefab`, `Assets/Data/Passive/패시브.xlsx`, `Assets/Resources/Passive/PassiveDatabase.asset`, `PassiveEffectRegistry.asset` 및 `Passive_001~018.asset` |

## 15. 기존 전체 분석서 사용법

[WESTERN_LEMEGETON_PROJECT_ANALYSIS.md](WESTERN_LEMEGETON_PROJECT_ANALYSIS.md)는 Scene/좌표/아트/진행의 전체 baseline이다. 특히 §13 패시브, RunBuild 책임·하드코딩 3종·옛 bonus/획득/event, 관련 Player/Raven/UI 설명과 “SO/Registry 없음”은 현재보다 오래된 설명이다. 이 부분은 현재 문서와 실제 코드로 대체해서 읽는다. baseline 자체를 덮어쓰지 않았다.

루트 `ActorPresentation.cs`, `Dungeon.cs`, `GameUIView.cs`, `MovementSmoke.cs`, `Player.cs` 및 Verification의 과거 복사본은 **Assets runtime이 아니다**. 루트 5개는 미완성 교체 후보이며 통째 복사/병합하지 않았다. SpriteAnimator/프레임 catalog, Raven 소환·표시 API, CameraFollow2_5D/PresentationSmoke 등의 의존성이 충족되지 않고 걷기/Command·입력/UI 손실 위험이 있다. `--presentation-test`/`--crow-test` 등을 현행 Dungeon 지원 인자로 가정하지 않는다. 과거 후보 통합을 재개하려면 실제 Assets에 대해 독립 diff와 누락 API/Prefab 계약을 다시 검증한다.

현재 `Assets/Editable/README.md`의 Dungeon.AcquirePassive 안내도 오래된 설명이다. 코드 검색은 우선 `Assets/**/*.cs`로 한정한다. 해당 범위에서 old RunBuild passive API/DTO/bonus 및 옛 3종 ID 사용은 **0개**다. `CardRarity0~2` 문자열은 현재 UIReference key이며 삭제된 Dungeon.CardRarity property와 구분한다. 남은 AcquirePassive/PassiveAcquired/PassiveRarity는 신규 패시브 API/event/enum이다.

유지한 주제 문서는 BuildIntegration, HunterMotion, LevelDressing, Locomotion, MotionArtPrompts, TimelineIntegration이다. 중간 분석/블럭화/패시브 Phase 문서의 현재 유효 계약은 이번 두 기준 문서와 [작업 로그](WORK_LOG_2026-10-03.md)에 통합하였다.
