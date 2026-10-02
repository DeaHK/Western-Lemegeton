# RunBuild Block Refactor

- 작업일: 2026-10-03 (Asia/Seoul).
- 대상: `E:\GitHub\Western-Lemegeton\Assets\Scripts\RunBuild.cs`.
- 시작 HEAD: `c218280dbb80d83b03e17b05ad92f8bd50df86a3`.
- 기준: 작업 시작 시 Assets의 실제 RunBuild. 같은 파일의 enum/데이터 타입/RunBuild를 유지했다.
- 참고: `Docs/WESTERN_LEMEGETON_PROJECT_ANALYSIS.md`, `Docs/PLAYER_BLOCK_REFACTOR.md`, `Docs/DUNGEON_BLOCK_REFACTOR.md`, `Docs/ENEMY_BLOCK_REFACTOR.md`, `Docs/GAME_UI_VIEW_BLOCK_REFACTOR.md`의 관련 계약.
- [확인] 내부 배치/주석/공백 정리 및 private method 추출만 수행했다. 다른 C#·Scene·Prefab·Material·Animation·ProjectSettings·기존 문서 수정 없음. 이전 리팩터링과 기존 ShaderGraphSettings.asset 변경을 보존했다.
- [확인] 새 Manager/클래스/interface/namespace/assembly definition/SO/loader/effect registry/LINQ/reflection 구현 없음. Unity Editor/Play Mode/빌드/Smoke 및 commit/push 실행 없음.

## 1. 변경 전 RunBuild 책임

RunBuild 한 MonoBehaviour에 Passive definition Dictionary, 획득 inventory, 등록/획득 검증, bonus 합산과 Passive 이벤트, 성흔 stack/threshold/query/mutation과 성흔 이벤트, 런 Reset이 함께 들어 있었다. 같은 파일에는 enum 3개와 일반 C# 데이터 타입 2개가 있다.

기존 함수들은 이미 짧게 구분돼 있었다. 성흔 query/mutation, Bonus, ResetRun은 함수명을 유지하고 region/서식을 정리했다. 기본 catalog 등록, 정의 검증, append+획득 event만 새 private 함수로 추출했다. Manager 이식이나 데이터화는 수행하지 않았다.

## 2. 생성한 책임 블럭

region은 총 13개다. 첫 region은 namespace 안의 기존 enum/데이터 타입을 묶으며 나머지는 RunBuild 내부다. 기존 타입의 선언 순서와 필드 초기화 순서는 유지했다.

| 블럭 | 구성 |
|---|---|
| Enums / Passive Data Types | SeongheunType, PassiveRarity, PassiveSource, PassiveDefinition, OwnedPassive |
| Passive Definition Catalog | 기존 readonly catalog |
| Passive Runtime Inventory | 기존 readonly inventory, Passives |
| Stigma Runtime State | 기존 readonly stacks/thresholds |
| Events | StackChanged, PassiveAcquired, Reset |
| Initialization / Catalog Registration | 기존 Awake, RegisterDefaultPassives |
| Passive Lookup / Validation | 기존 RegisterPassive, ValidatePassiveDefinition; 획득 lookup의 조건/return은 AcquirePassive에 유지 |
| Passive Acquisition | 기존 AcquirePassive, RecordPassiveAcquisition |
| Passive Bonus Calculation | 기존 Bonus, PlayerDamageBonus/MoveSpeedBonus/RavenDamageBonus |
| Stigma Query | 기존 Stack/Threshold/IsActive/EffectLevel |
| Stigma Mutation | 기존 SetThreshold/ApplySeongheunStack/SetStack |
| Run Reset | 기존 ResetRun |
| Utility / Validation | 기존 Index |

추출한 private 메서드:

| 함수 | 원래 위치 | 책임 |
|---|---|---|
| `RegisterDefaultPassives()` | Awake 본문 | 기존 3개 definition을 같은 순서로 RegisterPassive |
| `ValidatePassiveDefinition(PassiveDefinition definition)` (private static) | RegisterPassive의 if/throw | 기존 정의/ID/이름/icon 검증 |
| `RecordPassiveAcquisition(OwnedPassive item)` | AcquirePassive의 append/event | inventory.Add 다음 PassiveAcquired 발행 |

기존 private Bonus/Index는 새로 만든 함수가 아니다. 계산별 wrapper나 별도 Stigma helper를 늘리지 않았다.

## 3. Passive Catalog

`Awake → RegisterDefaultPassives → RegisterPassive` 순서다. 기존 Awake 시점에 spent_bullet → blue_charm → raven_seal 순으로 등록하며, 중간 검증이 예외를 던지면 이후 등록이 진행되지 않는 동작도 유지했다.

