# Passive Migration Phase 1 — Scaffold

## 1. 작업 기준

- 작업일: 2026-10-03 (Asia/Seoul).
- 대상 프로젝트 HEAD: `c218280dbb80d83b03e17b05ad92f8bd50df86a3`.
- 실제 donor: `E:\GitHub\ProjectTP\Assets\Scripts\Passive`.
- donor HEAD: `84e01f7a527b20463c4ca3698fa57e3f83ae893d`. 사용한 Passive 하위 파일은 작업 시 Git 로컬 변경이 없었다.
- 지정한 16개 원본 모두 실제 파일을 확보하고 읽은 뒤 작업 폴더에 snapshot을 저장했다. before-Astra의 불완전한 파일 집합, 기존 분석 문서/기억으로 재작성한 코드, 기존 Western 루트 후보 코드는 사용하지 않았다.
- 참고: `Docs/WESTERN_LEMEGETON_PROJECT_ANALYSIS.md`, `Docs/RUN_BUILD_BLOCK_REFACTOR.md`, `Docs/GAME_UI_VIEW_BLOCK_REFACTOR.md`의 Passive/성흔/UI 계약.
- [확인] 신규 C# 16개와 이 문서만 프로젝트에 추가했다. 기존 코드/자산/메타데이터/문서/ProjectSettings를 보존했다. 새 asmdef 및 수동 .meta 작성 없음.
- [확인] Unity Editor/Play Mode/빌드, Excel Import/Registry Rebuild 메뉴, 에셋 생성 API, commit/push를 실행하지 않았다.

donor와 신규 파일의 대응:

| donor 상대 경로 (위 donor 폴더 기준) | 신규 상대 경로 (Assets/Scripts/Passives 기준) |
|---|---|
| Runtime/PassiveEnums.cs | Runtime/Data/PassiveEnums.cs |
| Runtime/PassiveSO.cs | Runtime/Data/PassiveSO.cs |
| Runtime/PassiveDatabaseSO.cs | Runtime/Data/PassiveDatabaseSO.cs |
| Runtime/PassiveInstance.cs | Runtime/Core/PassiveInstance.cs |
| Runtime/PassiveManager.cs | Runtime/Core/PassiveManager.cs |
| Runtime/PassiveRuntimeContext.cs | Runtime/Core/PassiveRuntimeContext.cs |
| Runtime/PassiveEffectContext.cs | Runtime/Core/PassiveEffectContext.cs |
| Runtime/PassiveEffectAttribute.cs | Runtime/Effects/PassiveEffectAttribute.cs |
| Runtime/PassiveEffectScript.cs | Runtime/Effects/PassiveEffectScript.cs |
| Runtime/PassiveEffectRegistrySO.cs | Runtime/Effects/PassiveEffectRegistrySO.cs |
| Runtime/StatModifierContainer.cs | Runtime/Stats/StatModifierContainer.cs |
| Runtime/AstraPassiveStatKeys.cs | Runtime/Stats/AstraPassiveStatKeys.cs |
| Editor/SimpleXlsxReader.cs | Editor/SimpleXlsxReader.cs |
| Editor/PassiveExcelImporter.cs | Editor/PassiveExcelImporter.cs |
| Editor/PassiveEffectRegistryGenerator.cs | Editor/PassiveEffectRegistryGenerator.cs |
| Editor/AstraPassiveDataGeneration.cs | Editor/AstraPassiveDataGeneration.cs |

## 2. 신규 폴더 구조

```text
Assets/Scripts/Passives/
├─ Runtime/
│  ├─ Data/     PassiveEnums.cs, PassiveSO.cs, PassiveDatabaseSO.cs
│  ├─ Core/     PassiveInstance.cs, PassiveManager.cs,
│  │           PassiveRuntimeContext.cs, PassiveEffectContext.cs
│  ├─ Effects/  PassiveEffectAttribute.cs, PassiveEffectScript.cs,
│  │           PassiveEffectRegistrySO.cs
│  └─ Stats/    StatModifierContainer.cs, AstraPassiveStatKeys.cs
└─ Editor/      SimpleXlsxReader.cs, PassiveExcelImporter.cs,
                PassiveEffectRegistryGenerator.cs, AstraPassiveDataGeneration.cs
```

Generated 폴더는 만들지 않았다.

## 3. Namespace 격리

