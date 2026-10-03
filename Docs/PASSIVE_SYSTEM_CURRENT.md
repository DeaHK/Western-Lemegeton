# Western-Lemegeton — Passive System Current

## 1. 목적

2026-10-03의 실제 Assets 코드, Main Scene 연결, 생성된 Database를 기준으로 현재 실사용 패시브 제작·획득·효과·UI 계약을 기록한다. HEAD는 `8208001d6dd271e099d488f40780411c680f5e45`이며 이후 변경이 미커밋 작업 트리에 있다. 전체 프로젝트 상태는 [Current AI Handoff](AI_HANDOFF_CURRENT.md)를 먼저 읽는다.

[확인] 신규 패시브가 현재 카드방·Flat 스탯·UI의 주 시스템이다. RunBuild의 구 패시브 구현과 Dungeon wrapper/CardRarity property는 제거되었다. `[사용자 확인]` Import/Verify와 실사용 전환 동작 성공이 보고되었다. 이 문서 정리는 읽기 전용 대조이며 Unity/메뉴/Play Mode/Build를 실행하지 않았다.

## 2. 폴더 구조

```text
Assets/Scripts/Passives/
  Runtime/
    Data/    PassiveEnums, PassiveSO, PassiveDatabaseSO
    Core/    PassiveInstance, PassiveManager, PassiveAcquisition,
             PassiveOfferService, PassiveRuntimeContext, PassiveEffectContext
    Effects/ PassiveEffectAttribute, PassiveEffectScript, PassiveEffectRegistrySO
    Stats/   StatModifierContainer, WesternPassiveStatKeys
  Editor/    SimpleXlsxReader, PassiveExcelImporter,
             PassiveEffectRegistryGenerator, WesternPassiveDataGeneration
  Generated/ link.xml (수동 generation 결과)
Assets/Data/Passive/패시브.xlsx
Assets/Resources/Passive/
  Passive_001.asset ... Passive_018.asset
  PassiveDatabase.asset
  PassiveEffectRegistry.asset
```

Runtime namespace: `WesternLemegeton.Passives`; Editor namespace: `WesternLemegeton.Passives.Editor`. Editor 코드는 Editor 폴더와 UNITY_EDITOR guard로 격리된다. Runtime에 UnityEditor 의존성을 넣지 않는다. 새 asmdef는 없다. donor의 AstraPassiveStatKeys/DataGeneration은 Western 이름으로 전환되었다.

## 3. Excel 계약

단일 상수 기준은 `WesternPassiveDataGeneration`이다.

| 항목 | 값 |
|---|---|
| WorkbookAssetPath | `Assets/Data/Passive/패시브.xlsx` |
| SheetName | `Passive` |
| OutputFolder | `Assets/Resources/Passive` |
| DatabaseAssetPath | `Assets/Resources/Passive/PassiveDatabase.asset` |
| RegistryAssetPath | `Assets/Resources/Passive/PassiveEffectRegistry.asset` |

필수 Header 10개: `PassiveID`, `PassiveIconResource`, `NameStringKey`, `DescriptionStringKey`, `Rarity`, `StatType`, `Category`, `Value`, `ValueType`, `ScriptName`.

Western Import는 `SimpleXlsxReader.ReadRequiredSheet`로 정확한 Passive sheet를 요구한다. 없으면 Error이며 다른 첫 sheet로 fallback하지 않는다. 기존 ReadSheet API의 fallback은 호환용으로 남는다. ID 조건은 **PassiveID > 0**이며 1000 이상 필터는 제거되었다.

NameStringKey/DescriptionStringKey의 실제 셀은 현재 한국어 표시 원문이다. 필드명/header를 유지하며 가짜 localization key로 변환하지 않는다. 향후 Localization 이관 대상이다. workbook은 원본 `패시브 (1).xlsx`의 무수정 복사이며 데이터 자동 교정 경로가 없다.

현재 생성 데이터: ID 1~18, 중복 없음, Flat11 / Ability6 / Shift1, Common10 / Uncommon4 / Rare3 / Legendary1. 18개 아이콘 경로는 비어 있다. 원본의 다음 주의점을 유지한다.

