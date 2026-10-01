using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    // Ground decals belong to the render representation, never to the collision layout.
    // The wagon artwork includes wheels and debris at different heights inside one PNG.
    // Project its painted contact points along the viewing ray onto the XZ floor so that
    // shadows stay attached during soft follow, Timeline zooms and sprite mirroring.
    public sealed class PropGrounding
    {
        [System.Serializable] public class Decal
        {
            public SpriteRenderer Renderer;
            public Vector2 Anchor, Offset, Size;
            public Color Color;
            public float Angle;
        }

        readonly List<Decal> decals=new List<Decal>();
        readonly Transform root;
        public Transform Root=>root;
        public List<Decal> Decals=>decals;
        public PropGrounding(Transform savedRoot,List<Decal> saved){root=savedRoot;decals=saved;}
        const float GroundHeight=.017f;
        static readonly Color Soil=new Color(.26f,.115f,.045f,.22f);
        static readonly Color Shadow=new Color(.105f,.048f,.055f,.29f);
        static readonly Color Contact=new Color(.075f,.031f,.022f,.63f);

        public PropGrounding(SpriteRenderer source)
        {
            root=new GameObject("Grounding / "+source.name).transform;
            string asset=source.sprite.name;
            if(asset=="Wnn1") Wagon();
            else if(asset=="OBJ10") Cart();
            else Base(asset);
        }

        void Add(string name,Vector2 anchor,Vector2 offset,Vector2 size,Color color,int order,float angle=0,bool soft=true)
        {
            var sr=new GameObject(name,typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            sr.transform.SetParent(root,false);sr.sprite=soft?Ink.SoftDisc:Ink.Square;
            sr.color=color;sr.sortingOrder=order;
            decals.Add(new Decal{Renderer=sr,Anchor=anchor,Offset=offset,Size=size,Color=color,Angle=angle});
        }

        void Foot(Vector2 anchor,float width,float depth)
        {
            Add("Compressed earth",anchor,Vector2.zero,new Vector2(width*1.9f,depth*2.8f),Soil,-9700);
            Add("Attached sunset shadow",anchor,new Vector2(width*.20f,-depth*.22f),new Vector2(width*1.65f,depth*1.8f),Shadow,-9500,-24);
            Add("Contact occlusion",anchor,new Vector2(0,.008f),new Vector2(width,depth),Contact,-9100);
        }

        void Wagon()
        {
            var body=new Vector2(.45f,.27f);
            Add("Wagon settled soil",body,Vector2.zero,new Vector2(.98f,.46f),Soil,-9800,-18);
            Add("Under wagon body",body,Vector2.zero,new Vector2(.70f,.33f),new Color(.12f,.055f,.035f,.43f),-9450,-18);
            // Coordinates are normalized within WesternArt's cropped sprite, bottom to top.
            Foot(new Vector2(.135f,.317f),.17f,.080f); // Standing wheel's bottom rim.
            Foot(new Vector2(.345f,.175f),.23f,.074f); // Crate resting on the dirt.
            Foot(new Vector2(.840f,.128f),.26f,.090f); // Detached wheel and splinters.
            Foot(new Vector2(.748f,.031f),.15f,.054f); // Broken shaft's tip.
            Ruts(new Vector2(.135f,.317f));
            Ruts(new Vector2(.54f,.37f));
            Scatter(body,25,1957,.55f,.25f);
        }

        void Cart()
        {
            var body=new Vector2(.44f,.34f);
            Add("Cart settled soil",body,Vector2.zero,new Vector2(.88f,.39f),Soil,-9800,-16);
            Add("Under cart",body,Vector2.zero,new Vector2(.62f,.30f),Shadow,-9450,-16);
            Foot(new Vector2(.14f,.274f),.19f,.075f);
            Foot(new Vector2(.75f,.318f),.17f,.068f);
            Foot(new Vector2(.965f,.014f),.08f,.045f);
            Ruts(new Vector2(.14f,.274f));Ruts(new Vector2(.75f,.318f));
            Scatter(body,20,3301,.50f,.23f);
        }

        void Base(string asset)
        {
            var anchor=new Vector2(.5f,.055f);
            bool fence=asset.StartsWith("Fence"),rock=asset=="Deco4"||asset=="Deco9";
            if(fence)
            {
                Foot(new Vector2(.16f,.065f),.15f,.085f);
                Foot(new Vector2(.83f,.065f),.15f,.085f);
            }
            else
            {
                Add("Settled ground",anchor,Vector2.zero,new Vector2(.97f,.28f),Soil,-9800);
                Add("Short attached cast",anchor,new Vector2(.095f,-.025f),new Vector2(.91f,.22f),Shadow,-9500,-20);
                Add("Broad base contact",anchor,Vector2.zero,new Vector2(.74f,.135f),new Color(.085f,.038f,.025f,.51f),-9100);
                if(rock)
                {
                    Foot(new Vector2(.22f,.075f),.22f,.085f);
                    Foot(new Vector2(.74f,.052f),.21f,.075f);
                }
            }
            // Stable asset-based variation must not consume Unity's combat random sequence.
            int seed=17;foreach(char c in asset)seed=unchecked(seed*31+c);
            Scatter(anchor,fence?5:9,seed,.53f,.14f);
        }

        void Ruts(Vector2 wheel)
        {
            var direction=new Vector2(-.80f,.60f);
            for(int i=0;i<7;i++)
            {
                var offset=direction*(.06f+i*.066f);
                Add("Broken wheel rut",wheel,offset,new Vector2(.095f,.012f),new Color(.20f,.085f,.035f,.25f),-9600,-37);
                Add("Rut sand lip",wheel,offset+new Vector2(.006f,.012f),new Vector2(.076f,.009f),new Color(.79f,.46f,.21f,.22f),-9550,-37);
            }
        }

        void Scatter(Vector2 anchor,int count,int seed,float radiusX,float radiusY)
        {
            var rng=new System.Random(seed);
            for(int i=0;i<count;i++)
            {
                float angle=(float)rng.NextDouble()*Mathf.PI*2;
                float radius=.65f+(float)rng.NextDouble()*.5f;
                var offset=new Vector2(Mathf.Cos(angle)*radiusX,Mathf.Sin(angle)*radiusY)*radius;
                float size=.012f+(float)rng.NextDouble()*.014f;
                float turn=(float)rng.NextDouble()*80-40;
                Add("Grit shadow",anchor,offset,new Vector2(size*1.8f,size*.80f),new Color(.15f,.066f,.027f,.31f),-9300,turn);
                Add("Ochre stone chip",anchor,offset+new Vector2(-.002f,.004f),new Vector2(size,size*.46f),new Color(.63f,.35f,.16f,.47f),-9200,turn,false);
                if(i%4==0)
                    Add("Wind banked sand",anchor,offset,new Vector2(.17f,.055f),new Color(.76f,.40f,.17f,.15f),-9750,-18);
            }
        }

        public void Update(SpriteRenderer card,Camera camera)
        {
            SetVisible(card.enabled);
            if(!card.enabled||!camera||!card.sprite)return;
            var bounds=card.sprite.bounds;
            float width=bounds.size.x*Mathf.Abs(card.transform.lossyScale.x);
            var plane=new Plane(Vector3.up,new Vector3(0,GroundHeight,0));
            foreach(var decal in decals)
            {
                var local=new Vector3(bounds.min.x+bounds.size.x*decal.Anchor.x,bounds.min.y+bounds.size.y*decal.Anchor.y,0);
                if(card.flipX)local.x=-local.x;if(card.flipY)local.y=-local.y;
                var paintedPoint=card.transform.TransformPoint(local);
                var ray=camera.orthographic?new Ray(paintedPoint-camera.transform.forward*100,camera.transform.forward):new Ray(camera.transform.position,paintedPoint-camera.transform.position);
                if(!plane.Raycast(ray,out float distance)){decal.Renderer.enabled=false;continue;}
                decal.Renderer.enabled=true;
                var floor=ray.GetPoint(distance);
                float mirror=card.flipX?-1:1;
                decal.Renderer.transform.position=floor+new Vector3(decal.Offset.x*mirror,0,decal.Offset.y)*width;
                decal.Renderer.transform.rotation=Quaternion.Euler(90,0,decal.Angle*mirror);
                decal.Renderer.transform.localScale=new Vector3(decal.Size.x*width,decal.Size.y*width,1);
                // Occluder fading must expose enemies rather than leave an opaque shadow slab.
                var color=decal.Color;color.a*=Mathf.Lerp(.48f,1,card.color.a);decal.Renderer.color=color;
            }
        }

        public void SetVisible(bool value){if(root && root.gameObject.activeSelf!=value)root.gameObject.SetActive(value);}
        public void Dispose(){if(root)Object.Destroy(root.gameObject);}
    }
}
