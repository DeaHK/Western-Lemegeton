# 코덱스 검수 하네스

> 현재 진행 상태와 재개 순서는 [CONTINUE_HERE.md](CONTINUE_HERE.md)를 먼저 확인한다. 공용 세션 유지 방향으로 구현 중이며, 새 씬과 프리팹 생성·컴파일 검증은 미완료다.

이 문서는 사용자가 코덱스에 씬 분리 작업을 맡기거나 클로드 맥스의 결과를 검수시킬 때 사용하는 작업 절차다. [프로젝트 리뷰](PROJECT_REVIEW.md), [맥스 작업 지시문](CLAUDE_TASK.md), [정적 조사 도구](../../Tools/SceneReview.ps1), [원본 스냅샷](baseline.json)을 함께 사용한다.

## 코덱스에 붙여 넣을 요청

```text
E:\GitHub\Western-Lemegeton의 씬 분리 검수 담당으로 작업해.
AGENTS.md와 Docs/SceneReview/PROJECT_REVIEW.md, CLAUDE_TASK.md, CODEX_HARNESS.md를 읽어.
현재 코드를 기준으로 클로드의 변경을 리뷰하고, 승인된 범위의 결함은 직접 수정해.
목표는 MainMenu, Lobby, Game, Debug 분류와 로딩 화면, GameManager를 통한 안전한 상태 전달이야.
Tools/SceneReview.ps1의 Compare를 실행하고, 변경 파일과 현재 씬 연결을 조사해.
기준 스냅샷은 덮어쓰지 마. 기존 작업과 클로드의 변경을 통째로 되돌리지 마.
씬 수만 보고 완료 처리하지 말고 전환 표와 회귀 검증표로 기능을 확인해.
수정한 몬스터 동작·프리팹·연출은 Docs/Monsters.md에도 반영해.
사용자가 플레이 검사를 제외했으면 그 조건을 지켜. 미실행 항목을 통과로 표시하지 마.
결과는 구현 완료, 정적 검증 완료, 실행 검증 완료, 미검증을 구분해서 보고해.
```

## 기준 확보와 변경 조사

PowerShell 7에서 저장소 루트를 작업 디렉터리로 사용한다. 도구는 Unity를 실행하지 않는다. Assets·Packages·ProjectSettings의 선택된 텍스트 자산과 모든 meta를 해시한다. 이미지·음원 원본 바이트, Library, Temp, 복제 프로젝트는 해시 범위 밖이다.

```powershell
pwsh -NoProfile -File Tools/SceneReview.ps1 -Mode Compare
pwsh -NoProfile -File Tools/SceneReview.ps1 -Mode Compare -Output Logs/SceneReview/after.json
```

결과의 added/removed/changed를 실제 diff와 함께 읽는다. 씬 분리에서는 변경 파일이 생기는 것이 정상이다. 도구 종료 코드 0은 기본 정적 조건 통과만 뜻한다. 중복 Assets GUID와 존재하지 않는 활성 빌드 씬은 실패로 처리한다. 씬의 GUID가 Assets에서 해석되지 않으면 패키지·내장·누락 후보로 표시하며 자동으로 결함 판정을 내리지 않는다.

다음 작업을 시작할 때 별도 기준이 필요하면 새 이름을 사용한다.

```powershell
pwsh -NoProfile -File Tools/SceneReview.ps1 -Mode Snapshot -Baseline Logs/SceneReview/before-next-task.json
```

Snapshot은 기존 파일을 덮어쓰지 않는다. 기본 baseline.json은 2026-11-24 조사 기준으로 보관한다. 조사 도구는 asset hash·코드 분류·직접 직렬화 루트·컴포넌트·씬 외부 GUID 참조를 수집한다. 프리팹 내부 참조의 재귀 검증, fileID 무결성, Resources 동적 문자열 경로, C# 컴파일, 실제 씬 전환을 대신하지 않는다.

## 작업 단위와 중간 완료 조건

