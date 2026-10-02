# GameUIView Block Refactor

- 작업일: 2026-10-03 (Asia/Seoul).
- 대상: `E:\GitHub\Western-Lemegeton\Assets\Scripts\Authoring\GameUIView.cs`.
- 시작 HEAD: `c218280dbb80d83b03e17b05ad92f8bd50df86a3`.
- 기준: 작업 시작 시 Assets의 실제 GameUIView. 루트 후보 GameUIView의 코드/키/입력/Raven UI는 가져오지 않았다.
- 참고: `Docs/WESTERN_LEMEGETON_PROJECT_ANALYSIS.md`, `Docs/PLAYER_BLOCK_REFACTOR.md`, `Docs/DUNGEON_BLOCK_REFACTOR.md`, `Docs/ENEMY_BLOCK_REFACTOR.md`, `Docs/ROOT_CODE_INTEGRATION_ANALYSIS.md`의 관련 계약.
- [확인] GameUIView 내부 배치·주석·공백 정리와 private 함수 추출만 수행했다. 기존 UIReference 클래스의 직렬화 구조를 유지했고 새 클래스/MonoBehaviour/ScriptableObject를 만들지 않았다.
- [확인] 기존 Player/Dungeon/Enemy 리팩터링 및 ShaderGraphSettings.asset 로컬 변경을 보존했다. 다른 C#·Scene·Prefab·Material·Animation·ProjectSettings·기존 문서 수정 없음. Unity Editor/Play Mode/빌드/Smoke 및 commit/push 실행 없음.

## 1. 변경 전 GameUIView 책임

GameUIView는 UGUI Text/Image/Graphic/CanvasGroup을 조회하여 패널 표시, 위치/웨이브/HP/탄약/스킬, Raven, 성흔, 6방 지도/Route, 카드 선택, 패시브 슬롯/tooltip 연결/toast, 결과와 컷신 표시를 담당했다. public UIReference 목록을 Awake에서 문자열 Dictionary로 구성한다.

`LateUpdate`에는 위 표시가 순서대로 섞여 있었으며 Map/Sigil/Passives/Prompt/Skill 등은 이미 별도 함수였다. Sigil 끝에는 카드 선택 표시, Passives 끝에는 toast 표시도 들어 있었다. 해당 함수의 호출 위치를 유지한 채 내부 책임을 나눴다.

표시와 명령/이벤트 책임은 원래 다음과 같다:

```text
Dungeon/Player/Raven/RunBuild 상태 → GameUIView.LateUpdate polling → UGUI 표시
RunBuild.StackChanged / PassiveAcquired / Reset → GameHUD → pulse / toast queue 상태
Button/Slider UnityEvent → UICommand → Dungeon/GameHUD/volume/fullscreen 명령
PassiveSlotView pointer enter/exit → shared PassiveTooltip
```

GameUIView가 새 이벤트를 구독하거나 다른 클래스의 책임을 가져오지 않았다. RunState/입력/gameplay 규칙을 바꾸는 코드는 추가하지 않았다.

## 2. 생성한 책임 블럭

GameUIView 내부 region은 16개다. Title/Pause/Settings에는 독립 표시 갱신이 없으므로 전역 패널 표시 블럭에 함께 두었다. 별도 빈 블럭이나 1~2줄짜리 명령 wrapper를 만들지 않았다.

| 블럭 | 주요 구성 |
|---|---|
| Serialized References / UI Reference Definitions | 기존 panels, Art/font/colors, References, PassiveContent/Prefab/Tooltip; 같은 파일의 UIReference Key/Target 유지 |
| Runtime Cache / Dictionary | refs, slots, 기존 3종 buildIcons/buildNames/effects |
| Initialization / Awake | 기존 Awake의 Dictionary 구성과 font 선택/대입 |
| LateUpdate Orchestration | 기존 가드와 상태 캡처, 표시 함수의 원래 순서 |
| Global Panel Visibility / Title / Pause / Settings | UpdatePanelVisibility |
| Core HUD | UpdateCoreHud: Wave/WaveBreak/CombatStatus |
| Player Health / Ammo / Weapon | UpdatePlayerHealthAndAmmo; 무기 선택 표시는 아래 기존 Skill 슬롯 경계에 유지 |
| Skill / Cooldown UI | UpdatePlayerSkillSlots, 기존 Skill |
| Raven Existing UI | UpdateRavenSkillSlots, UpdateRavenPrompt |
| Passive Inventory / Slot / Tooltip / Toast / Card Choices | 기존 Passives, UpdatePassiveToast, UpdateCardChoices |
| Stigma / Sigil UI | UpdateStigmaHud, 기존 Sigil |
| Dungeon Map / Route / Room Nodes | UpdateLocationAndSupplies, 기존 Prompt/Map |
| Existing Boss Prototype UI | UpdateBossPrototype |
| Results / Dead / Victory | UpdateResults |
| Cinematic Overlay | UpdateCinematicOverlay |
| Utility / Element Lookup / Formatting | 기존 Object/Element/Text/Show/Panel/Image/Color/Fill/Rarity/Source |

