using UnityEngine;

namespace WesternLemegeton
{
    public class Enemy:MonoBehaviour
    {
        public int Kind;public float Hp,MaxHp;public bool Dead=>Hp<=0;
        public float AttackPower=>Kind==3?25:Kind==1?22:Kind==0?15:13;
        float cooldown,windup=-1,burnTime,burnTick,chargeTime,flash;
        Vector2 aim;Transform art,bar;SpriteRenderer warning;int cycle;
        public Transform AuthoredArt,HealthFill;
        public SpriteRenderer AuthoredSprite,AuthoredWarning;
        public Transform PresentationRoot=>art;
        public SpriteRenderer Presentation {get;private set;}
        public bool Telegraphing=>windup>0;
        public void Init(int kind,int room)
        {
            Kind=kind;MaxHp=kind==3?650:kind==1?80+room*13:55+room*13;Hp=MaxHp;
            Color color=kind==0?new Color(.55f,.19f,.23f):kind==1?new Color(.42f,.32f,.52f):new Color(.21f,.42f,.4f);
            if(AuthoredArt&&AuthoredSprite&&HealthFill&&AuthoredWarning)
            {art=AuthoredArt;Presentation=AuthoredSprite;bar=HealthFill;warning=AuthoredWarning;warning.enabled=false;cooldown=Random.Range(.8f,2);return;}
            art=new GameObject("Enemy art").transform;art.SetParent(transform,false);var painted=CombatArt.Actor(art,"Enemy",1.7f);Presentation=painted;painted.color=Color.Lerp(Color.white,color,.3f);
            if(kind==3){art.localScale=Vector3.one*1.9f;Ink.Shape("Cursed crown",art,new Vector2(0,1.2f),new Vector2(1,.18f),Ink.Red,15,false,12);}
            var back=Ink.Shape("HP background",transform,new Vector2(0,kind==3?2.5f:1.35f),new Vector2(kind==3?2.3f:1,.09f),new Color(.05f,.03f,.05f),20);
            bar=Ink.Shape("HP",back.transform,Vector2.zero,Vector2.one,Ink.Red,21).transform;
            warning=Ink.Shape("Attack warning",null,transform.position,Vector2.one, new Color(.85f,.035f,.08f,.42f),-600,true);warning.enabled=false;
            cooldown=Random.Range(.8f,2); 
            AuthoredArt=art;AuthoredSprite=painted;HealthFill=bar;AuthoredWarning=warning;
        }
        void OnDestroy(){if(warning)Destroy(warning.gameObject);}
        void Update()
        {
            var g=Dungeon.I;if(!g.Running||Dead)return;float dt=Time.deltaTime;
            if(burnTime>0){burnTime-=dt;burnTick-=dt;if(burnTick<=0){burnTick=.5f;TakeDamage(3+g.BurnLevel*2,false);Ink.Burst(transform.position,Ink.Gold,3);if(Dead)return;}}
            flash-=dt;art.localScale=Vector3.one*(Kind==3?1.9f:1)*(flash>0?1.1f:1);
            bar.localScale=new Vector3(Hp/MaxHp,1,1);
            Vector2 p=transform.position,delta=g.Pos-p;float dist=delta.magnitude;
            if(chargeTime>0)
            {
                chargeTime-=dt;transform.position=Rules.ResolveObstacles(p,p+aim*12*dt,.5f);
                if(dist<1.15f)g.Hero.Hurt(22);return;
            }
            if(windup>=0)
            {
                windup-=dt;
                if(windup<=0){warning.enabled=false;Execute();windup=-1;cooldown=Kind==3?1.3f:Kind==1?2:1.5f;}
                return;
            }
            cooldown-=dt;
            float range=Kind==0?1.7f:Kind==1?6:Kind==2?8:8.5f;
            if(cooldown<=0&&dist<range)
            {
                aim=delta.normalized;windup=Kind==0?.65f:Kind==1?.85f:.9f;
                warning.enabled=true;warning.transform.position=Kind==0?p:Kind==1?p+aim*2.8f:p;
                warning.transform.localScale=Kind==0?Vector3.one*3.1f:Kind==1?new Vector3(6,1,1):Vector3.one*(Kind==3?5:2);
                warning.transform.rotation=Quaternion.Euler(0,0,Kind==1?Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg:0);
            }
            else
            {
                Vector2 movement=dist>range*.8f?delta.normalized:Kind==2&&dist<4?-delta.normalized:Vector2.zero;
                foreach(var other in g.Enemies) if(other && other!=this&&!other.Dead){Vector2 away=p-(Vector2)other.transform.position;if(away.sqrMagnitude<1.4f&&away.sqrMagnitude>.01f)movement+=away.normalized*.7f;}
                transform.position=Rules.ResolveObstacles(p,p+Vector2.ClampMagnitude(movement,1)*(Kind==3?1.7f:2.2f)*dt,.5f);
            }
        }
        void Execute()
        {
            Vector2 p=transform.position;var g=Dungeon.I;
            if(Kind==0){Ink.Ring(p,1.55f,Ink.Red);if(Vector2.Distance(p,g.Pos)<1.75f)g.Hero.Hurt(15);}
            if(Kind==1){chargeTime=.45f;Ink.Line(p,p+aim*5.4f,Ink.Red,.2f,.35f);}
            if(Kind==2)for(int i=-1;i<=1;i++)Bullet.Spawn(p,Quaternion.Euler(0,0,i*16)*aim*6.5f,13,true,3);
            if(Kind==3)
            {
                cycle++;int count=Hp<MaxHp*.5f?20:12;
                if(cycle%3==0){chargeTime=.55f;Ink.Ring(p,2.5f,Ink.Red);}
                else for(int i=0;i<count;i++){float a=(i/(float)count*360+cycle*13)*Mathf.Deg2Rad;Bullet.Spawn(p,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(Hp<MaxHp*.5f?6:4.5f),17,true,5);}
            }
        }
        public void TakeDamage(float damage,bool ignite)
        {
            if(Dead||Dungeon.I.State==RunState.Cinematic)return;Hp=Mathf.Max(0,Hp-damage);flash=.1f;Ink.Burst(transform.position,ignite?Ink.Gold:Ink.Cyan,5);
            if(ignite)burnTime=3;
            if(Dead){Dungeon.I.Kills++;warning.enabled=false;Ink.Burst(transform.position,Ink.Red,14);Destroy(gameObject);}
        }
    }
    public class Bullet:MonoBehaviour
    {
        Vector2 velocity;float damage,life;bool hostile;int stage;long attackId;
        public static void Spawn(Vector2 p,Vector2 v,float damage,bool hostile,float life,int stage=0,long attackId=0)
        {
            var sr=Ink.Shape(hostile?"Hostile bullet":"Revolver bullet",null,p,hostile?Vector2.one*.25f:new Vector2(.4f,.1f),hostile?Ink.Red:Ink.Gold,1000,hostile,Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg);
            if(!hostile){var card=sr.GetComponent<PaperCard>();if(card)card.RenderOffset=SkillEffects.MuzzleOffset;}
            var b=sr.gameObject.AddComponent<Bullet>();b.velocity=v;b.damage=damage;b.hostile=hostile;b.life=life;b.stage=stage;b.attackId=attackId;
        }
        void Update()
        {
            var g=Dungeon.I;if(!g.Running)return;float dt=Time.deltaTime;life-=dt;
            if(life<=0){Destroy(gameObject);return;}
            // Substeps avoid tunnelling at low frame rates and into cover.
            int steps=Mathf.Max(1,Mathf.CeilToInt(velocity.magnitude*dt/.16f));
            for(int i=0;i<steps;i++)
            {
                transform.position+=(Vector3)(velocity*dt/steps);Vector2 p=transform.position;
                if(Mathf.Abs(p.x-Rules.RoomOrigin.x)>14.8f||Mathf.Abs(p.y-Rules.RoomOrigin.y)>8.8f){Destroy(gameObject);return;}
                foreach(Rect r in Dungeon.Obstacles)if(r.Contains(p)){Ink.Burst(p,Ink.Gold,2);Destroy(gameObject);return;}
                if(hostile){if(Vector2.Distance(p,g.Pos)<.48f){g.Hero.Hurt(damage);Destroy(gameObject);return;}}
                else foreach(var e in g.Enemies)if(e&&!e.Dead&&Vector2.Distance(p,e.transform.position)<(e.Kind==3?1:.55f)){g.Hero.Hit(e,damage,stage,attackId);Destroy(gameObject);return;}
            }
        }
    }
}



