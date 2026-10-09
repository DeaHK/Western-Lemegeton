using UnityEngine;
using WesternLemegeton.Passives;
namespace WesternLemegeton
{
 public enum RavenState { Idle, Follow, Attack, Return, ComboWindup, ComboDash, LinkAttack }
 public class Raven:MonoBehaviour
 {
  public float Active,Cooldown,LinkWindow,LinkCooldown,ComboFlash;public string Status="동행";
  public RavenState CurrentState {get;private set;}
  public bool ComboBusy=>comboPhase!=0;
  public Enemy AttackTarget=>comboTarget;
  public int ComboDashStarts {get;private set;} public int ComboEffects {get;private set;} public int LastComboHits {get;private set;}
  readonly System.Collections.Generic.Queue<Enemy> comboQueue=new System.Collections.Generic.Queue<Enemy>();
  readonly System.Collections.Generic.HashSet<int> comboVictims=new System.Collections.Generic.HashSet<int>();
  Enemy comboTarget;bool returning;int comboPhase;float comboElapsed,trailClock;Vector2 dashStart;
  const float ActivityRadius=7,WindupDuration=.18f,ComboDashDuration=.24f;
  public bool Linking=>linkTime>0; public Enemy LinkTarget=>linkTarget;
  Enemy marked,linkTarget;float attack,linkTime;long offerId=-1;[SerializeField] SpriteRenderer art;
  void Awake(){if(!art)art=CombatArt.Actor(transform,"Raven",1.1f);}
  public void ResetForRun(){Cooldown=LinkCooldown=attack=0;offerId=-1;ComboDashStarts=ComboEffects=LastComboHits=0;ResetAt(Vector2.zero);}
  public void ResetAt(Vector2 p){transform.position=p+new Vector2(-1,1);Active=LinkWindow=linkTime=ComboFlash=0;returning=false;marked=linkTarget=null;Status="동행";comboPhase=0;comboTarget=null;comboQueue.Clear();comboVictims.Clear();CurrentState=RavenState.Idle;}
  public void Mark(Enemy e){marked=e;}
  public void Command(){if(Cooldown>0||!Dungeon.I.Running||Dungeon.I.IsTown)return;Active=60;Cooldown=10;attack=0;Ink.Ring(transform.position,1.1f,Ink.Cyan);}
  public void OfferLink(Enemy e,long id)
  {
   if(LinkCooldown>0||Linking)return;var g=Dungeon.I;
   if(!e||e.Dead)e=g.Nearest(g.Pos,7);if(!e||Vector2.Distance(e.transform.position,g.Pos)>7)return;
   if(id!=offerId||!linkTarget||linkTarget.Dead||e.AttackPower>linkTarget.AttackPower)linkTarget=e;
   offerId=id;LinkWindow=6;
  }
  public bool TryLink()
  {
   var g=Dungeon.I;if(!g.Running||g.IsTown||LinkWindow<=0||LinkCooldown>0||Linking||ComboBusy||comboQueue.Count>0)return false;
   if(!linkTarget||linkTarget.Dead||Vector2.Distance(linkTarget.transform.position,g.Pos)>7)linkTarget=g.Nearest(g.Pos,7);
   if(!linkTarget)return false;
   linkTime=.6f;LinkWindow=0;Status="연계 공격";return true;
  }
  public void ComboFollowup(Enemy original)
  {
   var g=Dungeon.I;if(!g||!g.Running||g.IsTown)return;ComboFlash=1.5f;
   var marker=new GameObject("Crow combo indicator",typeof(SpriteRenderer));marker.transform.position=g.Pos+Vector2.up*1.8f;
   var sr=marker.GetComponent<SpriteRenderer>();sr.sprite=CombatArt.Get("RavenPortrait");marker.transform.localScale=Vector3.one*(.85f/sr.sprite.bounds.size.y);sr.sortingOrder=1100;PaperWorld.Attach(sr,false);marker.AddComponent<Fade>().Init(.75f);
   comboQueue.Enqueue(original);
   if(!ComboBusy&&!Linking)BeginCombo();
  }
  bool ValidComboTarget(Enemy e)=>e&&!e.Dead&&!comboVictims.Contains(e.GetInstanceID())&&Vector2.Distance(e.transform.position,Dungeon.I.Pos)<=ActivityRadius;
  Enemy Replacement(Vector2 origin)
  {
   Enemy best=null;float distance=float.MaxValue;
   foreach(var e in Dungeon.I.Enemies)if(ValidComboTarget(e)){float d=Vector2.Distance(origin,e.transform.position);if(d<distance){distance=d;best=e;}}
   return best;
  }
  void BeginCombo()
  {
   comboVictims.Clear();LastComboHits=0;var requested=comboQueue.Dequeue();comboTarget=ValidComboTarget(requested)?requested:Replacement(Dungeon.I.Pos);
   // The icon has already spawned. No target means no dash or dash effects.
   if(!comboTarget){EndCombo();return;}
   comboPhase=1;comboElapsed=0;CurrentState=RavenState.ComboWindup;Status="연계 준비";
  }
  void EndCombo(){returning=true;comboPhase=0;comboTarget=null;CurrentState=RavenState.Return;Status="복귀";}
  void TickCombo(float dt)
  {
   if(!ValidComboTarget(comboTarget))
   {
    comboTarget=Replacement(Dungeon.I.Pos);
    if(!comboTarget){EndCombo();return;}
    comboPhase=1;comboElapsed=0;
   }
   comboElapsed+=dt;
   if(comboPhase==1)
   {
    CurrentState=RavenState.ComboWindup;Status="연계 준비";CombatArt.Pose(art,"Raven",1.2f);
    art.transform.localRotation=Quaternion.Euler(0,0,-15);
    if(comboElapsed<WindupDuration)return;
    comboPhase=2;comboElapsed=0;trailClock=0;dashStart=transform.position;ComboDashStarts++;
   }
   CurrentState=RavenState.ComboDash;Status="콤보 돌진";
   CombatArt.Pose(art,"Raven",1.45f);Vector2 end=comboTarget.transform.position;art.flipX=end.x<transform.position.x;
   Vector2 previous=transform.position;float t=Mathf.Clamp01(comboElapsed/ComboDashDuration);
   transform.position=Vector2.Lerp(dashStart,end,t*t*(3-2*t));art.transform.localRotation=Quaternion.Euler(0,0,(art.flipX?1:-1)*18);
   trailClock-=dt;if(trailClock<=0&&Vector2.Distance(previous,transform.position)>.01f)
   {trailClock=.035f;Ink.Line(previous,transform.position,new Color(.67f,.34f,1),.23f,.18f);ComboEffects++;}
   if(t<1)return;
   Enemy hit=comboTarget;comboVictims.Add(hit.GetInstanceID());LastComboHits++;
   hit.TakeDamage(EvaluateRavenDamage(18)+Dungeon.I.RavenLevel*6,Dungeon.I.BurnLevel>0);Ink.Ring(transform.position,.85f,new Color(.75f,.45f,1),.22f);Ink.Burst(transform.position,Ink.Cyan,6);ComboEffects++;
   if(LastComboHits>=2){EndCombo();return;}
   comboTarget=Replacement(transform.position);if(!comboTarget){EndCombo();return;}
   comboPhase=1;comboElapsed=0;
  }
  void Update()
  {
   var g=Dungeon.I;if(!g.Running)return;float dt=Time.deltaTime;
   if (Active == 0 && Cooldown > 0)
   {
   Cooldown = Mathf.Max(0, Cooldown - dt);
   }
   LinkCooldown=Mathf.Max(0,LinkCooldown-dt);LinkWindow=Mathf.Max(0,LinkWindow-dt);ComboFlash=Mathf.Max(0,ComboFlash-dt);Active=Mathf.Max(0,Active-dt);attack-=dt;
   if(ComboBusy){TickCombo(dt);return;}
   if(!Linking&&comboQueue.Count>0){BeginCombo();if(ComboBusy)return;}
   if(Linking)
   {
    CurrentState=RavenState.LinkAttack;linkTime-=dt;CombatArt.Pose(art,"RavenLink",1.8f);
    if(linkTarget&&!linkTarget.Dead)transform.position=Vector2.MoveTowards(transform.position,linkTarget.transform.position,dt*25);
    if(linkTime<=0){Vector2 p=linkTarget&&!linkTarget.Dead?(Vector2)linkTarget.transform.position:(Vector2)transform.position;foreach(var e in g.Enemies)if(e&&!e.Dead&&Vector2.Distance(p,e.transform.position)<2.5f)e.TakeDamage(EvaluateRavenDamage(45)+g.RavenLevel*10,g.BurnLevel>0);Ink.Ring(p,2.5f,Ink.Cyan,.4f);LinkCooldown=8;linkTarget=null;}return;
   }
   CombatArt.Pose(art,"Raven",1.1f);
   Vector2 follow=g.Pos+new Vector2(-.95f,1.15f+Mathf.Sin(Time.time*3)*.15f),goal=follow;Enemy target=null;
   if(Active>0&&!returning){target=marked&&!marked.Dead&&Vector2.Distance(marked.transform.position,g.Pos)<7?marked:g.Nearest(g.Pos,7);if(target){goal=(Vector2)target.transform.position+Vector2.up*.5f;Status="공격";}else Status="탐색";}
   else Status=returning?"복귀":Vector2.Distance(transform.position,follow)>1?"추적":"동행";
   if(returning&&Vector2.Distance(transform.position,follow)<.3f){returning=false;Status="동행";}
   if(Vector2.Distance(transform.position,g.Pos)>9){Ink.Burst(transform.position,Ink.Cyan,4);transform.position=follow;target=null;goal=follow;Status="복귀";}
   CurrentState=Status=="공격"?RavenState.Attack:Status=="복귀"?RavenState.Return:Status=="동행"?RavenState.Idle:RavenState.Follow;
   art.flipX=goal.x<transform.position.x;transform.position=Vector2.MoveTowards(transform.position,goal,dt*10);art.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(Time.time*13)*8);
   if(target&&Vector2.Distance(transform.position,target.transform.position)<1.3f&&attack<=0){attack=.5f;target.TakeDamage(EvaluateRavenDamage(10)+g.RavenLevel*6+(g.BurnLevel>0?3:0),g.BurnLevel>0);Ink.Line(transform.position,target.transform.position,Ink.Cyan,.13f,.2f);g.Sound();}
  }
  // Evaluate the base attack once; existing Butterfly/Fire additions follow it.
  private float EvaluateRavenDamage(float baseDamage)=>Dungeon.I.PassiveStats.Evaluate(WesternPassiveStatKeys.RavenAttack,baseDamage);
 }
}