`RegisterPassive`는 ValidatePassiveDefinition을 호출한 뒤 `catalog[definition.Id]=definition`을 실행한다. 검증의 단락 평가 순서는 다음과 같다:

```text
definition==null
→ Id의 IsNullOrWhiteSpace
→ Name의 IsNullOrWhiteSpace
→ !CombatArt.Texture(definition.Icon)
실패: ArgumentException("Passive requires an id, name and valid Combat icon")
성공: catalog[id]=definition
```

같은 ID 등록은 기존처럼 덮어쓴다. 새 ID 중복 거부, catalog clear, icon 조회 방식/검증 변경, trim/대소문자 정규화는 없다. 이미 OwnedPassive가 보유한 Definition 객체를 다시 연결하는 처리도 추가하지 않았다.

## 4. Passive Inventory / Acquisition

`AcquirePassive`의 실제 검증 순서는 ID null → catalog.TryGetValue → PassiveRarity Enum.IsDefined → PassiveSource Enum.IsDefined다. 실패는 false이며 inventory/event 변경이 없다. 기존 enum 검사 API는 그대로 사용했다.

성공 흐름:

```text
new OwnedPassive(definition,rarity,source)
→ RecordPassiveAcquisition(item)
    inventory.Add(item)
    → PassiveAcquired?.Invoke(item)
→ return true
```

같은 ID를 여러 번 획득할 수 있고 매번 별도 OwnedPassive를 append한다. Definition 참조/rarity/source 저장, 획득 순서, event 인자와 시점은 동일하다. subscriber 예외를 catch하거나 append를 rollback하는 코드도 추가하지 않았다.

`Passives`는 기존 `IReadOnlyList<OwnedPassive>`와 매 getter의 `inventory.AsReadOnly()`를 유지한다. wrapper cache, 배열 복사, 다른 반환 컬렉션, 정렬/dedup/pooling은 없다.

## 5. Passive Bonus Calculation

기존 `Bonus(string id,float baseValue)`는 value=0부터 inventory 저장 순서대로 순회하고, ID가 같을 때 다음 float 연산을 더한다:

```text
value += baseValue * (1 + .5f * (int)item.Rarity)
```

| 프로퍼티 | ID | baseValue | Common / Uncommon / Rare의 배수 |
|---|---|---|---|
| PlayerDamageBonus | spent_bullet | .08f | 1 / 1.5 / 2 |
| MoveSpeedBonus | blue_charm | .08f | 1 / 1.5 / 2 |
| RavenDamageBonus | raven_seal | .12f | 1 / 1.5 / 2 |

중복 획득은 각각 합산한다. 순회/곱셈/덧셈 순서, float literal 및 프로퍼티 반환식이 동일하다. bonus cache나 event에서 효과를 적용하는 방식으로 바꾸지 않았다. PassiveDefinition에 base bonus 필드를 추가하지 않았으며, 현재 base 값은 이 세 프로퍼티의 호출 인자다.

## 6. Stigma Stack / Threshold / Effect Level

- enum은 Fire=0/Nature=1/Butterfly=2다. stacks는 int[3] 기본0, thresholds는 int[3] {1,1,1}이다.
- `Index`는 enum을 int로 변환하고 n<0 또는 n>=3이면 ArgumentOutOfRangeException(nameof(type))을 던진다.
- `Stack`/`Threshold`는 기존 Index로 조회한다. `IsActive`는 Stack>=Threshold, `EffectLevel`은 active일 때 Stack, 아니면0이다. 조회 횟수/표현식을 변경하지 않았다.
- `SetThreshold`: threshold<1 검사/예외 → Index → thresholds 대입 → StackChanged(type,stacks[i],stacks[i]). 같은 threshold도 발행한다. threshold 검사와 enum 검사 우선순위도 그대로다.
- `ApplySeongheunStack`: Index → 기존 stack+increment를 long으로 계산 → 0~int.MaxValue 제한 → int 변환 → 기존 SetStack. overflow를 int 덧셈으로 바꾸지 않았다.
- `SetStack`: Index 및 old 캡처 → Math.Max(0,value) 대입 → old!=새 stack일 때만 StackChanged(type,old,new).

GameUI의 threshold+2 노드는 UI 표시 계약으로 유지되며 RunBuild에 독립 노드 ownership/skill tree를 추가하지 않았다. Dungeon의 BurnLevel/FuryLevel/RavenLevel query/set 위임을 통해 읽는 결과가 동일하다.

## 7. Event 계약