필드는 패널, 공통 아트/font/color, serialized key 목록, passive 참조, dictionary/slot 캐시, 성흔 표시 데이터 순으로 인접 배치했다. 이름·타입·접근성·readonly·초기식은 동일하다. PassiveSlotView의 tooltip callback과 GameHUD의 성흔 선택/pulse/toast queue 상태를 이 클래스에 새 필드로 만들지 않았다.

## 3. LateUpdate 호출 구조

작업 전 순서를 책임 이름으로 기록하면 다음과 같다:

```text
Dungeon.I 읽기 → Dungeon/Hero 없으면 return
GameHUD 획득 → Hero 캡처 → Cinematic 여부 캡처
Title/Hud/Route/Sigil/Cards/Pause/Settings/Results/Cinematic 패널 표시
Location → Notice → Supplies
Wave/WaveText → WaveBreak/WaveBreakText → CombatStatus
HP/HPText → Ammo/AmmoText
Skill0 → Skill1 → Skill2 → Skill3 → Skill4 → Skill5
성흔 HUD 3종 (Stack → RouteStack → border → icon → pulse)
Prompt → Map → Sigil(마지막에 카드 3종) → Passives(마지막에 toast)
ResultsTitle/Detail → Boss/BossHP → CrowPrompt/Text
Cinematic일 때만 letterbox/title/shade/heading/caption/hint
```

작업 후 호출 순서는 다음과 같다. 위 문장의 순서를 그대로 유지한다:

```text
LateUpdate
  기존 Dungeon/Hero 가드 및 g/h/p/cinematic 캡처
  UpdatePanelVisibility(g,h,cinematic)
  UpdateLocationAndSupplies(g)
  UpdateCoreHud(g,p)
  UpdatePlayerHealthAndAmmo(p)
  UpdatePlayerSkillSlots(p)       // Skill0~3
  UpdateRavenSkillSlots(g)        // Skill4~5
  UpdateStigmaHud(g,h)
  Prompt(g)
  Map(g)
  Sigil(g,h)                     // 기존 위치에서 UpdateCardChoices(g)
  Passives(g,h)                  // slot 처리 후 UpdatePassiveToast(g,h)
  UpdateResults(g)
  UpdateBossPrototype(g)
  UpdateRavenPrompt(g)
  if(cinematic): UpdateCinematicOverlay(g)
```

[확인] inactive Panel을 이유로 갱신을 새로 건너뛰지 않는다. Title/Pause/Route/Results/Cinematic에서도 기존 가드를 통과하면 Map/Sigil/Passives 등을 매 LateUpdate 호출한다. Raven 두 함수도 읽기 책임만 같은 블럭에 모았으며 호출 시점은 합치지 않았다. `DefaultExecutionOrder(500)`을 유지했다.

## 4. 추출한 private method

새 private 메서드는 13개다. 기존 17개 메서드를 삭제/rename하지 않았다.

