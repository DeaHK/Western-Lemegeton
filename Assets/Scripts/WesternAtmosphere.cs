using UnityEngine;
namespace WesternLemegeton
{
 
 public class WesternAtmosphere:MonoBehaviour
 {
  struct Drift {public Transform Root;public SpriteRenderer Sprite;public float Speed,Alpha,Phase;public bool Cloud;}
  Drift[] dust;float age,footClock;Vector2 lastPlayer;bool havePlayer;
  public void Initialize(bool town,int stage,int map)
  {
   var sun=Ink.Shape("Low sunset amber",transform,new Vector2(-12,10),new Vector2(47,24),new Color(1,.57f,.25f,town?.2f:.38f),-1200,true);sun.sprite=Ink.SoftDisc;
   var horizon=Ink.Shape("Sunset horizon wash",transform,new Vector2(-6,13),new Vector2(48,8),new Color(1,.68f,.39f,.18f),-380,true);horizon.sprite=Ink.SoftDisc;
   var violet=Ink.Shape("Distant violet haze",transform,new Vector2(9,13),new Vector2(38,10),new Color(.63f,.5f,.67f,.22f),-370,true);violet.sprite=Ink.SoftDisc;
   for(int i=0;i<4;i++){var beam=Ink.Shape("Slanting sunset shaft",transform,new Vector2(-13+i*8,4),new Vector2(30,2.4f),new Color(1,.68f,.38f,.07f),-1000,true,-26);beam.sprite=Ink.SoftDisc;}
   dust=new Drift[town?28:94];var random=new System.Random(7103+stage*19+map*101);
   for(int i=0;i<dust.Length;i++)
   {
    bool cloud=i<(town?5:12);float x=(float)random.NextDouble()*50-25,y=(float)random.NextDouble()*29-12;
    // Thin, fast near grains and slow broad veils provide motion parallax.
    var sprite=Ink.Shape(cloud?"Windblown sand veil":"Wind sand streak",transform,new Vector2(x,y),cloud?new Vector2(7+(float)random.NextDouble()*8,1.1f+(float)random.NextDouble()*.8f):new Vector2(.14f+(float)random.NextDouble()*.24f,.025f),Color.white,cloud?600:800,true,-16);sprite.sprite=Ink.SoftDisc;
    dust[i]=new Drift{Root=sprite.transform,Sprite=sprite,Speed=(cloud?1.3f:3.2f)+(float)random.NextDouble()*(cloud?1.1f:3.3f),Alpha=cloud?(town?.025f:.095f):.4f,Phase=(float)random.NextDouble()*6.28f,Cloud=cloud};
   }
  }
  void OnEnable(){havePlayer=false;}
  void Update()
  {
   var g=Dungeon.I;if(!g||!g.Running&&g.State!=RunState.Title&&g.State!=RunState.Cinematic||dust==null)return;
   float dt=Time.deltaTime;age+=dt;float gust=.85f+.65f*Mathf.Pow(.5f+.5f*Mathf.Sin(age*.7f),2);
   for(int i=0;i<dust.Length;i++)
   {
    var d=dust[i];var p=d.Root.localPosition;p.x+=d.Speed*gust*dt;p.y-=d.Speed*.13f*dt;if(p.x>26){p.x=-26;p.y=-11+Mathf.Repeat(p.y+17.3f,27);}d.Root.localPosition=p;
    float edge=Mathf.Clamp01((26-Mathf.Abs(p.x))/4);
    float distance=Vector2.Distance(p,g.LocalPos);float clear=Mathf.Lerp(d.Cloud?.22f:.5f,1,Mathf.InverseLerp(2,7,distance));
    d.Sprite.color=new Color(1,.78f,.5f,d.Alpha*edge*clear*(.5f+.5f*gust));
   }
   Vector2 at=g.Pos;if(!havePlayer){lastPlayer=at;havePlayer=true;return;}
   float travel=Vector2.Distance(at,lastPlayer);footClock-=dt;
   if(g.Running&&travel>.025f&&travel<1.5f&&footClock<=0)
   {
    bool dash=travel/Mathf.Max(dt,.001f)>9;footClock=dash?.045f:.13f;
    var s=Ink.Shape("Footfall sand",transform,at-(Vector2)transform.position+Vector2.down*.27f,dash?new Vector2(.95f,.4f):new Vector2(.5f,.22f),new Color(.94f,.65f,.35f,dash?.24f:.16f),-850,true);s.sprite=Ink.SoftDisc;
    s.gameObject.AddComponent<SandWake>().Initialize((at-lastPlayer).normalized*-.5f+Vector2.right*.7f);
   }
   lastPlayer=at;
  }
 }
 public class SandWake:MonoBehaviour
 {
  SpriteRenderer sprite;Vector2 drift;float age,alpha;
  public void Initialize(Vector2 velocity){sprite=GetComponent<SpriteRenderer>();alpha=sprite.color.a;drift=velocity;}
  void Update(){if(!Dungeon.I||!Dungeon.I.Running)return;float dt=Time.deltaTime;age+=dt;if(age>.4f){Destroy(gameObject);return;}transform.localPosition+=(Vector3)(drift*dt);transform.localScale+=Vector3.one*(dt*.7f);var c=sprite.color;c.a=alpha*(1-age/.4f);sprite.color=c;}
 }
}
