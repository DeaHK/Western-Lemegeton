# 몬스터를 기능으로 조립하기

최종 갱신: **2026-10-22 (한국 시간)**

## 변경 기록

- 기존 구성: `Enemy_0`의 체력·추적·근접 공격·행동 제어 분리.
- **2026-10-21 추가**: 이동 공통 규격 `EnemyMovementModule`, 공격 공통 규격 `EnemyAttackModule`.
- **2026-10-21 변경**: `EnemyBasicBrain`이 구체적인 추적/근접 클래스 대신 공통 규격을 찾고 실행하도록 변경.
- **2026-10-21 추가**: `EnemyKeepDistanceMovement`, `EnemyChargeAttack`, `EnemyRangedAttack` 및 `Enemy_1`, `Enemy_2`의 조립형 전환. 기존 이동·공격 수치와 프레임 처리 순서를 보존.
- **2026-10-21 추가**: 기존 행동 경로와 새 블록의 프레임별 비교 검사 `EnemyPatternChecks`.
- **2026-10-21 추가 갱신**: 공격 공통 규격의 `AttackRange`와 거리 유지 이동의 사거리 자동 연동. `Enemy_1`, `Enemy_2`는 공격의 `Trigger Range`만 바꾸면 접근 기준도 함께 변경됩니다.
- **2026-10-22 추가**: `EnemyBossAttack` 복합 공격 모듈. 근접·돌진·원거리의 개별 ON/OFF와 일반/강화 방사 탄환 개수를 Inspector에 노출.
- **2026-10-22 수정**: Boss 프리팹을 체력·거리 유지 이동·복합 공격·Brain 조합으로 전환. 기본 근접 OFF, 돌진/원거리 ON으로 기존 방사 2회 → 돌진 1회 패턴과 수치를 유지.
- **2026-10-22 추가**: 기존 보스와의 프레임별 비교, 체력 50% 경계, 공격 조합 8종, 진행 중 취소, 탄환 개수 설정 검사.

## 기반 부품과 공통 규격 — 2026-10-22 수정

모든 부품은 **같은 루트 GameObject**에 붙입니다. 자식의 부품은 자동으로 찾지 않습니다.

| 기반 부품 | 역할 |
| --- | --- |
| Enemy | 게임의 몬스터 목록과 연결되는 본체. 초기화, 피격·사망, 보상, 낙인, 체력바·경고 표시를 연결 |
| EnemyHealth | 최대 체력 계산과 현재 체력·피해 관리 |
| EnemyBasicBrain | 공통 규격의 공격을 먼저 실행하고 반환값에 따라 일반 이동 실행 |

`EnemyBasicBrain`을 추가하면 `RequireComponent`에 의해 `EnemyHealth`가 함께 추가됩니다. `Enemy`는 별도로 필요합니다.

| 선택 부품 | 공통 규격 | 역할 |
| --- | --- | --- |
| EnemyChaseMovement | EnemyMovementModule | 목표 추적, 몬스터 간 간격 유지, 장애물 처리 |
| EnemyKeepDistanceMovement | EnemyMovementModule | 공격 사거리와 연동하여 접근, 가까우면 후퇴. 후퇴 거리를 0으로 하면 접근만 수행 |
| EnemyMeleeAttack | EnemyAttackModule | 준비 후 현재 거리로 근접 명중 판정 |
| EnemyChargeAttack | EnemyAttackModule | 준비 시작 시 방향 고정, 준비 후 돌진 및 접촉 피해 |
| EnemyRangedAttack | EnemyAttackModule | 준비 시작 시 방향 고정, 준비 후 부채꼴 탄환 발사 |
| EnemyBossAttack | EnemyAttackModule | 켜진 근접·돌진·방사 원거리 공격을 순서대로 실행하는 보스용 복합 공격 |

추상 기반 클래스 `EnemyMovementModule`, `EnemyAttackModule` 자체를 붙이지 않습니다. 이를 상속한 구체적인 부품을 **이동 하나, 공격 하나까지** 붙입니다. 각각 없어도 Brain은 나머지 부품을 실행합니다. 같은 계열 부품 중복은 `DisallowMultipleComponent`로 제한됩니다.

## 저장된 프리팹 조합 — 2026-10-21 추가, 2026-10-22 수정