| 새 메서드 | 원래 위치 | 옮긴 책임 |
|---|---|---|
| `UpdatePanelVisibility(Dungeon g, GameHUD h, bool cinematic)` | LateUpdate의 첫 패널 호출 묶음 | 기존 순서대로 9개 패널 조건 |
| `UpdateLocationAndSupplies(Dungeon g)` | LateUpdate의 Location/Notice/Supplies | 기존 지도/방 진행 의존 표시; Notice도 사이의 위치 유지 |
| `UpdateCoreHud(Dungeon g, Player p)` | LateUpdate의 Wave~CombatStatus | 웨이브/휴식/회피/콤보/태그 강화 표시 |
| `UpdatePlayerHealthAndAmmo(Player p)` | LateUpdate의 HP/Ammo | fill과 기존 HP/재장전 문자열 |
| `UpdatePlayerSkillSlots(Player p)` | LateUpdate의 Skill0~3 | 무기/회피/스킬 슬롯 |
| `UpdateRavenSkillSlots(Dungeon g)` | LateUpdate의 Skill4~5 | 기존 Command/Link 상태 |
| `UpdateStigmaHud(Dungeon g, GameHUD h)` | LateUpdate의 i<3 for | Stack/RouteStack/border/icon/pulse |
| `UpdateResults(Dungeon g)` | LateUpdate의 ResultsTitle/Detail | Dead/Victory 제목과 도달/처치/시간 |
| `UpdateBossPrototype(Dungeon g)` | LateUpdate의 Boss/BossHP | 기존 IsBossWave/Enemies[0] 조회 |
| `UpdateRavenPrompt(Dungeon g)` | LateUpdate의 CrowPrompt/Text | LinkWindow/ComboFlash 조건과 R 텍스트 |
| `UpdateCinematicOverlay(Dungeon g)` | LateUpdate의 cinematic if 본문 | 기존 CanvasGroup alpha 및 제목/힌트 |
| `UpdateCardChoices(Dungeon g)` | Sigil 마지막 n<3 for | 기존 rarity border/label/effect |
| `UpdatePassiveToast(Dungeon g, GameHUD h)` | Passives 마지막 | 기존 toast 조건·icon/text/border/opacity |

Awake, Map, Prompt, Skill의 책임은 이미 독립돼 있어 helper를 더 쪼개지 않았다. 슬롯 생성/삭제도 기존 Passives에 그대로 둔다. 모든 추출 함수는 기존 문장을 이동했으며 새 조기 반환이 없다. 표시 함수는 호출 당시의 g/h/p/cinematic을 전달받는다.

정적 검증 결과:

- [확인] UIReference 필드 2개, GameUIView 필드 26개의 이름/타입/접근성/속성/초기식 일치. 두 클래스의 namespace/수식자/속성/상속 동일.
- [확인] 기존 17개 메서드의 이름/signature/접근성 일치. `Element<T>`의 type parameter와 `where T:Component`도 보존했다. 원래 implicit private인 함수만 private를 명시했다.
- [확인] 13개 helper 호출을 원래 문장으로 재귀적으로 펼쳐 작업 전 함수와 C# 토큰을 비교했다. 17개 모두 if/else/for/while/return, 호출/표시 순서, 조건/연산식/숫자/문자열이 일치했다.
- [확인] UI lookup의 key 인자 표현 72개의 multiset이 동일하다. 문자열 literal/접두사/인덱스식을 변경하지 않았다.
- [확인] C# 구문 파싱 오류 없음. 컴파일/Unity 실행/빌드 검증은 하지 않았다.
- [확인] 검증 도구와 중간 파일은 프로젝트 밖 작업 폴더에 두었다. 외부 호출 계약을 바꾸는 의존성이나 테스트 C#을 프로젝트에 추가하지 않았다.

## 5. 유지된 public / serialized API

| 종류 | 유지된 계약 |
|---|---|
| UIReference | `[System.Serializable] public class UIReference`; `public string Key`, `public GameObject Target` |
| 클래스 | `public sealed class GameUIView:MonoBehaviour`, `[DefaultExecutionOrder(500)]` |
| 패널 필드 | TitlePanel, HudPanel, RoutePanel, SigilPanel, CardsPanel, PausePanel, SettingsPanel, ResultsPanel, CinematicPanel, ToastPanel (GameObject) |
| 공통 필드 | Art(VisualCatalog), FontOverride(Font), SystemFonts(string[]), ActiveColor/UnlockedColor/InactiveColor(Color), RarityColors(Color[]), References(List<UIReference>) |
| Passive 필드 | PassiveContent(RectTransform), PassivePrefab(PassiveSlotView), PassiveTooltip(Text) |
| public 메서드 | `GameObject Object(string key)`, `T Element<T>(string key) where T:Component` |

현재 파일에는 명시적 `[SerializeField]`가 없으며 기존 public field serialization을 유지했다. GameHUD.View/GameSceneBindings.UI의 serialized 참조와 AuthoringSmoke의 Element<Button>/Element<Text>/패널 참조는 수정 없이 유지된다. GameHUD는 View 렌더링을 호출하지 않으며 GameUIView가 독립적으로 polling한다.

