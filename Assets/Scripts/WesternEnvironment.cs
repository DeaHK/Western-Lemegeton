using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WesternLemegeton
{
    public static class WesternArt
    {
        [Serializable] public class Entry { public string name; public int x,y,width,height,sourceWidth,sourceHeight; }
        [Serializable] public class Catalog { public Entry[] entries; }
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static Dictionary<string, Entry> bounds;
        public static Sprite Get(string name)
        {
            if (sprites.TryGetValue(name, out var sprite)) return sprite;
            if (bounds == null)
            {
                bounds = new Dictionary<string, Entry>();
                var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Western/bounds").text.TrimStart('\uFEFF'));
                foreach (var entry in catalog.entries) bounds.Add(entry.name, entry);
            }
            var texture = Resources.Load<Texture2D>("Western/" + name);
            if (!texture || !bounds.TryGetValue(name, out var b)) throw new InvalidOperationException("Missing WNN art: " + name);
            float sx = texture.width / (float)b.sourceWidth, sy = texture.height / (float)b.sourceHeight;
            sprite = Sprite.Create(texture, new Rect(b.x*sx,b.y*sy,b.width*sx,b.height*sy), new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);
            sprite.name = name; sprites.Add(name,sprite); return sprite;
        }
    }

    // Keep a multipart actor together, then order it against scenery by the feet position.
    

    // Tall scenery becomes translucent only when it would conceal the player behind its roof/leaves.
    

    public static class WesternEnvironment
    {
        public class Layout
        {
            public Transform Root;
            public bool Town;
            public RoomAuthoring Authored;
            public SpriteRenderer ExitMarker;
            public readonly List<Rect> Obstacles=new List<Rect>();
            public readonly List<Rect> Buildings=new List<Rect>();
        }
        public static Layout Build(bool town,int stage,int mapIndex=0,bool connected=false)
        {
            var map=new Layout{Town=town,Root=new GameObject(town?"HIRVA • frontier town":"WILDERNESS • sector "+(stage+1)).transform};
            Ground(map,town);
            if(town) Town(map); else Wilderness(map,mapIndex,connected);
            Mood(map,town);DepthScenery(map,town,stage,mapIndex);if(Application.isPlaying)map.Root.gameObject.AddComponent<WesternAtmosphere>().Initialize(town,stage,mapIndex);
            return map;
        }
        // Width multipliers are asset-specific; façades keep the town street spacing.
        public static float PropScale(string asset,bool town=false)
        {
            if(asset=="floor_texture_original")return 1;
            if(asset=="Wnn")return 1.35f;
            if(town)return asset.StartsWith("Deco")||asset=="OBJ10"?1.15f:1;
            switch(asset)
            {
                case "Wnn1":return 1.55f;
                case "Wnn8":return 1.6f;
                case "OBJ10":return 1.4f;
                case "Deco1":case "Deco2":case "Deco3":case "Deco8":return 1.4f;
                case "Deco4":case "Deco9":return 1.25f;
                case "OBJ8":case "OBJ9":return 1.35f;
                case "OBJ5":return 1.2f;
                default:return asset.StartsWith("Fence")?1.2f:1.12f;
            }
        }
        public static float PropWidth(string asset,float requested,bool town=false)
        {
            float width=requested*PropScale(asset,town);
            var sprite=WesternArt.Get(asset);float aspect=sprite.bounds.size.x/sprite.bounds.size.y;
            if(asset=="Wnn")return 5.2f*aspect;
            if(town)return width;
            switch(asset)
            {
                case "Wnn1":return Mathf.Clamp(width/aspect,3.5f,3.9f)*aspect;
                case "Wnn8":return Mathf.Clamp(width/aspect,5.5f,6.2f)*aspect;
                case "OBJ10":return Mathf.Clamp(width/aspect,1.8f,2.25f)*aspect;
                case "OBJ8":case "OBJ9":return Mathf.Clamp(width/aspect,.9f,1.2f)*aspect;
                case "Deco1":case "Deco2":case "Deco3":case "Deco8":return Mathf.Clamp(width/aspect,2.4f,3.5f)*aspect;
                case "Deco7":return Mathf.Clamp(width,.65f,1.45f);
                default:return asset.StartsWith("Fence")?Mathf.Clamp(width/aspect,1.25f,1.7f)*aspect:width;
            }
        }
        public static SpriteRenderer Prop(Layout map,string asset,float x,float y,float width,bool fade=true,bool mirror=false)
        {
            width=PropWidth(asset,width,map.Town);
            var go=new GameObject(asset);go.transform.SetParent(map.Root,false);go.transform.localPosition=new Vector3(x,y,0);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=WesternArt.Get(asset);sr.flipX=mirror;
            float scale=width/sr.sprite.bounds.size.x;go.transform.localScale=Vector3.one*scale;
            sr.sortingOrder=WorldDepth.Order(y);PaperWorld.Attach(sr,asset=="floor_texture_original");
            float height=sr.sprite.bounds.size.y*scale;
            if(fade&&height>1.7f){var f=go.AddComponent<SceneryOccluder>();f.Width=width;f.Height=height;}
            return sr;
        }
        static void Ground(Layout map,bool town)
        {
            var ground=Prop(map,"floor_texture_original",0,-24,64,false);
            ground.transform.localScale=new Vector3(64/ground.sprite.bounds.size.x,48/ground.sprite.bounds.size.y,1);
            ground.sortingOrder=-1500;ground.color=town?new Color(.64f,.53f,.62f):new Color(.65f,.49f,.48f);
            if(town)Ink.Shape("Sunlit dust road",map.Root,new Vector2(0,-.7f),new Vector2(35,4.6f),new Color(1,.67f,.38f,.12f),-1400,true).sprite=Ink.SoftDisc;
            // Local wheel tracks are placed at the wagon's actual contact points by PropGrounding.
        }
        static void Mood(Layout map,bool town)
        {
            // Broad transparent light pools keep the original painted textures legible.
            float[] lamps=town?new[]{-10.5f,-4.8f,.55f,6.2f,12.8f}:new[]{-10f,4f,13f};
            foreach(float x in lamps)
            {
                float y=town?.4f:x==4?3:-1;
                Ink.Shape("Amber lantern pool",map.Root,new Vector2(x,y),new Vector2(7,4.5f),new Color(1,.53f,.19f,.19f),-1100,true).sprite=Ink.SoftDisc;
                Ink.Shape("Lantern ember",map.Root,new Vector2(x,y+.7f),new Vector2(.13f,.2f),new Color(1,.75f,.32f),WorldDepth.Order(y)+1,true);
            }
            // Large foreground silhouettes frame the playable clearing without blocking its exits.
            var left=Prop(map,"Deco9",-17.6f,-10.3f,8.3f);var right=Prop(map,"Deco4",17.8f,-10.3f,8.1f);
            left.color=right.color=new Color(.45f,.34f,.4f);
            if(Application.isPlaying){left.gameObject.AddComponent<LandscapeParallax>().Factor=-.065f;right.gameObject.AddComponent<LandscapeParallax>().Factor=-.065f;}
        }
        static void DepthScenery(Layout map,bool town,int stage,int mapIndex)
        {
            for(int i=0;i<5;i++)
            {
                var distant=Prop(map,i%2==0?"Deco9":"Deco4",-27+i*13,12.7f+(i%3)*1.15f,13+(i%2)*2,false);
                distant.color=new Color(.52f,.47f,.60f,.82f);
                if(Application.isPlaying)distant.gameObject.AddComponent<LandscapeParallax>().Factor=.26f;
            }
            // The six-room dressing owns near scenery so actual door openings stay clear.
        }
        static void Building(Layout map,string asset,float x,float y,float width,float depth)
        {
            Prop(map,asset,x,y,width);
            float finalWidth=PropWidth(asset,width,map.Town);depth*=Mathf.Sqrt(finalWidth/width);width=finalWidth;
            var rect=new Rect(x-width*.42f,y+.05f,width*.84f,depth);
            map.Obstacles.Add(rect);map.Buildings.Add(rect);
        }
        static void Cover(Layout map,string asset,float x,float y,float width,float depth,bool mirror=false)
        {
            Prop(map,asset,x,y,width,true,mirror);
            float finalWidth=PropWidth(asset,width,map.Town);depth*=Mathf.Sqrt(finalWidth/width);width=finalWidth;
            map.Obstacles.Add(new Rect(x-width*.35f,y+.08f,width*.7f,depth));
        }
        static void Exit(Layout map,bool town)
        {
            Prop(map,"Wnn",13.1f,-1.6f,3.5f);
            Prop(map,"OBJ7",10.9f,-1.5f,1.1f);
            map.ExitMarker=Ink.Shape("Trail exit marker",map.Root,new Vector2(13.1f,-1.3f),new Vector2(2.1f,.6f),town?new Color(.45f,1,.78f,.65f):new Color(1,.27f,.12f,.7f),-700,true);
            // Corner posts flank a traversable gateway; the center remains open for the E prompt.
            map.Obstacles.Add(new Rect(11.55f,-1.5f,.3f,.45f));
            map.Obstacles.Add(new Rect(14.4f,-1.5f,.3f,.45f));
        }
        static void Town(Layout map)
        {
            // A continuous east-west main street separates the two rows of frontier facades.
            Building(map,"Wnn6",-10.5f,1.0f,5.4f,1.5f); // saloon
            Building(map,"OBJ2",-4.8f,1.55f,4.8f,1.45f); // sheriff
            Building(map,"OBJ1",.55f,1.45f,4.7f,1.45f);  // general store
            Building(map,"OBJ5",6.2f,1.75f,3.15f,1.4f); // church
            Building(map,"Wnn4",-10.6f,-5.85f,4.7f,1.5f);// blacksmith
            Building(map,"Wnn5",-4.8f,-5.95f,5.1f,1.5f); // stable
            Building(map,"Wnn3",1.4f,-5.75f,4.1f,1.35f); // residence
            Building(map,"Wnn7",7.1f,-5.85f,4.2f,1.4f);
            Cover(map,"OBJ3",9.8f,2.6f,1.7f,.8f);     // well beside the chapel
            Building(map,"Wnn2",12.8f,3.6f,1.9f,.6f);
            Prop(map,"OBJ6",-13.8f,-1.6f,1.05f);
            Prop(map,"OBJ6",4.7f,-2.5f,.85f);
            Cover(map,"OBJ10",-12.8f,-4,2.1f,.75f);
            Cover(map,"OBJ8",-7.15f,1.5f,.62f,.42f);
            Cover(map,"OBJ9",3.35f,1.5f,.8f,.48f);
            for(int i=0;i<7;i++)Prop(map,"Fence10",-12+i*4,7.7f,3.8f,false);
            Prop(map,"Fence09",-14.7f,3.3f,2.2f,false);
            Prop(map,"Fence08",12.5f,-6.4f,2.5f,false);
            Prop(map,"Deco1",-14.3f,-6.8f,1.15f);
            Prop(map,"Deco3",14.35f,5.9f,1.05f);
            Prop(map,"Deco10",9.7f,7.65f,1.7f,false);
            Prop(map,"Deco5",-1.5f,-7.6f,1.5f,false);
            Prop(map,"Deco6",9.7f,-3.1f,.7f,false);
            Exit(map,true);
        }
        static void Wilderness(Layout map,int stage,bool connected=false)
        {
            // Connected rooms use door-aware boundary clusters instead of scattered borders.
            if(!connected){
            float[] ridgeX={-14,-7.6f,7.4f,15.2f},ridgeY={8.6f,9.8f,9.3f,8.9f},ridgeSize={5.5f,4.1f,4.7f,5.8f};
            for(int i=0;i<4;i++)
            {
                var rock=Prop(map,i%2==0?"Deco9":"Deco4",ridgeX[i],ridgeY[i],ridgeSize[i]);
                rock.color=new Color(.82f,.66f,.63f);
            }
            for(int i=0;i<7;i++)Prop(map,i%3==0?"Deco10":"Deco5",-13+i*4.3f,-9.5f,1.6f+(i%2)*.6f,false);
            Prop(map,"Deco2",-14.5f,3.8f,1.25f);
            Prop(map,"Deco8",-14.1f,-5.3f,1.5f);
            Prop(map,"Deco3",14.2f,4.9f,1.4f);
            Prop(map,"Deco1",14.2f,-6.6f,1.7f);
            Prop(map,"Fence08",-14.1f,-1.5f,2.0f,false);
            Prop(map,"Deco7",-8.4f,-.8f,1.05f,false);
            Prop(map,"Deco5",-4.2f,6.5f,1.5f,false);
            Prop(map,"Deco6",2.1f,-6.7f,.9f,false);
            Prop(map,"Deco10",11.2f,-5.8f,1.65f,false);
            }
            switch(stage)
            {
                case 0:
                    Cover(map,"Deco4",-5.9f,-3.2f,2.7f,1.0f);
                    Cover(map,"Deco8",5.9f,2.1f,1.75f,.85f);
                    Cover(map,"Wnn1",-2.1f,3.6f,3.1f,1.0f);
                    Cover(map,"Deco9",3.4f,-5.5f,2.7f,1.0f);
                    Prop(map,"Fence08",8.8f,5.4f,2.8f,false);break;
                case 1:
                    Cover(map,"OBJ10",-5.5f,-3.4f,2.9f,.9f);
                    Cover(map,"Wnn1",3.7f,3.0f,3.8f,1.3f);
                    Cover(map,"OBJ9",-.8f,4.5f,1.1f,.65f);
                    Cover(map,"Deco4",7.6f,-5.3f,3.0f,1.1f);
                    Cover(map,"Deco1",-6.9f,4.2f,1.8f,.75f);
                    Prop(map,"Fence07",8.6f,6.4f,3.4f,false);break;
                case 2:
                    Building(map,"OBJ5",-6.4f,3.5f,4.3f,1.8f);
                    Cover(map,"Deco9",-5.6f,-3.8f,3.1f,1.1f);
                    Cover(map,"Deco8",6.4f,-4.1f,2,1);
                    Prop(map,"Fence03",-3.8f,5.4f,3.0f,false);
                    Prop(map,"Fence01",7.1f,5.4f,3.2f,false);
                    Prop(map,"Deco7",.2f,-4.9f,1.7f,false);break;
                case 3:
                    Building(map,"Wnn8",-5.8f,3.4f,2.4f,.8f);
                    Cover(map,"Deco9",4.0f,3.7f,3.8f,1.5f);
                    Cover(map,"Wnn1",-3.2f,-4.9f,3.2f,1.1f);
                    Cover(map,"Deco2",7.5f,-4.6f,1.5f,.75f);
                    Prop(map,"Fence08",.2f,5.4f,3,false);break;
                case 4:
                    Cover(map,"Deco9",-5.5f,2.5f,4.1f,1.3f);
                    Cover(map,"Deco4",5.5f,-4.6f,4.2f,1.3f);
                    Cover(map,"OBJ10",1.2f,4.5f,2.5f,.85f);
                    Cover(map,"OBJ9",-3.7f,-4.4f,1.1f,.6f);
                    Prop(map,"Fence07",8.5f,4.4f,3.1f,false);
                    Prop(map,"Deco7",-1,-4.1f,1.2f,false);break;
                default:
                    Cover(map,"Deco9",-6.9f,4.3f,3.7f,1.5f);
                    Cover(map,"Deco4",7.6f,4.5f,3.5f,1.1f);
                    Cover(map,"Deco8",-6.1f,-5.9f,1.7f,.85f);
                    Cover(map,"Deco1",7.3f,-5.5f,1.9f,.85f);
                    Prop(map,"Deco7",0,5.8f,2.2f,false);
                    // A dusty ritual clearing, with an open center for the boss patterns.
                    for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Prop(map,"Deco7",Mathf.Cos(a)*5,Mathf.Sin(a)*3.6f,.48f,false);}
                    break;
            }
            if(!connected)Exit(map,false);
        }
    }
}