아래 네 프리팹 모두 `Enemy`, `EnemyHealth`, `EnemyBasicBrain`을 갖습니다.

| 프리팹 | 이동 | 공격 | 체력 |
| --- | --- | --- | --- |
| Enemy_0 | EnemyChaseMovement | EnemyMeleeAttack | 55 + 방 번호 × 13 |
| Enemy_1 | EnemyKeepDistanceMovement: 공격거리 × 0.8 (기본 약 4.8), 후퇴 0 | EnemyChargeAttack | 80 + 방 번호 × 13 |
| Enemy_2 | EnemyKeepDistanceMovement: 공격거리 × 0.8 (기본 약 6.4), 후퇴 4 | EnemyRangedAttack | 55 + 방 번호 × 13 |
| Boss | EnemyKeepDistanceMovement: 다음 공격거리 × 0.8 (기본 약 6.8), 속도 1.7, 후퇴 0 | EnemyBossAttack: 근접 OFF, 돌진/원거리 ON | 650 (방 번호 증가 없음) |

`Kind`는 기존 생성 목록, 보상, 외형과의 호환을 위해 유지합니다. Brain이 있으면 행동은 `Kind` 대신 실제 부품으로 결정됩니다. Boss의 `Kind = 3`은 유지하며, 저장된 Boss 프리팹도 이제 조립형 행동 경로를 사용합니다.

### 보존한 Enemy_1 동작

- 일반 이동 속도 2.2, 목표 거리 4.8 초과일 때 접근. 후퇴 없음.
- 다른 몬스터와의 제곱 거리 1.4 미만에서 분리 강도 0.7 적용, 몸체 반경 0.5로 장애물 처리.
- 최초 공격 대기 0.8~2초 무작위, 목표 거리 6 미만이면 공격 준비 시작.
- 준비 시간 0.85초, 이때 목표 방향을 고정하며 일반 이동 정지.
- 경고 위치는 시작 위치 + 조준 방향 × 2.8, 크기는 (6, 1, 1), 회전은 조준 방향.
- 준비가 끝난 프레임에는 돌진을 예약하고, 다음 프레임부터 속도 12로 0.45초간 돌진. 감속 효과와 장애물 적용.
- 돌진 프레임마다 **이동 전 거리**가 1.15 미만이면 `Player.Hurt(22)` 호출. 플레이어의 기존 무적시간 적용.
- 공격 후 대기 2초. 준비·돌진 중에는 대기시간을 줄이지 않음.
- 돌진 종료 프레임에서 남은 시간만큼 이동량을 잘라내지 않는 기존 처리도 유지.

### 보존한 Enemy_2 동작

- 일반 이동 속도 2.2. 거리 6.4 초과이면 접근, 4 미만이면 후퇴, 그 사이에서는 목표 방향 이동 정지. 분리 이동은 별도 적용.
- 몬스터 분리와 장애물 처리는 Enemy_1과 동일.
- 최초 공격 대기 0.8~2초 무작위, 거리 8 미만이면 공격 준비 시작.
- 준비 시간 0.9초, 준비 중 정지. **준비 시작 시 고정한 방향**으로 발사하며 발사 직전에 재조준하지 않음.
- 자신 위치에 크기 (2, 2, 2)의 경고 표시.
- -16°, 0°, +16° 방향으로 탄환 3개 발사. 속도 6.5, 피해 13, 수명 3초.
- 발사한 프레임에는 일반 이동을 하지 않으며, 이후 공격 대기 1.5초 동안 거리 유지 이동.
- 공격 준비가 가능한 프레임에는 후퇴보다 공격 시작을 우선하는 기존 순서 유지.

## Inspector에서 수정하기 — 2026-10-22 수정

1. Unity의 스크립트 가져오기·컴파일 완료 후 `Assets/Editable/Actors`의 `Enemy_0.prefab`, `Enemy_1.prefab`, `Enemy_2.prefab`, `Boss.prefab` 중 하나를 엽니다.
2. Prefab Mode에서 맨 위 루트를 선택합니다. 자식 그림이 아니라 루트에 부품이 있습니다.
3. 원하는 수치를 수정하고 프리팹을 저장합니다. 네 프리팹 모두 부품이 이미 연결되어 있습니다.