| 구분 | 실제 namespace / 계약 |
|---|---|
| 현재 gameplay | `WesternLemegeton`: PassiveRarity(Common/Uncommon/Rare), PassiveSource, PassiveDefinition, OwnedPassive, RunBuild 유지 |
| 신규 Runtime 12파일 | `WesternLemegeton.Passives`: donor 타입 전체. 새 PassiveRarity는 donor의 Legendary까지 포함 |
| 신규 Editor 4파일 | `WesternLemegeton.Passives.Editor`; 각 파일에 `using WesternLemegeton.Passives;` 명시 |

[확인] 기존 enum/class 이름은 바꾸지 않았다. 신규 PassiveRarity와 기존 PassiveRarity는 서로 다른 타입이며 변환/bridge는 없다. 기존 gameplay에 신규 namespace using을 추가하지 않았다. AstraPassiveStatKeys/AstraPassiveDataGeneration 클래스명도 유지했다.

동일 namespace 내 원본 내부 참조와 Editor에서 Runtime SO/enum/Registry를 참조하는 해석을 정적으로 확인했다. `CreateAssetMenu`의 기존 `Game/Passive/...` 문자열도 이번에는 유지했다.

## 4. 이식된 Runtime 골격

### Data

- PassiveEnums: PassiveRarity(Common/Uncommon/Rare/Legendary), PassiveCategory(Flat/Ability/Shift), PassiveValueType(None/Raw/Percent)의 순서/값 유지.
- PassiveSO: PassiveID/PassiveIconResource/Icon, 문자열 key 2개, Rarity/StatType/Category/Value/HasValue/ValueType/ScriptName과 모든 serialized field 유지. `ApplyImportedData`는 원본처럼 UNITY_EDITOR 조건의 public 메서드이며 icon replace 조건과 대입 순서도 동일하다.
- PassiveDatabaseSO: passives/effectRegistry, ID cache, TryGetById/GetById 유지. cache는 첫 유효 ID 우선이고 OnEnable에서 무효화한다. SetImportedPassives/SetEffectRegistry는 기존 UNITY_EDITOR public API를 유지한다.

### Core

- PassiveInstance: 원본 내부 constructor, Level/Stack 최소1, Source, Data와 Value/ValueType/StatType/ScriptName/Category, 내부 ModifierSourceKey=`Passive:{PassiveID}` 유지. 현재 Value는 Data.Value이며 Level/Stack에 따른 새 scaling을 넣지 않았다.
- PassiveRuntimeContext: DefaultFlatStatTarget 참조 및 타입별 service Dictionary, Register/Unregister/TryGet/Get 계약 유지.
- PassiveEffectContext: 새 namespace의 PassiveManager/PassiveRuntimeContext를 참조하며 원본 service query를 그대로 위임한다.
- PassiveManager: RequireComponent(PassiveRuntimeContext), activePassives ID Dictionary, Awake의 context/service 등록, Start의 startingPassives 적용, OnDestroy의 제거/해제 유지. Scene/Prefab에는 붙이지 않았다.
- AddPassive: 동일 ID는 원본대로 false다. 삽입→TryApply→실패 시 remove/cleanup→성공 시 PassiveAdded 순서 유지. RemovePassive, Level/Stack 변경, Refresh, SetDatabase, ResetRun도 유지했다.
- Reconfigure: 기존 효과 제거/cleanup→Level/Stack 변경→재적용→성공 Changed, 실패 시 cleanup/이전 값 복원/재적용 rollback을 보존했다.
- Flat은 RuntimeContext.DefaultFlatStatTarget.SetModifier에, Ability/Shift는 registry에서 만든 EffectScript.Apply/Remove에 위임한다. 이 단계에서 gameplay stat 대상이나 구체 effect 구현을 만들지 않았다.

### Effects

- PassiveEffectAttribute(Key), abstract PassiveEffectScript.Apply/Remove 유지.
- PassiveEffectRegistrySO: entries/byKey, TryCreate의 prototype Instantiate/name/HideFlags.DontSave, Contains/cache/OnEnable, UNITY_EDITOR SetGeneratedEntries/Entry constructor 유지.
- 이번 16파일에 포함되지 않은 구체 Ability/Shift 효과는 추가하지 않았다. registry asset과 prototype도 생성하지 않았다.

### Stats

