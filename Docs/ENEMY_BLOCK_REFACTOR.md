# Enemy Block Refactor

- 작업일: 2026-10-03 (Asia/Seoul).
- 대상: `E:\GitHub\Western-Lemegeton\Assets\Scripts\Enemy.cs`. 같은 파일의 `Enemy`, `Bullet` 두 클래스만 내부 정리.
- 시작 HEAD: `c218280dbb80d83b03e17b05ad92f8bd50df86a3`.
- 기준: 작업 시작 시 Assets의 실제 코드. 루트/백업 후보 코드를 가져오지 않았다.
- 참고: `Docs/WESTERN_LEMEGETON_PROJECT_ANALYSIS.md`, `Docs/PLAYER_BLOCK_REFACTOR.md`, `Docs/DUNGEON_BLOCK_REFACTOR.md`의 관련 책임/계약.
- [확인] 새로운 클래스·컴포넌트·공개 API·이벤트·상태는 만들지 않았다. Player/Dungeon의 이전 리팩터링과 기존 `ProjectSettings/ShaderGraphSettings.asset` 변경을 보존했다.
- [확인] Unity Editor/Play Mode/빌드/Smoke 실행, Scene/Prefab/Material/Animation/ProjectSettings 수정, commit/push 없음.

## 1. 변경 전 Enemy.cs 책임

`Enemy`는 Kind별 초기 HP, authored/fallback 표현 초기화, Burn/flash, Player 표적 계산, 돌진/접근/거리 유지/separation, 공격 예고/실행, 피해/사망을 담당했다. `Bullet`은 같은 파일에서 탄환 표시 생성, 속도/수명/피해 payload, substep 이동, 경계/장애물/Player/Enemy 충돌을 담당했다.

- `Init`: Kind/HP 설정 → 색 계산 → 4개 authored reference 전부 존재 시 연결/초기 cooldown/return → 아니면 runtime art·HP bar·warning 생성.
- `Enemy.Update`: Running/Dead 검사, Burn 및 사망 반환, flash/HP bar, 위치·표적 거리, charge 또는 windup 반환, cooldown, 공격 예고 또는 이동.
- `Execute`: Kind 0~3의 독립 `if` 네 개. 보스 cycle·강화·돌진·방사탄도 이 함수 안에 있었다.
- `TakeDamage`: 피해·효과·ignite·사망. `OnDestroy`는 warning 오브젝트 정리.
- `Bullet.Spawn`/`Bullet.Update`: 탄환 표시/컴포넌트 생성과 상태 지정, 수명 및 substep별 충돌 후 Destroy/return.

책임 범위는 유지했다. 필드 인접 배치, region, 기존 코드 묶음/초기식의 private 함수 추출만 수행했다.

## 2. 생성한 Enemy 책임 블럭

Enemy 내부 region은 15개다. 같은 상태를 사용하는 코드라도 실행 위치를 옮겨야 하는 조건은 오케스트레이션에 남겼다.

| 블럭 | 함수/책임 |
|---|---|
| Enemy Fields / Runtime State | Kind/HP, cooldown/windup/aim, 공유 chargeTime, Burn/flash, boss cycle |
| Authored References / Presentation References | 기존 public Inspector 참조 4개, 내부 art/bar/warning, PresentationRoot/Presentation |
| Initialization | 기존 `Init(int kind, int room)`, authored 분기 조건/Random 호출/return |
| Update Orchestration | 기존 `Update`, 분기 우선순위와 모든 조기 반환 |
| Target Acquisition | `GetTargetDelta`; Player 위치는 기존 Dungeon.Pos |
| Movement / Separation / Obstacle Resolution | `TickMovement`, `TickCharge` |
| Common Attack Timing | `GetAttackRange`, `BeginAttackWindup`, `TickWindup`, 기존 `Execute` |
| Kind 0 Melee | `ExecuteMeleeAttack` |
| Kind 1 Charge | `BeginChargeAttack` |
| Kind 2 Ranged Projectile | `FireRangedProjectiles` |
| Kind 3 Existing Boss Pattern | `ExecuteBossPattern`, `BeginBossCharge`, `FireBossRadialPattern`, `CreateBossFallbackPresentation` |
| Damage / Burn / Flash | 기존 `TakeDamage`, `ApplyDamageImpact`, `TickBurnTimers`, `ApplyBurnTick`, `TickFlash` |
| Death | `Die`, 기존 `OnDestroy` |
| Health Bar / Warning / Presentation | `BindAuthoredPresentation`, `CreateFallbackPresentation`, `UpdateHealthBar`, `ShowAttackWarning` |
| Utility / Query | 기존 `Dead`, `AttackPower`, `Telegraphing` |