| event 선언 | 발행 조건/순서 | 기존 소비자 |
|---|---|---|
| `public event Action<SeongheunType,int,int> StackChanged` | SetStack의 대입 후 값이 달라졌을 때; SetThreshold의 대입 후 항상 같은 old/new stack | GameHUD의 StackChanged; after>before일 때만 pulse |
| `public event Action<OwnedPassive> PassiveAcquired` | 검증/생성/inventory.Add 이후 같은 item을 동기 Invoke | GameHUD.Acquired의 FIFO notices.Enqueue |
| `public event Action Reset` | stacks clear 다음 inventory clear 후, ResetRun당 Invoke 한 번 | GameHUD.ResetBuild의 pulse/queue/Toast/tree 상태 초기화 |

event 타입/이름/선언 순서, null 조건부 Invoke, subscriber 호출 방식은 같다. RegisterPassive/Bonus 조회에 event를 추가하지 않았으며 Reset 시 속성별 StackChanged를 발행하지 않는다. GameHUD 구독/해제 코드와 UI polling은 수정하지 않았다.

## 8. Reset 계약

기존 `ResetRun`은 다음 순서를 그대로 사용한다:

```text
Array.Clear(stacks,0,3)
→ inventory.Clear()
→ Reset?.Invoke()
```

catalog와 thresholds를 유지한다. 빈 런에도 Reset 호출은 같은 방식으로 한 번 발행한다. inventory가 비워진 뒤 다음 bonus query에서0이며 Reset subscriber도 clear 후 상태를 읽는다. 새 threshold 초기화/등록 재실행/저장·불러오기/StackChanged 반복 발행은 없다.

## 9. 유지된 public API

| 종류 | 유지 항목 |
|---|---|
| enum | SeongheunType / PassiveRarity / PassiveSource: 선언 순서·멤버 순서·암시적 0/1/2 값 동일 |
| PassiveDefinition | public sealed class; get-only string Id/Name/Icon; public constructor(string id,string name,string icon) |
| OwnedPassive | public sealed class; get-only PassiveDefinition Definition/PassiveRarity Rarity/PassiveSource Source; public constructor(definition,rarity,source) |
| RunBuild | public class RunBuild:MonoBehaviour; 새 namespace/상속/접근 제한자 변경 없음 |
| 컬렉션/bonus 프로퍼티 | Passives(IReadOnlyList<OwnedPassive>), PlayerDamageBonus/MoveSpeedBonus/RavenDamageBonus(float) |
| public 메서드 10개 | RegisterPassive, AcquirePassive, Stack, Threshold, IsActive, EffectLevel, SetStack, SetThreshold, ApplySeongheunStack, ResetRun |
| event 3개 | StackChanged, PassiveAcquired, Reset (7절 선언 유지) |

원래 public 필드나 명시적 SerializeField는 없다. 기존 private readonly 필드 4개(stacks/thresholds/catalog/inventory)의 이름/타입/초기식/초기화 순서를 유지했다. `RunBuild.cs.meta` GUID `325245e0ce5558449a6d45bb4a4dd43c`와 Scene 연결도 변경하지 않았다.

직접 호출 계약 대조:

- Dungeon: Awake GetComponent<RunBuild>, ResetRun, ClaimCard의 3개 ID/rarity/source→AcquirePassive, ClaimSigil→ApplySeongheunStack, 성흔 level의 EffectLevel/SetStack 위임 유지.
- Player: PlayerDamageBonus를 DamageScale에, MoveSpeedBonus를 기존 이동식에 사용. 함수/프로퍼티 이름과 반환형 동일.
- Raven: 기존 RavenDamageBonus 및 Dungeon의 RavenLevel/BurnLevel 간접 성흔 query를 바꾸지 않았다.
- GameHUD: 3개 Action event를 기존 signature로 구독/해제한다. pulse/queue/reset callback 호출 계약 동일.
- GameUIView: Passives, Stack, IsActive, Threshold, bonus 3개를 기존 polling으로 읽는다. 반환 컬렉션 및 get-only 데이터 타입 계약 동일.

정적 검증:

- [확인] enum 3개와 기존 타입의 선언 순서/계약 동일. 데이터 타입 생성자 2개와 프로퍼티 6개 동일.
- [확인] RunBuild 필드 4개, 프로퍼티 4개, event 3개 선언 및 기존 메서드 13개 signature 동일. private/static 여부도 유지했다.
- [확인] 추출 helper를 원래 문장으로 펼쳐 기존 13개 메서드의 C# 토큰을 비교했다. 검증/단락 평가/조건/예외/반환/등록/append/float 계산/event/Reset 순서 모두 일치했다.
- [확인] 새 helper 3개는 void이며 원래 인자를 그대로 전달한다. 정의/OwnedPassive 타입, static 검증 호출, event Invoke 접근, 기존 Index/long/int/return 흐름을 호출부와 대조했다.
- [확인] C# 구문 파싱 오류 없음. Unity 컴파일/실행/빌드를 수행한 결과는 아니다. 검증 도구는 프로젝트 밖 작업 폴더에만 있다.

