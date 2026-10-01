using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace WesternLemegeton.EditorTools
{
 public static class AuthoringFinalization
 {
  // Targeted additions to the initial migration, not a scene or layout generator.
  public static void Run()
  {
   var oldSquare=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Editable/Shared/Sprite_0005.asset");
   var solid=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Editable/Shared/SolidWhiteSprite.asset");
   if(!solid){var texture=new Texture2D(4,4,TextureFormat.RGBA32,false);texture.name="SolidWhite";texture.SetPixels(Enumerable.Repeat(Color.white,16).ToArray());texture.Apply();AssetDatabase.CreateAsset(texture,"Assets/Editable/Shared/SolidWhiteTexture.asset");solid=Sprite.Create(texture,new Rect(0,0,4,4),Vector2.one*.5f,4);AssetDatabase.CreateAsset(solid,"Assets/Editable/Shared/SolidWhiteSprite.asset");}
   foreach(var id in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Editable"}))
   {
    string assetPath=AssetDatabase.GUIDToAssetPath(id);var prefab=PrefabUtility.LoadPrefabContents(assetPath);bool changed=false;
    foreach(var sr in prefab.GetComponentsInChildren<SpriteRenderer>(true))if(sr.sprite==oldSquare){sr.sprite=solid;changed=true;}
    foreach(var im in prefab.GetComponentsInChildren<Image>(true))if(im.sprite==oldSquare){im.sprite=solid;changed=true;}
    if(changed)PrefabUtility.SaveAsPrefabAsset(prefab,assetPath);PrefabUtility.UnloadPrefabContents(prefab);
   }
   AssetDatabase.SaveAssets();
   string path="Assets/Editable/Actors/Hunter.prefab";var root=PrefabUtility.LoadPrefabContents(path);var p=root.GetComponent<ActorPresentation>();var bindings=p.Animations.ToList();
   for(int i=1;i<=4;i++){string state="Attack"+i.ToString("D2");if(!bindings.Any(b=>b.State==state))bindings.Add(new MotionBinding(state,"attack"+i.ToString("D2"),false));}p.Animations=bindings.ToArray();PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
   var uiMaterial=Material("UI_Default","UI/Default");var fontMaterial=uiMaterial;
   foreach(var asset in new[]{"GameUI","PassiveSlot"})
   {
    string uiPath="Assets/Editable/UI/"+asset+".prefab";var canvas=PrefabUtility.LoadPrefabContents(uiPath);
    foreach(var graphic in canvas.GetComponentsInChildren<Graphic>(true))if(!graphic.material||graphic.material==graphic.defaultMaterial)graphic.material=graphic is Text?fontMaterial:uiMaterial;
    foreach(var button in canvas.GetComponentsInChildren<Button>(true))if(button.GetComponent<RectTransform>().rect.height<33)foreach(var label in button.GetComponentsInChildren<Text>(true))label.fontSize=13;
    foreach(var label in canvas.GetComponentsInChildren<Text>(true))if(label.name.StartsWith("SkillLabel")){label.fontSize=12;label.alignment=TextAnchor.UpperCenter;}
    PrefabUtility.SaveAsPrefabAsset(canvas,uiPath);PrefabUtility.UnloadPrefabContents(canvas);
   }
   ProjectSetup.ValidateAndBuild();
  }
  static Material Material(string name,string shader)
  {
   string path="Assets/Editable/UI/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material)return material;
   var found=Shader.Find(shader);if(!found)throw new System.Exception("Missing UI shader "+shader);material=new Material(found);AssetDatabase.CreateAsset(material,path);return material;
  }
 }
}

