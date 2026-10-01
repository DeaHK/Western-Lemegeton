using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
namespace WesternLemegeton.EditorTools
{
 public sealed class EditableUIBaker
 {
  GameUIView view;Font font;
  readonly Color paper=new Color(.97f,.89f,.72f),muted=new Color(.71f,.64f,.64f),panel=new Color(.09f,.055f,.105f,.94f);
  public GameUIView Build(VisualCatalog catalog)
  {
   var go=new GameObject("UI_Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
   var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=.5f;
   view=go.AddComponent<GameUIView>();view.Art=catalog;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
   var title=Panel("Title",false);view.TitlePanel=title.gameObject;
   var art=Rect(title,"Title_Artwork",new Rect(0,0,1280,720)).gameObject.AddComponent<RawImage>();art.texture=CombatArt.Texture("Title");art.raycastTarget=false;
   Button(title,"Start",new Rect(34,435,263,97),"",GameUIAction.EnterTown,-1,true);
   Button(title,"New_Game",new Rect(39,546,267,71),"",GameUIAction.EnterTown,-1,true);
   Button(title,"Title_Settings",new Rect(49,635,239,70),"",GameUIAction.OpenSettings,-1,true);
   Label(title,"Title_Caption",new Rect(947,654,316,48),"히르바 · 저주받은 황야",18);
   HUD();Route();Sigil();Cards();PauseAndSettings();Cinematic();
   var toast=Panel("Passive_Acquisition",false);view.ToastPanel=toast.gameObject;toast.gameObject.AddComponent<CanvasGroup>();
   Frame(toast,"ToastBorder",new Rect(458,104,430,91),Ink.Gold);Icon(toast,"ToastIcon",new Rect(472,117,65,65),"BulletIcon");Label(toast,"ToastName",new Rect(554,117,315,30),"악마카드 획득",19);Label(toast,"ToastDetail",new Rect(554,152,315,26),"일반 · 이벤트 보상",14,muted);
   view.HudPanel.SetActive(false);view.RoutePanel.SetActive(false);view.SigilPanel.SetActive(false);view.CardsPanel.SetActive(false);view.PausePanel.SetActive(false);view.SettingsPanel.SetActive(false);view.ResultsPanel.SetActive(false);view.CinematicPanel.SetActive(false);view.ToastPanel.SetActive(false);
   PassivePrefab();return view;
  }
  void Bind(string key,GameObject o){view.References.Add(new UIReference{Key=key,Target=o});}
  RectTransform Rect(Transform parent,string name,Rect r)
  {
   var t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();t.SetParent(parent,false);t.anchorMin=t.anchorMax=new Vector2(0,1);t.pivot=new Vector2(0,1);t.anchoredPosition=new Vector2(r.x,-r.y);t.sizeDelta=r.size;return t;
  }
  RectTransform Panel(string name,bool dark=true)
  {var t=Rect(view.transform,name,new Rect(0,0,1280,720));if(dark){var image=t.gameObject.AddComponent<Image>();image.color=new Color(.025f,.015f,.04f,.985f);image.raycastTarget=true;}return t;}
  Image Box(Transform p,string name,Rect r,Color color,bool hit=false)
  {var t=Rect(p,name,r);var image=t.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=hit;Bind(name,t.gameObject);return image;}
  Image Frame(Transform p,string name,Rect r,Color color)
  {var edge=Box(p,name,r,color);Box(edge.transform,name+"_Fill",new Rect(2,2,r.width-4,r.height-4),panel);return edge;}
  Text Label(Transform p,string name,Rect r,string value,int size=17,Color? color=null,TextAnchor anchor=TextAnchor.UpperLeft)
  {var t=Rect(p,name,r);var label=t.gameObject.AddComponent<Text>();label.font=font;label.fontSize=size;label.text=value;label.color=color??paper;label.alignment=anchor;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;Bind(name,t.gameObject);return label;}
  Image Icon(Transform p,string name,Rect r,string icon)
  {var image=Box(p,name,r,Color.white);image.sprite=view.Art.Get(icon);image.preserveAspect=true;return image;}
  Button Button(Transform p,string name,Rect r,string label,GameUIAction action,int parameter=-1,bool transparent=false)
  {
   var image=Box(p,name,r,transparent?Color.clear:new Color(.26f,.15f,.21f),true);var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
   var command=image.gameObject.AddComponent<UICommand>();command.Action=action;command.Parameter=parameter;UnityEventTools.AddPersistentListener(button.onClick,command.Execute);
   Label(image.transform,name+"Text",new Rect(5,4,r.width-10,r.height-8),label,17,null,TextAnchor.MiddleCenter);return button;
  }
  void Bar(Transform p,string name,Rect r,Color color,string text)
  {
   Box(p,name+"_Background",new Rect(r.x-3,r.y-3,r.width+6,r.height+6),panel);var fill=Box(p,name,r,color);fill.sprite=EditableAssetStore.Save(Ink.Square);fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;
   Label(p,name+"Text",new Rect(r.x,r.y-4,r.width,r.height+8),text,14,null,TextAnchor.MiddleCenter);
  }
  void Line(Transform parent,string name,Vector2 a,Vector2 b,Color color,float thickness=3)
  {var image=Box(parent,name,new Rect(a.x,a.y,Vector2.Distance(a,b),thickness),color);var rt=image.rectTransform;rt.pivot=new Vector2(0,.5f);rt.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);}
  void HUD()
  {
   var p=Panel("Combat_HUD",false);view.HudPanel=p.gameObject;
   var texture=new Texture2D(128,72,TextureFormat.RGBA32,false);texture.name="HUD_Vignette";
   for(int y=0;y<72;y++)for(int x=0;x<128;x++){float d=new Vector2((x-63.5f)/75,(y-35.5f)/43).magnitude;texture.SetPixel(x,y,new Color(.025f,.008f,.05f,Mathf.SmoothStep(0,.82f,Mathf.InverseLerp(.45f,1.15f,d))));}texture.Apply();
   var vignette=Rect(p,"Vignette",new Rect(0,0,1280,720)).gameObject.AddComponent<RawImage>();vignette.texture=EditableAssetStore.Save(texture);vignette.raycastTarget=false;
   Label(p,"Location",new Rect(25,20,840,36),"스테이지 01 · 방 1/6 / 붉은 바위 입구",27);Label(p,"Notice",new Rect(27,59,850,28),"문을 향해 이동하세요",14,muted);
   Label(p,"PassiveCount",new Rect(27,98,460,24),"획득 패시브 0 · 성흔과 별도",14,muted);Label(p,"PassiveEmpty",new Rect(28,126,400,25),"획득한 아이템이 없습니다",14,muted);
   var scroll=Rect(p,"Passive_Inventory",new Rect(25,124,425,67)).gameObject.AddComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;
   var viewport=Rect(scroll.transform,"Viewport",new Rect(0,0,425,67));viewport.gameObject.AddComponent<RectMask2D>();scroll.viewport=viewport;
   var content=Rect(viewport,"Owned_Passives",new Rect(0,0,425,54));view.PassiveContent=content;scroll.content=content;
   var layout=content.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=7;layout.childControlWidth=false;layout.childControlHeight=false;layout.childForceExpandHeight=false;layout.childForceExpandWidth=false;
   var fit=content.gameObject.AddComponent<ContentSizeFitter>();fit.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;
   view.PassiveTooltip=Label(p,"PassiveTooltip",new Rect(27,197,510,31),"",14);view.PassiveTooltip.gameObject.SetActive(false);
   var mini=Frame(p,"Minimap",new Rect(1063,16,190,145),muted*.7f);Vector2[] points=new Vector2[6];
   for(int i=0;i<6;i++)points[i]=new Vector2(19+RoomGraph.Centers[i].x/120*147,121-RoomGraph.Centers[i].y/88*103);
   for(int i=0;i<RoomGraph.Links.GetLength(0);i++)Line(mini.transform,"MiniLink"+i,points[RoomGraph.Links[i,0]],points[RoomGraph.Links[i,1]],muted*.4f,2);
   for(int i=0;i<6;i++){var at=points[i];var node=Frame(mini.transform,"MiniNode"+i,new Rect(at.x-15,at.y-10,30,20),muted);Label(node.transform,"MiniLabel"+i,new Rect(0,0,30,20),i==2?"성":i==3?"카":(i+1).ToString(),12,null,TextAnchor.MiddleCenter);}
   Button(p,"Map_Button",new Rect(1063,165,190,26),"M · 탐험 지도",GameUIAction.OpenMap);
   Box(p,"Supply_Panel",new Rect(1110,199,143,72),panel);Label(p,"Supplies",new Rect(1121,210,128,56),"보급품 0\n방 완료 0 / 6",14,muted);
   var wave=Box(p,"Wave",new Rect(491,91,343,40),panel);Label(wave.transform,"WaveText",new Rect(12,7,319,31),"WAVE 1 / 3 · 남은 적 3");
   var waveBreak=Box(p,"WaveBreak",new Rect(436,255,410,90),panel);Label(waveBreak.transform,"WaveBreakText",new Rect(20,12,370,65),"다음 웨이브",24,null,TextAnchor.MiddleCenter);
   Label(p,"CombatStatus",new Rect(27,605,360,25),"회피 ● · COMBO 0",14,muted);Bar(p,"HP",new Rect(27,638,322,16),Ink.Red,"100 / 100");Bar(p,"Ammo",new Rect(27,664,322,13),Ink.Cyan,"리볼버 6 / 6");
   string[] keys={"Q","SPACE","2","3","1","R"},icons={"SlashIcon","HunterDash","SlashIcon","SlamIcon","Raven","RavenPortrait"};
   for(int i=0;i<6;i++)
   {float x=382+i*87;Label(p,"SkillKey"+i,new Rect(x,587,88,22),keys[i],13,muted);Box(p,"SkillFrame"+i,new Rect(x,610,69,67),panel);Icon(p,"SkillIcon"+i,new Rect(x+6,617,57,52),icons[i]);var cd=Box(p,"SkillCooldown"+i,new Rect(x,610,69,67),new Color(0,0,0,.67f));Label(cd.transform,"SkillCooldownText"+i,new Rect(2,20,65,31),"0.0",20,null,TextAnchor.MiddleCenter);Label(p,"SkillLabel"+i,new Rect(x-6,684,96,28),"기술",14,muted);}
   string[] stigma={"FireIcon","NatureIcon","Raven"};
   for(int i=0;i<3;i++)
   {float x=1008+i*87;var pulse=Rect(p,"Stigma_"+i,new Rect(x-30,608,60,60));pulse.pivot=new Vector2(.5f,.5f);pulse.anchoredPosition=new Vector2(x,-638);Bind("StigmaPulse"+i,pulse.gameObject);Frame(pulse,"StigmaBorder"+i,new Rect(0,0,60,60),muted);Icon(pulse,"StigmaIcon"+i,new Rect(6,6,48,48),stigma[i]);Button(pulse,"Inspect_Stigma"+i,new Rect(0,0,60,60),"",GameUIAction.OpenTree,-1,true);Label(p,"Stack"+i,new Rect(x-30,675,84,25),"각인 0",14,muted);}
   var prompt=Box(p,"Prompt",new Rect(370,530,540,44),panel);Label(prompt.transform,"PromptText",new Rect(9,6,522,32),"E · 상호작용",17,Ink.Gold,TextAnchor.MiddleCenter);
   var crow=Box(p,"CrowPrompt",new Rect(480,470,320,38),panel);Label(crow.transform,"CrowPromptText",new Rect(8,4,304,30),"R · 까마귀 연계",16,Ink.Cyan,TextAnchor.MiddleCenter);
   var boss=Rect(p,"Boss",new Rect(415,137,450,22));Bind("Boss",boss.gameObject);Bar(boss,"BossHP",new Rect(0,0,450,14),Ink.Red,"봉인의 파수꾼");
  }
  void Route()
  {
   var p=Panel("Route_Map");view.RoutePanel=p.gameObject;Label(p,"Route_Title",new Rect(38,25,850,44),"황야 탐험 지도",27);Label(p,"Route_Legend",new Rect(40,72,845,30),"청록: 현재 위치 · 금색: 완료 · 밝은색: 방문 · 회색: 미방문",14,muted);
   Vector2[] points=new Vector2[6];for(int i=0;i<6;i++)points[i]=new Vector2(153+RoomGraph.Centers[i].x/120*587,552-RoomGraph.Centers[i].y/88*391);
   for(int i=0;i<RoomGraph.Links.GetLength(0);i++)Line(p,"RouteLink"+i,points[RoomGraph.Links[i,0]],points[RoomGraph.Links[i,1]],muted*.4f);
   for(int i=0;i<6;i++){var at=points[i];var node=Frame(p,"RouteNode"+i,new Rect(at.x-104,at.y-43,208,86),muted);Label(node.transform,"RouteNodeText"+i,new Rect(12,10,187,69),Dungeon.MapNames[i]+"\n미방문",16);}
   Label(p,"Route_Instructions",new Rect(42,619,832,70),"각 방의 문에서 E 키로 이동합니다. 여섯 방의 전투와 보상 선택을 마친 뒤\n06번 방 동쪽 출구에서 다음 스테이지를 선택하세요.",14,muted);
   Box(p,"Route_Sidebar",new Rect(920,0,360,720),new Color(.12f,.075f,.15f));Label(p,"RouteStage",new Rect(945,31,313,41),"STAGE 01",27);Label(p,"RouteName",new Rect(946,79,310,34),"붉은 모래길");Label(p,"RouteProgress",new Rect(946,130,304,60),"방 완료 0/6");Bar(p,"RouteHP",new Rect(946,210,285,16),Ink.Red,"HP 100 / 100");
   string[] icons={"FireIcon","NatureIcon","Raven"};for(int i=0;i<3;i++){Icon(p,"RouteIcon"+i,new Rect(946+i*98,277,46,46),icons[i]);Label(p,"RouteStack"+i,new Rect(949+i*98,329,93,35),"0 각인",14,muted);}
   Label(p,"RouteBuild",new Rect(946,388,290,100),"악마카드 0장");Label(p,"RouteHint",new Rect(946,492,292,62),"다음 스테이지");Button(p,"RouteTravel",new Rect(945,565,309,54),"다음 스테이지로 이동",GameUIAction.NextStage);Button(p,"Route_Close",new Rect(945,641,309,43),"닫기 · M / ESC",GameUIAction.CloseMap);
  }
  void Sigil()
  {
   var p=Panel("Stigma_Build_Tree");view.SigilPanel=p.gameObject;Label(p,"Sigil_Title",new Rect(53,32,950,48),"성흔 각인 · SIGIL MANAGEMENT",27);Label(p,"Sigil_Subtitle",new Rect(55,87,1050,32),"속성 노드를 선택해 각인 수치와 효과를 확인하세요.",14,muted);
   Vector2 center=new Vector2(385,335);float radius=184;for(int n=0;n<48;n++){float a=n*Mathf.PI/24,b=(n+1)*Mathf.PI/24;Line(p,"Circle_"+n,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,Ink.Red*.65f,2);}
   string[] icons={"FireIcon","NatureIcon","Raven"};
   for(int n=0;n<6;n++)
   {float a=(-120+n*60)*Mathf.Deg2Rad;var at=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;Line(p,"Branch_"+n,center,at,muted*.3f);var node=Frame(p,"SigilNode"+n,new Rect(at.x-35,at.y-35,70,70),muted);Icon(node.transform,"SigilNodeIcon"+n,new Rect(12,12,46,46),icons[n/2]);Button(node.transform,"Sigil_Select_"+n,new Rect(0,0,70,70),"",GameUIAction.SelectSigil,n/2,true);Label(p,"SigilNodeText"+n,new Rect(at.x-74,at.y+39,148,29),"1 각인",14,muted,TextAnchor.MiddleCenter);}
   Icon(p,"SigilCenter",new Rect(center.x-33,center.y-33,66,66),icons[0]);Frame(p,"Sigil_Detail",new Rect(733,151,492,402),muted*.6f);Icon(p,"SigilDetailIcon",new Rect(760,180,74,74),icons[0]);Label(p,"SigilName",new Rect(856,182,336,38),"잿불",27);Label(p,"SigilStack",new Rect(856,228,329,32),"각인 0 · 대기",19);Label(p,"SigilEffect",new Rect(761,289,435,100),"성흔의 효과");Label(p,"SigilThreshold",new Rect(761,397,435,58),"첫 활성화: 1 각인",14,muted);Button(p,"SigilClaim",new Rect(760,474,438,51),"각인 +1",GameUIAction.ClaimSigil);Button(p,"Sigil_Close",new Rect(760,610,438,54),"돌아가기 · ESC",GameUIAction.CloseSigil);
  }
  void Cards()
  {
   var p=Panel("Devil_Card_Choice");view.CardsPanel=p.gameObject;Label(p,"Card_Title",new Rect(101,47,1100,46),"악마카드 · 하나의 계약을 선택하세요",27);Label(p,"Card_Subtitle",new Rect(103,106,1030,41),"획득한 카드는 이번 런 동안 유지됩니다. 성흔 각인과 별도로 효과가 누적됩니다.");
   string[] names={"탄환의 계약","방랑자의 계약","검은 날개의 계약"},icons={"BulletIcon","WaterIcon","RavenPortrait"};
   for(int i=0;i<3;i++){float x=102+i*365;var card=Frame(p,"CardBorder"+i,new Rect(x,187,345,400),Color.white);Label(card.transform,"CardRarity"+i,new Rect(23,20,296,33),"일반 · 악마카드",16);Icon(card.transform,"CardIcon"+i,new Rect(104,68,135,135),icons[i]);Label(card.transform,"CardName"+i,new Rect(25,222,297,37),names[i],23);Label(card.transform,"CardEffect"+i,new Rect(25,269,297,52),"효과");Button(card.transform,"Card_Choose_"+i,new Rect(24,334,297,44),"이 카드 선택",GameUIAction.ClaimCard,i);}
   Button(p,"Cards_Close",new Rect(450,631,380,43),"나중에 선택 · ESC",GameUIAction.CloseService);
  }
  void PauseAndSettings()
  {
   var pause=Panel("Pause");view.PausePanel=pause.gameObject;Label(pause,"Pause_Title",new Rect(430,210,480,45),"잠시 숨을 고르다",27);Label(pause,"Controls",new Rect(430,273,520,100),"WASD 달리기 · SHIFT 걷기 · 좌클릭 공격\nQ 태그 · SPACE 회피 · 2 / 3 기술\n1 까마귀 명령 · R 연계 · M 지도");Button(pause,"Resume",new Rect(430,390,420,52),"계속하기",GameUIAction.Resume);Button(pause,"Pause_Settings",new Rect(430,458,420,48),"설정",GameUIAction.OpenSettings);
   var result=Panel("Results");view.ResultsPanel=result.gameObject;Label(result,"ResultsTitle",new Rect(330,240,700,65),"다시, 잿빛 황야로",27);Label(result,"ResultsDetail",new Rect(330,317,700,35),"탐험 기록");Button(result,"Results_Town",new Rect(330,410,620,60),"히르바로 돌아가기",GameUIAction.EnterTown);
   var settings=Panel("Settings");view.SettingsPanel=settings.gameObject;Label(settings,"Settings_Title",new Rect(431,210,450,45),"설정",27);Label(settings,"Volume_Label",new Rect(431,290,450,27),"효과음 음량");
   var track=Box(settings,"Volume_Slider",new Rect(431,333,418,22),muted,true);var slider=track.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.value=1;var handle=Box(track.transform,"Volume_Handle",new Rect(0,-3,20,28),paper,true);slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
   var command=track.gameObject.AddComponent<UICommand>();UnityEventTools.AddPersistentListener(slider.onValueChanged,command.SetVolume);
   Button(settings,"Fullscreen",new Rect(431,389,418,48),"창 모드 / 전체 화면 전환",GameUIAction.ToggleFullscreen);Button(settings,"Settings_Close",new Rect(431,463,418,48),"돌아가기",GameUIAction.CloseSettings);
  }
  void Cinematic()
  {
   var p=Panel("Cinematic_Overlay",false);view.CinematicPanel=p.gameObject;
   var bars=Rect(p,"Letterbox",new Rect(0,0,1280,720));bars.gameObject.AddComponent<CanvasGroup>();Bind("CinemaBars",bars.gameObject);Box(bars,"Letterbox_Top",new Rect(0,0,1280,67),Color.black);Box(bars,"Letterbox_Bottom",new Rect(0,653,1280,67),Color.black);
   var title=Rect(p,"Chapter_Title",new Rect(0,0,1280,720));title.gameObject.AddComponent<CanvasGroup>();Bind("CinemaTitle",title.gameObject);Box(title,"Chapter_Scrim",new Rect(0,444,1280,220),new Color(.015f,.008f,.025f,.65f));Box(title,"Chapter_Line",new Rect(350,487,580,3),Ink.Gold);Label(title,"CinemaHeading",new Rect(180,509,920,63),"붉은 모래길",36,null,TextAnchor.MiddleCenter);Label(title,"CinemaCaption",new Rect(220,577,840,35),"STAGE 01 · 황야의 여섯 방",17,muted,TextAnchor.MiddleCenter);
   var fade=Box(p,"CinemaShade",new Rect(0,0,1280,720),Color.black);fade.gameObject.AddComponent<CanvasGroup>();Label(p,"CinemaHint",new Rect(820,684,430,27),"SPACE / ESC 건너뛰기 · P 일시정지",14,muted);
  }
  void PassivePrefab()
  {
   var root=Rect(null,"Passive_Slot",new Rect(0,0,52,52));var border=root.gameObject.AddComponent<Image>();border.color=Color.white;var layout=root.gameObject.AddComponent<LayoutElement>();layout.preferredWidth=52;layout.preferredHeight=52;
   var fill=Rect(root,"Background",new Rect(2,2,48,48)).gameObject.AddComponent<Image>();fill.color=panel;fill.raycastTarget=false;
   var icon=Rect(root,"Icon",new Rect(6,6,40,40)).gameObject.AddComponent<Image>();icon.preserveAspect=true;icon.raycastTarget=false;icon.sprite=view.Art.Get("BulletIcon");var slot=root.gameObject.AddComponent<PassiveSlotView>();slot.Icon=icon;slot.Border=border;
   Directory.CreateDirectory(EditableAssetStore.Root+"/UI");view.PassivePrefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,EditableAssetStore.Root+"/UI/PassiveSlot.prefab").GetComponent<PassiveSlotView>();Object.DestroyImmediate(root.gameObject);
  }
 }
}