UICommand의 Execute/SetVolume UnityEvent, PassiveSlotView의 pointer callback은 외부 클래스에 그대로 있다. Dungeon/CinematicDirector에 새 View callback을 만들지 않았으며 현재 컷신 상태를 조회하는 경로도 유지했다.

## 6. 유지된 UIReference key 계약

- [확인] GameUI.prefab의 저장된 References는 327개이며 key도 327개로 중복이 없다. Prefab을 수정하지 않았다.
- [확인] 코드의 literal 및 기존 loop 범위/Skill0~5를 펼친 조회 key 127개는 현재 Prefab에 모두 존재하며 비영(0이 아닌) GameObject target fileID를 가리킨다. 새 key/연결은 필요하지 않다.
- [확인] GameUIView.cs.meta GUID `32c3bfa79359cd1498d971edee62c863`와 GameUI.prefab의 스크립트 참조/serialized field/UnityEvent를 유지했다.
- [확인] Awake는 References 순서대로 `if(r.Target) refs[r.Key]=r.Target`를 수행한다. Dictionary 구조, 유효 target 조건, 동일 key의 덮어쓰기 의미, 잘못된 key/item에 대한 기존 실패 방식도 바꾸지 않았다. Clear/TryAdd/새 null guard를 넣지 않았다.
- [확인] Object는 TryGetValue 실패 시 null, Element는 Object가 Unity truthy일 때 GetComponent<T>, 아니면 null이다. Text/Image/Graphic/CanvasGroup 조회와 각 원래 null guard도 동일하다.
- [확인] Show/Panel은 참조가 존재하고 activeSelf가 달라야 SetActive한다. Fill은 기존 Mathf.Clamp01, Image는 기존 Art.Get, Color는 기존 Graphic.color 대입을 유지한다.
- [확인] key enum, Find/Transform.Find/hierarchy 탐색, reflection, LINQ, 추가 cache 또는 event 전환은 없다. font 이름/18pt 선택 및 inactive Text까지 포함하는 기존 Awake 처리를 유지했다.

주요 동적 key 계약은 다음과 같다:

| 접두사 | 기존 인덱스 범위 |
|---|---|
| SkillIcon / SkillLabel / SkillCooldown / SkillCooldownText | 0~5 |
| Stack / RouteStack / StigmaBorder / StigmaIcon / StigmaPulse | 0~2 |
| MiniNode / RouteNode / RouteNodeText | 0~5 |
| SigilNode / SigilNodeText | 0~5 (성흔 표시 노드이며 방 인덱스와 별개) |
| CardBorder / CardRarity / CardEffect | 0~2 |

## 7. Player / Weapon / Skill HUD

`UpdateCoreHud`의 CombatStatus는 기존 DashCd의 F1+s 또는 ●, HitCombo, Fury>0의 태그 강화 문구를 그대로 사용한다. Wave/WaveBreak 조건, 적 수, CeilToInt countdown도 동일하다.

`UpdatePlayerHealthAndAmmo`는 HP/100, Ammo/6f와 Clamp01, CeilToInt HP, Reload>0의 장전 F1s 또는 리볼버 문구를 유지한다. 무기/스킬 mapping은 `UpdatePlayerSkillSlots`에 있다:

| UI 슬롯 | 기존 icon/label/cooldown |
|---|---|
| 0 | Weapon==0: SlashIcon/단검, 그 밖: ShotIcon/리볼버; TagCd |
| 1 | HunterDash/회피; DashCd |
| 2 | Weapon==0: SlashIcon/베어 가르기, 그 밖: ShotIcon/원 샷; Skill2Cd |
| 3 | Weapon==0: SlamIcon/내려찍기, 그 밖: BarrageIcon/난사; Skill3Cd |

기존 Skill은 icon → label → cooldown>0 표시 → cooldown F1 text 순서다. 무기0/1, skill1~4 gameplay 구현, cooldown 계산, 입력/키 안내/걷기/Shift는 수정하지 않았다. UIKey 안내는 기존 Prefab 값 그대로이며 루트의 새 입력은 병합하지 않았다.

## 8. Raven 기존 UI

`UpdateRavenSkillSlots`에서 Skill4는 Raven icon/Crow.Cooldown, Active>0이면 공격 F1s 아니면 까마귀 명령이다. Skill5는 RavenPortrait/Crow.LinkCooldown, LinkWindow>0이면 연계 F1s 아니면 연계 대기다.

