# Western Lemegeton · 씬/프리팹 편집 안내

Unity 6000.3.17f1, Built-in Render Pipeline 기준입니다. `Assets/Scenes/Main.unity`를 열면 **Play를 누르기 전부터** 아래 오브젝트가 Hierarchy에 있습니다.

```
Main Camera
Game_Systems
World
  Hirva
  Room_01 ~ Room_06
    Ground_and_Paths
    Scenery
    Doors
    Gameplay_Markers
    Atmosphere
Actors
  Hunter
  Raven
  Spawned_Enemies
UI_Canvas
  Title
  Combat_HUD
  Route_Map
  Stigma_Build_Tree
  Devil_Card_Choice
  Pause / Settings / Results / Cinematic_Overlay
  Passive_Acquisition
EventSystem
Viewport_Backdrop
```

게임은 저장된 방과 캐릭터를 사용합니다. 새 런을 시작해도 이 오브젝트를 다시 만들지 않습니다. 현재 방문하지 않은 방과 팝업은 비활성 상태이며, 편집하려면 활성화하거나 해당 프리팹을 엽니다. 적과 획득 패시브 슬롯은 연결된 프리팹에서 필요할 때 생성됩니다.

## 패키지 가져오기

1. Unity 6.3의 Built-in 프로젝트에서 Package Manager의 **Unity UI 2.0.0** 및 **Timeline 1.8.12**를 준비합니다. 기존 프로젝트에는 먼저 백업을 권장합니다.
2. `WesternLemegeton_Editable.unitypackage`를 가져옵니다. 패키지는 Assets를 포함하며 Library, 빌드 파일, 외부 Spine 런타임은 포함하지 않습니다.
3. `Assets/Scenes/Main.unity`를 열고 Play 합니다. 빈 씬에서 자동으로 게임을 생성하는 기존 방식은 제거했습니다.
4. `Western Lemegeton > Authoring > Validate Saved Scene`으로 연결 상태를 확인합니다.
5. 빌드 설정 및 제공된 게임 로고 적용은 `Western Lemegeton > Configure Build Settings` 메뉴를 사용합니다. Unity 패키지는 ProjectSettings를 포함하지 않으므로 이 메뉴로 Main 씬과 실행 로고를 설정합니다.

이 프로젝트는 키보드·마우스의 기존 Input Manager API를 사용합니다. 새 프로젝트의 Active Input Handling이 New Input System Only인 경우 Both 또는 Input Manager로 설정하세요. URP 프로젝트에는 머티리얼 변환이 별도로 필요합니다.

## 배경과 문 편집

- `Assets/Editable/Rooms`에 마을과 여섯 방의 프리팹이 있습니다. `Scenery` 아래 오브젝트 부모를 움직이거나 크기를 조절합니다. `Visual`의 Sprite를 교체할 수 있습니다.
- 배경 지면은 **X/Z 평면**, 높이는 Y입니다. 씬의 3D 모드에서 편집하세요. 전투 계산의 XY 좌표와 표시 좌표를 연결하는 구조입니다.
- `WorldFootprint`의 Offset / Size가 해당 오브젝트의 이동을 막는 바닥 영역입니다. 선택하면 윤곽이 표시됩니다. 배치 부모를 이동하면 충돌 영역도 함께 이동하며 방 입장 때 읽어 적용합니다. 축에 정렬된 충돌 영역이며, 장식 이미지의 회전과 별도로 조절합니다.
- `Contact_Shadows`는 지면 접촉 그림자입니다. 기존 오브젝트를 유지하면 카메라에 맞춰 위치를 갱신합니다. 그림 모양이 크게 바뀌면 `WorldProp > Contacts`의 Anchor / Size / Color를 조정합니다.
- `Doors > To_Room_*`와 `Stage_Exit`는 문 그림, 봉인 표시, 도착 지점을 포함합니다. 문 전체 부모를 옮기세요. `Arrival`은 이 문을 통해 들어올 때 플레이어가 서는 위치입니다. 문 오브젝트를 삭제·재생성할 경우 RoomAuthoring의 Doors 배열과 Owner / Marker / Arrival을 다시 연결해야 합니다.
- `Gameplay_Markers`에서 최초 등장, 제단/카드 상호작용, 적 등장 위치를 조절합니다. 방 크기와 여섯 방의 연결 규칙은 현재 게임 규칙을 유지합니다.
- `Atmosphere`에는 실제 모래바람 오브젝트와 색조 레이어가 저장되어 있습니다. 입자별 Speed / Alpha와 배경 레이어의 색·크기를 조절할 수 있습니다.

## 캐릭터 / 까마귀 / 적 작업물 교체

프리팹 위치: `Assets/Editable/Actors`. Hunter / Raven / Enemy_0~2 / Boss를 편집합니다.