| 단계 | 제출물 | 코덱스 검수 |
| --- | --- | --- |
| A 현황 고정 | 기준 HEAD·diff, 현재 Hierarchy와 각 루트 소유권 표 | Main에서 활성 연결된 컴포넌트와 런타임 생성물을 모두 포함했는지 |
| B 상태·전환 계약 | GameManager/씬 로컬 소유권, 전환 사유별 유지·초기화 표 | 새 런과 단순 씬 이동이 구분되는지, 상태의 원본 소유자가 하나인지 |
| C 초기화·로딩 | 중복 방지, 바인딩/해제, 실패 복구, 로딩 화면 | 씬 활성화 전 전투가 시작되는지, 0.9 대기에서 멈추는지 |
| D 씬 분류 | MainMenu/Lobby/Game/Debug, 씬별 연결 객체, 빌드 설정 | prefab GUID·직렬화 참조·Resources 경로, 런타임 생성물 소유 씬 |
| E 기존 기능 연결 | 시작·로비·생성형·훈련·보스·성흔·인장·UI 동선 | 기존 public 진입점, UICommand, 키 설정, Timeline·음악 |
| F 검증·인계 | 컴파일·Editor/플레이/빌드 결과와 제한, 파일 목록 | 아래 시나리오별 증거와 미실행 표시 |

중간 단계의 검토를 매번 사용자 승인 요청으로 바꾸지 않는다. 이미 승인된 구현은 계속 진행한다. 실제 기능의 선택이 필요한 충돌만 질문하고 독립 작업을 진행한다.

## 코드 리뷰 체크

- GameManager가 씬 전환을 직렬화하고 로딩 중 재입력·사망·귀환 요청을 일관되게 처리한다.
- Dungeon.Awake의 기존 마을 초기화, GetComponent 서비스 조회, StigmaManager.Start의 전투 객체 생성이 새 생명주기에 맞게 분리돼 있다.
- 씬 외부 GameObject/Transform을 저장 데이터로 참조하지 않는다. 레벨·카드·성흔 ID와 숫자 상태, 복구 가능한 전환 사유를 전달한다.
- 같은 런 이동에서 ResetRun·Wallet.Reset·Passives.ResetRun·Stigmas.ResetRun을 호출하는 경로를 전수 확인한다.
- 새 씬 바인딩 전에 Dungeon.I 소비 코드가 실행되지 않고, 이전 인스턴스 파괴가 새 singleton을 지우지 않는다.
- StageDungeonBuilder가 생성된 방을 콘텐츠 씬에 넣고 실패한 pendingRoot를 정리한다.
- UI·지도·Timeline·음악이 새 씬에 다시 연결되고 이벤트 구독이 짝을 이룬다.
- PaperCard의 독립 Visible, 장판·투사체·독·로프·구속·추적검·암전·카메라 상태가 퇴장 시 정리된다.
- Main 전용 Editor 메뉴·BuildPlayer 배열·테스트 진입점이 새 구조를 사용한다. 검증을 지워서 통과시키지 않는다.
- 2.5D 좌표 변환, 룸 스케일, RoomOrigin, ProjectileSpace와 장판 표시/피격의 계약이 보존된다.
- 전투 수치 변경이 섞였다면 씬 분리에 필요한 근거와 사용자 요청을 대조한다.

## 필수 회귀 시나리오

각 행에 실제 결과, 실행 방식, 로그/캡처 경로를 기록한다. 아래 행들은 아직 실행 결과가 없는 검수 명세다.