필드 이름·타입·접근성·초기식은 유지했다. `windup=-1` 외에 새 초기식은 없으며, `cycle`, `burnTick` 등의 기존 기본값/Init 시 재초기화하지 않는 동작도 유지했다.

## 3. 생성한 Bullet 책임 블럭

| 블럭 | 함수/책임 |
|---|---|
| Bullet Fields / Initialization | 기존 velocity/life/damage/hostile/stage/attackId, 기존 `Spawn`, `InitializeBullet` |
| Bullet Movement / Collision / Lifetime | 기존 `Update`, `GetSubstepCount`, `MoveSubstep`, `HitObstacle`, `HitPlayer`, `HitEnemy` |
| Bullet Presentation / Utility | `CreatePresentation`, `ApplyMuzzleOffset` |

Bullet은 기존처럼 같은 namespace/파일의 public MonoBehaviour다. 파일 분리, 탄환 Prefab, pooling 또는 새로운 상태 머신은 만들지 않았다.

## 4. 추출한 private method

Enemy 22개, Bullet 8개, 총 30개다. 기존 메서드는 삭제/rename하지 않았으며 기존 비공개 Unity 함수에는 `private`을 명시했다.

| Enemy의 새 함수 | 원래 위치 | 옮긴 코드 |
|---|---|---|
| `BindAuthoredPresentation()` | Init의 authored 분기 | art/Presentation/bar/warning 연결과 warning 비활성화 |
| `CreateFallbackPresentation(int kind, Color color)` | Init의 fallback 부분 | 생성·색·HP bar·warning·Random cooldown·Authored 참조 되쓰기 |
| `GetTargetDelta(Dungeon g, Vector2 p)` | Update의 delta 초기식 | `g.Pos-p` |
| `TickMovement(Dungeon g, Vector2 p, Vector2 delta, float dist, float range, float dt)` | Update의 else | 접근/후퇴, 적 목록 순서대로 separation 누적, ClampMagnitude/ResolveObstacles |
| `TickCharge(Dungeon g, Vector2 p, float dist, float dt)` | Update의 charge 분기 | charge 감소·이동·이동 전 dist로 접촉 피해 |
| `GetAttackRange()` | Update의 range 초기식 | 기존 Kind별 삼항식 |
| `BeginAttackWindup(Vector2 p, Vector2 delta)` | Update의 공격 진입 분기 | aim 캡처·windup·warning |
| `TickWindup(float dt)` | Update의 windup 분기 | 시간 감소·warning off·Execute·windup/cooldown 설정 |
| `ExecuteMeleeAttack(Vector2 p, Dungeon g)` | Execute의 Kind 0 | Ring 및 Player 거리 피해 |
| `BeginChargeAttack(Vector2 p)` | Execute의 Kind 1 | chargeTime 및 Line |
| `FireRangedProjectiles(Vector2 p)` | Execute의 Kind 2 | 기존 -1~1 for loop/3탄 |
| `ExecuteBossPattern(Vector2 p)` | Execute의 Kind 3 | cycle 증가·count·매 3회 돌진/나머지 방사탄 |
| `BeginBossCharge(Vector2 p)` | Kind 3의 cycle%3 분기 | chargeTime 및 Ring |
| `FireBossRadialPattern(Vector2 p, int count)` | Kind 3의 else for | cycle 각도·HP 강화 속도·방사탄 |
| `CreateBossFallbackPresentation()` | Init fallback의 kind==3 | 기존 art scale/crown 생성 |
| `ApplyDamageImpact(float damage, bool ignite)` | TakeDamage의 가드 다음 | HP clamp·flash·5개 impact burst |
| `TickBurnTimers(float dt)` | Update의 burnTime>0 | burnTime/burnTick 감소 |
| `ApplyBurnTick(Dungeon g)` | Update의 burnTick<=0 | tick .5초 설정·피해·Gold burst; Dead/return은 호출 직후 Update에 유지 |
| `TickFlash(float dt)` | Update의 Burn 다음 | flash 감소와 Kind/flash별 art scale |
| `Die()` | TakeDamage의 Dead 분기 | Kills++·warning off·Red burst·Destroy |
| `UpdateHealthBar()` | Update의 flash 다음 | 기존 Hp/MaxHp scale |
| `ShowAttackWarning(Vector2 p)` | Update의 공격 진입 분기 | 활성화·Kind별 위치/scale/회전 |