- ID4: 설명의 이동 5와 Value10 차이. ID7: 설명의 이동 3과 Value5 차이. warning으로 보고하며 값/Raw 단위를 바꾸지 않는다.
- ID1: Value15, ValueType Percent를 원본 그대로 사용한다.
- ID11~18: Name_11~Name_18 placeholder 이름. 새 이름을 생성하지 않는다.
- 18개 icon resource 빈 값 및 7개 Ability/Shift Effect 미구현. 데이터 생성 성공과 runtime-ready는 별개다.

## 4. 데이터 생성 흐름

```text
Excel → ParseAndValidate / Validate Only
      → Error 0 확인 → ApplyImport
      → Output/Database 준비 → Registry Rebuild 성공 확인
      → PassiveSO 반영 → Database 목록/Registry 연결 → SaveAssets
      → Verify Generated Data
```

`ParseAndValidate`는 모든 행 DTO, header/값, ID 중복, icon resolve, 기존 asset 중복/orphan, Effect key/type를 먼저 검사한다. `PassiveImportValidationResult`에 Rows/Errors/Warnings와 Category/Rarity 분포를 수집한다. Error가 하나라도 있으면 ApplyImport에 진입하지 않는다. Validate Only와 Import는 같은 규칙을 사용한다.

Validate Only는 폴더/SO/DB/Registry/link.xml 생성·수정, Rebuild, SaveAssets, Refresh를 하지 않는다. Verify도 생성 데이터 조회/대조 경로다. 성공 로그는 각각 `WESTERN_PASSIVE_VALIDATION`과 `WESTERN_PASSIVE_DATA_VERIFIED`다.

| Error: Import 차단 | Warning: 데이터 보존/Import 가능 |
|---|---|
| workbook/sheet/header 문제, 양수가 아닌 ID/Excel 중복 ID | 빈 설명, Name_숫자 placeholder |
| undefined enum, 잘못된/비유한 Value, 빈 이름 | 빈 icon resource와 missing Sprite |
| Flat Stat/Value/Raw·Percent 누락 또는 unknown key | known Stat이 아직 gameplay 미연결 |
| Ability/Shift ScriptName 누락, 제공한 unknown Stat | Ability/Shift effect 미등록, Flat에 ScriptName 존재 |
| nonblank icon 경로 resolve 실패 | 설명/Value 차이, Excel에 없는 기존 SO orphan |
| 기존 SO의 동일 ID 2개 이상, Registry 빈/중복 key·잘못된 type | 현재 7개 구체 효과 미구현 |

Flat은 known StatType, Value, Raw 또는 Percent가 필수다. Ability/Shift는 Value/StatType이 비거나 ValueType=None이어도 정상이며 ScriptName은 필수다. 미등록 효과는 “Imported as data, but not runtime-ready until a PassiveEffect is registered.” 경고다.

아이콘 정책: 빈 셀+기존 SO는 기존 resource/Sprite 보존, 빈 셀+신규 SO는 빈 resource/null Sprite 허용 및 경고, 정상 nonblank 경로는 resource/Sprite 함께 교체, 탐색 실패는 전체 Import Error다. 잘못된 새 문자열에 예전 Sprite를 붙여 저장하지 않는다.

같은 ID의 기존 SO가 여러 개면 조용히 하나를 고르지 않는다. Excel에 없는 SO는 orphan warning이며 파일을 삭제하지 않는다. 성공 Import의 DB 목록은 현재 Excel 행의 ID 정렬 목록으로 교체한다.

Registry의 자동 InitializeOnLoad/static delayCall/RebuildIfNeeded 경로는 없다. 수동 Rebuild/Import만 effect prototype, Registry, `Effects/_Generated` 및 link.xml을 생성/갱신할 수 있다. generator의 orphan generated-effect 정리는 PassiveSO orphan 파일 보존과 다른 정책이다. **ApplyImport는 Unity I/O 오류까지 복구하는 완전한 transaction이 아니다.** Rebuild 실패 시 준비된 폴더/DB/일부 Registry 파일은 남을 수 있으나 이후 SO 행 적용은 시작하지 않으며 실패를 오류로 보고한다.

Verify는 유효 Excel 행 수/DB 수, 각 ID의 이름·설명·희귀도·Category·StatType·HasValue·Value·ValueType·ScriptName 및 필요한 icon/Registry 읽기를 대조한다. 빈 icon 경로와 unimplemented Ability/Shift는 실패로 취급하지 않는다.

## 5. PassiveSO 구조

