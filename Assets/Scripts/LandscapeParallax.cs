using UnityEngine;
using UnityEngine.Rendering;
namespace WesternLemegeton
{
 public class LandscapeParallax:MonoBehaviour
 {
  public float Factor;Vector3 anchor;
  void Start(){anchor=transform.position;}
  void LateUpdate(){if(!Dungeon.I)return;var c=Dungeon.I.Cam.transform.position;transform.position=anchor+new Vector3((c.x-Rules.RoomOrigin.x)*Factor,0,0);}
 }
}
