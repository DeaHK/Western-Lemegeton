using UnityEngine;
namespace WesternLemegeton
{
 [System.Serializable]public class NamedSprite{public string Id;public Sprite Sprite;}
 [CreateAssetMenu(menuName="Western Lemegeton/Visual Catalog")]
 public sealed class VisualCatalog:ScriptableObject
 {
  public NamedSprite[] Icons;
  public Sprite Get(string id){foreach(var item in Icons)if(item.Id==id)return item.Sprite;return null;}
 }
}
