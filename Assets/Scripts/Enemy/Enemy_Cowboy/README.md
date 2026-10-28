# Cowboy와 HauntedBull 조립 및 테스트

## 유니티에서 확인

1. Main 씬의 `Game_Systems → Game Scene Bindings → Enemy Prefabs`에서 Element 1은 HauntedBull, Element 2는 Cowboy입니다. Element 0과 보스 슬롯은 그대로 유지했습니다.
2. 두 프리팹은 `Assets/Editable/Actors`에 저장되어 있습니다. 프리팹을 더블클릭하고 맨 위 오브젝트를 선택하면 수치를 수정할 수 있습니다.
3. Cowboy에는 `Enemy`, `EnemyHealth`, `EnemyBasicBrain`, `EnemyKeepDistanceMovement`, `CowboyAttack`이 필요합니다. 같은 오브젝트에 다른 공격 부품을 추가하지 않습니다.
4. HauntedBull에는 `Enemy`, `EnemyHealth`, `EnemyBasicBrain`, `EnemyChaseMovement`, `HauntedBullAttack`이 필요합니다.
5. Play 후 전투에서 Cowboy의 사격 범위 안에 들어갔다가 나옵니다. 머리 위에서 작은 노란 고리가 회전하고 3초 후 올가미가 발사됩니다. 올가미에 잡힌 상태에서 밧줄을 공격하거나 까마귀가 공격하도록 하면 풀립니다.
6. Cowboy보다 Hunter에서 더 가까운 황소가 아니라, **Cowboy에서 더 가까운 Wild 황소**가 있을 때 그 황소 방향으로 올가미를 던집니다. 황소 탑승은 충돌한 대상이 살아 있고 Wild일 때만 가능합니다.
7. 탑승 후 벽에 부딪히거나 황소를 처치하면 Cowboy가 저장된 체력으로 다시 등장합니다. Instantiate로 새 복사본을 만드는 대신 기존 Cowboy 오브젝트를 숨겼다가 다시 활성화해서 개별 설정과 최대 체력을 유지합니다.

## 코드 읽기

- `CowboyAttack.Action`은 현재 행동 상태입니다. 상태별 분기를 통해 사격과 올가미가 동시에 실행되는 것을 막습니다.
- `wasInRange`는 직전 검사에서 Hunter가 사격 범위 안에 있었는지 기억합니다. 안에서 밖으로 바뀌는 순간에만 올가미 준비를 시작합니다.
- `ThrowLasso()`는 던지는 순간 가장 가까운 후보의 위치를 확인합니다. 비행 중에는 자동 추적하지 않으며 실제로 먼저 닿은 Hunter/Wild 황소를 잡습니다.
- **2026-10-28 공통 처리 변경**: 일반 이동은 `EnemyKeepDistanceMovement`가 담당합니다. 올가미는 `ISlowMovementSkill`로 분류하며 준비·비행·회수 중 `LassoMoveMultiplier`를 `EnemyBasicBrain`에서 한 번 곱합니다. 공통 규칙과 조립 설정은 `Docs/EnemyModulesGuide.md`의 슬로우 무브먼트 항목을 참고하세요.
- `CowboyRope`는 노란 선과 여러 개의 작은 `Enemy` 판정을 함께 만듭니다. 이는 선의 중간을 때려도 기존 근접 공격·총알·까마귀 공격이 맞도록 하기 위한 것입니다. 피해를 받으면 모든 판정을 제거하고 플레이어의 구속을 해제합니다.
- `Player.Tether`는 현재 자신을 묶은 밧줄입니다. 묶여 있는 동안 일반 이동/회피 대신 밧줄 방향으로 이동하며, 공격은 계속 사용할 수 있습니다.
- `Enemy.Damaged`와 `Enemy.Dying`은 피해/사망을 다른 부품에 알리는 이벤트입니다. 이벤트는 해당 일이 발생했을 때 연결한 메서드를 호출하는 장치입니다. 기존 몬스터는 연결한 메서드가 없으면 기존처럼 동작합니다.
- `Dungeon.QueueEnemy()`는 하차 Cowboy와 밧줄 판정을 등록할 대기 목록에 넣습니다. 적 목록을 돌면서 공격하는 도중 목록에 항목을 추가하면 오류가 날 수 있어서, 다음 적 목록 정리 시점에 등록하고 나서 전멸 여부를 검사합니다.

## 임시값과 표현

올가미 준비 3초와 하차 기절 3초 외 거리·속도·피해·체력은 임시값입니다. `Docs/Monsters.md`의 표와 Inspector에서 확인하세요. 독립적인 Cowboy 외형, 총쏘기/올가미/탑승/기절 애니메이션은 아직 없으며 원거리형 임시 외형과 노란 올가미 표시를 사용합니다. 밧줄은 한 번의 양수 피해로 끊어지고 별도의 처치 보상은 주지 않습니다.

## 자동 검사

저장된 Main 씬에서 `Western Lemegeton → Tests → Check HauntedBull and Cowboy`를 실행합니다. 새 프리팹이 없다면 함께 생성하며, 이미 존재하는 프리팹은 덮어쓰지 않습니다. 검사에서는 임시 씬을 사용하고 Main 씬 파일을 저장하지 않습니다. 밸런스 값을 바꾸면 검사에 명시한 기본값 기대치도 함께 검토해야 합니다.

프리팹만 다시 만들 필요가 있다면 `Western Lemegeton → Monsters → Create HauntedBull and Cowboy Prefabs`를 사용합니다.

## 올가미 준비 표시 조절

Cowboy 프리팹의 최상위 오브젝트를 선택하고 Cowboy Attack 컴포넌트를 확인합니다. Spin Height는 표시 높이(1.8), Spin Orbit Radius는 머리 위 공전 반지름(0.35), Spin Diameter는 고리 지름(0.45), Spin Turns Per Second는 초당 회전 수(2)입니다. 모두 임시 연출값이며 Lasso Range(실제 사거리 12)와 독립적입니다. 코드가 고리를 자동 생성하므로 별도 프리팹 연결은 필요 없습니다. PaperCard.RenderOffset으로 표시만 공중에 올리고 게임의 공격 위치는 바꾸지 않습니다. 준비가 끝나거나 취소되면 고리를 제거합니다.