정의 API: PassiveID, PassiveIconResource, Icon, NameStringKey, DescriptionStringKey, Rarity, StatType, Category, Value, HasValue, ValueType, ScriptName, ApplyImportedData. serialized 이름/타입과 asset GUID 계약을 유지한다.

SO는 콘텐츠 정의이며 런의 Stack/Level/Source 저장소가 아니다. `PassiveDatabaseSO`는 passives 목록/effectRegistry/ID cache, TryGetById/GetById, SetImportedPassives/SetEffectRegistry를 제공한다. 현재 DB18은 null 없는 ID1~18이며 EffectRegistry가 연결되어 있다. Registry entries=0은 현재 구체 효과 없음에 대응한다.

희귀도는 SO 고정값: **Common=0, Uncommon=1, Rare=2, Legendary=3**. 순서/숫자를 바꾸거나 획득 시 override하지 않는다. Flat=0/Ability=1/Shift=2, ValueType None=0/Raw=1/Percent=2 계약도 유지한다.

## 6. PassiveManager 구조

Main.Game_Systems의 Manager는 같은 GameObject의 RuntimeContext를 사용하며, Context.defaultFlatStatTarget은 같은 GameObject의 StatModifierContainer다. database는 PassiveDatabase.asset, effectRegistryOverride=null, startingPassives=0, logChanges=false다. Override가 없으면 Database.EffectRegistry를 사용한다.

`activePassives`는 하나의 ID당 하나의 Instance를 보관한다. `activeOrder`는 성공한 최초 획득 순서다. Count는 보유 ID 수이며 Stack 총합이 아니다. CopyActivePassives(List)는 destination을 비운 뒤 획득 순서대로 복사한다. null destination은 ArgumentNullException이다. Stack은 순서를 바꾸지 않고 Remove/Reset/OnDestroy가 목록도 정리한다.

| API / event | 계약 |
|---|---|
| AcquirePassiveById(int, PassiveAcquisitionSource) / AcquirePassive(PassiveSO, source) | 고수준 획득. 첫 획득 Added/Stack1, 같은 ID Stacked/Stack+1, 일반 실패는 결과로 반환 |
| AddPassiveById / AddPassive(PassiveSO, string source) | donor 호환 저수준 API. 동일 ID가 이미 있으면 false; 자동 Stack하지 않음 |
| SetDatabase / RemovePassive / UpgradePassive / SetPassiveLevel / AddPassiveStack / SetPassiveStack / RefreshPassive | 기존 관리 API 유지. SetDatabase는 보유 중 교체를 허용하지 않음 |
| HasPassive / TryGetPassive / ActivePassives / Count / CopyActivePassives | 조회/순서 있는 UI 복사 |
| PassiveAdded / PassiveChanged / PassiveRemoved | 기존 효과 적용·변경·제거 성공 event |
| PassiveAcquired | 성공 Acquire당 최대 1회. Added/Changed 이후 결과 완성 및 Source 기록 뒤 발행. 실패 시 없음 |
| PassivesReset | ResetRun에서 모든 제거가 끝난 뒤 정확히 1회 |

`PassiveAcquireResult`는 readonly struct: Kind(Failed=0/Added=1/Stacked=2), Instance, Source, PreviousStack, NewStack, Error와 Success/WasAdded/WasStacked. Instance는 살아 있는 객체 참조이고 결과의 Source/이전·새 Stack 값은 획득 시점 snapshot이다. 실패/default는 Success=false이며 Error를 제공한다.

Flat modifier key는 `Passive:{PassiveID}` 하나다. Stack Reconfigure는 기존 효과 제거 → 값 변경 → 재적용, 실패 시 이전 Level/Stack 및 효과 복원을 시도한다. 복원 대상/Registry 자체가 깨져 재적용도 실패하는 경우까지 완전한 transaction을 보장하지 않는다. 실패 시 새 Acquire event/LastSource 기록은 없다. 신규 Scripted 적용 실패는 active 목록에 남기지 않는다.

## 7. PassiveInstance