| Bullet의 새 함수 | 원래 위치 | 옮긴 코드 |
|---|---|---|
| `InitializeBullet(Bullet b, Vector2 v, float damage, bool hostile, float life, int stage, long attackId)` (private static) | Spawn 마지막 | 기존 순서대로 6개 필드 대입 |
| `CreatePresentation(Vector2 p, Vector2 v, bool hostile)` (private static) | Spawn의 sr 초기식 | 기존 Ink.Shape 호출 그대로 |
| `ApplyMuzzleOffset(SpriteRenderer sr, bool hostile)` (private static) | Spawn의 !hostile 분기 | PaperCard가 있으면 기존 SkillEffects.MuzzleOffset 적용 |
| `GetSubstepCount(float dt)` | Update의 steps 초기식 | Max/CeilToInt와 .16f 이동 간격 |
| `MoveSubstep(float dt, int steps)` | Update의 for 첫 문장 | velocity×dt/steps 위치 누적 |
| `HitObstacle(Vector2 p)` | Update의 Rect 충돌 분기 | Gold burst·Destroy |
| `HitPlayer(Dungeon g)` | Update의 hostile 적중 분기 | Hurt(damage)·Destroy |
| `HitEnemy(Dungeon g, Enemy e)` | Update의 비hostile 적중 분기 | Hit(e,damage,stage,attackId)·Destroy |

추출 함수가 새 조기 반환을 갖지 않도록 했다. Bullet의 경계/장애물/피격 후 `return`은 원래 `Update`의 분기 안에 남아 전체 Update를 끝낸다. 인자는 원래 캡처된 지역값을 전달하며 이동 후 표적 거리를 다시 계산하지 않는다.

정적 검증:

- [확인] C# 구문 파싱 오류 없음. 컴파일/빌드 검증은 하지 않았다.
- [확인] Enemy 필드 18개·프로퍼티 5개, Bullet 필드 6개의 이름/타입/접근성/초기식/속성 및 프로퍼티 본문 일치.
- [확인] 기존 Enemy 메서드 5개, Bullet 메서드 2개의 signature/기본 인자 유지.
- [확인] 새 helper 호출을 재귀적으로 원래 문장/식으로 펼쳐 작업 전 메서드와 C# 토큰을 비교했다. 7개 기존 메서드 모두 문장·연산식·상수·if/else/for·return 순서가 일치했다. 인자와 원래 지역값의 대응, helper의 조기 반환 부재도 검사했다.
- [확인] helper의 인자형/반환형은 기존 지역값과 호출 API에 맞춘다. 외부 Player/Raven/Dungeon/GameUIView/ActorPresentation 호출에 변경이 필요하지 않다.
- [사용자 Unity 테스트 필요] 실제 조작/표현 결과는 실행 미검증이며 13절에 별도로 기록한다.

## 5. 유지된 public API

| 분류 | 유지된 계약/호출자 |
|---|---|
| public 필드 | `int Kind`, `float Hp, MaxHp`, `Transform AuthoredArt, HealthFill`, `SpriteRenderer AuthoredSprite, AuthoredWarning` |
| public 프로퍼티 | `bool Dead`, `float AttackPower`, `Transform PresentationRoot`, `SpriteRenderer Presentation { get; private set; }`, `bool Telegraphing` |
| public 메서드 | `void Init(int kind,int room)`, `void TakeDamage(float damage,bool ignite)` |
| Bullet public static API | `void Spawn(Vector2 p,Vector2 v,float damage,bool hostile,float life,int stage=0,long attackId=0)` |
| Dungeon | `Scene.EnemyPrefabs[kind]` → Instantiate → `Init(kind,Room)` → Enemies.Add; null/Dead 목록 제거 및 Nearest |
| Player / Raven | Player.Hit → Enemy.TakeDamage; Raven은 Dead/위치 검사, TakeDamage 직접 호출, AttackPower 비교로 link 표적 선택 |
| UI / presentation | GameUIView의 BossHP는 Enemies[0].Hp/MaxHp; ActorPresentation의 Enemy 감지·Dead/Telegraphing 상태 읽기 유지 |

