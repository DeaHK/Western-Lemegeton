using UnityEngine;
namespace WesternLemegeton
{
 public enum ExplorationRoomKind { Combat, Sigil, DevilCards, Entry }
 public static class RoomGraph
 {
  public static readonly Vector2[] Centers={new Vector2(0,0),new Vector2(60,0),new Vector2(60,44),new Vector2(120,44),new Vector2(60,88),new Vector2(120,88)};
  public static readonly int[,] Links={{0,1},{1,2},{2,3},{2,4},{4,5}};
  public static ExplorationRoomKind Kind(int room)=>room==2?ExplorationRoomKind.Sigil:room==3?ExplorationRoomKind.DevilCards:ExplorationRoomKind.Combat;
  public static bool Adjacent(int a,int b){for(int i=0;i<Links.GetLength(0);i++)if((Links[i,0]==a&&Links[i,1]==b)||(Links[i,0]==b&&Links[i,1]==a))return true;return false;}
  public static Vector2 DoorLocal(int from,int to)
  {Vector2 delta=Centers[to]-Centers[from];return Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?new Vector2(Mathf.Sign(delta.x)*13.4f,0):new Vector2(0,Mathf.Sign(delta.y)*7.25f);}
  public static Vector2 EntryLocal(int from,int to)=>DoorLocal(to,from)*.76f;
 }
 public sealed class ExpeditionProgress
 {
  public const int MapCount=6,SegmentCount=5;
  readonly bool[] visited=new bool[6],cleared=new bool[6];readonly int[] waves=new int[6];
  public StageLayout GeneratedLayout {get;private set;}
  public ExplorationRoomKind KindAt(int room)
  {
   if(GeneratedLayout==null)return RoomGraph.Kind(room);
   var role=GeneratedLayout.Rooms[room].Role;
   return role==RoomRole.Combat||role==RoomRole.Boss?ExplorationRoomKind.Combat:
    role==RoomRole.Sigil?ExplorationRoomKind.Sigil:
    role==RoomRole.Shop||role==RoomRole.CardMerchant?ExplorationRoomKind.DevilCards:ExplorationRoomKind.Entry;
  }
  public void BeginGeneratedStage(StageLayout layout,int layer,int runSeed)
  {
   if(layout==null||layout.Rooms.Count!=6)throw new System.ArgumentException("Six generated rooms required.");
   GeneratedLayout=layout;Seed=runSeed;Segment=layer;ResetRooms();
   System.Array.Clear(visited,0,6);Map=layout.EntryIndex;visited[Map]=true;
  }
  public int Segment {get;private set;} public int Map {get;private set;} public int Wave=>waves[Map];
  public int ClearedMaps {get{int n=0;foreach(bool b in cleared)if(b)n++;return n;}}
  public int VisitedMaps {get{int n=0;foreach(bool b in visited)if(b)n++;return n;}}
  public bool MapComplete=>cleared[Map];public bool SegmentComplete=>ClearedMaps==6;
  public int Seed {get;private set;}
  public bool Visited(int room)=>room>=0&&room<6&&visited[room];public bool Cleared(int room)=>room>=0&&room<6&&cleared[room];
  public int WavesAt(int room)=>KindAt(room)==ExplorationRoomKind.Combat?2+(int)(((uint)Seed+(uint)(Segment*11+room*7))%2):0;
  public int WaveCount=>WavesAt(Map);
  public bool FinalBossWave=>GeneratedLayout!=null?GeneratedLayout.StageType==StageType.Boss&&GeneratedLayout.Rooms[Map].Role==RoomRole.Boss&&Wave==WaveCount-1:Segment==4&&Map==5&&Wave==WaveCount-1;
  public bool CanLeave=>MapComplete||KindAt(Map)!=ExplorationRoomKind.Combat;
  void ResetRooms(){System.Array.Clear(visited,0,6);System.Array.Clear(cleared,0,6);System.Array.Clear(waves,0,6);Map=0;visited[0]=true;}
  public void Reset(int seed){GeneratedLayout=null;Seed=seed;Segment=0;ResetRooms();}
  public bool CompleteWave(){if(MapComplete||KindAt(Map)!=ExplorationRoomKind.Combat)return false;if(Wave<WaveCount-1){waves[Map]++;return false;}cleared[Map]=true;return true;}
  public bool CompleteService(){if(MapComplete||KindAt(Map)==ExplorationRoomKind.Combat)return false;cleared[Map]=true;return true;}
  public bool TryVisit(int next){if(next<0||next>=6||!CanLeave||!(GeneratedLayout!=null?GeneratedLayout.Rooms[Map].Neighbors.Contains(next):RoomGraph.Adjacent(Map,next)))return false;Map=next;visited[next]=true;return true;}
  public bool NextSegment(){if(GeneratedLayout!=null||!SegmentComplete||Segment>=4)return false;Segment++;ResetRooms();return true;}
 }
}
