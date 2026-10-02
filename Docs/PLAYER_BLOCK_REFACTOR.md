# Player Block Refactor

- 작업일: 2026-10-03 (Asia/Seoul).
- 대상: `Assets/Scripts/Player.cs`만 내부 구조 정리.
- 작업 시작 HEAD: `c218280dbb80d83b03e17b05ad92f8bd50df86a3`.
- 참고: `Docs/WESTERN_LEMEGETON_PROJECT_ANALYSIS.md`, `Docs/ROOT_CODE_INTEGRATION_ANALYSIS.md`.
- 기준은 작업 시작 시 실제 Assets Player.cs다. 루트 `Player.cs`의 기능/입력/수치는 가져오지 않았다.
- 신규 클래스·MonoBehaviour·ScriptableObject 및 외부 클래스 수정 없음. Unity Editor/Play Mode/빌드/Smoke 실행, commit/push 없음.

## 변경 전 책임

Player 한 클래스가 입력, 이동/회피, HP/무적, 무기/탄약, 기본 공격, 4개 스킬, 콤보, Raven 호출, Sprite 표현과 초기화를 모두 담당했다. 여러 책임의 필드가 같은 선언문에 섞여 있고 긴 함수 안에 여러 처리 단계가 압축돼 있었다.

- `Update`: 타이머, 걷기/이동/조준 읽기, ActionLocked 검사, 회피·태그·Raven·스킬·좌클릭 입력, 액션 stepping.
- `AdvanceActions`: 액션 시작 상태 저장, 스킬/기본 공격 갱신, 대시/일반 이동, Locomotion, 표현.
- `TickSkill`: 네 스킬의 시간·이동·피해·효과·종료 처리.
- `TickBasic`: 공통 타격시점과 단검/총 분기, 탄약 소모·재장전 시작, 사운드.
- `Hit`: 적중 콤보 기록, Raven 표적 지정, 피해 적용, 표시용 카운터, Raven 연계.
- `TickTimers` 및 초기화 함수: 여러 책임의 공유 상태를 정해진 순서로 갱신.

클래스의 책임 범위 자체는 바꾸지 않았다. 내부 배치·공백·줄바꿈을 정리하고 기존 문장 묶음을 private 메서드로 옮겼다.

## 새 블럭 구조

상단 `Fields and state by responsibility`에서 필드/관련 프로퍼티를 HP, 무기/탄약, 스킬, 기본 공격, 콤보, 이동, 대시, 표현별로 모았다. 이름·타입·접근성·초기식·SerializeField를 유지했다. 입력처럼 별도 저장 필드가 없는 책임에 새 상태를 만들지 않았다.

| 책임 블럭 | 주요 함수/경계 |
|---|---|
| Input | `Update`, `HandleActionInput` |
| Movement | `AdvanceActions`, `TickMovement` |
| Dash | `TryDodge`, `TickDash` |
| Health / Damage / Invulnerability | `Hit`, `Hurt` |
| Basic Attack | `TryAttack`, `TickBasic`, `ApplyMeleeBasic`, `CancelBasic` |
| Weapon / Ammo / Reload | `TryTag`, `FireBasicShots`, `TickReload` |
| Skills | `TrySkill`, `TickSkill`, `TickSlash`, `TickSlam`, `TickShot`, `TickBarrage`, `FinishSkill` |
| Combo | `ClearExpiredCombos`; tracker/콤보 상태는 상단에 인접 배치 |
| Raven integration | `HandleRavenInput`, `ResolveRavenFollowups` |
| Animation / Presentation requests | `Animate`; PresentationSequence 및 Sprite query 상태 유지 |
| Reset / Safe Entry | `Awake`, `ResetForRun`, `SafeEntry` |
| Utility / Query / Shared timing | `Cooldown`, `AdvanceCombat`, `TickTimers`, `Area` |

`dashHits`는 일반 회피가 아니라 Slash 스킬의 중복 적중 방지 집합이므로 Skills 필드에 배치했다. `actionId`는 기본 공격과 스킬이 공유하는 식별자로 기존 공유 관계를 유지했다. `lastComboAttack`은 Raven 콤보 후속 호출 중복 방지 용도를 주석으로 구분했다.

보존한 호출 순서:

```text
Update
  Dungeon.Running 검사 → dt 읽기 → TickTimers
  Shift/WASD → LastMove → 마우스 aim/Facing
  ActionLocked이면 AdvanceActions(dt, zero) 후 반환
  HandleActionInput
    Space → Q → HandleRavenInput(1 Command → R TryLink)
    2 스킬 → 3 스킬 → 좌클릭 buffer → TryAttack
  AdvanceActions(dt, move)

AdvanceActions
  move clamp → before 위치 → performingAction 캡처
  TickSkill → TickBasic → TickMovement(대시 또는 일반 이동)
  Locomotion.Advance → Animate

Hit
  유효성 검사 → ComboTracker.Hit
  Crow.Mark → Enemy.TakeDamage → DamageEvents/HitCombo/hitTime
  ResolveRavenFollowups(OfferLink → 중복 검사 → ComboFollowup)
```