- StatModifierContainer는 sourceKey별 기존 modifier 교체/제거, Raw/Percent bucket, StatChanged 조건/순서 및 Mathf.RoundToInt(Evaluate)의 EvaluateInt를 유지한다.
- Evaluate 공식은 원본 그대로 `(baseValue + bucket.Raw) * (1f + bucket.Percent / 100f)`다. Percent를 현재 RunBuild의 소수 배수로 변환하지 않았다.
- AstraPassiveStatKeys의 PlayerDamage/MoveSpeed/RavenDamage 문자열 및 IsSupported를 유지했다. 현재 Player/Raven 계산식에 연결하지 않았다.

## 5. 이식된 Editor 골격

모든 파일은 Editor 폴더에 있고 전체 `#if UNITY_EDITOR ... #endif`를 유지한다. Runtime 파일에는 UnityEditor 참조가 없다. 원본 LINQ/reflection/TypeCache 동작을 축약하거나 새 프레임워크로 대체하지 않았다.

- **SimpleXlsxReader**: XLSX ZIP/XML, workbook relationship, preferred sheet 또는 첫 sheet fallback, 첫 row header, shared/inline strings, bool cell, 행/열 위치, invariant float parsing을 그대로 유지했다. 실제 workbook을 읽거나 import하지 않았다.
- **PassiveExcelImporter**: 필수 header 10개, file validation, Sheet1 읽기, ValidateStatTypes, registry effect 목록, ID/row/category/value/script/icon parsing, SO 갱신/생성, 정렬/DB/registry 흐름을 유지한다. 오류가 있으면 일부 에셋 작업 후 실패할 수 있는 원본 transaction 구조도 이번에는 수정하지 않았다.
- **PassiveEffectRegistryGenerator**: TryCollectEffects의 새 namespace EffectScript/Attribute 수집, 중복 key 검사, 수동 Rebuild의 prototype/registry/link.xml 생성 및 기존 DB 연결 경로 유지. 자동 bootstrap만 제거했다.
- **AstraPassiveDataGeneration**: Import Astra Workbook 메뉴, WorkbookPath, ImportWorkbook/VerifyGeneratedData 및 기존 >=1000 검증을 유지했다. importer의 Sheet1과 검증의 Passive 시트명 차이도 남겼다.

유지한 메뉴: `Tools/Passive/Import Excel To SO`, `Reimport Last Excel`, `Rebuild Effect Registry`, `Import Astra Workbook`. **Phase 1에서는 어느 메뉴도 실행하지 않는다.** Western workbook/schema validation 적용 전 importer는 사용하지 않는다.

## 6. 자동 실행 차단

PassiveEffectRegistryGenerator에서 정확히 다음 3개 선언을 제거했다:

1. `[InitializeOnLoad]` attribute.
2. static constructor 전체와 그 안의 `EditorApplication.delayCall += RebuildIfNeeded` 등록.
3. private RebuildIfNeeded 메서드의 자동 Rebuild wrapper.

[확인] TryCollectEffects, Rebuild(out int registeredCount,bool logErrors), RebuildMenu와 `Tools/Passive/Rebuild Effect Registry`를 유지했다. 다른 Editor 파일에도 자동 load/reload/postprocessor callback이 없다. static field 초기식은 원본 상수/표시/필수 header 목록이며 asset 생성 호출을 등록하지 않는다.

생성 코드는 수동 API/Import 호출 아래에만 남아 있다. 메뉴/API를 호출하면 기존 `Assets/Resources/Passive`, PassiveDatabase.asset, PassiveEffectRegistry.asset, Effects/_Generated, Passives/Generated/link.xml 생성/갱신이 가능하지만 이번에는 호출하지 않았다. Generated 폴더나 생성 파일도 만들지 않았다.

## 7. 현재 게임과 연결하지 않은 항목

- Scene/Prefab의 Game_Systems/Hunter/Raven/GameUI/Main Camera/Room/Enemy에 새 Component를 붙이지 않았다.
- PassiveManager/PassiveRuntimeContext/StatModifierContainer를 배치하지 않았다. DefaultFlatStatTarget/Starting Passives/Database/Effect Registry reference를 연결하지 않았다.
- PassiveSO/DB/Registry/prototype asset, Resources/Passive, Generated/link.xml 생성 없음. donor .meta/에셋/Excel은 복사하지 않았다.
- RunBuild/Dungeon/Player/Raven/Enemy/GameHUD/GameUIView/UICommand/GameSceneBindings를 수정하지 않았다. 기존 블럭 리팩터링 및 기존 UI를 보존했다.
- 현재 카드방 흐름은 `Dungeon.ClaimCard → Dungeon.AcquirePassive → RunBuild.AcquirePassive → GameHUD → GameUIView Slot/Toast` 그대로다.
- 현재 Passive 3종, 중복 획득 합산, PlayerDamageBonus/MoveSpeedBonus/RavenDamageBonus, 성흔과 Reset 이벤트는 기존 RunBuild가 담당한다. Bridge/stat 연결/정책 변환은 없다.

