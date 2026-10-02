using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace WesternLemegeton
{
 [System.Serializable]public class UIReference{public string Key;public GameObject Target;}
 [DefaultExecutionOrder(500)]
 public sealed class GameUIView:MonoBehaviour
 {
  public GameObject TitlePanel,HudPanel,RoutePanel,SigilPanel,CardsPanel,PausePanel,SettingsPanel,ResultsPanel,CinematicPanel,ToastPanel;
  public VisualCatalog Art;
  public Font FontOverride;
  public string[] SystemFonts={"Malgun Gothic","Arial"};
  public Color ActiveColor=new Color(1,.68f,.27f),UnlockedColor=new Color(.35f,.9f,.94f),InactiveColor=new Color(.42f,.38f,.42f);
  public Color[] RarityColors={new Color(.88f,.88f,.91f),new Color(.28f,.65f,1),new Color(.77f,.43f,1)};
  public List<UIReference> References=new List<UIReference>();
  public RectTransform PassiveContent;public PassiveSlotView PassivePrefab;public Text PassiveTooltip;
  [Header("Crow link comic bubble")]
  [Min(0)] public float CrowPromptHeight=1.45f;
  public Vector2 CrowPromptOffset=new Vector2(18,-6);
  public AnimationCurve CrowPromptPop=new AnimationCurve(new Keyframe(0,.28f),new Keyframe(.11f,1.17f),new Keyframe(.19f,.94f),new Keyframe(.26f,1));
  public const string ControlHelp="WASD 달리기 · 좌클릭 공격\nQ 무기 태그 · SPACE 회피 · E / R 기술\n왼쪽 쉬프트 까마귀 소환·회수 (최대 60초)\nF 연계 공격 · M 지도";
  readonly Dictionary<string,GameObject> refs=new Dictionary<string,GameObject>();readonly List<PassiveSlotView> slots=new List<PassiveSlotView>();
  Canvas canvas;float crowPromptAge;bool crowPromptOffered;int crowPromptTarget;
  readonly Vector3[] crowPromptCorners=new Vector3[4];
  readonly string[] buildIcons={"FireIcon","NatureIcon","Raven"},buildNames={"잿불","격노","검은 날개"};
  readonly string[] effects={"공격에 화상을 더해 지속 피해를 줍니다. 각인이 늘수록 화상 피해가 강해집니다.","무기 태그 후 일시적으로 공격력이 증가합니다. 각인이 늘수록 강화량이 커집니다.","까마귀의 일반 공격과 연계 공격을 강화합니다."};
  public GameObject Object(string key)=>refs.TryGetValue(key,out var value)?value:null;
  public T Element<T>(string key) where T:Component {var o=Object(key);return o?o.GetComponent<T>():null;}
  void Awake(){foreach(var r in References)if(r.Target)refs[r.Key]=r.Target;canvas=GetComponentInParent<Canvas>();var font=FontOverride?FontOverride:Font.CreateDynamicFontFromOSFont(SystemFonts,18);foreach(var t in GetComponentsInChildren<Text>(true))if(FontOverride||!t.font||t.font.name=="LegacyRuntime")t.font=font;Text("SkillKey0","Q");Text("Controls",ControlHelp);Show("CrowPrompt",false);}
  void Text(string key,string text){var t=Element<Text>(key);if(t)t.text=text;}
  void Show(string key,bool show){var o=Object(key);if(o&&o.activeSelf!=show)o.SetActive(show);}
  static void Panel(GameObject o,bool on){if(o&&o.activeSelf!=on)o.SetActive(on);}
  void Image(string key,string icon){var image=Element<Image>(key);if(image)image.sprite=Art.Get(icon);}
  void Color(string key,Color color){var graphic=Element<Graphic>(key);if(graphic)graphic.color=color;}
  void Fill(string key,float value){var image=Element<Image>(key);if(image)image.fillAmount=Mathf.Clamp01(value);}
  static string Rarity(PassiveRarity r)=>r==PassiveRarity.Common?"일반":r==PassiveRarity.Uncommon?"희귀":"레어";
  static string Source(PassiveSource s)=>s==PassiveSource.MonsterDrop?"몬스터 드롭":s==PassiveSource.ShopPurchase?"상점 구매":"이벤트 보상";
  void LateUpdate()
  {
   var g=Dungeon.I;if(!g||!g.Hero)return;var h=g.GetComponent<GameHUD>();var p=g.Hero;bool cinematic=g.State==RunState.Cinematic;
   Panel(TitlePanel,g.State==RunState.Title);Panel(HudPanel,g.State!=RunState.Title&&!cinematic);Panel(RoutePanel,g.State==RunState.Route);
   Panel(SigilPanel,g.State==RunState.SigilChoice||h.BuildTreeOpen);Panel(CardsPanel,g.State==RunState.CardChoice);
   Panel(PausePanel,g.State==RunState.Paused&&!h.BuildTreeOpen);Panel(SettingsPanel,h.SettingsOpen);
   Panel(ResultsPanel,g.State==RunState.Dead||g.State==RunState.Victory);Panel(CinematicPanel,cinematic);
   Text("Location",g.IsTown?"HIRVA / 히르바":$"스테이지 {g.Room+1:00} · 방 {g.MapIndex+1}/6 / {Dungeon.MapNames[g.MapIndex]}");
   Text("Notice",g.Notice);Text("Supplies",$"보급품 {g.Supplies}\n방 완료 {g.MapsCleared} / 6");
   Show("Wave",!g.IsTown&&g.CurrentKind==ExplorationRoomKind.Combat);Text("WaveText",$"WAVE {g.WaveIndex+1} / {g.WavesInMap} · 남은 적 {g.Enemies.Count}");
   Show("WaveBreak",g.State==RunState.WaveBreak);Text("WaveBreakText",$"다음 WAVE {g.WaveIndex+1}\n{Mathf.CeilToInt(g.WaveCountdown)}초 후 적이 접근합니다");
   Text("CombatStatus",$"회피 {(p.DashCd>0?p.DashCd.ToString("F1")+"s":"●")}  COMBO {p.HitCombo}  {(p.Fury>0?"태그 강화":"")}");
   Fill("HP",p.Hp/100);Text("HPText",$"{Mathf.CeilToInt(p.Hp)} / 100");Fill("Ammo",p.Ammo/6f);Text("AmmoText",p.Reload>0?$"장전 {p.Reload:F1}s · {p.Ammo}/6":$"리볼버 {p.Ammo}/6");
   Skill(0,p.Weapon==0?"SlashIcon":"ShotIcon",p.TagCd,p.Weapon==0?"단검":"리볼버");Skill(1,"HunterDash",p.DashCd,"회피");Skill(2,p.Weapon==0?"SlashIcon":"ShotIcon",p.Skill2Cd,p.Weapon==0?"베어 가르기":"원 샷");Skill(3,p.Weapon==0?"SlamIcon":"BarrageIcon",p.Skill3Cd,p.Weapon==0?"내려찍기":"난사");
   Skill(4,"Raven",0,g.Crow.IsSummoned?$"회수 {Mathf.CeilToInt(g.Crow.SummonRemaining)}초":"소환 60초");Skill(5,"RavenPortrait",g.Crow.LinkCooldown,g.Crow.LinkWindow>0?$"연계 {g.Crow.LinkWindow:F1}s":"연계 대기");
   for(int i=0;i<3;i++)
   {
    var type=(SeongheunType)i;int stack=g.Build.Stack(type);bool active=g.Build.IsActive(type);float pulse=h.StackPulse(type);
    Text("Stack"+i,"각인 "+stack);Text("RouteStack"+i,stack+" 각인");
    Color("StigmaBorder"+i,pulse>0?UnityEngine.Color.white:active?ActiveColor:InactiveColor);Color("StigmaIcon"+i,active?UnityEngine.Color.white:new Color(.5f,.5f,.55f));
    var icon=Object("StigmaPulse"+i);if(icon)icon.transform.localScale=Vector3.one*(1+(pulse>0?Mathf.Sin((1-pulse/.25f)*Mathf.PI)*.22f:0));
   }
   Prompt(g);Map(g);Sigil(g,h);Passives(g,h);
   Text("ResultsTitle",g.State==RunState.Victory?"봉인이 무너졌다":"다시, 잿빛 황야로");Text("ResultsDetail",$"도달 {g.Room+1} / 5 · 처치 {g.Kills} · {(int)g.RunTime}초");
   Show("Boss",g.IsBossWave&&g.Enemies.Count>0);if(g.IsBossWave&&g.Enemies.Count>0&&g.Enemies[0])Fill("BossHP",g.Enemies[0].Hp/g.Enemies[0].MaxHp);
   CrowLinkPrompt(g);
   if(cinematic)
   {
    var c=g.Cinematics;var top=Element<CanvasGroup>("CinemaBars");if(top)top.alpha=c.Letterbox;
    var title=Element<CanvasGroup>("CinemaTitle");if(title)title.alpha=c.TitleOpacity;
    var shade=Element<CanvasGroup>("CinemaShade");if(shade)shade.alpha=c.Shade;
    Text("CinemaHeading",c.Heading);Text("CinemaCaption",c.Caption);Text("CinemaHint",c.IsPaused?"P 계속 · SPACE 건너뛰기":"SPACE / ESC 건너뛰기 · P 일시정지");
   }
  }
  void Skill(int i,string icon,float cooldown,string label){Image("SkillIcon"+i,icon);Text("SkillLabel"+i,label);Show("SkillCooldown"+i,cooldown>0);Text("SkillCooldownText"+i,cooldown.ToString("F1"));}
  void CrowLinkPrompt(Dungeon g)
  {
   // The automatic four-hit combo owns its world PNG. Only the optional F offer
   // belongs in this bubble; its lifetime is controlled by Raven.LinkWindow.
   var prompt=Element<RectTransform>("CrowPrompt");if(!prompt)return;
   bool offered=g.Crow.LinkWindow>0&&g.Running&&g.Hero.Hp>0;
   var camera=g.Cam;var parent=prompt.parent as RectTransform;
   if(!offered||!camera||!parent){HideCrowPrompt(prompt);return;}
   var world=PaperWorld.Point(g.Hero.transform.position)+camera.transform.up*CrowPromptHeight;
   var screen=camera.WorldToScreenPoint(world);
   if(screen.z<=0||!camera.pixelRect.Contains(new Vector2(screen.x,screen.y))){HideCrowPrompt(prompt);return;}
   var uiCamera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
   if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,uiCamera,out var local)){HideCrowPrompt(prompt);return;}
   int target=g.Crow.LinkTarget?g.Crow.LinkTarget.GetInstanceID():0;
   if(!crowPromptOffered||target!=crowPromptTarget)crowPromptAge=0;
   else crowPromptAge+=Time.unscaledDeltaTime;
   crowPromptOffered=true;crowPromptTarget=target;
   prompt.localPosition=new Vector3(local.x+CrowPromptOffset.x,local.y+CrowPromptOffset.y,0);
   // The tail is the pivot: the portrait pops from the player's head, rather than
   // scaling a text banner around the middle of the screen. The key is part of it.
   float pop=crowPromptAge<.26f?CrowPromptPop.Evaluate(crowPromptAge):1+Mathf.Sin((crowPromptAge-.26f)*4)*.025f;
   prompt.localScale=Vector3.one*Mathf.Max(.05f,pop);
   float tilt=crowPromptAge<.4f?Mathf.Sin(crowPromptAge*28)*8*Mathf.Exp(-crowPromptAge*8):Mathf.Sin((crowPromptAge-.4f)*3)*1.4f;
   prompt.localRotation=Quaternion.Euler(0,0,tilt);
   Show("CrowPrompt",true);KeepCrowPromptOnScreen(prompt,parent,camera.pixelRect,uiCamera);
  }
  void HideCrowPrompt(RectTransform prompt)
  {
   Show("CrowPrompt",false);crowPromptOffered=false;crowPromptAge=0;crowPromptTarget=0;
   prompt.localScale=Vector3.one;prompt.localRotation=Quaternion.identity;
  }
  void KeepCrowPromptOnScreen(RectTransform prompt,RectTransform parent,Rect viewport,Camera uiCamera)
  {
   prompt.GetWorldCorners(crowPromptCorners);
   Vector2 min=Vector2.positiveInfinity,max=Vector2.negativeInfinity;
   foreach(var corner in crowPromptCorners){var point=RectTransformUtility.WorldToScreenPoint(uiCamera,corner);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
   const float margin=12;
   float dx=min.x<viewport.xMin+margin?viewport.xMin+margin-min.x:max.x>viewport.xMax-margin?viewport.xMax-margin-max.x:0;
   float dy=min.y<viewport.yMin+margin?viewport.yMin+margin-min.y:max.y>viewport.yMax-margin?viewport.yMax-margin-max.y:0;
   if(Mathf.Abs(dx)+Mathf.Abs(dy)<.01f)return;
   var screen=RectTransformUtility.WorldToScreenPoint(uiCamera,prompt.position);
   if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen+new Vector2(dx,dy),uiCamera,out var local))prompt.localPosition=new Vector3(local.x,local.y,0);
  }
  void Prompt(Dungeon g)
  {
   string message="";if(g.Running)
   {
    if(g.IsTown){if(Vector2.Distance(g.Pos,RoomAuthoring.Point(g.Scene.Hirva.TownExit))<3)message="마우스 우클릭 · 황야로 출발";}
    else{var door=g.NearbyDoor();if(door)message=door.StageExit?(g.Progress.SegmentComplete?"마우스 우클릭 · 스테이지 출구 / 다음 여정 선택":$"출구 봉인 · 미완료 방 {6-g.MapsCleared}개"):(g.Progress.CanLeave?$"마우스 우클릭 · {Dungeon.MapNames[door.Destination]} 이동":"문 봉인 · 이 방의 모든 적을 처치하세요");else if(g.CurrentKind!=ExplorationRoomKind.Combat&&Vector2.Distance(g.Pos,RoomAuthoring.Point(g.CurrentRoom.Layout.Authored.ServicePoint))<2.4f)message=g.Progress.MapComplete?"이 방의 보상을 선택했습니다":g.CurrentKind==ExplorationRoomKind.Sigil?"마우스 우클릭 · 성흔 제단 / 각인 강화":"마우스 우클릭 · 악마카드 진열대 / 카드 선택";}
   }
   Show("Prompt",message!="");Text("PromptText",message);
  }
  void Map(Dungeon g)
  {
   for(int i=0;i<6;i++)
   {
    bool current=!g.IsTown&&g.MapIndex==i,done=!g.IsTown&&g.Progress.Cleared(i),visited=!g.IsTown&&g.Progress.Visited(i);
    var color=current?UnlockedColor:done?ActiveColor:visited?new Color(.97f,.89f,.72f):InactiveColor;
    Color("MiniNode"+i,color);Color("RouteNode"+i,color);
    string kind=RoomGraph.Kind(i)==ExplorationRoomKind.Sigil?"성흔 강화":RoomGraph.Kind(i)==ExplorationRoomKind.DevilCards?"악마카드 선택":"전투";
    Text("RouteNodeText"+i,$"{i+1:00} {Dungeon.MapNames[i]}\n{kind} · {(current?"현재 위치":done?"완료":visited?"미완료":"미방문")}");
   }
   Show("RouteTravel",g.RouteTravel);Text("RouteStage",g.IsTown?"여정의 시작":$"STAGE {g.Room+1:00}");Text("RouteName",g.IsTown?"히르바 → 붉은 모래길":Dungeon.RoomNames[g.Room]);Text("RouteProgress",$"방 완료 {g.MapsCleared}/6 · 방문 {g.Progress.VisitedMaps}/6");
   Fill("RouteHP",g.Hero.Hp/100);Text("RouteHPText",$"HP {Mathf.CeilToInt(g.Hero.Hp)} / 100");Text("RouteBuild",$"악마카드 {g.Build.Passives.Count}장\n공격 +{g.Build.PlayerDamageBonus:P0} · 이동 +{g.Build.MoveSpeedBonus:P0}\n까마귀 공격 +{g.Build.RavenDamageBonus:P0}");
   Text("RouteTravelText",g.IsTown?"황야로 출발":g.Room==4?"탐험 완료":"다음 스테이지로 이동");Text("RouteHint",g.RouteTravel?g.Room==4&&!g.IsTown?"모든 봉인을 해제했습니다.":"다음: "+Dungeon.RoomNames[g.NextRoom]:"여섯 방을 완료한 뒤 출구 문에서 다음 스테이지로 이동할 수 있습니다.");
  }
  void Sigil(Dungeon g,GameHUD h)
  {
   int i=h.SelectedSigil;var type=(SeongheunType)i;int stack=g.Build.Stack(type);bool claim=g.State==RunState.SigilChoice;
   Image("SigilCenter",buildIcons[i]);Image("SigilDetailIcon",buildIcons[i]);Text("SigilName",buildNames[i]);Text("SigilStack",$"각인 {stack} · {(g.Build.IsActive(type)?"활성":"대기")}");Text("SigilEffect",effects[i]);Text("SigilThreshold",$"첫 활성화: {g.Build.Threshold(type)} 각인\n강화 노드: {g.Build.Threshold(type)+2} 각인");Show("SigilClaim",claim);Text("SigilClaimText",buildNames[i]+$" 각인 +1 ({stack} → {stack+1})");
   for(int n=0;n<6;n++){var t=(SeongheunType)(n/2);int needed=g.Build.Threshold(t)+(n%2)*2;Color("SigilNode"+n,g.Build.Stack(t)>=needed?UnlockedColor:InactiveColor);Text("SigilNodeText"+n,buildNames[n/2]+" · "+needed+" 각인");}
   for(int n=0;n<3;n++){Color("CardBorder"+n,RarityColors[(int)g.CardRarity]);Text("CardRarity"+n,Rarity(g.CardRarity)+" · 악마카드");Text("CardEffect"+n,(n==0?"플레이어 공격력":n==1?"이동 속도":"까마귀 공격력")+" +"+((n==2?.12f:.08f)*(1+.5f*(int)g.CardRarity)).ToString("P0"));}
  }
  void Passives(Dungeon g,GameHUD h)
  {
   var inventory=g.Build.Passives;Text("PassiveCount",$"획득 패시브 {inventory.Count} · 성흔과 별도");Show("PassiveEmpty",inventory.Count==0);
   if(slots.Count>inventory.Count){foreach(var slot in slots)if(slot)Destroy(slot.gameObject);slots.Clear();}
   while(slots.Count<inventory.Count)
   {
    var item=inventory[slots.Count];var slot=Instantiate(PassivePrefab,PassiveContent);slot.name=item.Definition.Id;slot.Icon.sprite=Art.Get(item.Definition.Icon);slot.Border.color=RarityColors[(int)item.Rarity];slot.Tooltip=PassiveTooltip;slot.Description=item.Definition.Name+" · "+Rarity(item.Rarity)+" · "+Source(item.Source);slots.Add(slot);
   }
   bool show=h.Toast!=null&&g.State!=RunState.Route&&g.State!=RunState.Cinematic&&!h.SettingsOpen;Panel(ToastPanel,show);if(show){Image("ToastIcon",h.Toast.Definition.Icon);Text("ToastName",h.Toast.Definition.Name);Text("ToastDetail",Rarity(h.Toast.Rarity)+" · "+Source(h.Toast.Source));Color("ToastBorder",RarityColors[(int)h.Toast.Rarity]);var group=ToastPanel.GetComponent<CanvasGroup>();if(group)group.alpha=h.ToastOpacity;}
  }
 }
}