| 부품 | 주요 설정 |
| --- | --- |
| EnemyHealth | Base Health, Health Per Room |
| EnemyChaseMovement | Speed, Stop Distance, Body Radius, Separation Radius, Separation Strength |
| EnemyKeepDistanceMovement | Speed, Use Attack Range, Approach Range (수동/대체값), Approach Range Factor, Retreat Distance, Body Radius, Separation Distance Squared, Separation Strength |
| EnemyMeleeAttack | Damage, Trigger Range, Hit Range, Windup Seconds, Cooldown Seconds, Warning Radius |
| EnemyChargeAttack | Damage, Trigger Range, Windup Seconds, Cooldown Seconds, Charge Seconds, Charge Speed, Hit Range, Body Radius, Initial Cooldown Range, Warning Size, Warning Offset, Trail Length |
| EnemyRangedAttack | Damage, Trigger Range, Windup Seconds, Cooldown Seconds, Projectile Speed, Projectile Lifetime, Projectile Count, Spread Angle, Warning Radius, Initial Cooldown Range |
| EnemyBossAttack | Enable Melee, Enable Charge, Enable Ranged, Melee/Charge/Ranged 하위 설정, Initial Cooldown Range, Threat Power, Idle Approach Range |

- `Base Health = 100`, `Health Per Room = 0`이면 방 번호에 관계없이 체력 100으로 초기화합니다. 체력 설정 변경은 다음 `Init()`에 반영되며 현재 체력을 즉시 바꾸지 않습니다.
- `Current`, `Maximum`, `Enemy.Hp`, `Enemy.MaxHp`는 프로퍼티로 기본 Inspector의 편집 필드가 아닙니다.
- 거리 유지 이동은 기본적으로 `공격 부품의 AttackRange × Approach Range Factor`를 접근 기준으로 사용합니다. 일반 몬스터의 단일 공격 3종은 Inspector의 `Trigger Range`를 반환하고, 보스 복합 공격은 진행 중이거나 다음 차례인 공격의 `Trigger Range`를 반환합니다. 후퇴 거리는 이 접근 기준 이하로 설정하세요. 기존 부동소수점 경계 판정까지 보존하기 위해 곱셈을 유지합니다.
- `Separation Distance Squared`는 반경이 아닌 **제곱 거리**입니다. 기존 경계 판정을 정확하게 보존하기 위해 기본값 1.4를 직접 사용합니다.
- 돌진 공격의 이동은 공격 자체의 동작입니다. 일반 이동 부품을 꺼도 공격 부품이 켜져 있으면 돌진합니다.
- `Cooldown Seconds`는 공격 후 대기입니다. 돌진/원거리의 첫 대기는 `Initial Cooldown Range`에서 정합니다. 기존 근접 부품의 첫 대기는 코드의 0.8~2초를 유지합니다.
- Play 중 바꾼 값은 일반적으로 Play 종료 시 되돌아갑니다. 영구 설정은 Play를 끈 상태에서 프리팹에 저장하세요.

### 공격거리 자동 연동 — 2026-10-21 추가 갱신

`Enemy_1`과 `Enemy_2`의 `EnemyKeepDistanceMovement > Use Attack Range`는 켜져 있습니다.

예를 들어 Enemy_1을 복제한 새 돌진 몬스터에서 `EnemyChargeAttack > Trigger Range`를 6에서 10으로 바꾸면, 일반 접근 기준도 자동으로 `10 × 0.8 = 약 8`이 됩니다. 이동 부품의 `Approach Range`를 함께 수정할 필요가 없습니다.

