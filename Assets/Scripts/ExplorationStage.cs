using System.Collections.Generic;
using UnityEngine;
namespace WesternLemegeton
{
 
 public sealed class ExplorationRoom
 {
  public int Index;public WesternEnvironment.Layout Layout;public readonly List<RoomDoor> Doors=new List<RoomDoor>();
  public Vector2 Center=>Layout.Authored?Layout.Authored.Center:RoomGraph.Centers[Index];
 }
 public static class ExplorationStage
 {
  public static ExplorationRoom[] Create(Transform parent,int segment)
  {
   int[] templates={0,1,2,1,3,5};var rooms=new ExplorationRoom[6];
   for(int i=0;i<6;i++)
   {
    var layout=WesternEnvironment.Build(false,segment,templates[i],true);layout.Root.name=$"Room {i+1:00} - {Dungeon.MapNames[i]}";
    layout.Root.SetParent(parent,false);layout.Root.position=RoomGraph.Centers[i];
    var room=new ExplorationRoom{Index=i,Layout=layout};rooms[i]=room;
    for(int j=0;j<6;j++)if(RoomGraph.Adjacent(i,j))AddDoor(room,j,false,RoomGraph.DoorLocal(i,j));
    if(i==5)AddDoor(room,-1,true,new Vector2(13.4f,0));
    if(i==2)
    {
     Ink.Shape("Stigma altar ground",layout.Root,Vector2.zero,new Vector2(3.4f,2.4f),new Color(.3f,.4f,1,.32f),-700,true).sprite=Ink.SoftDisc;
     for(int k=0;k<6;k++){float a=k*Mathf.PI/3;Ink.Shape("Altar sigil",layout.Root,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.95f,Vector2.one*.24f,Ink.Cyan,20,true);}
     AddCombatArt(room,"NatureIcon",new Vector2(0,.25f),.85f);
    }
    if(i==3)
    {
     // The room template already includes a wagon; avoid stacking two enlarged sprites.
     string[] icons={"BulletIcon","WaterIcon","RavenPortrait"};
     for(int k=0;k<3;k++){Ink.Shape("Devil card stand",layout.Root,new Vector2((k-1)*1.1f,0),new Vector2(.9f,1.2f),new Color(.17f,.08f,.24f),10);AddCombatArt(room,icons[k],new Vector2((k-1)*1.1f,0),.8f);}
    }
    FrontierDressing.Dress(room);
    layout.Root.gameObject.SetActive(false);
   }
   return rooms;
  }
  static void AddCombatArt(ExplorationRoom room,string name,Vector2 local,float height)
  {
   var go=new GameObject(name);go.transform.SetParent(room.Layout.Root,false);go.transform.localPosition=local;
   var sr=go.AddComponent<SpriteRenderer>();sr.sprite=CombatArt.Get(name);go.transform.localScale=Vector3.one*(height/sr.sprite.bounds.size.y);sr.sortingOrder=20;PaperWorld.Attach(sr,false);
  }
  static void AddArt(ExplorationRoom room,string asset,Vector2 local,float width,bool fade=true)
  {
   width=WesternEnvironment.PropWidth(asset,width);
   var go=new GameObject(asset);go.transform.SetParent(room.Layout.Root,false);go.transform.localPosition=local;
   var sr=go.AddComponent<SpriteRenderer>();sr.sprite=WesternArt.Get(asset);go.transform.localScale=Vector3.one*(width/sr.sprite.bounds.size.x);sr.sortingOrder=WorldDepth.Order(local.y);PaperWorld.Attach(sr,false);
   if(fade){var occlusion=go.AddComponent<SceneryOccluder>();occlusion.Width=width;occlusion.Height=sr.sprite.bounds.size.y*go.transform.localScale.y;}
  }
  static void AddDoor(ExplorationRoom room,int next,bool exit,Vector2 local)
  {
   var go=new GameObject(exit?"Stage exit gate":"Door to room "+(next+1));go.transform.SetParent(room.Layout.Root,false);go.transform.localPosition=local;
   var door=go.AddComponent<RoomDoor>();door.Destination=next;door.StageExit=exit;
   AddArt(room,"Wnn",local+Vector2.down*.35f,5.5f,false);
   door.Marker=Ink.Shape("Door seal",go.transform,Vector2.zero,new Vector2(3.0f,.5f),Ink.Cyan,-700,true);door.Marker.sprite=Ink.SoftDisc;room.Doors.Add(door);
   FrontierDressing.GateBanks(room,local);
   // Keep the arrival pad free of template collision footprints.
   var pad=new Rect(local-Vector2.one*1.6f,Vector2.one*3.2f);
   room.Layout.Obstacles.RemoveAll(r=>r.Overlaps(pad));room.Layout.Buildings.RemoveAll(r=>r.Overlaps(pad));
  }
 }
}

