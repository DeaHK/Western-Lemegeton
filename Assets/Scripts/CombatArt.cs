using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    public static class CombatArt
    {
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        public static Texture2D Texture(string name)=>Resources.Load<Texture2D>("Combat/"+name);
        public static Sprite Get(string name)
        {
            if(sprites.TryGetValue(name,out var found))return found;
            var texture=Texture(name);if(!texture)throw new System.Exception("Missing combat art: "+name);
            var pixels=texture.GetPixels32();int x0=texture.width,y0=texture.height,x1=0,y1=0;
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a>16){x0=Mathf.Min(x0,x);x1=Mathf.Max(x1,x);y0=Mathf.Min(y0,y);y1=Mathf.Max(y1,y);}
            var sprite=Sprite.Create(texture,new Rect(x0,y0,x1-x0+1,y1-y0+1),new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);
            sprite.name=name;sprites.Add(name,sprite);return sprite;
        }
        public static SpriteRenderer Actor(Transform parent,string name,float height)
        {
            if(!parent.GetComponent<WorldDepth>())parent.gameObject.AddComponent<WorldDepth>();
            Ink.Shape("Shadow",parent,new Vector2(0,-.32f),new Vector2(.85f,.35f),new Color(.025f,.01f,.035f,.5f),-1,true);
            var go=new GameObject(name+" art");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,-.35f,0);
            var sprite=go.AddComponent<SpriteRenderer>();Pose(sprite,name,height);sprite.sortingOrder=12;
            PaperWorld.Attach(sprite,false);return sprite;
        }
        public static void Pose(SpriteRenderer sprite,string name,float height)
        {
            var art=Get(name);sprite.sprite=art;sprite.transform.localScale=Vector3.one*(height/art.bounds.size.y);
        }
    }
}
