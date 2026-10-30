# HauntedBull

엘리트 몬스터이며 `HauntedBullAttack`을 기존 Enemy/체력/행동 제어/추적 이동 부품과 조합합니다. 프리팹은 `Assets/Editable/Actors/HauntedBull.prefab`입니다.

## 행동 상태

- `Action`: Chase(추적), Stomp(발 구르며 조준), Lock(마지막 준비), Charge(돌진), Stunned(기절).


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


| Inspector 항목 | 기본값 | 역할 |
| --- | --- | --- |

예고 이미지는 사각형 스프라이트를 사용합니다. Wild는 마지막 준비 중 방향을 고정합니다. 준비 중 Inspector 수치를 바꾸면 다음 Tick에 반영됩니다. 충돌 후 기절 표시는 기존 원형을 유지합니다.

직사각형은 돌진 방향을 미리 보여주는 표시이며, 영역 전체에 동시에 피해를 주지는 않습니다. 피해는 이동 중 접촉 시 적용됩니다. Warning Length는 돌진 종료 거리를 바꾸지 않습니다. Wild는 벽/장애물에 부딪힐 때까지 돌진합니다.