`performingAction`은 스킬/기본 공격 tick 전에 캡처한다. `TickSkill`의 네 조건은 기존처럼 독립된 `if`로 유지하여 스킬 종료로 필드가 바뀌는 순서까지 보존한다. Raven 입력은 ActionLocked 뒤에 있고, Mark는 피해 전에, 후속 연계는 피해 뒤에 있다.

## 추출된 private method

13개 모두 기존 함수 안의 문장을 옮겼다. 기존 메서드는 삭제/rename하지 않았다.

| 새 private 메서드 | 원래 위치 | 추출한 책임 |
|---|---|---|
| `HandleActionInput(Dungeon g, Vector2 move)` | Update | 회피부터 buffered 기본 공격까지, 기존 입력 순서 |
| `HandleRavenInput(Dungeon g)` | Update의 1/R 부분 | 기존 Command/TryLink 입력 |
| `TickMovement(float dt, Vector2 move)` | AdvanceActions | 대시/일반 이동 선택 |
| `TickDash(float dt)` | AdvanceActions의 대시 분기 | travel/substep/장애물 해결/대시 종료 위치 보정 |
| `ApplyMeleeBasic(Dungeon g, bool finisher)` | TickBasic의 단검 분기 | 원뿔 판정·피해·Slash 표현 |
| `FireBasicShots(bool finisher)` | TickBasic의 총 분기 | 탄약 차감·reload 시작·탄환·Shot 표현 |
| `TickReload(float dt)` | TickTimers 마지막 | 재장전 시간 감소 및 탄약6 복원 |
| `TickSlash(float previous)` | TickSkill의 skill1 분기 | 이전 elapsed를 포함한 active 구간 계산·돌진·타격 |
| `TickSlam()` | TickSkill의 skill2 분기 | 두 차례 범위 타격과 종료 |
| `TickShot()` | TickSkill의 skill3 분기 | 선형 타격·효과·사운드·종료 |
| `TickBarrage()` | TickSkill의 skill4 분기 | while 기반 누적 tick과 범위 타격·종료 |
| `ClearExpiredCombos()` | TickTimers의 두 만료 검사 | Combo/HitCombo 초기화 |
| `ResolveRavenFollowups(Dungeon g, Enemy e, int stage, long attack, bool isSkill, bool chain)` | Hit 마지막 | OfferLink 및 중복 없는 ComboFollowup |

호출 당시의 `g`, `move`, `dt`, `previous`, `finisher`, 적중 정보를 인자로 전달하며 전역을 새로 다시 읽는 방식으로 바꾸지 않았다. 추출한 메서드에는 반환값이나 조기 반환을 추가하지 않았다. 서로 의미가 다른 기존 유효성 조건도 합치지 않았다.

## 유지된 public API

| 종류 | 보존 항목 |
|---|---|
| public 필드/상수 13개 | MaxHp, Hp, Weapon, Ammo, Combo, HitCombo, DashCd, TagCd, Reload, Fury, Facing, LastMove, Locomotion |
| public 프로퍼티 19개 | Skill2Cd, Skill3Cd, ActionLocked, SkillBusy, ActiveSkill, BasicStage, PresentationSequence, BasicBusy, Dodging, SkillElapsed, Invulnerable, WalkHeld, Presentation, MotionRow, MotionFrame, Phase, DamageEvents, MotionDirection, DamageScale |
| public 메서드 9개 | Cooldown, ResetForRun, SafeEntry, TryTag, TryDodge, TrySkill, TryAttack, Hit, Hurt |
| internal API | AdvanceCombat(float dt, Vector2 move) |
| Inspector 계약 | `[SerializeField] SpriteRenderer art` 이름/타입/속성 유지. 기존 public 필드 이름/타입/접근성 유지. Player.cs.meta 미변경 |

프로퍼티의 get/set 접근성 및 구현, Hit의 기본 인자(stage=0, attack=0, isSkill=false), 메서드 반환형/인자도 유지했다. Dungeon, Enemy/Bullet, ActorPresentation, CinematicDirector, UI 및 Smoke 호출자가 수정될 필요가 없다.

## 변경하지 않은 gameplay 규칙