1. 캐릭터 프리팹의 `Custom_Visual_Spine_Or_Animator` 아래에 완성한 시각 프리팹을 자식으로 넣습니다. 원본 게임 로직 컴포넌트는 유지합니다.
2. 시각 작업물의 발바닥을 자식 로컬 원점에 맞추고, **자식**의 위치·크기로 비율을 맞춥니다. Custom Visual 부모의 위치·회전은 게임이 2.5D 표시와 좌우 반전을 위해 갱신합니다.
3. 루트 `ActorPresentation`에서 Use Custom Visual을 켭니다. 기본 스프라이트는 숨겨지고 Custom Visual이 표시됩니다. 끄면 기존 프레임 애니메이션으로 돌아갑니다.
4. Unity Animator 작업물은 Animator 슬롯을 연결하고 Animations 목록의 Animation 이름을 Animator 상태 이름에 맞춥니다. Idle / Walk / Run / Dodge / Attack01~04 / Slash / Slam / Shot / Barrage / Death / Entrance와 까마귀 상태를 연결할 수 있습니다.
5. 공격 판정, 대미지, 이동 거리는 기존 전투 로직이 관리합니다. 새 애니메이션의 타격 프레임이 다르면 전투 타이밍도 맞춰야 합니다. 이 작업은 아트 교체와 별도이며 자동으로 추정하지 않습니다.

### Spine SkeletonAnimation

실제 Spine 파일과 spine-unity 런타임은 제공되지 않아 패키지에 포함하지 않았습니다. 설치 후 사용할 연결 컴포넌트는 준비되어 있습니다.

1. 작업물을 내보낸 Spine 버전에 맞는 공식 spine-unity 런타임을 설치합니다.
2. Player Settings > Scripting Define Symbols에 `WNN_SPINE`을 추가합니다.
3. 루트에 `SpineAnimationDriver`를 추가하고, Custom Visual 아래의 SkeletonAnimation을 Skeleton 슬롯에 연결합니다.
4. ActorPresentation의 Animation Driver에 이 컴포넌트를 연결하고, Animations의 이름을 실제 Spine 애니메이션 이름으로 맞춥니다. Animator 슬롯은 이 경로에서는 비워 둡니다.
5. SkeletonMecanim 작업물은 위 Unity Animator 연결 방식을 사용할 수 있습니다.

커스텀 시스템을 쓰려면 `ActorAnimationDriver`를 구현하거나 Animation Requested 이벤트를 연결할 수 있습니다. 이동 속도와 일시정지 상태는 드라이버로 전달됩니다. 실제 Spine 작업물을 사용한 재생 검증은 해당 파일을 연결한 후 필요합니다.

공식 문서: https://esotericsoftware.com/spine-unity-main-components

## UI 작업물 교체

- `Assets/Editable/UI/GameUI.prefab`은 실제 Canvas / RectTransform / Image / Text / Button 구성입니다. 레이아웃, 이미지, 크기, 폰트는 Inspector에서 변경합니다.
- 시작할 때 Title만 표시합니다. 편집할 화면을 활성화하고 다른 팝업을 끄면 씬 뷰에서 편하게 확인할 수 있습니다. 런타임에는 상태에 맞는 화면만 표시합니다.
- 동적으로 바뀌는 무기·패시브 아이콘은 `VisualCatalog.asset`의 Sprite를 교체합니다. 고정 배경과 타이틀 이미지는 각 Image / RawImage에서 교체합니다.
- GameUIView의 Font Override에 한글 폰트를 넣으면 전체 UI에 적용합니다. 비어 있으면 기본 글꼴에 한해 운영체제의 Malgun Gothic을 사용하고, 개별 Text에 지정한 커스텀 폰트는 유지합니다.
- GameUIView의 Active / Unlocked / Inactive / Rarity Colors에서 상태색을 조절합니다. `PassiveSlot.prefab`은 획득 아이템 슬롯 모양입니다.
- 버튼의 On Click에는 UICommand.Execute와 명령 종류가 저장되어 있습니다. UI 오브젝트를 새것으로 통째로 교체하면 GameUIView의 References 및 패널 슬롯, Button의 On Click도 연결하세요. 단순 이미지·위치 교체는 연결을 바꿀 필요가 없습니다.
- 성흔 외부 진입점 `Dungeon.ApplySeongheunStack(type, increment)`, 패시브 진입점 `Dungeon.AcquirePassive(id, rarity, source)`는 유지됩니다.

## 저장 / 빌드 / 내보내기

씬 오버라이드는 씬에 저장하고, 공통 변경은 프리팹에 Apply 합니다. 정상 빌드와 내보내기는 저장된 배치를 이용하며 생성기로 다시 만들지 않습니다. `EditableProjectSetup.CreateEditableProject`는 최초 전환용이며 이미 방 프리팹이 있으면 덮어쓰기를 거부합니다.

실행 파일 빌드: `Western Lemegeton > Build Windows Player`.

내보내기: `Western Lemegeton > Authoring > Export Editable Unity Package`.
출력: `Builds/Packages/WesternLemegeton_Editable.unitypackage`.

게임 규칙, 공격 타이밍, 웨이브 구성까지 전부 데이터 에셋화한 것은 아닙니다. 이번 변경은 **씬 배치와 시각 작업물/UI 교체를 게임 로직에서 분리**하는 작업입니다.
