using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace WesternLemegeton.EditorTools
{
 public static class EditableWorldBaker
 {
  static Transform Folder(Transform p,string name){var t=new GameObject(name).transform;t.SetParent(p,false);return t;}
  static Transform Marker(Transform p,string name,Vector2 pos){var t=Folder(p,name);t.localPosition=PaperWorld.Point(pos);return t;}
  static bool Scenery(string name)=>name.StartsWith("Wnn")||name.StartsWith("OBJ")||name.StartsWith("Deco")||name.StartsWith("Fence");
  public static RoomAuthoring Bake(WesternEnvironment.Layout source,int index,RoomDoor[] doors,Camera camera)
  {
   var root=new GameObject(source.Town?"Hirva":"Room_"+(index+1).ToString("D2")+"_"+Dungeon.MapNames[index]);var room=root.AddComponent<RoomAuthoring>();room.RoomIndex=index;room.Town=source.Town;
   var ground=Folder(root.transform,"Ground_and_Paths");var geometry=Folder(root.transform,"Scenery");var connections=Folder(root.transform,"Doors");var markers=Folder(root.transform,"Gameplay_Markers");
   var dictionary=new Dictionary<SpriteRenderer,WorldProp>();var positions=new Dictionary<WorldProp,Vector2>();
   Vector2 origin=source.Root.position;
   foreach(var sr in source.Root.GetComponentsInChildren<SpriteRenderer>(true))
   {
    Vector2 p=(Vector2)sr.transform.position-origin;bool scenery=Scenery(sr.sprite.name);bool flat=scenery?false:sr.sortingOrder<0;
    if(sr.sprite.name=="floor_texture_original")flat=true;
    var wrapper=new GameObject(sr.name);wrapper.transform.SetParent(flat?ground:geometry,false);wrapper.transform.localPosition=PaperWorld.Point(p,flat?.015f:0);
    var image=new GameObject("Visual",typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();image.transform.SetParent(wrapper.transform,false);image.sprite=EditableAssetStore.Save(sr.sprite);image.color=flat?sr.color:sr.color*new Color(.94f,.85f,.80f,1);image.flipX=sr.flipX;image.enabled=sr.enabled;
    image.transform.localScale=sr.transform.lossyScale;image.transform.localRotation=Quaternion.Euler(flat?90:PaperWorld.Pitch,0,0)*Quaternion.Euler(0,0,sr.transform.eulerAngles.z);image.sortingOrder=sr.sortingOrder*10;
    var prop=wrapper.AddComponent<WorldProp>();prop.Visual=image;prop.Flat=flat;
    var fade=sr.GetComponent<SceneryOccluder>();if(fade){prop.FadeWhenOccluding=true;prop.OcclusionWidth=fade.Width;prop.OcclusionHeight=fade.Height;}
    float width=sr.sprite.bounds.size.x*sr.transform.lossyScale.x,height=sr.sprite.bounds.size.y*sr.transform.lossyScale.y;
    bool distant=p.y>12&&width>10;
    if(scenery&&height>.75f&&!distant)
    {
     var contact=new PropGrounding(image);contact.Update(image,camera);contact.Root.SetParent(wrapper.transform,true);contact.Root.name="Contact_Shadows";prop.ContactRoot=contact.Root;prop.Contacts=contact.Decals;
     EditableAssetStore.Renderers(contact.Root.gameObject);
    }
    if(distant||Mathf.Abs(p.x)>17&&p.y<-10){var parallax=wrapper.AddComponent<LandscapeParallax>();parallax.Factor=distant?.26f:-.065f;}
    dictionary.Add(sr,prop);positions[prop]=p;
   }
   foreach(var rect in source.Obstacles)
   {
    var nearest=dictionary.Where(k=>Scenery(k.Key.sprite.name)).OrderBy(k=>Vector2.Distance(positions[k.Value],new Vector2(rect.center.x,rect.yMin))).First().Value;
    var footprint=nearest.gameObject.AddComponent<WorldFootprint>();footprint.Offset=rect.center-positions[nearest];footprint.Size=rect.size;footprint.BlocksDodge=source.Buildings.Contains(rect);
   }
   var bakedDoors=new List<RoomDoor>();
   foreach(var d in doors)
   {
    Vector2 p=d.LocalPosition;var anchor=Marker(connections,d.StageExit?"Stage_Exit":"To_Room_"+(d.Destination+1).ToString("D2"),p);var next=anchor.gameObject.AddComponent<RoomDoor>();next.Owner=room;next.Destination=d.Destination;next.StageExit=d.StageExit;next.Marker=dictionary[d.Marker].Visual;
    dictionary[d.Marker].transform.SetParent(anchor,true);
    var gate=dictionary.Where(k=>k.Key.sprite.name=="Wnn").OrderBy(k=>Vector2.Distance(positions[k.Value],p)).First().Value;gate.transform.SetParent(anchor,true);
    next.Arrival=Marker(anchor,"Arrival",-p*.24f);bakedDoors.Add(next);
   }
   room.Doors=bakedDoors.ToArray();room.InitialSpawn=Marker(markers,"Initial_Player_Spawn",source.Town?new Vector2(0,-.7f):new Vector2(-10,0));room.ServicePoint=Marker(markers,"Service_Interaction",Vector2.zero);room.TownExit=Marker(markers,"Town_Exit_Interaction",new Vector2(13,0));
   var spawn=Folder(markers,"Enemy_Spawn_Points");Vector2[] spots={new Vector2(-9,4),new Vector2(0,5),new Vector2(9,4),new Vector2(-9,-5),new Vector2(1,-4),new Vector2(9,-5)};
   room.EnemySpawnPoints=spots.Select((p,i)=>Marker(spawn,"Spawn_"+(i+1),p)).ToArray();
   Atmosphere(room);EditableAssetStore.Renderers(root);return room;
  }
  static void Atmosphere(RoomAuthoring room)
  {
   var root=Folder(room.transform,"Atmosphere");var atmosphere=root.gameObject.AddComponent<AuthoredAtmosphere>();atmosphere.Room=room;var particles=new List<SandDrift>();var rng=new System.Random(7103+room.RoomIndex*101);
   for(int i=0;i<(room.Town?28:94);i++)
   {
    bool cloud=i<(room.Town?5:12);var node=Folder(root,cloud?"Sand_Veil_"+i:"Sand_Grain_"+i);node.localPosition=new Vector3((float)rng.NextDouble()*50-25,.02f,(float)rng.NextDouble()*29-12);
    var sprite=node.gameObject.AddComponent<SpriteRenderer>();sprite.sprite=EditableAssetStore.Save(Ink.SoftDisc);sprite.sortingOrder=cloud?6000:8000;sprite.transform.localRotation=Quaternion.Euler(PaperWorld.Pitch,0,-16);
    sprite.transform.localScale=cloud?new Vector3(7+(float)rng.NextDouble()*8,1.1f+(float)rng.NextDouble()*.8f,1):new Vector3(.14f+(float)rng.NextDouble()*.24f,.025f,1);
    sprite.color=new Color(1,.78f,.5f,cloud?.05f:.25f);particles.Add(new SandDrift{Root=node,Sprite=sprite,Cloud=cloud,Speed=(cloud?1.3f:3.2f)+(float)rng.NextDouble()*(cloud?1.1f:3.3f),Alpha=cloud?(room.Town?.025f:.095f):.4f,Phase=(float)rng.NextDouble()*6.28f});
   }
   atmosphere.Particles=particles.ToArray();
   Pool(root,"Low_Sunset",new Vector2(-12,10),new Vector2(47,24),new Color(1,.57f,.25f,room.Town?.2f:.38f),-12000);
   Pool(root,"Horizon",new Vector2(-6,13),new Vector2(48,8),new Color(1,.68f,.39f,.18f),-3800);
   Pool(root,"Violet_Distance",new Vector2(9,13),new Vector2(38,10),new Color(.63f,.5f,.67f,.22f),-3700);
  }
  static void Pool(Transform root,string name,Vector2 p,Vector2 size,Color c,int order){var t=Marker(root,name,p);t.localPosition+=Vector3.up*.016f;t.localRotation=Quaternion.Euler(90,0,0);t.localScale=new Vector3(size.x,size.y,1);var sr=t.gameObject.AddComponent<SpriteRenderer>();sr.sprite=EditableAssetStore.Save(Ink.SoftDisc);sr.color=c;sr.sortingOrder=order;}
 }
}
