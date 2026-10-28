# RobeMan 사용 및 코드 해설

RobeMan은 기존 Enemy, EnemyHealth, EnemyBasicBrain, EnemyChaseMovement를 재사용하고 공격만 전용 RobeManAttack으로 교체한 몬스터입니다. 몬스터 외형은 Enemy_0의 임시 외형입니다. 독병도 작은 초록색 임시 도형이며, 실제 독안개 프리팹 대신 통과 가능한 초록 원을 표시합니다.

## 실행 순서

1. 기존 Dungeon 생성기가 프리팹을 생성하고 Enemy.Init을 호출합니다.
2. EnemyBasicBrain이 RobeManAttack과 EnemyChaseMovement를 찾습니다.
3. 일반 추적 및 독병 사용 중에는 추적 부품이 Hunter를 향해 이동합니다. 근접 공격 중에는 일반 이동을 멈춥니다.
4. Hunter가 반지름 3 안에 연속 5초 있으면 독병 사용 조건을 달성합니다. 정확히 거리 3인 위치도 포함합니다. 5초 전에 나가면 시간은 0으로 초기화됩니다.
5. 근접 공격 중이라면 조건 달성을 기억하고, 타격과 후딜이 끝나자마자 조준합니다. 조건 달성 후 Hunter가 밖으로 나가더라도 예약된 조준은 실행합니다.
6. 조준 시작 순간 Hunter의 좌표를 LockedTarget에 복사합니다. 노란 원은 그 위치에 고정되고 Hunter가 움직여도 따라가지 않습니다. 조준·투척 후딜 중에는 근접 공격을 하지 않고 감속 이동합니다.
7. Aim Delay 후 독병을 던집니다. 병의 논리 위치는 목표까지 이동하고, 화면 표시 높이에 포물선을 적용하여 아치형으로 보이게 합니다. 장애물에 부딪혀 중간에 멈추는 투사체가 아닙니다.
8. 병이 도착하면 병과 노란 표시가 사라지고 초록 독 위험구역이 생성됩니다.
9. Hunter가 초록 원 안에 연속 1초 있으면 피해를 받으며 이후 1초마다 반복됩니다. 나가면 노출 시간이 초기화됩니다. 독은 보호막을 적용하지만 일반 피격/회피의 무적 시간은 적용하지 않습니다. 여러 독 구역이 겹치면 각 구역의 피해가 적용됩니다.
10. 독은 지정 시간 뒤 사라집니다. 이미 던진 병과 생성된 독은 RobeMan 사망 후에도 남으며, 방 이동/게임 초기화 시 기존 Dungeon.ClearTransient에서 정리됩니다. 일시 정지와 컷신에서는 시간이 멈춥니다.

## 파일별 역할

**2026-10-28 공통 처리 변경**: 독병은 `ISlowMovementSkill` 분류를 사용합니다. 조준·투척 후딜 중 `Poison Move Multiplier`(초기값 0.35)를 `EnemyBasicBrain`에서 이동 배율에 곱합니다. 독병 비행이나 독 구역의 수명은 감속을 연장하지 않습니다. 공통 규칙은 `Docs/EnemyModulesGuide.md`의 슬로우 무브먼트 항목을 참고하세요.

- RobeManAttack.cs: 근접 공격, 5초 체류 판정, 고정 조준, 투척 순서를 결정합니다. EnemyAttackModule을 상속하므로 기존 Brain이 일반 공격 부품처럼 사용할 수 있습니다. 상속은 기존 부품의 사용 규칙을 따르면서 전용 동작을 작성하는 방식입니다.
- RobeManPoisonBottle.cs: 목표까지의 비행 시간과 시각적인 포물선을 계산하고 착지 시 독 구역을 생성합니다.
- RobeManPoisonZone.cs: 원 표시, 지속 시간, 범위 내 주기적 피해를 처리합니다. Collider를 추가하지 않아서 이동을 막지 않습니다.
- Editor/RobeManSetup.cs: 기존 외형과 참조를 복사하여 RobeMan 프리팹을 만드는 편집기 도구입니다. 기존 RobeMan 프리팹은 덮어쓰지 않습니다.
- Editor/RobeManChecks.cs: 실제 공격 부품에 시간과 거리를 입력하여 동작을 검사하는 편집기 테스트입니다.