## 10. 기존 하드코딩 콘텐츠

| 등록 순서 | ID | 표시 이름 | Icon ID | bonus |
|---|---|---|---|---|
| 1 | spent_bullet | 악마카드 · 탄환의 계약 | BulletIcon | PlayerDamage .08f |
| 2 | blue_charm | 악마카드 · 방랑자의 계약 | WaterIcon | MoveSpeed .08f |
| 3 | raven_seal | 악마카드 · 검은 날개의 계약 | RavenPortrait | RavenDamage .12f |

enum은 SeongheunType(Fire,Nature,Butterfly), PassiveRarity(Common,Uncommon,Rare), PassiveSource(MonsterDrop,ShopPurchase,EventReward) 순서 그대로다. 성흔 3종/threshold1/최소0/Apply 증가의 int.MaxValue 상한, rarity .5f 배수는 기존 값이다.

등록 가능한 추가 ID에 자동 효과 mapping을 만들지 않았다. 현재 bonus 세 프로퍼티가 지정한 ID만 해당 합산을 받는 기존 구조다. 새 Passive/효과/Drop/Shop/Reward를 구현하지 않았다.

## 11. 향후 PassiveManager 이전 후보

후보는 catalog/RegisterDefaultPassives/RegisterPassive/ValidatePassiveDefinition, inventory/Passives/AcquirePassive/RecordPassiveAcquisition, 기존 Bonus/bonus 프로퍼티와 PassiveAcquired다. 이번에 이전하지 않았다.

후속 분리 시 catalog 등록 Awake 시점/아이콘 검증/덮어쓰기, Ordered inventory와 중복 합산, readonly wrapper, append 후 event 및 GameHUD 구독, Player/Raven/UI의 query 계약을 함께 보존해야 한다. PassiveDefinition/OwnedPassive의 property/constructor 계약도 후보 경계다. 새 EffectRegistry/RuntimeContext/SO/loader/interface를 미리 정의하지 않았다.

## 12. 향후 Stigma 분리 후보

후보는 stacks/thresholds, Index, Stack/Threshold/IsActive/EffectLevel, SetThreshold/SetStack/ApplySeongheunStack, StackChanged다. 이번에 분리하지 않았다.

enum/index0~2, threshold의 Reset 후 유지, SetThreshold의 같은 old/new 통지, SetStack의 값 변경 시 통지, long 기반 증가 clamp, Dungeon level 위임과 GameHUD/UI의 읽기 계약이 절단선이다. ResetRun은 Passive와 성흔의 clear 순서 및 공통 Reset event를 연결하므로 단순 독립 파일 이동 대상으로 처리하지 않는다. StigmaManager/새 skill tree/새 런 초기화 lifecycle은 구현하지 않았다.

## 13. 사용자 Unity 확인 항목

[사용자 Unity 테스트 필요] 실행하지 않았다. 정적 검증에서 누락된 타입/namespace/메서드/return, 변경된 event/signature/index 또는 구문 오류는 확인되지 않았다. 실제 Unity 컴파일/리소스/UI 동작은 아래 확인 범위다.

| 확인 방법 | 정상 결과 | 실패 시 증상 |
|---|---|---|
| Unity에서 변경 파일 import/컴파일 후 기존 카드 획득 | 신규 컴파일 오류/등록 예외 없음; 기존 3종 icon/이름, 슬롯·toast, 해당 bonus 유지 | 타입/signature 오류, icon 검증 예외, 획득 누락/이벤트 중복 |
| 기존 성흔 제단에서 획득하고 build UI 확인 | 같은 stack/active/threshold/detail/pulse 및 기존 전투 효과 | stack/활성 수치 차이, pulse 누락, 임계값 표시 차이 |
| 패시브/성흔 보유 후 기존 Town 복귀/새 런 시작 | stack/inventory/bonus 초기화와 UI queue/slot 정리; catalog 유지 | 이전 bonus/슬롯/toast 잔존, 재획득 실패 |

중복 획득/희귀도별 합산, 실패 검증, SetThreshold event 및 threshold 유지 계약은 원래 코드와 정적 비교했다. 추가 테스트용 UI/코드/자산을 만들거나 저장된 Scene/Prefab을 바꾸지는 않았다.