| 값 | 현재 의미 |
|---|---|
| Data / PassiveID / Category / StatType / ValueType / ScriptName | SO 정의 조회 |
| BaseValue | Excel/SO 원본 Value |
| Stack | 동일 ID의 재획득 횟수. 첫 획득 1, 재획득마다 +1 |
| Value | **BaseValue × Stack** |
| Level | 별도 강화 단계. 현재 Value 공식에는 포함하지 않음; 강화 테이블 미구현 |
| Rarity | Data.Rarity. null Data 편의 fallback은 Common |
| InitialSource / LastSource | 최초 획득 출처 고정 / 최근 성공 획득 출처 갱신 |
| string Source | 최초 출처 문자열 호환 API. 재획득으로 변경하지 않음 |
| ModifierSourceKey | 같은 ID의 Stack을 하나의 modifier로 교체하는 key |

ATK 15 Percent의 Stack1→2는 Value15→30이며 modifier 두 개를 만들지 않는다. HP100 Raw의 Stack3 값은 300으로 표현되지만 HP gameplay bridge가 있다는 뜻은 아니다. Level 변경 API는 Reconfigure를 수행해도 현재 Flat Value를 증가시키지 않는다.

기존 string Source constructor를 유지한다. enum으로 해석 가능한 문자열은 typed Source로 읽고, 해석 불가능하면 Initial/LastSource=Unknown, 기존 string Source는 보존한다. `RecordAcquisition`은 LastSource만 갱신한다.

## 8. PassiveAcquisitionSource

| 명시적 값 | 의미 / UI 표시 |
|---|---|
| Unknown=0 | 출처 미상 |
| EntranceChoice=1 | 던전 입구 |
| ShopPurchase=2 | 상점 구매 |
| MonsterDrop=3 | 몬스터 드롭 |
| BossReward=4 | 보스 보상 |
| EventReward=5 | 이벤트 보상; 현재 카드방 |
| Debug=6 | 디버그 지급 |

enum 항목이 있다는 이유로 해당 Pickup/Shop/Boss Reward 구현 완료를 가정하지 않는다.

## 9. PassiveOfferService

DisallowMultipleComponent/RequireComponent(PassiveManager), 같은 GameObject 캐싱. 공개 API는 CurrentOffers(IReadOnlyList), HasOffer, OfferCount, PrepareOffer(source, runSeed, contextA, contextB, count=3), GetOffer(index), TryClaim(index, out result), ResetRun이다.

후보 조건: nonnull SO, ID>0, Category.Flat, HasValue=true, ValueType!=None, 비어 있지 않은 StatType, IsInitialRuntimeSupported=true. Icon null과 이미 보유한 ID는 허용한다. 지원 후보 중 ID 중복 또는 후보 부족은 Error/false이며 unsupported 데이터로 채우지 않는다.

| 현재 후보 ID | 이름 | Stat / 원본 Value |
|---:|---|---|
| 1 | 칼날 벼리기 | ATK +15% |
| 4 | 질주 | Move +10 Raw |
| 5 | 파트너와의 신뢰 | CrowATK +10 Raw |
| 6 | 날카로운 단검 | ATK +10 Raw |
| 7 | 36계 줄행랑 | Move +5 Raw |

모두 Common이며 한 창에서는 서로 다른 3개다. 후보를 ID 오름차순 정렬 → 명시적 unchecked seed 결합(`seed*31+contextA` → `*31+contextB` → `*31+(int)source` → `*31+count`) → 로컬 System.Random Fisher–Yates shuffle한다. UnityEngine.Random/GetHashCode/HashCode.Combine을 쓰지 않는다. 같은 Unity/.NET 환경과 입력의 결정성이며 다른 runtime 버전의 System.Random 구현 호환까지 보장한 것은 아니다.

같은 문맥 재호출은 기존 offer를 유지한다. Prepare 실패는 이전 목록/문맥을 덮어쓰지 않는다. TryClaim 실패도 유지하고 성공만 목록을 비운다. 완료 문맥은 기억하여 같은 문맥에서 다시 reroll하지 않는다. 새 문맥/ResetRun에서 새 목록을 준비한다.

## 10. 현재 실제 Stat 연결

`WesternPassiveStatKeys.IsKnown`은 Excel canonical 문자열 9개를 인식한다. `IsInitialRuntimeSupported`는 ATK/Move/CrowATK만 true다. StatModifierContainer 공식은 **(base + Raw) × (1 + Percent / 100)**다. sourceKey별 modifier를 교체/제거하고 Raw/Percent를 합산한다. 15 Percent는 15%이며 0.15로 저장하지 않는다.

