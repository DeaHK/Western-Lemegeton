using UnityEngine;
using UnityEngine.Rendering;
namespace WesternLemegeton
{
 public class RoomDoor:MonoBehaviour
 {
  public int Destination;public bool StageExit;public SpriteRenderer Marker;
  public RoomAuthoring Owner;public Transform Arrival;
  public Vector2 Position=>Owner?RoomAuthoring.Point(transform):(Vector2)transform.position;
  public Vector2 LocalPosition=>Owner?Position-Owner.Center:(Vector2)transform.localPosition;
  void Update(){var g=Dungeon.I;if(!g||!Marker)return;bool open=StageExit?g.Progress.SegmentComplete:g.Progress.CanLeave;Marker.color=open?Ink.Cyan:new Color(.95f,.2f,.15f,.7f);}
 }
}
