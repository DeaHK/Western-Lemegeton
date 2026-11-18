using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    // 한 번 투척한 묶음 전체가 명중 기록을 공유하므로 착지/폭발 시각이 달라도 중복 피해가 없습니다.
    public sealed class LoganDynamiteVolley : MonoBehaviour
    {
        sealed class Bomb
        {
            public Vector2 Target;
            public float FlightTime;
            public SpriteRenderer Visual, Warning, FuseFill;
            public PaperCard Card;
            public bool Landed, Exploded;
        }
        readonly List<Bomb> bombs=new List<Bomb>();
        readonly HashSet<int> victims=new HashSet<int>();
        readonly List<Enemy> enemySnapshot=new List<Enemy>();
        Vector2 start;
        float age, damage, radius, fuse, height;
        bool hunterHit;
        int crowdControl;
        static Sprite warningRing;
        // 투명한 내부와 굵은 테두리를 가진 Logan 전용 원형 스프라이트입니다.
        static Sprite WarningRing
        {
            get
            {
                if (warningRing) return warningRing;
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "Logan dynamite warning ring";
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * 128);
                        // 반경의 약 2%인 얇은 테두리. 내부 도화선 표시는 별도로 유지합니다.
                        float alpha = Mathf.Clamp01(128 - d) * Mathf.Clamp01(d - 124);
                        pixels[y * size + x] = new Color(1, 1, 1, alpha);
                    }
                texture.SetPixels(pixels); texture.Apply();
                warningRing = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
                return warningRing;
            }
        }
        public int BombCount => bombs.Count;
        // 비행 중인 폭탄도 고정된 착지 예정 범위를 위험 구역으로 제공합니다.
        public bool Threatens(Vector2 position, float margin)
        {
            if(!isActiveAndEnabled) return false;
            foreach(var bomb in bombs)
                if(!bomb.Exploded && Vector2.Distance(position,bomb.Target)<=radius+Mathf.Max(0,margin)) return true;
            return false;
        }
        public static LoganDynamiteVolley Spawn(Vector2 origin, Vector2 target, LoganBossAttack.DynamiteSettings settings)
        {
            var volley=new GameObject("Logan / Dynamite volley").AddComponent<LoganDynamiteVolley>();
            volley.start=origin; volley.damage=Mathf.Max(0,settings.Damage);
            volley.crowdControl=settings.CrowdControl;
            volley.radius=Mathf.Max(.01f,settings.ExplosionRadius); volley.fuse=Mathf.Max(0,settings.FuseSeconds);
            volley.height=Mathf.Max(0,settings.ArcHeight);
            int count=1+Mathf.Max(0,settings.AdditionalCount);
            for(int i=0;i<count;i++)
            {
                Vector2 landing=Rules.ClampToRoom(target+Random.insideUnitCircle*Mathf.Max(0,settings.ScatterRadius),.05f);
                var visual=Ink.Shape("Logan / Dynamite",volley.transform,origin,new Vector2(.25f,.45f),Ink.Red,1000);
                var bomb=new Bomb { Target=landing, Visual=visual, Card=visual.GetComponent<PaperCard>(),
                    FlightTime=Mathf.Max(.01f,Vector2.Distance(origin,landing)/Mathf.Max(.01f,settings.FlightSpeed)) };
                if(bomb.Card) bomb.Card.Flat=false;
                volley.bombs.Add(bomb);
            }
            return volley;
        }
        void Update() { if(Dungeon.I && Dungeon.I.Running) Advance(Dungeon.I,Time.deltaTime); }
        public void Advance(Dungeon game,float dt)
        {
            if(!game || !game.Running) return;
            age+=Mathf.Max(0,dt);
            bool finished=true;
            foreach(var bomb in bombs)
            {
                if(bomb.Exploded) continue;
                float t=Mathf.Clamp01(age/bomb.FlightTime);
                if(bomb.Visual) bomb.Visual.transform.position=Vector2.Lerp(start,bomb.Target,t);
                if(bomb.Card) bomb.Card.RenderOffset=Vector3.up*(4*height*t*(1-t));
                if(t>=1 && !bomb.Landed)
                {
                    bomb.Landed=true;
                    bomb.Warning=Ink.Shape("Logan / Explosion warning",transform,bomb.Target,Vector2.one*radius*2,
                        new Color(1,0,0,.9f),-589,true);
                    bomb.Warning.sprite = WarningRing;
                    bomb.FuseFill=Ink.Shape("Logan / Fuse progress",transform,bomb.Target,Vector2.zero,
                        new Color(1,0,0,.35f),-590,true);
                }
                if (bomb.Landed && bomb.FuseFill)
                {
                    // 각 폭탄의 착지 시점부터 계산: 0이면 비어 있고 1이면 폭발 반경까지 찹니다.
                    float progress = fuse > 0 ? Mathf.Clamp01((age - bomb.FlightTime) / fuse) : 1;
                    bomb.FuseFill.transform.localScale = Vector3.one * (radius * 2 * progress);
                }
                if(age>=bomb.FlightTime+fuse)
                {
                    bomb.Exploded=true;
                    if(bomb.Visual) bomb.Visual.enabled=false;
                    if(bomb.Warning) bomb.Warning.enabled=false;
                    if(bomb.FuseFill) bomb.FuseFill.enabled=false;
                    Explode(game,bomb.Target);
                }
                else finished=false;
            }
            if(finished) RobeManPoisonZone.Remove(gameObject);
        }
        void Explode(Dungeon game,Vector2 center)
        {
            Ink.Ring(center,radius,Ink.Red,.3f);
            enemySnapshot.Clear(); enemySnapshot.AddRange(game.Enemies);
            foreach(var enemy in enemySnapshot)
                if(enemy && !enemy.Dead && enemy.gameObject.activeInHierarchy && Vector2.Distance(center,enemy.transform.position)<=radius && victims.Add(enemy.GetInstanceID()))
                    enemy.TakeEnvironmentDamage(damage);
            if(!hunterHit && game.Hero && game.Hero.Hp>0 && Vector2.Distance(center,game.Pos)<=radius)
            { hunterHit=true; game.Hero.Hurt(damage,crowdControl); }
        }
    }
}