- `EnemyBasicBrain.Initialize()`가 공격 부품을 이동 부품에 연결하며, 이동은 **매 Tick에 현재 사거리**를 읽습니다. Play 중 사거리 수치를 수정해도 재초기화 없이 다음 일반 이동에 반영됩니다. 공격 준비·돌진 등으로 일반 이동이 막힌 동안에는 그 공격의 기존 실행 순서를 유지합니다.
- `Use Attack Range`를 끄면 수동 설정 `Approach Range × Approach Range Factor`를 사용합니다.
- 공격 부품이 아예 없거나 제거되었으면 같은 수동 설정으로 안전하게 돌아갑니다. `Approach Range`는 이 경우를 위한 대체값이기도 합니다.
- 공격 부품을 비활성화한 경우에는 사거리 설정을 계속 읽습니다. 공격을 끄는 것만으로 일반 이동의 거리까지 바뀌지 않습니다.
- `Retreat Distance`, 명중 거리, 돌진 속도·지속시간, 경고 크기는 자동으로 비례 변경하지 않습니다. 예를 들어 돌진 시작 거리를 늘려도 실제 돌진 이동량은 `Charge Speed`와 `Charge Seconds`로 별도 결정합니다.
- `Enemy_0`의 `EnemyChaseMovement`는 기존의 고정 `Stop Distance` 방식을 유지합니다. 자동 연동을 원하는 추적 몬스터에는 `EnemyKeepDistanceMovement`를 사용하고 `Retreat Distance = 0`으로 설정할 수 있습니다.

새 공격 부품도 공통 규격의 `AttackRange`를 구현하면 거리 유지 이동과 자동으로 연결됩니다. 구체적인 공격 클래스 이름을 이동 코드에 추가할 필요가 없습니다.

## 보스 조립과 공격 선택 — 2026-10-22 추가

Boss 루트에는 `Enemy`, `EnemyHealth`, `EnemyKeepDistanceMovement`, `EnemyBossAttack`, `EnemyBasicBrain`이 있습니다. 별도의 근접/돌진/원거리 컴포넌트 세 개를 동시에 붙이지 않습니다. `EnemyBossAttack` 하나가 공통 규격을 따르고 그 안에서 세 공격 종류를 관리합니다.

Inspector의 `Enemy Boss Attack`에서 다음 체크박스를 사용합니다.

| 항목 | Boss 프리팹 기본값 | 동작 |
| --- | --- | --- |
| Enable Melee | OFF | 준비 종료 시 실제 거리로 명중을 판정하는 근접 공격 |
| Enable Charge | ON | 준비 시작 때 고정한 방향으로 돌진 |
| Enable Ranged | ON | 자신 위치에서 360도 방사 탄환 발사 |

기본 순서는 **원거리 → 원거리 → 돌진 → 근접**이며, 꺼진 종류의 순서는 건너뜁니다.

- 기본 Boss: **원거리 → 원거리 → 돌진** 반복. 기존과 동일합니다.
- 모두 ON: 원거리 → 원거리 → 돌진 → 근접 반복.
- 원거리 OFF: 돌진 → 근접 반복. 근접도 OFF이면 돌진만 반복합니다.
- 모두 OFF: 공격하지 않고 일반 이동만 수행합니다. 이때 접근 기준은 `Idle Approach Range × Approach Range Factor`입니다.
- 선택된 공격의 사거리 밖이면 그 차례를 건너뛰지 않고 접근하여 사거리 안에서 준비합니다. 근접 차례에는 가까이 접근할 수 있도록 다음 공격의 사거리를 이동 부품에 전달합니다.
- 개별 공격을 준비/돌진 도중 OFF로 바꾸면 다음 Tick에서 그 공격과 경고를 취소합니다. 취소한 프레임에는 일반 이동도 건너뛰고 다음 프레임부터 다음 켜진 종류를 선택합니다. 일시 정지 중에는 다음 게임 진행 Tick에 반영됩니다.
- 공격 컴포넌트 전체 또는 Brain을 비활성화하면 진행 중 공격을 취소합니다. 이미 발사된 탄환은 독립적으로 수명이 끝날 때까지 동작합니다.

### Boss 기본 수치 — 2026-10-22 추가

| 항목 | 근접 (기본 OFF) | 돌진 | 원거리 |
| --- | --- | --- | --- |
| 기본 피해 | 15 | 22 | 탄환당 17 |
| 공격 준비 시작 거리 | 1.7 | 8.5 | 8.5 |
| 준비 시간 | 0.65초 | 0.9초 | 0.9초 |
| 공격 후 대기 | 1.5초 | 1.3초 | 1.3초 |
| 명중/공격 방식 | 발동 시 거리 1.75 미만 | 이동 전 거리 1.15 미만 | 직진 탄환 |
| 경고 반경 | 1.55 | 2.5 | 2.5 |

