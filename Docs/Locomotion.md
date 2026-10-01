# 사냥꾼 이동 애니메이션

## 조작과 재생

WASD 달리기: 기본 속도 5.6. Shift + WASD 걷기: 2.8. 두 속도 모두 기존 패시브 이동 보너스를 적용합니다. 걷기와 달리기는 같은 여섯 프레임 보행 루프를 이동 거리에 맞춰 다른 속도로 재생합니다. 별도의 달리기/걷기 원화 두 세트는 아닙니다.

네 방향 원화(정면, 오른쪽 측면, 오른쪽 뒤 대각, 뒷모습)와 좌우 반전을 사용합니다. 여덟 방향 입력을 판정하되 아래 대각선은 측면 원화를 공유합니다. 방향마다 Idle 1장 → 이동 루프 6장 → 정지 1장, 총 32장입니다. 정지 포즈는 약 0.12초 후 해당 방향의 Idle로 돌아갑니다.

입력 유지 시간이 아닌 충돌 처리 이후의 실제 이동 거리로 루프를 진행합니다. 한 주기는 2.4 게임 단위이며 이동 보너스에 따라 발동작도 빨라집니다. 벽에 막히면 발동작이 멈추고, 벽을 따라 미끄러질 때는 실제 진행 방향을 사용합니다. 작은 방향 흔들림에는 이전 방향을 유지합니다.

공격·스킬·회피 포즈가 이동보다 우선합니다. 동작이 끝나고 이동을 유지하면 보행으로 복귀합니다. 총의 마우스 조준 방향은 이동 그림 방향과 독립적입니다. 일시정지와 Timeline 중에는 이동 업데이트가 중단되고 방 진입 시 이전 보행 상태를 초기화합니다.

## 파일과 발 기준

- `Assets/Resources/Motion/HunterLocomotion.png`: 1536×1024, 8열×4행 원본 아틀라스.
- `Assets/Scripts/HunterLocomotion.cs`: 거리 기반 재생과 방향 선택, 아틀라스 로딩.
- `Assets/Scripts/Player.cs`: 실제 입력·충돌·전투 우선순위 연결.
- `Assets/Editor/MotionSetup.cs`: 32개 셀, 경계 침범, 고정 지면 기준, 재질 검사.
- `Assets/Scripts/MovementSmoke.cs`: 실행 파일의 이동 검증과 화면 캡처.

그림 크기는 첫 정면 Idle 높이에 맞춰 기존 1.9 단위를 유지하며 모든 프레임에 같은 스케일을 사용합니다. 방향별 지면 기준은 고정하여 발을 들 때 몸 전체가 내려앉는 현상을 방지합니다. 가로 위치는 모자/머리 영역으로 정렬해 원본 시트의 셀 내부 배치 오차를 보정합니다. 원본 PNG는 변경하지 않고 스프라이트 기준점으로 처리합니다. RGB 마젠타 배경은 기존 CartoonAtlas 셰이더로 제거하며 텍스처는 비압축·Bilinear를 사용합니다.

## 검증 방법

Windows 실행 파일에 `--movement-test`를 지정하면 방향, 대각선 속도, 여섯 보행 프레임, 정지, 걷기, 일시정지, 벽, 스킬·회피·무기 태그 복귀, 조준 분리, 방 진입과 스케일을 검사합니다. `Logs/movement-checks.txt`와 `Logs/movement-행-열.png` 32개에 결과를 저장합니다. 기존 스킬은 `--motion-test`, 던전과 Timeline은 `--smoke-test`로 확인합니다.

## 제작 도구와 프롬프트

최종 검증(2026-09-15): Windows 빌드 성공. 이동 80개, 기존 스킬 62개, 던전·전투·Timeline 통합 2100개 검사 통과(30방 / 50웨이브). 이동 32개 화면을 캡처했으며 앞·옆·뒤 대각·뒷모습의 실제 게임 표시를 확인했습니다. 대표 화면은 Previews/HunterMovementSide.png와 Previews/HunterMovementBack.png입니다.

내장 image_gen을 사용했습니다. 기존 HunterSkills.png는 캐릭터 정체성과 선화 참고이며, 첫 이동 시트를 만든 뒤 아래 프롬프트로 경계 여백과 보행을 보정했습니다. 채택된 결과는 Assets/Resources/Motion/HunterLocomotion.png에 보관합니다.

### 초기 생성 사양

Same western female hunter as HunterSkills.png, identical costume, proportions and flat western cartoon cel linework. Make one 1536×1024 atlas, exactly 8 columns and 4 rows. Rows: down/front, right side, up-right rear three-quarter, up/full rear. Columns: idle, left contact, left-weight passing, right knee flight, right contact, right-weight passing, left knee flight, stop. Six distinct alternating strides with opposite arm swing and coat/hair follow-through. Both weapons secured at belt. Solid #FF00FF background; no text, grid, effects, shadows, pixel art, dots, anime, chibi or realism. Preserve existing size and design.

### 최종 보정 프롬프트

Edit target image 1 is an 8-column 4-row locomotion sheet. Reference image 2 is character identity and western cel line style only. Preserve exact character, same costume, body proportions, colors, bold black outlines and all four directional rows. Fix production grid placement: output 1536x1024 EXACT 8 columns of192px, 4 rows of256px. Each of32 cells contains one complete figure scaled to around170px height, feet baseline local y218, head/torso centered local x96. Leave minimum20px pure magenta padding on EVERY side of EVERY cell. Current sheet incorrectly compresses horizontal spacing and row1 feet bleed into row2; FIX THIS. Center each figure within its OWN cell; cell center x=96,288,480,672,864,1056,1248,1440. Row baselines y=218,474,730,986. No ink may touch any cell boundary. Do NOT add gridlines.
Improve six-phase running cycle: cols2-7 must alternate both legs distinctly, not repeat the same leading leg. col1idle, col2LEFT foot contact RIGHT armforward, col3leftlegweighted rightkneepassing, col4rightkneeforward leftpushoff, col5RIGHT foot contact LEFT armforward, col6rightlegweighted leftkneepassing, col7leftkneeforward rightpushoff, col8stopfeetclose. Row1 front facing down; row2 side facingRIGHT; row3rear threequarter towardUPRIGHT; row4 fullREAR towardUP, noface. Upper body centered consistently, knees and opposite arms actually move through run. Hair and split coattails follow through. Bothweapons holstered.
Entire background PURE FLAT MAGENTA #FF00FF, no text, no ground, no shadows or effects. Character bold clean western animated cartoon flat cel colors, no anime/chibi/pixel/dots/realism. Same character scale in every cell, no repeated static poses. Make figures smaller than input1 to guarantee cell padding.