접근 제한자, namespace, 두 클래스의 MonoBehaviour 상속, 자동 프로퍼티 setter 접근성은 동일하다. 이 파일에는 원래 명시적 `[SerializeField]`가 없으며 public 필드 직렬화 계약을 유지했다. 신규 public/internal API나 EnemyKilled/BossKilled 이벤트, Dungeon callback은 없다.

## 6. 유지된 Kind 0~3 동작

| 항목 | Kind 0 | Kind 1 | Kind 2 | Kind 3 |
|---|---|---|---|---|
| Init MaxHp/Hp | 55+room×13 | 80+room×13 | 55+room×13 | 650 |
| AttackPower query | 15 | 22 | 13 | 25 |
| 공격 진입 거리 (dist<) | 1.7 | 6 | 8 | 8.5 |
| windup | .65초 | .85초 | .9초 | .9초 |
| 실행 후 cooldown | 1.5초 | 2초 | 1.5초 | 1.3초 |
| 일반 이동 속도 | 2.2 | 2.2 | 2.2 | 1.7 |
| 실행 | Ring1.55, dist<1.75일 때 Hurt15 | .45초 charge, Line5.4 | -16/0/+16도 3탄, speed6.5, damage13, life3 | cycle++, 매 3회 .55초 charge, 나머지 방사탄 |

- [확인] 초기 cooldown은 authored/fallback 양쪽에서 `Random.Range(.8f,2)` 한 번씩, 기존 호출 위치에 설정한다.
- [확인] 이동은 dist>range×.8 접근, 아니면 Kind2에서 dist<4일 때 후퇴, 나머지는 zero. 가까운 다른 살아 있는 적의 separation을 목록 순서대로 더한 후 ClampMagnitude(1)한다.
- [확인] separation 조건은 `away.sqrMagnitude<1.4f && >.01f`, 가중치 .7이다. 1.4를 반경 제곱값으로 새로 바꾸지 않았다.
- [확인] 논리 XY, `Rules.ResolveObstacles` 반경 .5와 기존 Rect/room clamp를 사용한다. Rigidbody/Collider/NavMesh/CharacterController/3D forward 이동을 추가하지 않았다.
- [확인] Kind1/3의 공유 charge는 speed12, 이동 전 dist<1.15에서 Hurt22다. boss AttackPower25를 실제 돌진 피해로 바꾸지 않았다.
- [확인] 보스 HP가 **엄격히** `Hp<MaxHp*.5f`일 때 count20/speed6, 그 밖에는 count12/speed4.5. damage17/life5, 각도 `i/count*360+cycle*13` 유지.
- [확인] 기존 Kind 범위 밖 값에 새 validation/default 패턴을 추가하지 않았다. Execute의 독립 if 네 개도 유지했다.

## 7. 유지된 Damage / Burn / Death 흐름

```text
Enemy.Update
  Dungeon.I → !Running 또는 Dead면 return → dt
  burnTime>0: timer 감소 → burnTick<=0: tick=.5 → TakeDamage → Gold burst3
    → 이때 Dead면 return
  flash 감소/art scale → HP bar
  p/delta/dist 캡처
  chargeTime>0: 감소 → 장애물 해결 이동 → 기존 dist의 접촉 Hurt → return
  windup>=0: 감소 → 만료 시 warning off → Execute → windup=-1 → cooldown 설정
    → 만료 여부와 관계없이 return
  cooldown 감소 → range → 공격 windup/warning 또는 이동

TakeDamage
  Dead 또는 Cinematic이면 return
  HP=max(0,HP-damage) → flash=.1 → ignite별 Gold/Cyan burst5
  ignite이면 burnTime=3
  Dead이면 Die: Kills++ → warning off → Red burst14 → Destroy(gameObject)
  OnDestroy: warning 오브젝트가 있으면 Destroy

Dungeon (기존 코드, 수정 없음)
  RemoveDefeatedEnemies(!e || e.Dead)
  → Combat && Enemies.Count==0 → ClearTransient → Progress.CompleteWave
  → 다음 WaveBreak 또는 Room 완료/HP+18/State.Exit
```

Burn은 기존처럼 초기 burnTick 기본값 0을 사용하며 ignite에서 burnTick을 재설정하지 않는다. `TakeDamage`는 Running 여부를 새로 검사하지 않는다. Burn으로 죽어도 그 Tick의 Gold burst는 TakeDamage 이후, Dead return 이전에 발생한다. Death를 코루틴/애니메이션 대기/보상 이벤트로 바꾸지 않았다.

