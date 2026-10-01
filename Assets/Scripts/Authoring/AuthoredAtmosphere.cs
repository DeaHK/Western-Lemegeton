using UnityEngine;
namespace WesternLemegeton
{
 [System.Serializable]public class SandDrift {public Transform Root;public SpriteRenderer Sprite;public float Speed,Alpha,Phase;public bool Cloud;}
 public sealed class AuthoredAtmosphere:MonoBehaviour
 {
  public SandDrift[] Particles;public RoomAuthoring Room;float age;
  void Update()
  {
   var g=Dungeon.I;if(!g||!g.Running&&g.State!=RunState.Title&&g.State!=RunState.Cinematic)return;
   age+=Time.deltaTime;float gust=.85f+.65f*Mathf.Pow(.5f+.5f*Mathf.Sin(age*.7f),2);
   foreach(var d in Particles)
   {
    if(!d.Root||!d.Sprite)continue;var p=d.Root.localPosition;p.x+=d.Speed*gust*Time.deltaTime;p.z-=d.Speed*.13f*Time.deltaTime;if(p.x>26){p.x=-26;p.z=-11+Mathf.Repeat(p.z+17.3f,27);}d.Root.localPosition=p;
    float edge=Mathf.Clamp01((26-Mathf.Abs(p.x))/4),distance=Vector2.Distance(new Vector2(p.x,p.z),g.Pos-Room.Center);float clear=Mathf.Lerp(d.Cloud?.22f:.5f,1,Mathf.InverseLerp(2,7,distance));
    d.Sprite.color=new Color(1,.78f,.5f,d.Alpha*edge*clear*(.5f+.5f*gust));
   }
  }
 }
}