| Stat | 실제 계산 위치 / 순서 |
|---|---|
| ATK | Player.Hit → EvaluateOutgoingDamage: Evaluate(ATK, baseDamage) 1회 → 기존 Fury multiplier. Fire ignite 유지. 친화 Bullet도 Hero.Hit을 통과하므로 Bullet에서 중복 적용하지 않음 |
| Move | Player.TickMovement: Evaluate(Move, 난사3.1 / 걷기2.8 / 달리기5.6). Dash 속도15에는 미적용 |
| CrowATK | Raven.EvaluateRavenDamage: Combo `Evaluate(18)+RavenLevel*6`; Link `Evaluate(45)+RavenLevel*10`; Command `Evaluate(10)+RavenLevel*6+(BurnLevel>0?3:0)`. 기존 성흔 가산은 Evaluate 뒤에 적용 |

Player.DamageScale은 public 호환 API로 남으며 ATK Percent×Fury 비율만 표현한다. Raw는 비율로 표현할 수 없으므로 실제 피해에는 EvaluateOutgoingDamage를 사용한다. 기존 성흔/공격별 base 값·범위·쿨다운을 바꾸지 않았다.

ID4 Raw10은 달리기5.6→15.6, 걷기2.8→12.8로 계산된다. 이는 원본 Value/단위를 따르는 결과이며 설명 차이를 코드로 보정하지 않는다.

## 11. 카드방 현재 흐름

```text
Dungeon.TryTravel (미완료 DevilCards 서비스 상호작용)
  → PrepareOffer(EventReward, Seed, Room[Segment], MapIndex, 3)
  → 성공 시 State.CardChoice
  → GameUIView.UpdateCardChoices → UICommand → Dungeon.ClaimCard(index)
  → PassiveOffers.TryClaim → Passives.AcquirePassiveById
  → Added 또는 Stacked 성공 → offer clear
  → Progress.CompleteService → State.Exit → 기존 Notice
```

State/RoomKind/완료 여부/index 검증은 유지한다. 준비 실패는 CardChoice에 진입하지 않고, claim 실패는 방 완료·Exit 처리 없이 선택지와 상태를 유지한다. 같은 방을 닫았다 다시 열어도 목록이 유지된다. UICommand의 ClaimCard(int) 계약은 그대로다. 옛 문자열 3종 배열과 Stage/CardRarity 기반 획득은 없다.

## 12. UI 연결

UGUI Canvas/Text/Image/Button과 UIReference 문자열 Dictionary를 유지한다. TMP/event-only UI로 바꾸지 않았다. LateUpdate polling, GameHUD event/queue, UICommand UnityEvent의 혼합 방식이다.

- Slot: Manager.CopyActivePassives의 재사용 List를 사용한다. count/ID 순서/slot 생존이 다르면 재구성, 같으면 내용을 갱신한다. 같은 ID Stack 증가는 슬롯 1개를 유지한다.
- Tooltip: SO 이름/설명 원문, 고정 Rarity, InitialSource, Stack/Level을 표시하고 hover 중 변경도 반영한다. 제거/Reset 시 닫는다.
- Icon: SO.Icon 우선; null이면 ATK→BulletIcon, Move→WaterIcon, CrowATK→RavenPortrait의 기존 VisualCatalog fallback. SO를 수정하지 않는다.
- Toast: GameHUD가 Manager.PassiveAcquired/PassivesReset을 구독/해제하고 중복 구독을 막는다. Added/Stacked, source 및 이전/새 Stack snapshot을 queue에 보관한다. 획득/STACK 이전→새 값 표시, 기존 시간/opacity 및 Route에서의 timer 처리 유지.
- RouteBuild/M: Count는 보유 ID 수. ATK/Move/CrowATK GetRawBonus/GetPercentBonus를 읽는다. 0 항목은 생략하고 둘 다 0이면 +0이다.
- 카드: 실제 CardBorder/CardRarity/CardEffect/CardName/CardIcon0~2 key가 GameUI.prefab에 존재한다. 이름/설명/원본 Raw·Percent/보유 Stack→선택 후 Stack을 표시한다. 준비된 offer가 없으면 하드코딩 카드로 대체하지 않는다.