`UpdateRavenPrompt`는 `LinkWindow>0 || ComboFlash>0`으로 CrowPrompt를 표시한다. 텍스트는 LinkWindow>0의 `R · 연계 {F1}s`, 아니면 기존 까마귀 콤보 연계 문구다. Skill4/5 다음에 안내를 함께 갱신하도록 순서를 바꾸지 않았다. 안내는 기존처럼 Results/Boss 갱신 뒤다.

Summon/Recall/visibility/frame animation/F 말풍선/머리 위 투영, 새 sprite/field/key/input을 추가하지 않았다. Raven.cs와 Player.cs는 수정하지 않았다.

## 9. Passive / Toast / Tooltip

`Passives`는 Build.Passives를 읽고 count/empty를 표시한다. `slots.Count>inventory.Count`이면 목록 순서대로 존재하는 슬롯 GameObject를 Destroy한 뒤 slots.Clear한다. 그 뒤 while로 부족한 슬롯을 inventory 순서대로 만든다.

생성 순서는 item 읽기 → Instantiate(PassivePrefab,PassiveContent) → name=Definition.Id → icon → rarity border → shared Tooltip → Description(name/rarity/source) → slots.Add다. 이전처럼 이미 존재하는 같은 개수의 슬롯을 재구성/갱신하지 않으며 ID dedup/pooling/sort를 추가하지 않았다.

`UpdatePassiveToast`는 슬롯 처리 뒤에 실행한다. 표시 조건은 Toast!=null, State가 Route/Cinematic 아님, SettingsOpen 아님이다. 기존처럼 Paused를 별도로 제외하지 않는다. 표시할 때 icon/name/detail/border/CanvasGroup alpha를 기존 순서로 갱신한다.

획득/Reset 이벤트 구독, FIFO queue, 2.4초 timer/opacity/pulse는 GameHUD 책임으로 유지한다. Reset 시 View의 슬롯 삭제는 이벤트 callback이 아니라 다음 기존 polling에서 inventory 감소를 보는 방식이다. Tooltip enter에서 text/활성화, exit에서 비활성화하는 callback은 PassiveSlotView에 그대로 있다. View는 슬롯의 Tooltip/Description만 연결한다.

Sigil 마지막의 카드 3개는 `UpdateCardChoices`로 격리했다. rarity border/label, PlayerDamage/MoveSpeed/RavenDamage 문구와 .08/.12 × (1+.5×rarity)의 P0 계산을 보존했다. 서비스 카드 UI를 Boss Reward에 연결하지 않았다.

## 10. Stigma / Sigil UI

HUD 부분은 `UpdateStigmaHud`, 선택/detail/claim/6노드는 기존 `Sigil`에 모였다. Fire/Nature/Butterfly 인덱스와 FireIcon/NatureIcon/Raven, 잿불/격노/검은 날개 이름, 효과 설명 문자열을 유지했다.

- HUD 3종: Build.Stack/IsActive, GameHUD.StackPulse를 읽는다. Stack/RouteStack text, pulse>0의 white border, 활성/비활성 색, 기존 .25초/.22 scale pulse 수식이 동일하다.
- 선택: h.SelectedSigil을 그대로 인덱스로 사용한다. View에 새 clamp/선택 상태를 추가하지 않았다.
- Detail: 두 icon, 이름/stack/활성 여부/effect, Threshold와 Threshold+2 문구를 유지한다.
- Claim: State==SigilChoice일 때만 SigilClaim 표시. 기존 +1과 전후 stack 문구 유지.
- 노드6개: n/2 타입, Threshold+(n%2)×2, Stack>=needed 색상과 이름/필요 각인 문구 유지.

ApplySeongheunStack, threshold/데이터, RunBuild event, UICommand 선택/claim은 변경하지 않았다. 독립 skill tree/새 능력/노드 의미를 만들지 않았다.

## 11. 고정 6방 Map / Route UI 격리 위치

`Dungeon Map / Route / Room Nodes` region에 `UpdateLocationAndSupplies`, `Prompt`, `Map`을 모았다. 최초 위치/보급 표시를 다른 위치에서 갱신하도록 바꾸지 않았고, 사이의 Notice도 원래 순서에 남겼다.