Bullet은 Running 가드 → life 감소 → 만료 Destroy/return → substeps 순서다. 매 substep은 이동 → RoomOrigin 경계 → Rect 장애물 → hostile Player 또는 비hostile Enemy 순서이며 첫 충돌에서 전체 Update가 반환된다. lifetime/substep(.16)/경계14.8·8.8/Player radius.48/일반 Enemy radius.55/Boss radius1, stage/attackId 전달 및 Destroy 시점이 동일하다.

현재 Enemy/Bullet에는 직접 Sound 호출이 없다. 기존 Ink/CombatArt/SkillEffects 호출과 호출 순서를 보존했으며 새 사운드나 표현 시스템을 추가하지 않았다.

## 8. Boss Kind 3 격리 위치

실행 패턴은 `ExecuteBossPattern` → `BeginBossCharge` 또는 `FireBossRadialPattern`에 모았다. 기존 fallback crown/scale만 `CreateBossFallbackPresentation`으로 옮겼다.

보스와 일반 적이 공유하던 부분은 공유 상태로 남아 있다:

- `Init`의 650HP, `GetAttackRange`의 8.5, `BeginAttackWindup`의 .9, `TickWindup`의 cooldown1.3.
- `TickMovement`의 speed1.7, `TickFlash`의 기본 scale1.9, `ShowAttackWarning`의 scale5.
- `CreateFallbackPresentation`의 HP bar 위치/폭, `AttackPower`의 query25.
- `TickCharge`의 이동/접촉 피해, `TakeDamage`/`Die`의 공통 사망 처리, Bullet의 Kind3 적중 반경1.

BossController는 생성하지 않았다. 향후 이전 시 위 공유 분기와 public Enemy 참조 계약까지 함께 고려해야 하며, 이번에 보스 사망/clear/reward lifecycle을 분리한 것은 아니다.

## 9. Authored Prefab 연결 보존

- [확인] `AuthoredArt`, `HealthFill`은 public Transform, `AuthoredSprite`, `AuthoredWarning`은 public SpriteRenderer로 이름/타입을 유지했다.
- [확인] `Enemy.cs.meta` GUID `75c6310d2c42f904896768749a5f794c` 유지. Enemy_0/Enemy_1/Enemy_2/Boss.prefab은 이 스크립트와 `WesternLemegeton.Enemy`를 참조한다.
- [확인] 4개 Prefab에서 위 4개 reference의 fileID가 모두 존재하며 각각 Transform/SpriteRenderer YAML record를 가리킨다. 저장된 fileID와 Prefab 내용은 수정하지 않았다.
- [확인] authored 경로는 4개가 **전부** 존재할 때만 사용한다. bind 후 warning off → Random cooldown → Init return을 유지했다.
- [확인] fallback은 기존 `Enemy art`, CombatArt.Actor("Enemy",1.7), color tint, Kind3 crown/scale, HP background/HP, 독립 Attack warning 생성 후 같은 public 참조에 되쓴다. 생성 조건/계층/이름/인자/정리 방식은 동일하다.
- [확인] flash scale, Hp/MaxHp bar scale, warning 위치/각도/scale 및 PaperCard MuzzleOffset 경로 유지. 새 Animator/Spine/CustomVisual 활성화는 없다.

## 10. 향후 일반 Enemy 분리 후보

아래는 제안만 하며 실제 분리하지 않았다.

| 후보 책임 | 현재 절단선 | 분리 시 유지할 공유 계약 |
|---|---|---|
| 표적/이동 | GetTargetDelta, TickMovement, TickCharge | 기존 캡처 p/dist, Enemies 순서, Rules Rect/XY |
| 일반 공격 | ExecuteMeleeAttack, BeginChargeAttack, FireRangedProjectiles | windup/aim/cooldown과 공통 charge의 실행 순서 |
| HP/상태 | ApplyDamageImpact, TickBurnTimers, ApplyBurnTick, Die | Dead/TakeDamage API, Burn 사망 반환, Dungeon 목록 polling |
| 표시 | BindAuthoredPresentation, CreateFallbackPresentation, ShowAttackWarning, UpdateHealthBar, TickFlash | 기존 Inspector/GUID와 fallback 경로 |
| Bullet | 같은 파일의 Bullet 전용 3개 region | Spawn signature, 양쪽 Hit 경로, lifetime/return |