| 시나리오 | 기대 결과 |
| --- | --- |
| 빌드 실행 → MainMenu | 메뉴가 처음 나타나며 GameManager·EventSystem·활성 AudioListener가 중복되지 않음 |
| 메인 → 로비 → 메인 3회 | 로딩창·입력·볼륨 정상, 중복 UI·음악·이벤트 없음 |
| 로비 NPC·집·던전 입구 | 기존 선택/대화·무기/입장 흐름 연결 |
| 새 런 시작 → 훈련 → 초기 선택 → 첫 전투방 | 입장 절차와 초기화가 한 번씩 실행됨 |
| 생성형 첫 노드 → 6방 → 다음 노드 | seed·그래프·문 연결·완료 조건·보상 중복 방지 유지 |
| 같은 런의 서비스방·왕복 | 성흔 대상별 레벨·장착, 인장 중첩·스탯, 동전, 무기·HP·탄약·쿨다운 유지 정책 준수 |
| 로비 → Debug → 보스 선택 → 전투 → 귀환 → 재도전 | 보스·장치·장판·음악 중복 없음, 선택한 보스와 준비 상태 유지 |
| 무적 HealthOnly로 보스 피격 | 피해만 0, 구속·넉백·피격 반응 유지 |
| RobeMan·Cowboy·HauntedBull·Logan | 공격·CC·사망·투사체 정리와 프리팹 연결 정상 |
| 심판관 1페 → 2페 → 1% 발악 → 퇴장 | 체력바 이름·BGM·지형파괴·추적검·분신·바닥 복구 정상 |
| 보스 구속/암전/파괴 바닥 중 사망·재도전·귀환 | 입력 잠금·어둠·경계·음악·추적검이 다음 공간에 남지 않음 |
| 일시정지·대화·지도 열린 채 전환 요청 | 정의한 정책대로 닫거나 취소, timeScale/입력 잠금 복구 |
| 로딩 중 버튼 연타·실패·재시도 | 중복 씬/중복 상태 복원 없음, 오류 안내·복귀 가능 |
| MainMenu/Lobby/Game/Debug 각각 직접 Play | 누락 서비스가 초기화되거나 정해진 진입 경로가 안내됨 |
| 새 빌드 메뉴 → Windows 플레이어 | 씬 4종과 필요 서비스 씬이 실제 빌드에 포함됨 |

기존 RuntimeSmoke, AuthoringSmoke, GeneratedDungeonSmoke, PaladinTrialCheck와 몬스터 Checks는 현재 구조·호출 방식부터 읽는다. `EditableValidation.ValidateImportedProject`는 씬을 열고 `ProjectSetup.ValidateAndBuild`는 설정 변경과 빌드를 수행한다. 단순 읽기 검사로 취급하지 않는다. 열려 있는 미저장 씬을 훼손하지 않는 실행 환경을 선택한다.

## 결과 형식

```text
기준 커밋 / 결과 커밋 또는 작업 트리
구현한 씬과 각 진입점
GameManager 소유 데이터 / 씬 로컬 소유 데이터
전환 사유별 유지·초기화 결과
발견한 결함과 수정 파일·근거
컴파일 결과
회귀 시나리오별 통과 / 실패 / 미실행 및 증거
사용자가 Unity에서 추가로 연결해야 하는 항목
남은 문제와 검증하지 못한 범위
```

파일 생성이나 컴파일 성공만으로 모든 기능 정상이라고 보고하지 않는다. 플레이 검사가 제외되면 구현과 정적 검증까지 완료한 상태로 보고하고 실행 검증을 명시적으로 남긴다.

## 이번 하네스 제작에서 확인한 결과

2026-11-24 Snapshot으로 C# 174개, Assets 내 씬 4개, 해시 항목 932개를 수집했다. 현재 작업 트리와 Compare한 결과 조사 대상 게임 파일의 추가·삭제·변경은 0개이며 중복 Assets GUID와 누락된 활성 빌드 씬은 발견되지 않았다.

임시 기준 JSON에 추가·삭제·변경 상황을 각각 만들어 Compare의 세 목록이 예상 경로를 검출하는 것을 확인했다. 이 과정에서 절대 경로로 전달한 보고서 위치 처리도 수정하고 다시 확인했다. 실제 게임 파일을 변경해 검사하지 않았다. 이번 검증 범위는 하네스의 정적 비교 기능이며 Unity 컴파일·플레이·Windows 빌드는 미실행이다.