| 현재 의존 | 위치/조건 |
|---|---|
| 6방 총수/MapIndex/MapsCleared | UpdateLocationAndSupplies의 위치/보급, Prompt의 미완료6-count, Map의 i<6 및 진행 문구 |
| 고정 node key | Map의 MiniNode/RouteNode/RouteNodeText+i |
| 현재/완료/방문 | !IsTown, MapIndex==i, Progress.Cleared/Visited; 색 우선순위는 current→done→visited→inactive |
| 고정 RoomGraph.Kind | Map의 서비스방/전투 label; Room03/04 같은 새 직접 분기는 추가하지 않음 |
| 문/서비스 안내 | Prompt의 TownExit, NearbyDoor/StageExit/Destination, SegmentComplete/CanLeave/MapComplete, ServicePoint |
| 다음 스테이지 | Map의 RouteTravel, Room/NextRoom/RoomNames, Room==4 종료 문구 |
| Route 상태 요약 | Map의 HP, Passives.Count, 기존 Build bonus P0 및 RouteStack는 원래 성흔 HUD 갱신 위치 유지 |

Prompt는 Running 검사, TownExit 거리<3, 기존 NearbyDoor 결과, 서비스 거리<2.4 및 문 안내 우선순위를 유지한다. 실제 이동은 기존 RoomDoor 근처 E/Dungeon 흐름이며 View는 문 상태/메시지를 조회만 한다.

Route UI는 지도 읽기와 Town→Stage/Stage→다음 Stage 진행을 구분하는 기존 RouteTravel 계약을 유지한다. MiniNode/RouteNode를 클릭하여 방 이동하는 기능은 추가하지 않았다. Wave UI와 결과의 기존 stage 표시를 재설계하거나 새 고정방 의존성을 다른 블럭에 퍼뜨리지 않았다.

## 12. Existing Boss Prototype UI

**현재 보스 콘텐츠 검증 대상이 아닌 dormant/prototype UI 경로**로 취급한다. 이전 분석 문서의 보스 gameplay 설명을 근거로 이번 UI 리팩터링에서 실제 보스 콘텐츠 검증을 요구하거나 기능을 확장하지 않는다.

`UpdateBossPrototype`의 조건은 원래 그대로다:

```text
Show("Boss", IsBossWave && Enemies.Count>0)
IsBossWave && Enemies.Count>0 && Enemies[0]이면
  Fill("BossHP", Enemies[0].Hp / Enemies[0].MaxHp)
```

Enemies[0] 의존성과 기존 null 조건을 그대로 남겼다. BossController, Kind3 추가 UI, Death 표시, Reward UI 연결, maxHP 가드 또는 새로운 표적 선택을 넣지 않았다. 호출은 Results 뒤, RavenPrompt 앞이다.

## 13. Result / Settings / Cinematic

- ResultsPanel 표시 조건은 Dead 또는 Victory다. `UpdateResults`는 기존처럼 패널 표시 여부와 무관하게 Victory/그 밖의 제목을 갱신하며 Room+1 / 5, Kills, RunTime int 초 문구를 유지한다. Town 복귀 버튼은 기존 UICommand.EnterTown 경로다.
- TitlePanel은 Title, PausePanel은 Paused&&!BuildTreeOpen, SettingsPanel은 SettingsOpen이다. Settings에서 volume/fullscreen 상태를 새로 polling하거나 값을 바꾸는 코드가 GameUIView에 있지 않았다. AudioListener.volume과 Screen.fullScreen 명령은 원래 UICommand.SetVolume/Execute에 그대로 있다. 해당 호출을 GameUIView로 옮기지 않았다.
- CinematicPanel은 LateUpdate 시작 시 캡처한 cinematic 값으로 표시한다. `UpdateCinematicOverlay`는 그 if 안에서만 실행한다. CinemaBars/Title/Shade의 기존 CanvasGroup null guard와 Letterbox/TitleOpacity/Shade alpha, Heading/Caption, IsPaused에 따른 P/SPACE/ESC 안내를 유지한다.

RunState 추가/삭제/transition, Reward 활성화, 새로운 상태 변경 명령은 없다.

## 14. 향후 View 클래스 분리 후보

아래는 후속 검토 후보이며 실제 파일/클래스를 만들지 않았다.