- 체력 650, 방당 증가 0. 일반 이동 속도 1.7, 접근 비율 0.8, 후퇴 0, 몸체 반경 0.5, 분리 제곱 거리 1.4·강도 0.7을 유지합니다.
- 최초 공격 대기는 0.8~2초 무작위입니다. 준비 중 일반 이동을 멈추고, 준비가 끝난 프레임에도 일반 이동을 하지 않습니다.
- 돌진은 다음 프레임부터 속도 12로 0.55초간 진행합니다. 이동 전 거리로 접촉을 판정하고, 돌진 중에는 공격 후 대기시간을 줄이지 않습니다. 플레이어의 기존 무적시간이 반복 피해를 제한합니다.
- 기본 원거리는 체력 50% 이상일 때 12발·속도 4.5, **50% 미만**일 때 20발·속도 6입니다. 정확히 체력 325에서는 기본 상태입니다. 실제 발사 시 체력으로 판정하며 순서를 초기화하지 않습니다.
- 원거리 탄환 수명 5초, 기본 회전 증가량은 실행한 전체 공격 횟수 × 13도입니다. 돌진 및 활성화한 근접 공격도 이 횟수에 포함됩니다.
- 보스는 기존 원형 경고를 유지합니다. 돌진 시작 링은 표시 효과이며 별도의 범위 피해가 없습니다.
- `Threat Power = 25`는 Raven 대상 우선순위용 조회값이며 실제 피해 15/22/17과 별개입니다. 모든 공격을 끄면 조회값은 0입니다.
- `Init()`을 다시 호출하면 체력뿐 아니라 보스 공격 순서·준비·돌진 상태도 초기화합니다.

### 원거리 탄환 개수 설정 — 2026-10-22 추가

**일반 원거리 유닛과 보스 모두 Inspector에서 탄환 개수를 설정할 수 있습니다.**

| 대상 | Inspector 설정 위치 | 기본값 |
| --- | --- | --- |
| Enemy_2 | Enemy Ranged Attack > Projectile Count | 3 |
| Boss 일반 상태 | Enemy Boss Attack > Ranged > Projectile Count | 12 |
| Boss 저체력 상태 | Enemy Boss Attack > Ranged > Low Health Projectile Count | 20 |

- Enemy_2는 부채꼴 발사입니다. `Spread Angle`은 탄환 사이 각도이며 기본 16도입니다. 개수만 늘리면 전체 부채꼴 폭이 커집니다.
- Boss는 360도를 지정한 개수로 균등 분할합니다. 개수를 늘리면 탄환 사이 간격이 좁아집니다.
- 보스 강화 기준은 `Ranged > Low Health Threshold`로 변경할 수 있습니다. 기본 0.5입니다. 두 상태의 개수와 속도를 같게 설정하면 저체력에서도 탄환 개수·속도가 바뀌지 않습니다.
- 개수는 1 이상으로 설정합니다. 원거리 공격 자체를 끄려면 Boss는 `Enable Ranged`, Enemy_2는 공격 컴포넌트의 활성 체크를 사용합니다.
- Play 중 바꾼 개수는 다음 발사에 반영됩니다. 이미 발사된 탄환의 개수는 바뀌지 않습니다.

## 부품 교체와 새로운 패턴 — 2026-10-21 추가

Prefab Mode에서 기존 이동 또는 공격 부품을 제거하고, 같은 공통 규격을 상속한 새 부품을 붙이면 됩니다. 기존 부품을 비활성화하는 것만으로는 같은 계열 부품을 중복 추가할 수 없습니다. 부품 검색은 `Init()` 시점에 이루어지므로 실행 중 부품을 교체했다면 다시 초기화해야 합니다. 재초기화는 체력과 공격 상태도 초기화합니다.

이동 부품은 `EnemyMovementModule`을 상속하고 아래 함수를 구현합니다.

```csharp
public override void Tick(Enemy owner, Dungeon game, Vector2 target, float speedMultiplier, float dt)
```

공격 부품은 `EnemyAttackModule`을 상속하고 아래 규격을 구현합니다.

```csharp
public override bool Telegraphing { get; }
public override float AttackPower { get; }
public override float AttackRange { get; }
public override void Initialize(Enemy owner)
public override bool Tick(Dungeon game, float speedMultiplier, float dt)
public override void Cancel()
```