RobeManAttack의 Phase는 현재 행동 상태를 나타내는 이름입니다. Chase → MeleeWindup → MeleeRecovery 또는 Chase → Aim → ThrowRecovery 순으로 변경됩니다. Tick에서 true를 반환하면 Brain은 일반 이동을 생략하고, false이면 추적 이동을 실행합니다.

## 임시 기본값 (Inspector에서 변경 가능)

| 항목 | 기본값 | 의미 |
| --- | --- | --- |
| Detection Radius | 3 | 독병 사용을 위한 체류 감지 반지름 |
| Required Stay Seconds | 5초 | 필요한 연속 체류 시간 |
| Aim Delay | 1초 | 위치 고정 후 발사까지의 시간 |
| Flight Seconds | 0.8초 | 독병 비행 시간 |
| Arc Height | 3 | 비행 중간의 최대 표시 높이 |
| Throw Recovery | 0.4초 | 발사 후 일반 행동을 재개하기까지의 시간 |
| Poison Radius | 4 | 독 위험구역 반지름, 지름은 8 |
| Poison Lifetime | 6초 | 독 구역 유지 시간 |
| Poison Damage / Interval | 5 / 1초 | 주기 피해량과 주기 |
| Melee Range / Hit Range | 1.7 / 1.75 | 근접 공격 시작 거리 / 타격 순간 판정 거리 |
| Melee Damage | 15 | 근접 피해 |
| Melee Windup / Recovery | 0.65 / 0.25초 | 근접 준비 / 타격 후 동작 시간 |
| Melee Cooldown | 1.5초 | 근접 동작 종료 후 다음 근접 공격까지 대기 |

거리 3, 연속 5초, 독 피해 간격 1초를 제외한 수치는 임시값입니다. 독 반지름 4는 앞선 대화의 예시를 사용했습니다. 체력과 이동 속도는 공통 컴포넌트에서 설정합니다.

## 유니티 조작

1. 코드 컴파일을 기다립니다. `Assets/Editable/Actors/RobeMan.prefab`을 더블클릭합니다. 아직 없으면 메뉴 `Western Lemegeton > Monsters > Create RobeMan Prefab`을 실행합니다.
2. 맨 위 RobeMan 오브젝트를 선택합니다. Enemy, EnemyHealth, EnemyBasicBrain, EnemyChaseMovement, RobeManAttack이 붙어 있어야 합니다. 다른 공격 컴포넌트와 RobeManAttack을 동시에 붙이지 않습니다.
3. RobeManAttack에서 위 표의 값을 설정합니다. 독안개 프리팹이나 Rigidbody를 따로 연결할 필요가 없습니다.
4. Main 씬을 열고 Hierarchy에서 Game_Systems를 선택합니다. Game Scene Bindings 컴포넌트의 Enemy Prefabs 목록에서 Element 0에 RobeMan 프리팹을 연결하면 기존 근접 몬스터 생성 자리에 RobeMan이 나옵니다. 현재 생성기가 0~3번 종류를 사용하므로 목록 끝에 추가하는 것만으로는 등장하지 않습니다.
5. Play 후 전투에 진입합니다. 반지름 3 안에 5초 머문 뒤 노란 표시에서 벗어나면, 병이 이전 위치로 날아가는지 확인할 수 있습니다. 초록 원에 들어가 1초마다 체력이 줄어드는지도 확인합니다.
6. Play 중 변경한 수치는 종료 시 되돌아갑니다. 유지할 값은 Play를 끄고 프리팹에 저장하세요. 원래 몬스터로 되돌리려면 Enemy Prefabs의 Element 0을 Enemy_0으로 복원합니다.

이번 작업은 Main 씬의 생성 목록을 자동 변경하지 않습니다. 프리팹을 Hierarchy에 끌어놓기만 하면 기존 Dungeon의 몬스터 등록/초기화 과정을 거치지 않으므로 위 생성 목록을 이용하세요.

## 검증

메뉴 `Western Lemegeton > Tests > Check RobeMan`으로 편집기 동작 검사를 실행합니다. 저장된 씬에서 실행하세요. 검사는 임시 씬을 만들고 끝나면 정리하며 Main 씬을 저장하지 않습니다. 화면상 포물선과 색상은 실제 Play에서 별도 확인해야 합니다.
