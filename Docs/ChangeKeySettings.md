# 10/10 키 세팅 및 기타 변경사항

## 1. 키 세팅 변경
### 1-1. 헌터 (Player.cs)
-1번 스킬 2->E
-2번 스킬 3->R
```
if (Input.GetKeyDown(KeyCode.E)) TrySkill(0);
if (Input.GetKeyDown(KeyCode.R)) TrySkill(1);
```
E을 누를 시 TrySkill(0) == ﻿무기 스킬 1을 사용하며, 기존에는 상단 숫자 키 2 키였습니다
R을 누를 시 TrySkill(1) == ﻿무기 스킬 2을 사용하며, 기존에는 상단 숫자 키 3 키였습니다


### 1-2. 까마귀 (Raven.cs)
-까마귀 소환 1->왼쪽 쉬프트
-까마귀 연계 R->F
```
if (Input.GetKeyDown(KeyCode.LeftShift)) g.Crow.Command();
if (Input.GetKeyDown(KeyCode.F)) g.Crow.TryLink();
```
왼쪽 쉬프트를 누를 시 Crow.Command() == ﻿까마귀 소환을 사용하며, 기존에는 상단 숫자 키 1 키였습니다
F을 누를 시 Crow.TryLink() == ﻿까마귀 연계 스킬을 사용하며, 기존에는 영키 R 키였습니다


### 1-3. 던전 (Dungeon.cs)
-상호작용 R->F
```
if (Input.GetKeyDown(KeyCode.F) && TryTravel()) return;
```
상호작용이 가능한 상황에서 F을 누를 시 TryTravel() == ﻿상호작용 함수를 실행하며, 기존에는 영키 E 키였습니다
TryTravel()로 상호작용이 가능한 조건을 불러오고 해당 조건 중 10/10 기준 전투중에 조건이 만족되는 경우는 없기 때문에 전투중에는 상호작용이 실행되지 않습니다
또한 


## 2. 기타 변경 사항
### 2-1. 까마귀 (Raven.cs)
-까마귀 소환 유지 시간 5s->60s
```
public void Command(){if(Cooldown>0||!Dungeon.I.Running||Dungeon.I.IsTown)return;Active=60;Cooldown=10;attack=0;Ink.Ring(transform.position,1.1f,Ink.Cyan);}
```
Active는 까마귀 소환의 유지시간으로 Active=Mathf.Max(0,Active-dt); 코드에 의해 dt, 즉 현실 시간으로 1초가 지날때마다 1씩 감소하는 코드입니다
Cooldown=10; 코드는 카마귀 소환시간을 정하는 변수로 if(Cooldown>0||!Dungeon.I.Running||Dungeon.I.IsTown)return; 코드중 return에 의해 까마귀를 소환시 Cooldown의 값을 10으로 초기화 합니다


-까마귀 소환 시간 도중 쿨타임이 감소하지 않도록 변경
```
if (Active == 0 && Cooldown > 0)
{
Cooldown = Mathf.Max(0, Cooldown - dt);
}
```
까마귀 소환의 유지시간이 0 이고(Active == 0) 쿨타임이 0보다 클때(Cooldown > 0) Cooldown 변수가 Cooldown = Mathf.Max(0, Cooldown - dt); 코드에 의해 dt, 즉 현실 시간으로 1초가 지날때마다 1씩 감소하는 코드입니다