- `Tick`에서 `true`를 반환하면 해당 프레임의 일반 이동을 막습니다. 공격 준비, 근접 공격, 돌진에 사용합니다.
- `false`를 반환하면 공격 처리 후 이동 부품도 실행합니다. 이동하면서 발사하는 패턴도 이 규격으로 구현할 수 있습니다.
- 공격 자체의 이동은 공격 블록 내부에서 처리할 수 있습니다. `EnemyChargeAttack`이 예시입니다.
- 준비/돌진/회복/연속 공격 등 여러 단계는 공격 부품 내부 상태로 표현할 수 있습니다. Brain은 구체적인 상태를 알 필요가 없습니다.
- `Cancel()`은 진행 중 공격과 경고를 정리해야 합니다. 개별 공격 부품에도 `OnDisable()`에서 `Cancel()`을 호출하도록 구현하세요. Brain 비활성화 시에도 현재 공격의 `Cancel()`을 호출합니다.
- `Telegraphing`은 준비 표시/애니메이션, `AttackPower`는 Raven의 대상 우선순위 조회에 사용합니다.
- `AttackRange`는 일반 이동의 접근 기준에 사용되는 공격 시작 거리입니다. 새 공격 부품은 예를 들어 `public override float AttackRange => TriggerRange;`로 구현합니다. 실제 명중 거리나 돌진 이동량과 구분하세요.
- 이동 기반의 `Initialize(EnemyAttackModule attack)`는 Brain이 호출하며, 파생 이동 부품은 `protected Attack`으로 공통 공격 정보를 읽을 수 있습니다. 이를 오버라이드하면 `base.Initialize(attack)`도 호출하세요.

**현재 범위와 한계 (2026-10-22 수정):** 한 이동 부품과 한 공격 부품의 조합이며, 여러 공격 컴포넌트를 동시에 붙여 자동 선택하는 시스템은 아닙니다. `EnemyBossAttack`은 하나의 공격 부품 안에서 세 종류와 순서를 관리하는 복합 공격 예시입니다. 일반 몬스터와 보스 모두 같은 Brain 규격을 사용합니다.

## 실행 조건과 기존 코드 호환 — 2026-10-22 수정

- 씬의 `Dungeon`, 연결된 플레이어, 낙인·패시브 시스템 등 기존 게임 환경이 필요합니다. 이번 변경은 독립 씬용 게임 시스템 분리가 아닙니다.
- 새 프리팹을 만들기만 하면 전투에 자동으로 등장하지 않습니다. Main 씬의 `GameSceneBindings.EnemyPrefabs` 목록을 사용합니다. 기존 일반 몬스터와 Boss 프리팹은 GUID를 유지하므로 다시 연결할 필요가 없습니다.
- 현재 생성기는 0~2번 일반 몬스터와 3번 보스 슬롯을 사용합니다. 목록에 다섯 번째 프리팹을 추가하는 것만으로 새 종류가 생성되지는 않습니다.
- 직접 생성하는 코드는 `Enemy.Init(kind, room)`을 호출하고 `Dungeon.Enemies`에 등록해야 합니다. 씬에 끌어다 놓는 것만으로 자동 초기화·등록되지 않습니다.
- `AuthoredArt`, `AuthoredSprite`, `HealthFill`, `AuthoredWarning`은 기존처럼 연결해야 합니다. 하나라도 빠지면 대체 외형 생성 경로를 사용합니다.
- Brain이 있는 프리팹의 이동·공격 부품은 선택 사항입니다. 제거한 부품을 `Init()`이 임의로 다시 추가하지 않습니다.
- Brain이 없는 구형 0번 몬스터는 초기화 시 기본 부품을 추가합니다. 이미 공통 규격의 다른 이동·공격이 있으면 이를 유지합니다.
- Brain이 없는 구형 1/2번 생성 코드와 보스의 기존 행동 경로는 호환 및 비교 검사를 위해 유지합니다. 저장된 Enemy_1/Enemy_2/Boss 프리팹은 새 블록 경로를 사용합니다.
- 체력 부품을 비활성화하는 것으로 무적을 구현하지 않습니다. Brain을 끄면 행동이 멈추고 체력·피격 처리는 유지됩니다.

