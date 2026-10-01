using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace WesternLemegeton.EditorTools
{
 public static class EditableAssetStore
 {
  static readonly Dictionary<int,Object> saved=new Dictionary<int,Object>();static int count;
  public const string Root="Assets/Editable";
  public static T Save<T>(T value) where T:Object
  {
   if(!value||AssetDatabase.Contains(value)||!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(value)))return value;
   if(saved.TryGetValue(value.GetInstanceID(),out var found))return (T)found;
   if(value is Sprite sprite)Save(sprite.texture);
   if(value is Material material&&material.mainTexture)Save(material.mainTexture);
   Directory.CreateDirectory(Root+"/Shared");string path=Root+"/Shared/"+typeof(T).Name+"_"+(count++).ToString("D4")+".asset";
   string originalName=value.name;AssetDatabase.CreateAsset(value,AssetDatabase.GenerateUniqueAssetPath(path));value.name=originalName;EditorUtility.SetDirty(value);saved[value.GetInstanceID()]=value;return value;
  }
  public static void Renderers(GameObject root)
  {foreach(var sr in root.GetComponentsInChildren<SpriteRenderer>(true)){sr.sprite=Save(sr.sprite);if(sr.sharedMaterial&&!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sr.sharedMaterial)))continue;sr.sharedMaterial=Save(sr.sharedMaterial);}}
 }
}