## 8. Phase 2에서 수정할 Astra 전용 정책

이번 단계에서는 아래를 바꾸지 않았다. 다음은 실제 Western workbook/기획 데이터를 기준으로 재검토할 항목이다.

| 항목 | 현재 남아 있는 donor 정책/후속 검토 |
|---|---|
| ID | Import minimumPassiveId=1000, VerifyGeneratedData >=1000; Western ID1~18 대응 필요 |
| workbook / menu / 명칭 | Assets/Data/Passive/패시브.xlsx, Import Astra Workbook, Astra 클래스명 유지 |
| sheet / schema | importer Sheet1, verifier Passive; 필수 header/타입/행 검증을 Western에 맞춰 확인 |
| stat keys | PlayerDamage/MoveSpeed/RavenDamage 3개와 IsSupported; Western Excel StatType 대응 |
| importer validation / transaction | 현행 원본 검증과 일부 쓰기 후 errorCount 실패; 후속 validation/transaction 검토 필요 |
| Ability / Shift | abstract effect/registry 골격만 이식; 실제 Western 효과 및 미구현 데이터 처리 필요 |
| output / UI | Resources/Passive 경로, Legendary 데이터; 기존 3등급 UI 연결 없음 |
| Percent / rarity | Percent는 /100 계산, rarity는 donor SO 소유; 기존 RunBuild 방식에 맞춰 바꾸지 않음 |
| 중복 획득 | donor 동일 ID AddPassive=false 유지. RunBuild 중복 합산과의 정책 대응은 **Phase 3 Bridge** 범위 |

Phase 2 변경을 임시 코드로 선반영하지 않았다.

## 9. 컴파일 계약

[확인] 원본/후보 16개의 C# 선언 토큰을 대조했다. namespace/Editor using 추가와 Registry의 위 3개 자동 bootstrap 선언 제거 외에는 필드·serialized 속성·enum·signature·접근성·constructor·property·event·함수 본문이 동일하다. 현재 프로젝트의 기존 파일도 작업 시작 해시와 일치한다.

[확인] 구문/전처리 검증:

- Runtime 12개는 WesternLemegeton.Passives, Editor 4개는 WesternLemegeton.Passives.Editor.
- Editor 파일은 UNITY_EDITOR 미정의 시 타입 선언이 없다. Runtime의 editor-only 데이터 갱신 메서드는 원본 guard 유지.
- RequireComponent가 신규 RuntimeContext를 가리키고 EffectContext/Registry/Importer의 내부 타입을 해석한다.
- 프로젝트 Assets의 요청한 7개 타입 선언을 검색했다. 기존 WesternLemegeton.PassiveRarity와 신규 namespace 타입만 별도로 공존하며 전역 duplicate 타입을 추가하지 않았다.
- 새 asmdef/.meta/Scene/Prefab/asset 없음. 파일명과 주 타입 이름 일치; PassiveEnums는 원본처럼 enum 3개 묶음이다.
- InitializeOnLoad/auto delayCall/RebuildIfNeeded/reload/postprocessor 자동 생성 경로 없음.

[확인] 프로젝트 Unity 버전은 6000.3.17f1이다. 기존 Unity 참조 목록의 실제 DLL 304개를 metadata로 읽고 Roslyn의 메모리 내 diagnostics만 수행했다. 별도 compiler process/Emit/어셈블리 생성/Unity 실행/프로젝트 빌드는 하지 않았다.

| 정적 타입 검사 | 결과 |
|---|---|
| 신규 Runtime (UNITY_EDITOR 정의) | 오류 0 |
| 신규 Runtime (UNITY_EDITOR 미정의) | 오류 0 |
| 별도 Editor 경계 (신규 Runtime compilation reference) | 오류 0 |
| 기존/Common enum과 신규/Legendary enum 공존 probe | 오류 0 |
| Editor PassiveRarity symbol binding | 신규 WesternLemegeton.Passives.PassiveRarity로 해석 |

Editor가 호출하는 ApplyImportedData/SetImportedPassives/SetEffectRegistry/SetGeneratedEntries/Entry 생성자는 public이고 editor 빌드에서 존재한다. PassiveInstance의 내부 constructor/ModifierSourceKey/setter는 Runtime 내부 사용으로 유지한다. 접근 제한자나 API를 넓히지 않았다.