## 검사 실행 — 2026-10-22 수정

Unity 메뉴 `Western Lemegeton > Tests > Check Basic Enemy Modules`에서 실행합니다.

- 기존 근접 체력·이동·명중·취소 검사와 보스 초기화 검사.
- 저장된 0/1/2/Boss 프리팹의 부품 연결과 Inspector 설정 전달.
- `EnemyPatternChecks`: 같은 프리팹에서 부품을 제거한 기존 Enemy 경로와 조립형 경로를 같은 난수 시드·플레이어 위치·프레임 시간으로 비교.
- 1/2번 각각 420프레임에 걸쳐 이동, 감속, 분리, 장애물, 준비·대기·돌진 시간, 접촉 피해, 경고 표시, 탄환 방향·속도·피해·수명 비교.
- 공격 일시 정지·비활성화, 다른 종류 부품으로 교체, 이동/공격 부품 없는 조합 검사.
- **2026-10-21 추가 갱신 검사**: 초기화 후 공격거리만 변경했을 때의 실제 이동, 공격 비활성화 시 거리 유지, 수동 거리 선택, 공격 제거 시 대체값, 다른 공격 부품으로 교체한 뒤 자동 연동 검사.
- **2026-10-22 추가 검사**: Boss 840프레임 기존 코드 비교, 체력 650/325/324와 회복 시 패턴 유지, ON/OFF 조합 8종, 준비·돌진 도중 취소, 근접 회피, 보스 재초기화, Enemy_2와 Boss의 사용자 지정 탄환 개수.

검사는 임시 씬에서 실행하고 원래 씬 파일을 저장하지 않습니다. 화면의 애니메이션과 연출은 Main 씬 Play에서도 확인하세요. 실행 결과는 해당 실행 로그를 기준으로 확인합니다.

### 2026-10-21 최초 조립형 전환 실행 결과

- Unity 6000.3.17f1의 분리된 검증 프로젝트에서 배치 실행 완료, 종료 코드 0.
- `ENEMY_PATTERN_CHECKS_PASSED: 9040`, `ENEMY_MODULE_CHECKS_PASSED: 23`.
- 검사에 사용한 C# 소스와 세 몬스터 프리팹이 작업 폴더와 동일한지 파일 해시로 확인.
- 로그: `Logs/EnemyModules-20261021.log` (로컬 검증 로그).
- 그래픽 없는 배치 검사이므로 실제 화면의 애니메이션·렌더링 육안 검증은 포함하지 않습니다.

### 2026-10-21 사거리 자동 연동 추가 갱신 실행 결과

- 같은 Unity 6000.3.17f1 검증 프로젝트에서 재실행 완료, 종료 코드 0.
- `ENEMY_PATTERN_CHECKS_PASSED: 9056`, `ENEMY_MODULE_CHECKS_PASSED: 23` — 총 9,079개 검사 통과.
- 기존 패턴 비교에 더해, 두 프리팹의 사거리 자동 연동·수동 전환·공격 제거/교체 검사를 통과했습니다.
- 변경한 소스와 프리팹이 검증 사본과 동일한지 파일 해시로 확인했습니다.
- `Logs/EnemyModules-20261021.log`는 이 최신 실행 결과로 갱신되었습니다. 육안 검증은 포함하지 않습니다.

### 2026-10-22 보스 모듈화 실행 결과

- Unity 6000.3.17f1의 분리 검증 프로젝트에서 컴파일과 배치 검사 통과, Unity 로그 종료 코드 0.
- `ENEMY_PATTERN_CHECKS_PASSED: 20597`, `ENEMY_MODULE_CHECKS_PASSED: 23` — 총 20,620개 검사 통과.
- 기본 Boss와 기존 코드의 840프레임 비교, ON/OFF 조합 8종, 근접 명중·회피, 돌진 취소, 일반/저체력 탄환 개수 설정을 확인했습니다.
- 변경한 코드와 네 몬스터 프리팹이 검증 사본과 동일한지 파일 해시로 확인했습니다.
- 실행 로그 사본: `Logs/EnemyBoss-20261022.log`. 그래픽 없는 검사이며 실제 화면의 애니메이션·렌더링 육안 검증은 포함하지 않습니다.
