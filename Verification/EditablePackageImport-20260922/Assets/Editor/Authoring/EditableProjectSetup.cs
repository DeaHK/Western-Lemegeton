using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;
namespace WesternLemegeton.EditorTools
{
 public static class EditableProjectSetup
 {
  const string Root=EditableAssetStore.Root;
  [MenuItem("Western Lemegeton/Authoring/Open Editable Main Scene")]
  public static void Open(){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");}
  // One-time migration only. Regular builds never invoke this generator.
  public static void CreateEditableProject()
  {
   if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Rooms/Room_01.prefab"))throw new System.InvalidOperationException("Editable content already exists. Edit it directly; migration will not overwrite artist changes.");
   Directory.CreateDirectory(Root+"/Rooms");Directory.CreateDirectory(Root+"/Actors");Directory.CreateDirectory(Root+"/UI");
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";PaperWorld.Configure(camera);camera.transform.position=PaperWorld.CameraPosition(Vector2.zero);
   var systems=new GameObject("Game_Systems");var game=systems.AddComponent<Dungeon>();var bindings=systems.AddComponent<GameSceneBindings>();game.Scene=bindings;bindings.GameCamera=camera;
   systems.AddComponent<RunBuild>();var sound=systems.AddComponent<AudioSource>();sound.playOnAwake=false;var director=systems.AddComponent<PlayableDirector>();director.playOnAwake=false;var cinematic=systems.AddComponent<CinematicDirector>();
   cinematic.assets=new[]{Resources.Load<TimelineAsset>("Cinematics/DungeonEntrance"),Resources.Load<TimelineAsset>("Cinematics/PlayerDeath"),Resources.Load<TimelineAsset>("Cinematics/BossEntrance")};var hud=systems.AddComponent<GameHUD>();
   var world=new GameObject("World").transform;
   var town=WesternEnvironment.Build(true,0);var authoredTown=EditableWorldBaker.Bake(town,-1,new RoomDoor[0],camera);Object.DestroyImmediate(town.Root.gameObject);var townPrefab=Save(authoredTown.gameObject,Root+"/Rooms/Hirva.prefab");bindings.Hirva=((GameObject)PrefabUtility.InstantiatePrefab(townPrefab,world)).GetComponent<RoomAuthoring>();
   var temporary=new GameObject("Legacy_Migration_Source");var rooms=ExplorationStage.Create(temporary.transform,0);bindings.Rooms=new RoomAuthoring[6];
   for(int i=0;i<6;i++)
   {
    var room=EditableWorldBaker.Bake(rooms[i].Layout,i,rooms[i].Doors.ToArray(),camera);var prefab=Save(room.gameObject,Root+"/Rooms/Room_"+(i+1).ToString("D2")+".prefab");var instance=((GameObject)PrefabUtility.InstantiatePrefab(prefab,world)).GetComponent<RoomAuthoring>();instance.transform.position=PaperWorld.Point(RoomGraph.Centers[i]);instance.gameObject.SetActive(false);bindings.Rooms[i]=instance;
   }
   Object.DestroyImmediate(temporary);
   var actors=new GameObject("Actors").transform;
   var hunter=Actor("Hunter",false);var raven=Actor("Raven",true);
   bindings.Hunter=((GameObject)PrefabUtility.InstantiatePrefab(hunter,actors)).GetComponent<Player>();bindings.Hunter.transform.position=new Vector2(0,-.7f);bindings.Raven=((GameObject)PrefabUtility.InstantiatePrefab(raven,actors)).GetComponent<Raven>();bindings.Raven.transform.position=new Vector2(-1,.5f);
   bindings.EnemyContainer=new GameObject("Spawned_Enemies").transform;bindings.EnemyContainer.SetParent(actors,false);bindings.EnemyPrefabs=new Enemy[4];
   for(int i=0;i<4;i++)
   {
    var root=new GameObject(i==3?"Boss_SealKeeper":"Enemy_"+i);var enemy=root.AddComponent<Enemy>();enemy.Init(i,0);enemy.AuthoredWarning.transform.SetParent(root.transform,true);
    PrepareActor(root,enemy.AuthoredSprite);bindings.EnemyPrefabs[i]=Save(root,Root+"/Actors/"+(i==3?"Boss":"Enemy_"+i)+".prefab").GetComponent<Enemy>();
   }
   var catalog=ScriptableObject.CreateInstance<VisualCatalog>();catalog.Icons=Resources.LoadAll<Texture2D>("Combat").Select(t=>new NamedSprite{Id=t.name,Sprite=EditableAssetStore.Save(CombatArt.Get(t.name))}).ToArray();AssetDatabase.CreateAsset(catalog,Root+"/UI/VisualCatalog.asset");
   var ui=new EditableUIBaker().Build(catalog);var uiPrefab=Save(ui.gameObject,Root+"/UI/GameUI.prefab");bindings.UI=((GameObject)PrefabUtility.InstantiatePrefab(uiPrefab)).GetComponent<GameUIView>();hud.View=bindings.UI;
   new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
   game.Hero=bindings.Hunter;game.Crow=bindings.Raven;game.Cam=camera;
   var backdrop=new GameObject("Viewport_Backdrop",typeof(Camera)).GetComponent<Camera>();backdrop.depth=-100;backdrop.cullingMask=0;backdrop.backgroundColor=Color.black;
   AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/Main.unity");
   EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Main.unity",true)};
   Debug.Log("WNN_EDITABLE_MIGRATION_SUCCESS");
   ProjectSetup.ValidateAndBuild();
  }
  static GameObject Save(GameObject root,string path){EditableAssetStore.Renderers(root);var result=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return result;}
  static GameObject Actor(string name,bool raven)
  {
   var root=new GameObject(name);var sprite=CombatArt.Actor(root.transform,raven?"Raven":"Hunter",raven?1.1f:1.9f);sprite.name="Sprite_Frames";
   Component component=raven?(Component)root.AddComponent<Raven>():root.AddComponent<Player>();if(!raven)HunterLocomotionAtlas.Pose(sprite,0,0);
   var serialized=new SerializedObject(component);serialized.FindProperty("art").objectReferenceValue=sprite;serialized.ApplyModifiedPropertiesWithoutUndo();PrepareActor(root,sprite);return Save(root,Root+"/Actors/"+name+".prefab");
  }
  static void PrepareActor(GameObject root,SpriteRenderer sprite)
  {
   if(!root.GetComponent<WorldDepth>())root.AddComponent<WorldDepth>();if(!root.GetComponent<SortingGroup>())root.AddComponent<SortingGroup>();
   var presentation=root.AddComponent<ActorPresentation>();presentation.SpriteFrames=sprite;
   var custom=new GameObject("Custom_Visual_Spine_Or_Animator",typeof(SortingGroup)).transform;custom.SetParent(root.transform,false);custom.gameObject.SetActive(false);presentation.CustomVisual=custom;
   var renderers=new GameObject("Rendered_Sprites_2_5D").transform;renderers.SetParent(root.transform,false);
   foreach(var source in root.GetComponentsInChildren<SpriteRenderer>(true))
   {
    source.gameObject.layer=PaperWorld.LogicLayer;source.sprite=EditableAssetStore.Save(source.sprite);source.sharedMaterial=EditableAssetStore.Save(source.sharedMaterial);
    var visible=new GameObject("View_"+source.name,typeof(SpriteRenderer),typeof(SortingGroup)).GetComponent<SpriteRenderer>();visible.transform.SetParent(renderers,false);visible.sprite=source.sprite;visible.sharedMaterial=source.sharedMaterial;visible.color=source.color;visible.enabled=source.enabled;
    bool flat=source.sortingOrder<0||source.name=="Shadow"||source.name=="Attack warning";
    visible.transform.position=PaperWorld.Point(source.transform.position,flat?.015f:0);visible.transform.rotation=Quaternion.Euler(flat?90:PaperWorld.Pitch,0,0);visible.transform.localScale=source.transform.lossyScale;
    var card=source.gameObject.AddComponent<PaperCard>();card.Source=source;card.Visible=visible;card.Flat=flat;visible.GetComponent<SortingGroup>().sortAtRoot=true;
    if(source==sprite)presentation.SpriteCard=card;
   }
  }
  [MenuItem("Western Lemegeton/Authoring/Export Editable Unity Package")]
  public static void Export()
  {
   EditableValidation.ValidateImportedProject();AssetDatabase.SaveAssets();Directory.CreateDirectory("Builds/Packages");
   AssetDatabase.ExportPackage("Assets","Builds/Packages/WesternLemegeton_Editable.unitypackage",ExportPackageOptions.Recurse);
   Debug.Log("WNN_EDITABLE_PACKAGE_EXPORTED");
  }
 }
}