## 11. 향후 BossController 이전 후보

`ExecuteBossPattern`, `BeginBossCharge`, `FireBossRadialPattern`은 기존 보스 공격 이전 후보다. `cycle`, HP 강화 판정, aim/chargeTime, 공유 timing 상태의 소유권을 함께 검토해야 한다. `CreateBossFallbackPresentation`은 보스 전용 fallback 표현 후보다.

Enemy를 그대로 참조하는 Dungeon.Enemies/EnemyPrefabs, Player.Hit, Raven 표적, UI HP 계약은 이번 단계에서 유지했다. 새 BossController 설계, Death event, Reward 생성, 사망 대기는 구현하지 않았으며 후속 작업의 별도 범위다.

## 12. 향후 Enemy Data / Encounter 이전 후보

| 현재 의존 위치 | 현재 값/역할 | 향후 이전 후보 |
|---|---|---|
| Init(kind,room) | Kind 및 HP 수식; 현재 Dungeon은 `Room`(stage/segment)을 전달 | Enemy 수치 데이터 또는 encounter spawn context |
| GetAttackRange/BeginAttackWindup/TickWindup/AttackPower | Kind별 range/windup/cooldown/query | Enemy 데이터 후보 |
| TickMovement/TickCharge 및 Kind별 실행 함수 | 속도/판정/패턴 상수 | 기존 일반/보스 동작 데이터 후보 |
| ApplyBurnTick/TakeDamage | Dungeon.BurnLevel 및 Cinematic 상태 | 전투 상태/효과 context 후보 |
| Enemy/Bullet Update | Dungeon.Running, Pos/Hero, Enemies | 현재 encounter의 실행/표적/추적 context 후보 |
| Rules.ResolveObstacles, Bullet.Update | Dungeon.Obstacles, Rules.RoomOrigin, 기존 방 경계 | encounter/방 geometry context 후보 |
| Die | Dungeon.Kills 증가; 목록 제거/웨이브 완료는 Dungeon 책임 | 공통 사망 통계 계약 후보 |

새 MapIndex/Room03/Room04/Room06 분기, RoomGraph/door.Destination/Scene 이름 의존성은 추가하지 않았다. 기존 고정 room 경계 의존성을 기록했을 뿐 랜덤 던전/Encounter나 Enemy Data 파일을 구현하지 않았다. 향후 맵 구조 변경도 Enemy가 직접 방/웨이브/보상을 완료하게 만드는 근거가 아니다.

## 13. 사용자 Unity 테스트 필요 항목

[사용자 Unity 테스트 필요] 아래는 실행하지 않았다. 정적 비교가 보존한 코드의 실제 화면/프레임 동작 확인용이며, AI가 Editor/Play Mode/빌드를 반복 실행하는 단계는 아니다.

| 테스트 방법 | 정상 결과 | 실패 시 확인할 증상 |
|---|---|---|
| 일반 전투에서 Kind0/1/2의 접근·예고·공격을 관찰 | 기존 이동/separation/장애물, 근접·돌진·3탄, warning/HP/flash 표시 | 예고 후 정지, 돌진 중복 공격, 적/탄환 통과, HP/scale 누락 |
| 기존 최종 보스전에서 공격 3회 주기와 HP 절반 아래를 관찰 | 방사탄12/speed4.5 → HP<절반에서20/speed6; 매3회 돌진, 기존 warning | 주기/탄수/속도/돌진 접촉 판정 차이 |
| 기존 Fire 성흔으로 ignite 후 적/보스 처치 | Burn 피해/효과, Kills 증가, warning 제거; 전멸 시 기존 wave/clear/HP+18 흐름 | Burn 이후 죽은 적 잔존, Kills 중복, warning 잔존, 웨이브 정체 |
| 리볼버 및 Raven Combo/R Link로 적을 공격 | 탄환의 stage/attackId 적중 경로와 기존 Raven 표적/연계, BossHP 갱신 유지 | 탄환 다중 적중/관통, Combo/Link 표적 차이, HP UI 갱신 누락 |

authored/fallback 참조 조건과 생성/정리 코드는 정적으로 일치한다. fallback의 실제 화면은 이번에 실행하지 않았으며, 후속 작업에서 기존 fallback 진입 경로를 실행한다면 crown/HP/warning의 생성과 파괴도 함께 관찰할 수 있다. 테스트를 위해 저장된 Prefab 참조를 끊거나 새 자산을 만들지는 않았다.
