using UnityEngine;
using UnityEngine.Rendering;
namespace WesternLemegeton
{
 public class RoomDoor:MonoBehaviour
 {
  private static readonly Color[] RouteColors={new Color(.16f,.91f,1f),new Color(1f,.61f,.18f),new Color(.76f,.46f,1f),new Color(.22f,.9f,.49f),new Color(1f,.86f,.2f),new Color(1f,.39f,.68f)};
  public int Destination;public bool StageExit;public SpriteRenderer Marker;
  // Generated-run route doors are field choices to the next RunMap node, not room links.
  public int RunMapDestination=-1;
  public StageType RunMapDestinationType;
  public int RunMapRouteIndex=-1;
  public RoomAuthoring Owner;public Transform Arrival;
  public Vector2 Position=>Owner?RoomAuthoring.Point(transform):(Vector2)transform.position;
  public Vector2 LocalPosition=>Owner?Position-Owner.Center:(Vector2)transform.localPosition;
  public Color GetExpectedRunMapMarkerColor()=>RouteColors[Mathf.Abs(RunMapRouteIndex)%RouteColors.Length];
  public void RefreshRunMapMarkerColor()
  {
   if(!Marker||RunMapDestination<0)return;
   // Adjacent route choices use different accent colors, even when their stage types match.
   Marker.color=GetExpectedRunMapMarkerColor();
  }
  void Update(){var g=Dungeon.I;if(!g||!Marker)return;if(RunMapDestination>=0){RefreshRunMapMarkerColor();return;}bool open=StageExit?g.Progress.SegmentComplete:g.Progress.CanLeave;Marker.color=open?Ink.Cyan:new Color(.95f,.2f,.15f,.7f);}
 }
}
