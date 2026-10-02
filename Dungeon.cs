using System.Collections.Generic;
using UnityEngine;
namespace WesternLemegeton
{
 public enum RunState { Title,Town,Combat,Reward,Exit,Paused,Dead,Victory,Route,WaveBreak,SigilChoice,CardChoice,Cinematic }
 public class Dungeon:MonoBehaviour
 {
  public static Dungeon I;public static Rect[] Obstacles=System.Array.Empty<Rect>(),SolidObstacles=System.Array.Empty<Rect>();
  public readonly List<Enemy> Enemies=new List<Enemy>();public readonly ExpeditionProgress Progress=new ExpeditionProgress();
  public GameSceneBindings Scene;
  public Player Hero;public Raven Crow;public Camera Cam;public RunState State=RunState.Title,BeforePause,BeforeRoute;
  public int Kills,Seed,Supplies;public float RunTime;public bool RouteTravel;public readonly bool[] Events=new bool[5];
  public int Room=>Progress.Segment;public int MapIndex=>Progress.Map;public int WaveIndex=>Progress.Wave;public int WavesInMap=>Progress.WaveCount;public int MapsCleared=>Progress.ClearedMaps;
  public bool IsBossWave=>Progress.FinalBossWave;public float WaveCountdown {get;private set;}
  GameHUD hud;
  public bool IsTown {get;private set;}public bool Running=>(!hud||!hud.SettingsOpen)&&(State==RunState.Town||State==RunState.Combat||State==RunState.Exit||State==RunState.WaveBreak);
  public Vector2 Pos=>Hero.transform.position;public Vector2 RoomCenter=>IsTown?Scene.Hirva.Center:CurrentRoom.Center;public Vector2 LocalPos=>Pos-RoomCenter;
  public ExplorationRoom[] Rooms {get;private set;}public ExplorationRoom CurrentRoom=>IsTown||Rooms==null?null:Rooms[MapIndex];
  public ExplorationRoomKind CurrentKind=>IsTown?ExplorationRoomKind.Combat:RoomGraph.Kind(MapIndex);
  public static readonly string[] MapNames={"붉은 바위 입구","마차 잔해 전투지","성흔 제단","악마카드 역참","풍차 언덕","스테이지 출구"};
  public static readonly string[] RoomNames={"붉은 모래길","버려진 마차길","외딴 예배당","메마른 풍차","망자의 분지"};
  public RunBuild Build {get;private set;}
  public CinematicDirector Cinematics {get;private set;}
  public int BurnLevel {get=>Build.EffectLevel(SeongheunType.Fire);set=>Build.SetStack(SeongheunType.Fire,value);}
  public int FuryLevel {get=>Build.EffectLevel(SeongheunType.Nature);set=>Build.SetStack(SeongheunType.Nature,value);}
  public int RavenLevel {get=>Build.EffectLevel(SeongheunType.Butterfly);set=>Build.SetStack(SeongheunType.Butterfly,value);}
  public void ApplySeongheunStack(SeongheunType t,int n)=>Build.ApplySeongheunStack(t,n);
  public bool AcquirePassive(string id,PassiveRarity rarity,PassiveSource source)=>Build.AcquirePassive(id,rarity,source);
  public string Notice="";float noticeTime;Vector3 cameraVelocity;Transform actors,environment;AudioSource sound;AudioClip hitClip,shotClip;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot(){if(!FindAnyObjectByType<Dungeon>())Debug.LogError("Open Assets/Scenes/Main.unity: this game uses serialized scene objects.");}
  void Awake()
  {
   I=this;if(!Scene)throw new System.InvalidOperationException("GameSceneBindings is required. Open the authored Main scene.");
   Build=GetComponent<RunBuild>();hud=GetComponent<GameHUD>();Application.targetFrameRate=120;Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None);
   Hero=Scene.Hunter;Crow=Scene.Raven;Cam=Scene.GameCamera;PaperWorld.Configure(Cam);
   sound=GetComponent<AudioSource>();hitClip=Tone(130,.075f);shotClip=Tone(65,.12f);
   Cinematics=GetComponent<CinematicDirector>();actors=Scene.EnemyContainer;
   BuildTown();Hero.transform.position=RoomAuthoring.Point(Scene.Hirva.InitialSpawn);Crow.ResetAt(Pos);ResetCamera();
   if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--presentation-test")>=0||System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--presentation-before")>=0)gameObject.AddComponent<PresentationSmoke>();
   else if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--authoring-test")>=0)gameObject.AddComponent<AuthoringSmoke>();
   else if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--level-test")>=0)gameObject.AddComponent<LevelSmoke>();
   else if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--movement-test")>=0)gameObject.AddComponent<MovementSmoke>();
   else if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--motion-test")>=0)gameObject.AddComponent<MotionSmoke>();
   else if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--smoke-test")>=0||System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--crow-test")>=0)gameObject.AddComponent<RuntimeSmoke>();
  }
  AudioClip Tone(float hz,float length)
  {int n=(int)(44100*length);var values=new float[n];for(int i=0;i<n;i++)values[i]=(Mathf.Sin(i*hz*6.28318f/44100)*(1f-(float)i/n)+Random.Range(-.2f,.2f))*.12f;var clip=AudioClip.Create("Impact",n,1,44100,false);clip.SetData(values,0);return clip;}
  public void Sound(bool gun=false)=>sound.PlayOneShot(gun?shotClip:hitClip);
  internal void ClearTransient()
  {foreach(var b in FindObjectsByType<Bullet>(FindObjectsSortMode.None))Destroy(b.gameObject);foreach(var f in FindObjectsByType<Fade>(FindObjectsSortMode.None))Destroy(f.gameObject);foreach(var f in FindObjectsByType<SkillEffects>(FindObjectsSortMode.None))Destroy(f.gameObject);}
  void ClearEnvironment(){Scene.Hirva.gameObject.SetActive(false);foreach(var room in Scene.Rooms)room.gameObject.SetActive(false);Rooms=null;}
  void ResetRun()
  {
   Cinematics.Cancel();
   foreach(var e in Enemies)if(e)Destroy(e.gameObject);Enemies.Clear();ClearTransient();
   Hero.ResetForRun();Crow.ResetForRun();Build.ResetRun();Kills=Supplies=0;RunTime=0;Seed=System.Environment.TickCount;Progress.Reset(Seed);WaveCountdown=0;System.Array.Clear(Events,0,5);Random.InitState(Seed);
  }
  void ApplyFootprints(WesternEnvironment.Layout layout,Vector2 center)
  {
   Rules.RoomOrigin=center;Obstacles=new Rect[layout.Obstacles.Count];SolidObstacles=new Rect[layout.Buildings.Count];
   for(int i=0;i<Obstacles.Length;i++){Rect r=layout.Obstacles[i];r.position+=center;Obstacles[i]=r;}
   for(int i=0;i<SolidObstacles.Length;i++){Rect r=layout.Buildings[i];r.position+=center;SolidObstacles[i]=r;}
  }
  void BuildTown(){ClearEnvironment();IsTown=true;Scene.Hirva.gameObject.SetActive(true);var l=Scene.Hirva.Capture();ApplyFootprints(l,Scene.Hirva.Center);}
  public void EnterTown(){ResetRun();BuildTown();State=RunState.Town;Hero.transform.position=RoomAuthoring.Point(Scene.Hirva.InitialSpawn);Crow.ResetAt(Pos);Hero.SafeEntry();ResetCamera();Tell("히르바 · 동쪽 관문에서 마우스 우클릭으로 출발");}
  void BuildStage(){ClearEnvironment();IsTown=false;Rooms=new ExplorationRoom[Scene.Rooms.Length];for(int i=0;i<Rooms.Length;i++)Rooms[i]=Scene.Rooms[i].Bind();}
  public void StartRun(){ResetRun();BuildStage();ActivateRoom(-1);}
  // Room objects stay at separate world coordinates for the entire stage.
  void ActivateRoom(int from)
  {
   ClearTransient();foreach(var room in Rooms)room.Layout.Root.gameObject.SetActive(room.Index==MapIndex);
   CurrentRoom.Layout=CurrentRoom.Layout.Authored.Capture();ApplyFootprints(CurrentRoom.Layout,RoomCenter);
   var entryDoor=from<0?null:CurrentRoom.Doors.Find(d=>d.Destination==from);
   Vector2 entry=RoomAuthoring.Point(entryDoor&&entryDoor.Arrival?entryDoor.Arrival:CurrentRoom.Layout.Authored.InitialSpawn)-RoomCenter;
   Hero.transform.position=Rules.OutsideCover(RoomCenter+entry,.38f);Hero.SafeEntry();Crow.ResetAt(Pos);ResetCamera();
   if(Progress.MapComplete){State=RunState.Exit;Tell("정리한 방 · 문을 통해 다른 방으로 이동할 수 있습니다");}
   else if(CurrentKind==ExplorationRoomKind.Combat){SpawnWave();if(from<0)Cinematics.Play(CinematicKind.DungeonEntrance);}
   else{State=RunState.Exit;Tell(CurrentKind==ExplorationRoomKind.Sigil? "성흔 제단 · 중앙 제단에서 마우스 우클릭으로 각인 강화" : "악마카드 역참 · 중앙 진열대에서 마우스 우클릭으로 카드 선택");}
  }
  public RoomDoor NearbyDoor()
  {if(CurrentRoom==null)return null;RoomDoor closest=null;float best=2.1f;foreach(var d in CurrentRoom.Doors){float distance=Vector2.Distance(Pos,d.Position);if(distance<best){best=distance;closest=d;}}return closest;}
  public bool TryDoor(RoomDoor door)
  {
   if(!Running||door==null||CurrentRoom==null||!CurrentRoom.Doors.Contains(door)||Vector2.Distance(Pos,door.Position)>2.1f)return false;
   if(door.StageExit)
   {
    if(!Progress.SegmentComplete){Tell($"출구 봉인 · 아직 정리하지 않은 방 {6-MapsCleared}개");return false;}
    OpenRoute(true);return State==RunState.Route;
   }
   int previous=MapIndex;if(!Progress.TryVisit(door.Destination)){Tell("모든 웨이브의 적을 처치해야 방 문이 열립니다");return false;}
   ActivateRoom(previous);return true;
  }
  public bool TryTravel()
  {
   if(State==RunState.Town&&Vector2.Distance(Pos,RoomAuthoring.Point(Scene.Hirva.TownExit))<2.1f){OpenRoute(true);return true;}
   if(!Running||IsTown)return false;
   var door=NearbyDoor();if(door)return TryDoor(door);
   if(CurrentKind!=ExplorationRoomKind.Combat&&Vector2.Distance(Pos,RoomAuthoring.Point(CurrentRoom.Layout.Authored.ServicePoint))<2.4f)
   {if(Progress.MapComplete){Tell("이 방의 보상은 이미 선택했습니다");return false;}State=CurrentKind==ExplorationRoomKind.Sigil?RunState.SigilChoice:RunState.CardChoice;return true;}
   return false;
  }
  bool AtStageExit(){var d=NearbyDoor();return d&&d.StageExit&&Progress.SegmentComplete;}
  public void OpenRoute(bool travel=false)
  {if(!Running)return;BeforeRoute=State;RouteTravel=travel&&(IsTown?Vector2.Distance(Pos,RoomAuthoring.Point(Scene.Hirva.TownExit))<2.1f:AtStageExit());State=RunState.Route;}
  public void CloseRoute(){if(State==RunState.Route)State=BeforeRoute;}
  public int NextRoom=>IsTown?0:Room+1;
  public bool ChooseRoom(int index)
  {if(State!=RunState.Route||!RouteTravel||index!=NextRoom||index>=5)return false;if(IsTown)StartRun();else{if(!Progress.NextSegment())return false;BuildStage();ActivateRoom(-1);}return true;}
  public void FinishExpedition(){if(State==RunState.Route&&RouteTravel&&Room==4&&Progress.SegmentComplete)State=RunState.Victory;}
  public bool ChooseEvent(int index)=>false;
  public void CloseService(){if(State==RunState.SigilChoice||State==RunState.CardChoice)State=RunState.Exit;}
  public bool ClaimSigil(SeongheunType type)
  {if(State!=RunState.SigilChoice||CurrentKind!=ExplorationRoomKind.Sigil||Progress.MapComplete||(int)type<0||(int)type>2)return false;ApplySeongheunStack(type,1);Progress.CompleteService();State=RunState.Exit;Tell("성흔 각인 완료 · 다른 방 탐험을 계속하세요");return true;}
  public bool ClaimCard(int choice)
  {
   if(State!=RunState.CardChoice||CurrentKind!=ExplorationRoomKind.DevilCards||Progress.MapComplete||choice<0||choice>2)return false;
   string[] ids={"spent_bullet","blue_charm","raven_seal"};if(!AcquirePassive(ids[choice],CardRarity,PassiveSource.EventReward))return false;
   Progress.CompleteService();State=RunState.Exit;Tell("악마카드 획득 · 카드 효과가 이번 런에 적용됩니다");return true;
  }
  public PassiveRarity CardRarity=>(PassiveRarity)Mathf.Min(2,Room/2);
  public void Upgrade(int kind){if(State==RunState.SigilChoice)ClaimSigil((SeongheunType)kind);}
  void SpawnWave()
  {
   State=RunState.Combat;WaveCountdown=0;int count=IsBossWave?1:3+Room+MapIndex/2+WaveIndex;
   for(int i=0;i<count;i++){Vector2 at=SpawnPoint();int kind=IsBossWave?3:(i+Room+MapIndex+WaveIndex)%3;var e=Instantiate(Scene.EnemyPrefabs[kind],at,Quaternion.identity,actors);e.Init(kind,Room);Enemies.Add(e);Ink.Ring(at,.9f,Ink.Gold,.6f);}
   Tell($"{MapNames[MapIndex]} · WAVE {WaveIndex+1}/{WavesInMap} · 적 전멸 시 연결문 개방");
   if(IsBossWave)Cinematics.Play(CinematicKind.BossEntrance,Enemies[0]);
  }
  bool SpawnSafe(Vector2 p)
  {if(Vector2.Distance(p,Pos)<4)return false;foreach(var r in Obstacles)if(Rect.MinMaxRect(r.xMin-1,r.yMin-1,r.xMax+1,r.yMax+1).Contains(p))return false;foreach(var e in Enemies)if(e&&!e.Dead&&Vector2.Distance(e.transform.position,p)<1.4f)return false;return true;}
  Vector2 SpawnPoint()
  {for(int i=0;i<180;i++){var authoring=CurrentRoom.Layout.Authored;var points=authoring.EnemySpawnPoints;Vector2 p=points!=null&&points.Length>0?RoomAuthoring.Point(points[Random.Range(0,points.Length)])+Random.insideUnitCircle*authoring.SpawnRadius:RoomCenter+new Vector2(Random.Range(-12f,12f),Random.Range(-7f,7f));if(SpawnSafe(p))return p;}for(float y=-7;y<=7;y+=1.5f)for(float x=-12;x<=12;x+=1.5f){var p=RoomCenter+new Vector2(x,y);if(SpawnSafe(p))return p;}throw new System.InvalidOperationException("No safe spawn");}
  public void BeginDeath(){if(!Cinematics.Play(CinematicKind.PlayerDeath))State=RunState.Dead;}
  public void Tell(string text){Notice=text;noticeTime=4;}
  void Update()
  {
   if(State==RunState.Cinematic){if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Escape))Cinematics.Skip();else if(Input.GetKeyDown(KeyCode.P))Cinematics.SetPaused(!Cinematics.IsPaused);return;}
   if(GetComponent<GameHUD>().SettingsOpen){if(Input.GetKeyDown(KeyCode.Escape))GetComponent<GameHUD>().CloseSettings();return;}
   if(State==RunState.Route){if(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.M))CloseRoute();return;}
   if(State==RunState.SigilChoice||State==RunState.CardChoice){if(Input.GetKeyDown(KeyCode.Escape))CloseService();return;}
   if(Input.GetKeyDown(KeyCode.M)&&Running){OpenRoute();return;}
   if(Input.GetKeyDown(KeyCode.Escape)&&State!=RunState.Title&&State!=RunState.Dead&&State!=RunState.Victory){if(State==RunState.Paused)State=BeforePause;else{BeforePause=State;State=RunState.Paused;}}
   if(noticeTime>0)noticeTime-=Time.unscaledDeltaTime;else Notice="";
   if(!Running)return;if(Input.GetMouseButtonDown(1) && TryTravel())return;if(IsTown)return;RunTime+=Time.deltaTime;
   if(State==RunState.WaveBreak){WaveCountdown-=Time.deltaTime;if(WaveCountdown<=0)SpawnWave();return;}
   Enemies.RemoveAll(e=>!e||e.Dead);
   if(State==RunState.Combat&&Enemies.Count==0)
   {
    ClearTransient();
    if(!Progress.CompleteWave()){State=RunState.WaveBreak;WaveCountdown=2;Tell("다음 웨이브가 접근합니다");return;}
    Hero.Hp=Mathf.Min(Player.MaxHp,Hero.Hp+18);State=RunState.Exit;
    Tell(Progress.SegmentComplete? "여섯 방 정리 완료 · 출구방의 동쪽 문에서 마우스 우클릭" : "전투방 정리 완료 · 열린 문으로 다음 방을 탐험하세요");
   }
  }
  void ResetCamera()
  {
   var follow=Cam.GetComponent<CameraFollow2_5D>();
   if(follow&&follow.enabled){follow.Step(Pos,RoomCenter,IsTown,true);return;}
   Cam.transform.position=PaperWorld.CameraPosition(RoomCenter+new Vector2(Mathf.Clamp(LocalPos.x*.7f,-6,6),Mathf.Clamp(LocalPos.y*.45f,-2.5f,2.5f)));cameraVelocity=Vector3.zero;
  }
  void LateUpdate()
  {
   if(!Hero)return;var follow=Cam.GetComponent<CameraFollow2_5D>();
   if(Cinematics&&Cinematics.IsPlaying){Cinematics.ApplyCamera();if(follow&&follow.enabled)follow.ClampCurrentView();return;}
   if(follow&&follow.enabled){follow.Step(Pos,RoomCenter,IsTown);return;}
   Cam.fieldOfView=Cam.aspect<1.5f?48:38;Vector2 focus=RoomCenter+new Vector2(Mathf.Clamp(LocalPos.x*(IsTown?.42f:.7f),-6,6),Mathf.Clamp(LocalPos.y*.45f,-2.5f,2.5f));Cam.transform.position=Vector3.SmoothDamp(Cam.transform.position,PaperWorld.CameraPosition(focus),ref cameraVelocity,.24f,Mathf.Infinity,Time.unscaledDeltaTime);
  }
  public Enemy Nearest(Vector2 p,float radius){Enemy result=null;float best=radius;foreach(var e in Enemies)if(e&&!e.Dead){float distance=Vector2.Distance(p,e.transform.position);if(distance<best){best=distance;result=e;}}return result;}
 }
}