검사에 사용한 기존 gameplay DLL은 참조 해석용 metadata다. 현재 Unity의 실제 import/전체 프로젝트 재컴파일/Console/메뉴 실행 결과를 확인한 것은 아니다. 사용자 Console 오류가 제공되면 같은 Phase 1 범위의 namespace/using/type/access/signature 오류를 수정할 대상이다.

## 10. 사용자 Unity 확인 항목

[사용자 Unity 테스트 필요] AI는 Unity를 실행하지 않았다. 사용자가 다음만 확인한다:

1. Unity 프로젝트를 열고 Script compilation 완료를 기다린다. Console Error/Exception 0개를 확인한다.
2. 현재 Title→Town→일반 전투 진입이 기존대로 되는지 확인한다.
3. 기존 카드방에서 Passive 획득/RunBuild bonus/Slot/Toast가 기존 경로로 동작하는지 확인한다.
4. 에디터 로드/컴파일만으로 Assets/Resources/Passive, DB/Registry/prototype, Effects/_Generated, Generated/link.xml이 생기지 않았는지 확인한다.
5. Main Scene/Prefab에 PassiveManager/PassiveRuntimeContext/StatModifierContainer가 붙지 않았는지 확인한다. script/folder .meta는 Unity가 정상 import하며 자동 생성할 수 있다.

**Tools/Passive의 Import/Reimport/Rebuild/Astra Workbook 메뉴는 실행하지 않는다.** 신규 DB/Registry/SO/Inspector 연결이나 실제 stat 적용 테스트는 이번 Phase 1 범위가 아니다.

### donor SHA-256

작업에 사용한 원본 해시를 아래에 기록한다. 기존 donor 파일은 수정하지 않았다.

| donor 파일 | SHA-256 |
|---|---|
| PassiveEnums.cs | `EA61B31568EB82450192CFE11E227531CB9043825543673F805A144C434C668C` |
| PassiveSO.cs | `1A70DFFA5597C5CC6A119FBEBEA44ADCC3CF864A4B92ECA62B2FB2F7A9899869` |
| PassiveDatabaseSO.cs | `5674EA2F4BEF74763D6F460CF20A5372CAC90607DC7AD0B6E4382EE1E4DAC07B` |
| PassiveInstance.cs | `F3ECC7E7EEBE018F604D817B46D5DAA0BFFE0B6F1BD4EEA013950AB2BE7FF82F` |
| PassiveManager.cs | `2B33379D0F8B5785539E963323A8B6B601AAC73F7E634FEAE3A093836CB52851` |
| PassiveRuntimeContext.cs | `D8025E8B55518FB0D11EE7319FD15B710D67BD28CE01713FC60A212CE0FFDA23` |
| PassiveEffectContext.cs | `CA7B7718B03F408FFBF1975FF2381AF094DAF199035504273B98CAF7DDDF4817` |
| PassiveEffectAttribute.cs | `B74EC2DDDD618BACC966FD086A96B8C70737C5A786B6E90939602D7724E93C37` |
| PassiveEffectScript.cs | `05EA47EF6084A9312234F37E3CCC7EF62297511CE2B43C3A8E34B3BEB2C21BE9` |
| PassiveEffectRegistrySO.cs | `0F3C374A49CAA303564CB8F61D3D694D0C94B842B3EF60B800468BBF9EBE695F` |
| StatModifierContainer.cs | `4DEA34D44B1CBDBF67933642D780EA8E37D10CCE677F19C8C0076D8046F2D1CF` |
| AstraPassiveStatKeys.cs | `5B8B9CBC24337B672BB3855C2B320B23A78ED11FC6B7BCDDEA3CB44E3A35DC9C` |
| SimpleXlsxReader.cs | `2761EC8F5D7F4B616759B1093B3CED989271D170660253D22DDDE9B72CD2943B` |
| PassiveExcelImporter.cs | `EEED20FBE42B9ADDDA84058655149D6509A6321B69D05B9088EDD1A2514666CC` |
| PassiveEffectRegistryGenerator.cs | `407C8A12CE56265CBC13E4BA9F3FAAA9F634E55C3D4D95A7DEFAFFA4C11393B2` |
| AstraPassiveDataGeneration.cs | `51F12EF52E5C4D57C477F389449E787173607EEE54D5DA040986BB4C9C23B33B` |
