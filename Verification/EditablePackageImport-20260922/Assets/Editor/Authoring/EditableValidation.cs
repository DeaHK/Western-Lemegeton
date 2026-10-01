using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace WesternLemegeton.EditorTools
{
 public static class EditableValidation
 {
  [MenuItem("Western Lemegeton/Authoring/Validate Saved Scene")]
  public static void ValidateImportedProject()
  {
   if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())throw new OperationCanceledException("Validation cancelled; unsaved edits preserved.");
   EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");var game=UnityEngine.Object.FindFirstObjectByType<Dungeon>();Require(game&&game.Scene,"Main scene contains serialized game systems");
   var scene=game.Scene;Require(scene.Rooms.Length==6&&scene.Hirva&&scene.Hunter&&scene.Raven&&scene.UI,"All scene references persist");
   foreach(var room in scene.Rooms)
   {Require(room&&room.Doors.Length>0&&room.InitialSpawn&&room.ServicePoint,"Room authoring anchors persist");foreach(var door in room.Doors)Require(door&&door.Owner==room&&door.Arrival&&door.Marker,"Door and arrival links persist");Require(room.GetComponentsInChildren<WorldProp>(true).Length>20,"Room contains editable scenery");}
   var ui=scene.UI;Require(ui.GetComponentsInChildren<Button>(true).Length>=25,"Native Canvas contains interactive buttons");Require(ui.References.Select(r=>r.Key).Distinct().Count()==ui.References.Count,"UI binding keys are unique");foreach(var reference in ui.References)Require(reference.Target,"UI binding target persists: "+reference.Key);
   foreach(var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))Require(renderer.sprite&&EditorUtility.IsPersistent(renderer.sprite),"Renderer references an exported sprite asset");
   foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"No missing scripts: "+t.name);
   Require(scene.EnemyPrefabs.All(p=>p&&p.AuthoredArt&&p.AuthoredSprite&&p.HealthFill&&p.AuthoredWarning),"Enemies use editable prefab visuals");
   Debug.Log("WNN_EDITABLE_IMPORT_VALIDATION_PASSED");
  }
  static void Require(bool ok,string message){if(!ok)throw new Exception("AUTHORING_FAILED: "+message);}
 }
}
