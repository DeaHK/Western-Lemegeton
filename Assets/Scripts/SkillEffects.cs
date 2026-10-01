using UnityEngine;
namespace WesternLemegeton
{
    // Cel-colored effects have their own lifetime and never replace the actor sprite.
    public sealed class SkillEffects:MonoBehaviour
    {
        public static readonly Vector3 MuzzleOffset=new Vector3(0,.95f,.65f);
        float age,duration;bool expand;SpriteRenderer[] strokes;Color[] colors;Vector3 renderOffset;
        static readonly Color Outline=new Color(.055f,.025f,.035f,.92f);
        static SkillEffects Begin(Vector2 position,float duration,bool expand=false)
        {
            var root=new GameObject("Cel skill effect");root.transform.position=position;
            var effect=root.AddComponent<SkillEffects>();effect.duration=duration;effect.expand=expand;return effect;
        }
        void Stroke(Vector2 a,Vector2 b,Color color,float width)
        {
            var v=b-a;float angle=Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg;
            Ink.Shape("Trace",transform,(a+b)*.5f,new Vector2(v.magnitude,width*2.1f),Outline,900,false,angle);
            Ink.Shape("Trace",transform,(a+b)*.5f,new Vector2(v.magnitude,width),color,901,false,angle);
        }
        void Finish(){strokes=GetComponentsInChildren<SpriteRenderer>();colors=new Color[strokes.Length];for(int i=0;i<colors.Length;i++){colors[i]=strokes[i].color;var card=strokes[i].GetComponent<PaperCard>();if(card)card.RenderOffset=renderOffset;}}
        public static void Slash(Vector2 at,Vector2 direction,float radius)
        {
            var e=Begin(at,.16f);e.renderOffset=new Vector3(0,.45f,.25f);float facing=Mathf.Atan2(direction.y,direction.x);
            for(int i=0;i<12;i++)
            {
                float a=facing+Mathf.Lerp(-1.2f,1.2f,i/12f),b=facing+Mathf.Lerp(-1.2f,1.2f,(i+1)/12f);
                e.Stroke(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,Ink.Cyan,.075f+Mathf.Sin(i/12f*Mathf.PI)*.10f);
            }
            e.Finish();
        }
        public static void Shot(Vector2 at,Vector2 direction,float length)
        {
            var e=Begin(at,.12f);e.renderOffset=MuzzleOffset;e.Stroke(direction*1.05f,direction*Mathf.Max(length,1.6f),Ink.Gold,.085f);
            var muzzle=direction*1.17f;Vector2 side=new Vector2(-direction.y,direction.x);
            e.Stroke(muzzle-direction*.18f,muzzle+direction*.35f,new Color(1,.93f,.65f),.13f);
            e.Stroke(muzzle-side*.20f,muzzle+side*.20f,Ink.Gold,.09f);e.Finish();
        }
        public static void Impact(Vector2 at,float radius,Color color)
        {
            var e=Begin(at,.23f,true);
            for(int i=0;i<20;i++)
            {
                float a=i*Mathf.PI/10,b=(i+1)*Mathf.PI/10;
                e.Stroke(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,color,.075f);
                if(i%4==0)e.Stroke(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*.8f,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*1.12f,Ink.Gold,.13f);
            }
            e.Finish();
        }
        void Update()
        {
            if(!Dungeon.I||!Dungeon.I.Running)return;age+=Time.deltaTime;
            if(age>=duration){Destroy(gameObject);return;}
            if(expand)transform.localScale=Vector3.one*Mathf.Lerp(.60f,1,Mathf.Clamp01(age/duration*2));
            if(strokes==null)return;float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,age/duration));
            for(int i=0;i<strokes.Length;i++)if(strokes[i]){var c=colors[i];c.a*=alpha;strokes[i].color=c;}
        }
    }
}
