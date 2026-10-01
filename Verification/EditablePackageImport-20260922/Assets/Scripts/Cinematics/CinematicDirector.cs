using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
namespace WesternLemegeton
{
 public enum CinematicKind { DungeonEntrance, PlayerDeath, BossEntrance }
 // Timeline owns presentation while Dungeon owns the post-cinematic gameplay state.
 public sealed class CinematicDirector:MonoBehaviour
 {
  public PlayableDirector Director {get;private set;}
  public bool IsPlaying {get;private set;}
  public bool IsPaused {get;private set;}
  public CinematicKind Kind {get;private set;}
  public float Letterbox {get;private set;}
  public float TitleOpacity {get;private set;}
  public float Shade {get;private set;}
  public int CompletedCount {get;private set;}
  public int EntranceCount {get;private set;}
  public int BossCount {get;private set;}
  public int DeathCount {get;private set;}
  public double Time=>Director?Director.time:0;
  public double Duration=>Director?Director.duration:0;
  public string Heading=>Kind==CinematicKind.DungeonEntrance?Dungeon.RoomNames[Dungeon.I.Room]:Kind==CinematicKind.BossEntrance?"봉인의 파수꾼":"쓰러진 사냥꾼";
  public string Caption=>Kind==CinematicKind.DungeonEntrance?$"STAGE {Dungeon.I.Room+1:00} · 황야의 여섯 방":Kind==CinematicKind.BossEntrance?"망자의 분지 · 마지막 봉인":"황야에서의 여정이 끝났습니다";
  public TimelineAsset[] assets=new TimelineAsset[3];AudioSource audioSource;
  Dungeon game;RunState returnState;Enemy boss;Transform actorRoot,seal;
  SpriteRenderer actorSprite;Sprite savedSprite;Color savedColor;
  Vector3 actorPosition,actorScale,crowPosition,cameraPosition;Quaternion actorRotation;
  Vector2 focus;float cameraFov,cameraWeight;
  void Awake()
  {
   game=GetComponent<Dungeon>();Director=GetComponent<PlayableDirector>();Director.playOnAwake=false;
   Director.extrapolationMode=DirectorWrapMode.None;Director.timeUpdateMode=DirectorUpdateMode.UnscaledGameTime;Director.stopped+=OnStopped;
   audioSource=GetComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=0;
   string[] names={"DungeonEntrance","PlayerDeath","BossEntrance"};for(int i=0;i<3;i++)if(!assets[i])assets[i]=Resources.Load<TimelineAsset>("Cinematics/"+names[i]);
  }
  public bool Play(CinematicKind kind,Enemy target=null)
  {
   if(IsPlaying||!game.Hero||kind==CinematicKind.BossEntrance&&(!target||target.Dead))return false;
   var asset=assets[(int)kind];if(!asset){Debug.LogError("Missing Timeline: "+kind);return false;}
   Kind=kind;boss=target;returnState=kind==CinematicKind.PlayerDeath?RunState.Dead:game.State;
   cameraPosition=game.Cam.transform.position;cameraFov=game.Cam.fieldOfView;crowPosition=game.Crow.transform.position;
   focus=kind==CinematicKind.BossEntrance?(Vector2)target.transform.position:game.Pos;
   actorRoot=kind==CinematicKind.BossEntrance?target.PresentationRoot:game.Hero.Presentation.transform;
   actorSprite=kind==CinematicKind.BossEntrance?target.Presentation:game.Hero.Presentation;
   actorPosition=actorRoot.localPosition;actorScale=actorRoot.localScale;actorRotation=actorRoot.localRotation;
   savedColor=actorSprite.color;savedSprite=actorSprite.sprite;
   if(kind==CinematicKind.PlayerDeath){HunterAtlas.Pose(actorSprite,0,0);actorRoot.localPosition=new Vector3(0,-.35f,0);actorPosition=actorRoot.localPosition;}
   game.ClearTransient();cameraWeight=Letterbox=TitleOpacity=Shade=0;IsPlaying=true;IsPaused=false;game.State=RunState.Cinematic;
   game.GetComponent<GameHUD>().CloseSettings();CreateSeal();
   Director.playableAsset=asset;foreach(var output in asset.outputs)Director.SetGenericBinding(output.sourceObject,output.sourceObject is AudioTrack?(Object)audioSource:this);
   if(kind==CinematicKind.DungeonEntrance)EntranceCount++;else if(kind==CinematicKind.BossEntrance)BossCount++;else DeathCount++;
   Director.time=0;Director.Play();Director.Evaluate();return true;
  }
  public void ApplyCue(CinematicCue cue,float value,float phase,float envelope)
  {
   if(!IsPlaying)return;
   switch(cue)
   {
    case CinematicCue.Camera:cameraWeight=Mathf.Clamp01(value);break;
    case CinematicCue.Letterbox:Letterbox=Mathf.Clamp01(value*envelope);break;
    case CinematicCue.Title:TitleOpacity=Mathf.Clamp01(value*envelope);break;
    case CinematicCue.Fade:Shade=Mathf.Clamp01(value);break;
    case CinematicCue.Actor:AnimateActor(Mathf.Clamp01(value));break;
    case CinematicCue.Seal:
     if(!seal)break;seal.gameObject.SetActive(envelope>.001f);seal.localScale=Vector3.one*Mathf.Lerp(.65f,1.35f,phase);seal.rotation=Quaternion.Euler(0,0,phase*40);
     foreach(var sr in seal.GetComponentsInChildren<SpriteRenderer>()){var c=sr.color;c.a=envelope*.65f;sr.color=c;}break;
   }
  }
  void AnimateActor(float p)
  {
   if(!actorRoot||!actorSprite)return;
   if(Kind==CinematicKind.DungeonEntrance)
   {
    actorRoot.localPosition=actorPosition+new Vector3(-.8f*(1-p),Mathf.Sin(p*24)*.035f,0);
    actorSprite.color=Color.Lerp(new Color(.75f,.63f,.55f,0),savedColor,Mathf.Clamp01(p*4));
    game.Crow.transform.position=Vector3.Lerp(crowPosition+new Vector3(-2.5f,1.3f,0),crowPosition,p);
   }
   else if(Kind==CinematicKind.PlayerDeath)
   {
    actorRoot.localRotation=actorRotation*Quaternion.Euler(0,0,-82*p);actorRoot.localPosition=actorPosition+Vector3.down*(p*.2f);
    actorSprite.color=Color.Lerp(savedColor,new Color(.35f,.27f,.3f,1),p);
    game.Crow.transform.position=crowPosition+new Vector3(-p*1.2f,p*1.5f,0);
   }
   else
   {
    actorRoot.localScale=actorScale*Mathf.Lerp(.75f,1,p);actorRoot.localPosition=actorPosition+Vector3.up*(.7f*(1-p));
    actorSprite.color=Color.Lerp(new Color(.7f,.1f,.16f,.05f),savedColor,Mathf.Clamp01(p*2));
   }
  }
  public void ApplyCamera()
  {
   if(!IsPlaying)return;game.Cam.transform.position=Vector3.Lerp(cameraPosition,PaperWorld.CameraPosition(focus),cameraWeight);
   float close=Kind==CinematicKind.PlayerDeath?27:30;game.Cam.fieldOfView=Mathf.Lerp(cameraFov,close,cameraWeight);
  }
  void CreateSeal()
  {
   seal=new GameObject("Timeline summoning seal").transform;seal.position=focus;
   var tint=Kind==CinematicKind.BossEntrance?Ink.Red:Kind==CinematicKind.PlayerDeath?new Color(.5f,.3f,.65f):Ink.Gold;
   float radius=Kind==CinematicKind.BossEntrance?2.3f:1.1f;
   for(int i=0;i<24;i++)
   {
    float a=i*Mathf.PI/12;var p=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
    Ink.Shape("Timeline seal arc",seal,p,new Vector2(radius*.24f,.06f),tint,-590,false,a*Mathf.Rad2Deg+90);
   }
   for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Ink.Shape("Timeline seal ray",seal,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius, new Vector2(.11f,.38f),tint,-580,false,a*Mathf.Rad2Deg-90);}
   seal.gameObject.SetActive(false);
  }
  public void SetPaused(bool paused)
  {if(!IsPlaying||IsPaused==paused)return;IsPaused=paused;if(paused)Director.Pause();else Director.Resume();}
  public void Skip(){if(IsPlaying)Director.Stop();}
  void OnStopped(PlayableDirector ignored)
  {if(!IsPlaying)return;IsPlaying=false;IsPaused=false;Restore();game.State=returnState;if(returnState==RunState.Combat)game.Hero.SafeEntry();CompletedCount++;}
  public void Cancel()
  {if(!IsPlaying)return;IsPlaying=false;IsPaused=false;Director.Stop();Restore();}
  void Restore()
  {
   if(actorRoot){actorRoot.localPosition=actorPosition;actorRoot.localScale=actorScale;actorRoot.localRotation=actorRotation;}
   if(actorSprite){actorSprite.color=savedColor;actorSprite.sprite=savedSprite;}
   if(game&&game.Crow)game.Crow.transform.position=crowPosition;
   if(game&&game.Cam){game.Cam.transform.position=cameraPosition;game.Cam.fieldOfView=cameraFov;}
   if(seal){seal.gameObject.SetActive(false);Destroy(seal.gameObject);}seal=null;
   Letterbox=TitleOpacity=Shade=cameraWeight=0;audioSource.Stop();boss=null;actorRoot=null;actorSprite=null;
   Director.ClearGenericBindingsSafe();
  }
  void OnDisable(){Cancel();}
  void OnDestroy(){if(Director)Director.stopped-=OnStopped;}
 }
 internal static class CinematicBindings
 {
  public static void ClearGenericBindingsSafe(this PlayableDirector director)
  {if(!director||!director.playableAsset)return;foreach(var output in director.playableAsset.outputs)director.ClearGenericBinding(output.sourceObject);}
 }
}