Rarity 한국어: Common 일반 / Uncommon 희귀 / Rare 레어 / Legendary 전설. 저장 RarityColors가 3개인 경우 Legendary는 gold `(1, .72, .18)` fallback, 잘못된 값/기타 누락은 InactiveColor다. 배열을 무조건 enum index로 접근하지 않는다. 현재 후보가 모두 Common이므로 다른 등급은 일반 카드방 플레이만으로 검증되지 않는다.

GameHUD의 Build.StackChanged/Reset과 GameUIView의 Build.Stack/Threshold/IsActive는 성흔용으로 유지한다. 패시브는 신규 Manager/Stats만 사용한다. Boss UI는 dormant/prototype 경로이며 정식 보스용 신규 패시브 보상 UI가 아니다.

## 13. Reset

Dungeon.ResetRun의 실제 순서: Cinematics.Cancel → DestroyTrackedEnemies → ClearTransient → PassiveOffers.ResetRun → Passives.ResetRun → Hero.ResetForRun → Crow.ResetForRun → Build.ResetRun → ResetRunProgress.

Manager는 모든 보유 효과/Instance/order를 제거한 뒤 PassivesReset을 1회 발행한다. GameHUD는 queue/현재 Toast/opacity/timer를 비우고 GameUIView는 슬롯/Tooltip을 동기화한다. Offer는 목록/문맥을 지운다. RunBuild는 성흔 stack만 비우고 threshold를 유지한다. Stage 진행은 새 런 Reset이 아니므로 보유 패시브/Stack이 유지된다. Main의 Starting Passives는 0개라 자동 지급이 없다.

## 14. Ability / Shift

현재 framework만 있고 구체 PassiveEffectScript는 없다. ScriptName `Stigma01`, `Stigma02`, `Stigma03`, `Rare01`, `Rare02`, `Rare03`, `Legendary01`은 미래 효과 연결점이다. import/verify의 missing-effect Warning은 데이터 보존 허용이며 획득 성공 허용이 아니다.

지금 이 정의를 Acquire하면 TryApplyScripted 실패 → Failed/Error, active 목록 잔존 없음, PassiveAcquired 없음이다. 빈 placeholder Effect, 자동 성공, ScriptName 무시, Ability를 Flat으로 바꾸는 우회는 하지 않는다. 향후 실제 Effect가 Stack을 읽어야 하면 Instance.Value/Stack 정책을 명시적으로 구현해야 하며 Data.Value를 직접 읽는 효과를 자동 scaling하지 않는다.

## 15. 아직 미지원 Stat

| known key | 현재 상태 |
|---|---|
| HP | 데이터 보존, 실제 MaxHealth bridge 없음 |
| StigmaATK / StigmaDamage | 데이터 보존, 성흔 공격/피해 bridge 없음 |
| CrowRange | 데이터 보존, Raven 범위 bridge 없음 |
| CritDamage / CritRate | 데이터 보존, 치명타 bridge 없음 |

unknown Stat은 검증 Error다. known이지만 초기 runtime 미지원이면 Warning이며 Import/DB 포함을 허용한다. Manager의 저수준 Flat 저장 가능성과 해당 Stat이 실제 gameplay에 적용되는지는 구분한다. 현재 Offer는 이 6개를 제외한다.

## 16. 향후 Passive Field Item

[예정] PassivePickup / WorldRewardSpawner / Spawned_Rewards 및 공통 획득 계층, E Interaction 우선순위를 만든다. 현재 Pickup/RewardSpawner/Drop Table/Shop/Boss Reward/실질 Currency는 없다.

월드 아이템은 `AcquirePassiveById(id, MonsterDrop)`을 호출하고 result.Success일 때만 제거한다. 실패면 유지/오류 표시한다. 같은 ID의 Add/Stack을 직접 분기하거나 Manager dictionary/StatModifierContainer를 직접 수정하지 않는다. 카드·상점·보스 보상은 동일 고수준 Acquire API에 source를 전달할 기반이 있다. 별도 PassiveAcquisitionService는 향후 공통 interaction/획득 책임의 후보이며 현재 생성된 클래스가 아니다.

## 17. RunBuild와의 관계

**구 패시브 책임 완전 제거됨. RunBuild는 성흔만 담당.** 구 enum/DTO/catalog/inventory/Acquire/event/bonus와 Dungeon wrapper/CardRarity property가 없다. 현재 Assets C#의 old passive API/옛 3종 ID 참조는 0개다. 신규 PassiveRarity는 WesternLemegeton.Passives의 4등급 enum이다.

