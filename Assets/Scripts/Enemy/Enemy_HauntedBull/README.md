# HauntedBull

엘리트 몬스터이며 `HauntedBullAttack`을 기존 Enemy/체력/행동 제어/추적 이동 부품과 조합합니다. 프리팹은 `Assets/Editable/Actors/HauntedBull.prefab`입니다.

## 행동 상태

- `Action`: Chase(추적), Stomp(발 구르며 조준), Lock(마지막 준비), Charge(돌진), Stunned(기절).
- `CurrentCharge`: `FullCharge`(풀 차지), `ShortCharge`(숏 차지). 준비 시작 시 결정되며 애니메이션 연결 코드에서도 읽을 수 있습니다.

## 풀 차지와 숏 차지 (2026-11-03)

풀 차지는 기존 3초 조준 → 1초 방향 고정 → 벽까지 돌진을 사용합니다. 종료 시 `Charge Cooldown` 10초가 시작되고 충돌 기절 3초 동안 풀 차지와 숏 차지의 쿨타임이 모두 멈춥니다. `FullChargeCooldownRemaining`으로 남은 시간을 읽을 수 있습니다.

쿨타임 중에는 헌터에게 접근해 숏 차지를 사용합니다. 2026-11-03에 확인한 프리팹 저장값은 시작 거리 2.5, 조준 0.2초, 방향 고정 0.1초, 속도 10, 이동 거리 3, 재사용 대기 1.5초입니다. 확정 밸런스가 아닌 현재 조절값이며 코드 기본값과 다를 수 있습니다. 숏 차지는 정해진 거리를 이동하면 멈추며 벽과 충돌하면 기존 뒤로 밀림·기절을 적용합니다. 피해량은 두 돌진 모두 `Damage` 22를 사용합니다. 숏 차지는 풀 차지의 남은 쿨타임을 초기화하지 않습니다.

Unity에서 `Assets/Editable/Actors/HauntedBull.prefab`을 열고 루트의 **Haunted Bull Attack**을 선택하면 `Charge Cooldown`과 **Short Charge** 항목을 조절할 수 있습니다. **Enemy Chase Movement → Stop Distance**는 1.5로 설정했습니다. 이 값이 `Short Trigger Range`보다 크면 숏 차지 거리까지 접근하지 못할 수 있습니다. 기존 씬 인스턴스에 별도 오버라이드가 있으면 같은 값을 확인하세요. 기본 프리팹에는 적용되어 별도 연결이 필요 없습니다.

`Stomp Started`, `Charge Started` 이벤트는 두 돌진 모두 호출됩니다. 애니메이션을 구분하는 스크립트에서는 `CurrentCharge`를 확인하세요. 숏 차지용 애니메이션도 아직 연결되지 않은 임시 표현입니다.


## 핵심 메서드

- `Tick()`: 현재 행동을 진행합니다. 발 구르기 3초 동안 Hunter를 따라 방향을 바꾸고, 이후 1초는 Wild만 방향을 고정합니다.
- `Blocked()`: 방 경계와 장애물을 검사합니다. 돌진은 한 프레임의 이동을 0.1 단위 이하로 나눠 검사하므로 빠르게 이동해도 얇은 장애물을 건너뛰지 않습니다.
- `Crash()`: 뒤로 밀고 황소를 3초 기절시킵니다.


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


| Inspector 항목 | 현재 프리팹 값 (2026-11-03) | 역할 |
| --- | --- | --- |
| Wild Trigger Range | 6 | 풀 차지 시작 거리 |
| Wild Hit Radius | 1.1 | 접촉 반경, 표시 폭은 2.2 |
| Wild Warning Length | 30 | 풀 차지 예고 길이 |
| Short Trigger Range | 2.5 | 숏 차지 시작 거리 |
| Short Distance | 3 | 숏 차지 이동 거리 및 예고 길이 |

예고 이미지는 사각형 스프라이트를 사용합니다. Wild는 마지막 준비 중 방향을 고정합니다. 준비 중 Inspector 수치를 바꾸면 다음 Tick에 반영됩니다. 충돌 후 기절 표시는 기존 원형을 유지합니다.

직사각형은 돌진 방향을 미리 보여주는 표시이며, 영역 전체에 동시에 피해를 주지는 않습니다. 피해는 이동 중 접촉 시 적용됩니다. 풀 차지의 Warning Length는 돌진 종료 거리를 바꾸지 않습니다. 풀 차지는 벽/장애물까지, 숏 차지는 Short Distance까지만 이동하며 숏 차지 예고 길이도 이 값을 사용합니다.





## 돌진 피격 효과 (2026-11-03)

숏 차지는 헌터를 진행 방향으로 거리 3만큼 0.3초 동안 밀어냅니다. 외형은 최대 높이 0.45까지 아치형으로 떠올랐다 착지하고 그림자는 바닥에 남습니다. 장애물 판정은 이동 중에도 유지됩니다. 풀 차지는 헌터를 함께 밀고 가다가 벽·장애물 충돌 시 놓아주며 1초 기절시킵니다. 헌터도 기절하거나 끌려가는 동안 입력·행동 진행·재사용 대기가 정지합니다. 회피·피격 무적 중에는 붙잡거나 밀어내지 않으며, 돌진당 한 번만 명중합니다. 기존 밧줄은 붙잡힐 때 해제됩니다.

프리팹 루트의 Haunted Bull Attack에서 Short Hit Knockback(현재 3), Short Hit Knockback Duration(0.3초), Short Hit Knockback Height(0.45), Full Hit Stun Seconds(1초)를 조절할 수 있습니다. 별도 컴포넌트 연결은 필요 없습니다. Player의 돌진 피격·붙잡힘 처리와 연동합니다. 황소 사망/비활성화/취소 시 붙잡힘은 추가 기절 없이 해제됩니다. 게임 및 검사 코드 컴파일을 확인했으며 Unity 실행 검사는 아직 수행하지 않았습니다.

최종 수치 확인: 2026-11-03. HauntedBull.prefab 저장값을 기준으로 본문과 표를 동기화했습니다. 씬 인스턴스에서 변경한 값은 별도로 확인해야 합니다.
