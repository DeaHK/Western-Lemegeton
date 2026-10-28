# HauntedBull

엘리트 몬스터이며 `HauntedBullAttack`을 기존 Enemy/체력/행동 제어/추적 이동 부품과 조합합니다. 프리팹은 `Assets/Editable/Actors/HauntedBull.prefab`입니다.

## 두 종류의 상태

- `State`: Wild(미탑승) 또는 Ride(Cowboy 탑승).
- `Action`: Chase(추적), Stomp(발 구르며 조준), Lock(마지막 준비), Charge(돌진), Stunned(기절).

탑승 여부와 현재 동작은 별개입니다. 그래서 Wild와 Ride가 같은 조준·돌진 코드를 사용하면서, 마지막 준비 중 추적 여부, 사격 유무와 돌진 종료 조건을 다르게 적용할 수 있습니다.

## 핵심 메서드

- `Tick()`: 현재 행동을 진행합니다. 발 구르기 3초 동안 Hunter를 따라 방향을 바꾸고, 이후 1초는 Wild만 방향을 고정합니다. Ride는 마지막 1초에도 Hunter를 따라 조준하고 돌진 시작 시 방향을 고정합니다.
- `Blocked()`: 방 경계와 장애물을 검사합니다. 돌진은 한 프레임의 이동을 0.1 단위 이하로 나눠 검사하므로 빠르게 이동해도 얇은 장애물을 건너뛰지 않습니다.
- `Mount()`: Cowboy의 현재 체력을 RiderHealth에 저장하고 Cowboy를 비활성화한 뒤 Ride로 전환합니다.
- `Crash()`: 뒤로 밀고 Cowboy를 하차시킨 뒤 황소를 3초 기절시킵니다.
- `ReleaseRider()`: 저장한 체력을 되돌리고 Cowboy를 활성화합니다. 사망 이벤트에서도 이 메서드를 사용하지만, 방 이동으로 단순 제거되는 경우에는 호출하지 않습니다.

Wild는 충돌할 때까지 돌진하고 Ride는 RideDistance(기본 8)에 도달하면 정상 정지합니다. Ride에서도 그 전에 벽/장애물에 닿으면 하차 및 기절 처리가 우선합니다. 황소 체력과 저장된 Cowboy 체력은 서로 독립적입니다.

## 애니메이션 연결 작업이 필요함

**3초 동안 발을 구르는 애니메이션은 아직 없습니다.** 현재는 직선 공격 범위만 표시합니다. 기절은 임시 경고 원으로 표현합니다.

추후 클립과 Animator Controller를 준비한 뒤:

1. 프리팹의 외형 오브젝트에 Animator를 구성합니다.
2. Animator에 발 구르기, 돌진, 기절 상태와 전환 파라미터를 만듭니다.
3. `Haunted Bull Attack` Inspector의 `Stomp Started`, `Charge Started`, `Stunned` 이벤트에 해당 Animator 또는 전용 애니메이션 제어 컴포넌트를 연결합니다.
4. 예를 들어 Animator에 Stomp라는 Trigger를 만들었다면 `Stomp Started`에서 `Animator.SetTrigger(string)`을 선택하고 Stomp를 입력합니다. 애니메이션 길이·전환 시간은 코드의 3초/1초/3초 흐름에 맞춰야 합니다.

발 구르기 이벤트 연결만으로 애니메이션 클립이 생성되지는 않습니다. 실제 에셋과 전환 설정이 필요합니다.

공통 테스트/생성 절차는 옆 폴더 `Enemy_Cowboy/README.md`와 `Docs/Monsters.md`를 참고하세요.

## 상태별 공격 범위 (2026-10-28 수정·추가)

`Haunted Bull Attack` Inspector에서 Wild Attack Range와 Ride Attack Range를 각각 설정합니다.

| Inspector 항목 | 기본값 | 역할 |
| --- | --- | --- |
| Wild / Ride Trigger Range | 각각 6 | 해당 상태에서 발 구르기를 시작하는 거리 |
| Wild / Ride Hit Radius | 각각 1.1 | 돌진 중 황소 중심의 접촉 피해 반경. 예고 직사각형의 폭은 이 값의 2배 |
| Wild / Ride Warning Length | 각각 12 | 황소 앞에서 조준 방향으로 표시하는 직사각형의 길이 |

예고 이미지는 사각형 스프라이트를 사용합니다. Wild는 마지막 준비 중 방향을 고정하고 Ride는 계속 추적하며, 준비 중 탑승하면 즉시 Ride 범위로 바뀝니다. 준비 중 Inspector 수치를 바꾸면 다음 Tick에 반영됩니다. 충돌 후 기절 표시는 기존 원형을 유지합니다.

직사각형은 돌진 방향을 미리 보여주는 표시이며, 영역 전체에 동시에 피해를 주지는 않습니다. 피해는 이동 중 접촉 시 적용됩니다. Warning Length는 돌진 종료 거리를 바꾸지 않습니다. Wild는 벽/장애물에 부딪힐 때까지, Ride는 기존 Ride Distance만큼 돌진합니다. 두 상태의 기존 기본 수치와 행동 시간은 유지했습니다.

이전 TriggerRange, HitRadius, WarningLength 저장값은 Wild 항목으로 이관됩니다. Ride 항목은 별도로 설정할 수 있으며 기존 기본값으로 시작합니다.

