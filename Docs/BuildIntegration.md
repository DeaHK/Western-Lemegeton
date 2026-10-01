# 성흔 · 패시브 · 까마귀 연계 연결 안내

## 성흔

게임 메인 스레드에서 아래 진입점을 호출합니다. 획득 재화나 드롭 규칙을 이 함수 안에 넣지 않습니다.

```csharp
Dungeon.I.ApplySeongheunStack(SeongheunType.Fire, 1);
Dungeon.I.ApplySeongheunStack(SeongheunType.Nature, 2);
Dungeon.I.ApplySeongheunStack(SeongheunType.Butterfly, 1);
// 밸런스/트리 설정에서 임계값을 변경할 수 있습니다.
Dungeon.I.Build.SetThreshold(SeongheunType.Fire, 3);
```

Fire=잿불 지속 피해, Nature=태그 격노, Butterfly=검은 날개입니다. 기본 임계값은 기존 첫 각인 효과를 보존하기 위해 1이며 확정 밸런스가 아닙니다. `Stack`은 보유 수치, `EffectLevel`은 임계값 미만이면 0, 이상이면 보유 수치입니다. 기존 전투 로직도 EffectLevel을 사용합니다.

`StackChanged(type, old, current)` 이벤트가 즉시 발생합니다. HUD는 증가에만 0.25초 확대/흰 테두리 효과를 재생합니다. 회색은 잠금, 청록은 개방되었으나 발동 조건 대기, 금색은 효과 활성입니다. 격노는 태그 버프 지속시간 중에 금색이 됩니다. HUD 우측 아래 아이콘을 클릭하면 현재 수치·임계값·효과의 트리 확인 창이 열리고 게임은 일시정지합니다.

음수 증가는 감소를 의미하며 최소 0, 정수 상한을 넘는 증가는 최대 int 값으로 제한합니다. 감소 및 임계값만 변경할 때는 획득 펀치를 재생하지 않습니다. `SetStack`은 저장 데이터 복원/절대값 동기화용입니다. 새 런은 수치와 획득 UI 상태를 초기화하지만 밸런스 임계값 설정은 유지합니다.

## 패시브

```csharp
Dungeon.I.AcquirePassive("spent_bullet", PassiveRarity.Common,
    PassiveSource.MonsterDrop);
Dungeon.I.AcquirePassive("blue_charm", PassiveRarity.Uncommon,
    PassiveSource.ShopPurchase);
Dungeon.I.AcquirePassive("raven_seal", PassiveRarity.Rare,
    PassiveSource.EventReward);
```

세 ID는 악마카드 방의 선택지입니다. spent_bullet=플레이어 공격력 +8%, blue_charm=이동 속도 +8%, raven_seal=까마귀 공격력 +12%의 임시 효과를 적용합니다. 희귀 효과는 1.5배, 레어는 2배이며 같은 효과는 합산합니다. 가격과 드롭 확률은 별도로 확정하지 않았습니다. 각 경로의 실제 획득 확정 시 한 번만 호출하세요. 상점은 결제 확정 후 호출하는 구조입니다. 알 수 없는 ID/등급/경로는 false를 반환하며 인벤토리와 알림을 바꾸지 않습니다.

패시브 보유 목록은 획득 순서대로 추가되고 동일 ID도 각각 별도 획득 인스턴스로 보관합니다. 일반은 흰색, 희귀는 파란색, 레어는 보라색 임시 테두리입니다. 좌측 상단 목록은 가로 스크롤로 전체 아이템을 확인할 수 있고, 마우스를 올리면 이름·등급·경로가 나타납니다. 새 획득 팝업은 2.4초이며 여러 획득은 대기열로 순서대로 표시합니다. 성흔 수치는 변경하지 않습니다.

새 아이템 등록 예:

```csharp
Dungeon.I.Build.RegisterPassive(new PassiveDefinition(
    "my_item", "새 아이템", "MyItemIcon"));
// Assets/Resources/Combat/MyItemIcon.png 필요
```

`PassiveAcquired` 이벤트를 별도의 아이템 효과 시스템에서 구독할 수 있습니다. 새 런에서는 보유 목록과 알림 대기열을 초기화합니다. 현재 저장/영구 보유는 지원하지 않습니다.