- WASD normalized, 양쪽 Shift 걷기, walk2.8/run5.6, 난사3.1, MoveSpeedBonus, 논리 XY/Rect 충돌 유지.
- Space 회피: 속도15, .19초, CD2.5, 무적2, 기존 substep/반경/건물 판정 유지.
- Q 태그, Weapon0/1, TagCd .5, Fury 수치, 공격 취소/스킬 종료 순서 유지.
- 좌클릭 .18초 buffer 및 EventSystem UI 가드 유지. 기본공격 duration .25/.3, duration×.26 시점 타격, 단검 Combo1.2초/총4초 유지.
- 탄약6, Rules.AmmoCost, 공격 예약 후 타격시 소모, Ammo<=3 자동 reload2초, 잔탄 부족 시 reload 규칙 유지.
- 스킬 입력2/3, skill1~4의 HunterMotion 시간·피해·범위·이동·무적·CD8·난사5회 유지. literal/연산식 변경 없음.
- ComboTracker의 적중 계약과 표시 Combo/HitCombo 만료, lastComboAttack 중복 방지 유지.
- 숫자1 Raven.Command, R TryLink, Mark/OfferLink/ComboFollowup 유지. 소환/회수/F 연계/E·R 스킬 등 루트판 변경 없음.
- HP100, 기존 Hurt 조건·무적1·사망 호출, ResetForRun/SafeEntry 대입/호출 순서 유지.
- Animate의 atlas/방향/flip/lean/lift/tint, PresentationSequence 증가 위치 유지. PaperCard/PaperWorld, Scene/Prefab/UI/Dungeon API 및 Boss/Reward/Passive 미변경.

정적 검증 결과:

- Roslyn으로 변경 전후 C# 문법만 파싱: 오류 0. 프로젝트 컴파일·assembly emit·빌드는 하지 않았다.
- 전체 필드 40개의 이름/타입/접근성/속성/초기식 및 프로퍼티 19개의 접근자/구현 일치.
- 기존 메서드 20개(기존 private 및 Unity 콜백 포함)의 시그니처 유지. 새 메서드는 private 13개뿐이다.
- 새 helper 호출을 인자명 대응을 확인한 뒤 원래 위치에 재귀적으로 펼쳐 비교했다. 공백/주석을 제외한 기존 메서드 본문의 C# token 순서가 모두 일치한다. 따라서 조건식·수치·호출·대입·분기 순서를 원문과 대조했다.
- 필드 초기식의 new 인스턴스 생성 순서(cds 배열→dashHits→tracker→Locomotion)는 유지했다. 재배치한 값 초기식은 기존 상수/Vector2.right이며 다른 Player 필드에 의존하지 않는다.
- 변경 전 snapshot과 비교하여 루트 파일, 기존 문서, Player.cs.meta, 관련 외부 소스·Scene·Prefab의 보존을 확인했다. Player.cs 외 기존 tracked 코드/asset의 신규 수정이 없는지 Git 상태도 대조했다.

이 검증은 실행 결과를 확인한 것이 아니다. Unity 실행/컴파일 성공 또는 실제 화면/입력 테스트 통과를 주장하지 않는다. 검사 도구/중간 파일은 작업용 폴더에만 두며 프로젝트에 테스트 코드나 새 의존성을 추가하지 않는다.

## 다음 단계에서 분리 가능한 후보

아래는 추후 검토 후보이며 이번에는 어느 클래스도 분리하지 않았다.

| 현재 블럭 | 추후 후보 | 분리 전 보존해야 할 경계 |
|---|---|---|
| Input | PlayerInput | Running/ActionLocked 검사 위치, 입력 순서, 같은 frame의 상태 변화 |
| Movement + Dash | PlayerMovement | 스킬 이동과 일반 이동 우선순위, before/performingAction 캡처, 실제 이동거리 기반 gait |
| Health / Damage | PlayerHealth 또는 피해 처리 부분 | 무적/사망 조건, Mark→피해→후속 연계 순서 |
| Basic Attack + Weapon/Ammo | PlayerCombat/무기 처리 부분 | attack 예약과 타격시점, Ammo 소모 시점, actionId/basicId |
| Skills | 스킬 처리 부분 | 독립 if 검사, elapsed 이전값, FinishSkill의 slot별 cooldown, 이동/무적 공유 상태 |
| Combo + Raven integration | 콤보/동료 연결 부분 | 적중 기반 tracker와 입력 Combo 구분, target·attackId·Raven queue 계약 |
| Animation / Presentation | 표현 부분 | gameplay 판단과 frame 표시 분리, sequence 재요청, 현재 Sprite/2.5D 연결 |

Reset/SafeEntry와 공유 타이머는 여러 블럭의 상태 계약을 묶고 있으므로 단순 파일 이동 대상처럼 처리하지 않는다. 다음 단계도 이번처럼 behavior 유지 범위를 먼저 확정해야 한다.
