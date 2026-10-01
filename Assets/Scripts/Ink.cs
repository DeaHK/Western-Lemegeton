using UnityEngine;

namespace WesternLemegeton
{
    public static class Ink
    {
        static Sprite square, disc, softDisc;
        public static readonly Color Gold = new Color(1f,.68f,.27f);
        public static readonly Color Cyan = new Color(.35f,.9f,.94f);
        public static readonly Color Red = new Color(.94f,.22f,.26f);
        public static Sprite Square
        {
            get { if (!square) {var texture=new Texture2D(4,4,TextureFormat.RGBA32,false);texture.name="Solid_White";var pixels=new Color[16];for(int i=0;i<16;i++)pixels[i]=Color.white;texture.SetPixels(pixels);texture.Apply();square=Sprite.Create(texture,new Rect(0,0,4,4),Vector2.one*.5f,4);} return square; }
        }
        public static Sprite Disc
        {
            get
            {
                if (disc) return disc;
                var t = new Texture2D(64,64,TextureFormat.RGBA32,false); t.filterMode=FilterMode.Bilinear;
                var pixels = new Color[4096];
                for(int y=0;y<64;y++) for(int x=0;x<64;x++) { float d=Vector2.Distance(new Vector2(x+.5f,y+.5f), Vector2.one*32); pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-d)); }
                t.SetPixels(pixels); t.Apply(); disc=Sprite.Create(t,new Rect(0,0,64,64),Vector2.one*.5f,64); return disc;
            }
        }
        public static Sprite SoftDisc
        {
            get
            {
                if(softDisc)return softDisc;
                var t=new Texture2D(128,128,TextureFormat.RGBA32,false);t.filterMode=FilterMode.Bilinear;
                var pixels=new Color[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*64)/64;pixels[y*128+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d*d),2));}
                t.SetPixels(pixels);t.Apply();softDisc=Sprite.Create(t,new Rect(0,0,128,128),Vector2.one*.5f,128);return softDisc;
            }
        }
        public static SpriteRenderer Shape(string name, Transform parent, Vector2 pos, Vector2 size, Color color, int layer=0, bool round=false, float angle=0)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.localPosition=pos;
            go.transform.localScale=size; go.transform.localRotation=Quaternion.Euler(0,0,angle);
            var sr=go.AddComponent<SpriteRenderer>(); sr.sprite=round?Disc:Square; sr.color=color; sr.sortingOrder=layer; PaperWorld.Attach(sr,layer<0||name=="Shadow"||name=="Trace"||name=="Attack warning"||name.Contains("bullet")); return sr;
        }
        public static Transform Actor(string name, Transform parent, Color coat, bool crow=false)
        {
            if(parent && !parent.GetComponent<WorldDepth>())parent.gameObject.AddComponent<WorldDepth>();
            var root=new GameObject(name).transform; root.SetParent(parent,false);
            Shape("Shadow",root,new Vector2(0,-.38f),new Vector2(1.05f,.38f),new Color(0,0,0,.45f),9,true);
            if(crow)
            {
                Shape("Wing L",root,new Vector2(-.35f,.15f),new Vector2(.85f,.25f),new Color(.12f,.12f,.2f),12,false,-25);
                Shape("Wing R",root,new Vector2(.35f,.15f),new Vector2(.85f,.25f),new Color(.12f,.12f,.2f),12,false,25);
                Shape("Body",root,Vector2.zero,new Vector2(.42f,.7f),coat,13,true);
                Shape("Beak",root,new Vector2(.25f,.2f),new Vector2(.25f,.12f),Gold,14,false,-12);
                Shape("Eye",root,new Vector2(.13f,.24f),Vector2.one*.09f,Cyan,15,true);
            }
            else
            {
                Shape("Boot L",root,new Vector2(-.21f,-.35f),new Vector2(.23f,.4f),new Color(.07f,.06f,.08f),10);
                Shape("Boot R",root,new Vector2(.21f,-.35f),new Vector2(.23f,.4f),new Color(.07f,.06f,.08f),10);
                Shape("Coat",root,new Vector2(0,-.02f),new Vector2(.85f,.9f),coat,11,false,0);
                Shape("Scarf",root,new Vector2(0,.27f),new Vector2(.65f,.14f),Gold,13,false,-8);
                Shape("Face",root,new Vector2(0,.46f),new Vector2(.5f,.4f),new Color(.72f,.55f,.4f),12,true);
                Shape("Hat",root,new Vector2(0,.7f),new Vector2(1.15f,.2f),coat*.7f,14,false,-7);
                Shape("Crown",root,new Vector2(0,.88f),new Vector2(.64f,.35f),coat*.8f,13);
                Shape("Eyes",root,new Vector2(0,.48f),new Vector2(.32f,.07f),Cyan,14);
            }
            return root;
        }
        public static void Line(Vector2 start, Vector2 end, Color c, float width=.08f, float life=.15f)
        {
            Vector2 d=end-start;
            var s=Shape("Trace",null,(start+end)/2,new Vector2(d.magnitude,width),c,1000,false,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
            s.gameObject.AddComponent<Fade>().Init(life);
        }
        public static void Ring(Vector2 p,float radius,Color c,float life=.3f)
        {
            for(int i=0;i<24;i++) { float a=i*Mathf.PI/12, b=(i+1)*Mathf.PI/12; Line(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,c,.055f,life); }
        }
        public static void Burst(Vector2 p,Color c,int count=8)
        {
            for(int i=0;i<count;i++) { var s=Shape("Spark",null,p,Vector2.one*Random.Range(.07f,.17f),c,1001); var f=s.gameObject.AddComponent<Fade>(); f.Init(Random.Range(.15f,.4f),Random.insideUnitCircle*5); }
        }
    }
    public class Fade:MonoBehaviour
    {
        float remaining,duration; Vector2 velocity; SpriteRenderer sr;
        public void Init(float life,Vector2 speed=default) { remaining=duration=life;velocity=speed;sr=GetComponent<SpriteRenderer>(); }
        void Update() { if(Dungeon.I && !Dungeon.I.Running) return; remaining-=Time.deltaTime; if(remaining<=0){Destroy(gameObject);return;} transform.position+=(Vector3)(velocity*Time.deltaTime);var c=sr.color;c.a=remaining/duration;sr.color=c; }
    }
}