| 후보 | 현재 절단선 | 분리 시 보존할 경계 |
|---|---|---|
| PlayerHUDView / WeaponHUDView | UpdateCoreHud/UpdatePlayerHealthAndAmmo/UpdatePlayerSkillSlots/Skill | 슬롯 순서, 문자열/아이콘/format, 기존 직접 상태 조회 |
| RavenHUDView | UpdateRavenSkillSlots/UpdateRavenPrompt | 두 호출의 서로 다른 실행 위치, LinkWindow/ComboFlash |
| PassiveView / CardChoiceView | Passives/UpdatePassiveToast/UpdateCardChoices | slot 생성/삭제 순서, Sigil 마지막 카드 갱신, GameHUD queue/tooltip 소유권 |
| StigmaView | UpdateStigmaHud/Sigil | 3종/6노드, selection/pulse/threshold/claim |
| DungeonMapView | UpdateLocationAndSupplies/Prompt/Map | 표기와 실제 Dungeon 명령 분리, 고정키/진행 조건 |
| ResultView / CinematicView | UpdateResults/UpdateCinematicOverlay | 조건부/무조건 갱신 구분, 기존 실행 순서 |
| 패널 routing / SettingsView | UpdatePanelVisibility 및 기존 UICommand 연계 | GameHUD 상태/UnityEvent, volume/fullscreen 명령의 기존 소유자 |
| BossHUDView 후보 | UpdateBossPrototype | dormant/prototype 상태와 현재 조건만 유지; 실제 보스 설계는 별도 범위 |

공통 Object/Element/format/font/Art/색과 References는 현재 공유 계약이다. 기능마다 새 MonoBehaviour를 붙이는 작업은 이번에 하지 않았다.

## 15. 향후 랜덤 RunMap UI 교체 후보

`Dungeon Map / Route / Room Nodes` region의 `Map`, `Prompt`, `UpdateLocationAndSupplies`는 향후 RunMap/Node/Edge 기반 View로 교체할 후보 지점이다. 현재는 고정 배열/RoomGraph/i<6/문구/node key/방문·완료 조회를 그대로 사용한다.

후속 변경 시 현재 방 node 목록/표시 위치/연결선/visited/cleared와 stage 진행 RouteTravel을 구분해서 검토할 수 있다. 기존 UIReference 0~5 및 Prefab 배치 교체, Dungeon/ExpeditionProgress의 새 데이터 계약은 그 후속 작업의 범위다. 이 문서가 새 Node/Edge API나 지도 click travel 설계를 확정하지 않는다.

이번에는 랜덤 그래프, node/edge 타입, UI 배치/키, Room Prefab, MapIndex별 새 분기 또는 실제 이동 명령을 추가하지 않았다.

## 16. 사용자 Unity 테스트 필요 항목

[사용자 Unity 테스트 필요] 실행하지 않았다. 아래는 화면/조작 확인 항목이며 정적 검증을 대신해 AI가 Editor/Play Mode/빌드를 반복 실행하는 작업이 아니다. Boss prototype 실전 검증은 포함하지 않는다.

| 테스트 방법 | 정상 결과 | 실패 시 확인할 증상 |
|---|---|---|
| Title→Town, Pause/Settings 열기·닫기, 기존 컷신 보기 | 기존 패널 전환/font, volume/fullscreen 명령, letterbox/title/shade/힌트 유지 | 중복 패널, 닫힘 실패, 글꼴/컷신 alpha 누락 |
| HP 감소·무기 태그·리볼버 재장전·스킬 및 기존 Raven 연계 | HP/Ammo/fill, 기존 slot icon/label/F1 cooldown, R/ComboFlash 안내 | 슬롯 순서/아이콘/문구 차이, 값 갱신 누락 |
| 성흔 획득/조회·카드 선택·패시브 획득/hover·Town 복귀 | 3종 pulse/6노드/detail/claim, 카드 rarity/effect, 슬롯 순서/tooltip/toast, reset 후 슬롯 제거 | 선택/threshold 차이, toast 조건 차이, tooltip 잔존/슬롯 누락 |
| 기존 E 문 이동·지도 열기·완료/재방문·다음 stage 진행 | node 현재/완료/방문 색, 봉인/서비스 안내, RouteTravel/진행 문구 유지 | map 색/미완료 수 차이, 닫힌 지도 재개 시 값 누락, 방 선택 이동으로 의미 변경 |

UIReference/GUID/Prefab fileID와 표시 계산/문자열은 정적으로 보존 확인했다. 실제 화면 배치·입력 반응·UGUI 렌더링은 이번 작업에서 실행 검증하지 않았다.