## 까마귀 추가 공격

`Player.Hit(enemy, damage, stage, attackId)`가 실제 적중 시 ComboTracker로 전달됩니다. stage는 1~4, attackId는 한 번의 공격마다 고유해야 합니다. 한 공격의 여러 탄환은 같은 ID를 공유해야 중복 적중을 단계로 오인하지 않습니다. 기본 공격 단계를 입력받았다는 이유만으로 연계를 발생시키지 않습니다.

같은 적에게 4초 이내 순서대로 네 단계를 맞히면 PNG 표식이 생성되고 `ComboWindup` 0.18초 → `ComboDash` 0.24초 → 도착 피해/보라색 궤적/청록 깃털 불꽃 → 다음 대상 또는 Return 순서로 동작합니다. 두 번째 공격도 준비와 돌진을 거칩니다. 최대 두 개의 서로 다른 적 ID만 피해를 받습니다.

죽은 원래 대상은 플레이어 반경 7 안의 가장 가까운 생존 적으로 대체합니다. 돌진 도중 대상이 죽거나 범위 밖으로 나가도 다시 검증합니다. 대체 대상이 없으면 표식만 남기며 새로운 돌진/전용 효과를 만들지 않습니다. 다음 연계 요청은 대기열로 처리하고, R 수동 연계는 콤보 돌진 중 발동하지 않아 둘이 위치를 동시에 덮어쓰지 않습니다. 일시정지는 돌진 타이머도 멈춥니다.

현재 연출은 PNG 자세 변경과 시간 보간 이동입니다. 별도의 프레임 애니메이션/Animator/Spine 파일이 추가된 것은 아닙니다.

## 검사

Windows 실행 인자 `--smoke-test`는 기존 던전 검사를 포함해 임계값 전후 실제 효과, HUD 펀치 종료, 패시브 경로/등급/순서/알림 대기열/런 초기화, 도착 전 무피해, 최대 두 대상, 사망 대상 대체, 무대상 표식 전용 분기, 돌진 일시정지를 확인합니다. `Logs/build-passives.png`, `Logs/crow-dash.png`는 해당 검사 중 실제 화면 캡처입니다.

한 번의 광역 기본 4타가 여러 적의 콤보를 동시에 완성해도, 같은 attackId에서는 추가 연계를 한 번만 발동해 최대 두 대상 제한을 유지합니다.

## 여섯 방 탐험 연결

`RoomGraph.Centers`는 각 방의 실제 월드 좌표, `Links`는 양방향 연결, `Kind`는 방 종류입니다. `ExplorationStage.Create`는 스테이지의 여섯 환경을 모두 생성합니다. `ExpeditionProgress`는 방별 방문·완료·웨이브 상태를 유지합니다. 완료 방을 재방문하면 전투/보상은 다시 생성하지 않습니다.

`Dungeon.TryTravel()`은 E 입력 진입점입니다. 가까운 `RoomDoor`를 우선 확인하고, 서비스 방 중앙에서는 선택 UI를 엽니다. `ClaimSigil`과 `ClaimCard`는 올바른 UI 상태와 미완료 방에서 한 번만 성공합니다. 서비스 UI를 닫아도 보상은 남아 있습니다. `AcquirePassive`는 별도의 드롭/상점/이벤트 시스템에서도 호출 가능하며 방 완료 상태를 임의로 변경하지 않습니다.

`OpenRoute()`는 읽기용 지도이며 `OpenRoute(true)`도 출구 근접·여섯 방 완료 검증을 통과해야 이동을 허용합니다. `ChooseRoom`은 그 상태에서 바로 다음 스테이지만 선택할 수 있습니다. 마지막 스테이지는 `FinishExpedition`을 사용합니다.

월드 좌표가 달라지므로 충돌 사각형, 이동 경계, 카메라 중심 및 깊이 정렬은 현재 방 원점을 기준으로 갱신합니다. 문 이동은 입구로 전환하고 방 안에서는 소프트 팔로우를 유지합니다. 방 배치를 바꿀 때는 연결문과 반대편 도착 지점의 접근성을 함께 확인하세요.