성흔 stack/threshold/effect/event를 StigmaManager로 이관하고 모든 g.Build 연결을 전환한 뒤 RunBuild 자체도 삭제할 예정이다. 그 작업은 아직 수행되지 않았다. baseline/루트 후보/Verification/Assets Editable README의 옛 API 설명은 현재 계약으로 사용하지 않는다.

RuntimeSmoke/AuthoringSmoke는 신규 Manager/Offer/Source/Stack/SO rarity 기준으로 갱신되었다. ID1/4/5 및 ID1 Stack2 Percent30 fixture를 사용하고 opt-in 진입점은 유지한다. 이 문서 정리에서 Smoke를 실행하지 않았다.

## 18. 제작자가 패시브 추가하는 방법

1. `Assets/Data/Passive/패시브.xlsx`의 Passive sheet에 고유한 양수 ID와 필수 header에 맞는 행을 추가한다. 이름/설명은 현재 표시 원문이며 원본 값/Raw·Percent 단위를 의도대로 기록한다.
2. `Tools/Western Lemegeton/Passive/Validate Workbook` 실행. Error 0을 확인하고 placeholder/icon/unsupported Stat/missing effect 등의 Warning을 검토한다. 이 메뉴는 asset을 수정하지 않는다.
3. `Tools/Western Lemegeton/Passive/Import Workbook` 실행. Registry Rebuild와 SO/DB 반영을 수행하므로 실제 변경을 Git diff로 확인한다. 이전 지정 workbook 재사용 메뉴는 `Tools/Western Lemegeton/Passive/Reimport Last Workbook`이다.
4. `Tools/Western Lemegeton/Passive/Verify Generated Data` 실행하여 workbook/DB 필드 일치를 확인한다.
5. 유효 Flat + ATK/Move/CrowATK이면 현재 Offer 후보가 될 수 있다. 아이콘 null도 허용된다. 아직 미지원 Stat은 해당 gameplay bridge를 먼저 구현한다.
6. Ability/Shift는 ScriptName과 일치하는 PassiveEffectAttribute key의 구체 EffectScript를 구현하고 `Tools/Western Lemegeton/Passive/Rebuild Effect Registry` 또는 Import로 등록한다. 효과 등록만으로 현재 Flat-only 카드 후보 필터에 자동 포함되지 않는다. Reward Pool 정책은 별도 전환한다.
7. 사용자가 Unity compile/Console 확인 후 카드 선택·중복 Stack·Slot/Tooltip/Toast/M 수치·실제 피해/이동·새 런 Reset·성흔 회귀를 확인한다. Legendary는 현재 카드 후보 외 별도 검증 환경이 필요하다.

메뉴는 수동 실행한다. 이번 문서 작업에서 어떤 메뉴도 실행하지 않았다. Import의 I/O rollback 한계와 orphan 보존 정책을 알고 변경 파일을 확인한다.

## 19. 다음 AI 주의사항

- RunBuild에 패시브를 다시 넣거나 기존 3개 하드코딩 카드/옛 rarity 배율을 복구하지 않는다.
- SO=정의, Instance=런 상태, Manager=보유·획득, Offer=후보, Stats=Flat 계산의 책임을 유지한다.
- 동일 ID는 한 Instance/한 Slot/한 modifier source이며 Stack+1이다. Level scaling은 아직 없다.
- Percent는 100 기준, Value는 BaseValue×Stack이다. 원본 Excel 수치를 자동 교정하지 않는다.
- 신규 획득은 Acquire API, 호환 Add API는 duplicate=false다. UI event 순서를 바꾸지 않는다.
- import-ready 데이터와 runtime-ready 효과를 구분한다. Ability/Shift 미등록은 Import Warning, runtime Acquire Failed다.
- 기본 피해와 기존 성흔 강화 순서를 유지하고 Player/Raven/친화 Bullet에 modifier를 중복 적용하지 않는다.
- offer의 로컬 RNG, 문맥 캐시, 실패 유지/성공 clear와 서비스 완료 gate를 유지한다.
- Registry 자동 생성 부작용을 다시 추가하지 않는다. Validate Only의 무수정 계약을 유지한다.
- 실제 Scene 참조/저장 RarityColors/아이콘 fallback을 먼저 확인하고 UI key/Prefab을 불필요하게 재구축하지 않는다.
