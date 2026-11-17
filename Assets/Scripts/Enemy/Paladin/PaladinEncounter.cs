using System.Collections;
using UnityEngine;
using WesternLemegeton.Stigmas;
using Pose = WesternLemegeton.PaladinRig.Pose;
using Cue = WesternLemegeton.BossTrialAudio.Cue;

namespace WesternLemegeton
{
    public sealed partial class PaladinEncounter : Enemy
    {
        [Min(1)] public float PhaseHealth = 1800;
        [Range(1.5f,2f)] public float HunterHeightRatio = 1.75f;
        [Min(0)] public float BaseInterval = 3;
        [Min(0)] public float EnhancedInterval = 2;
        [Min(0)] public float FinalInterval = 1.5f;
        [Min(0)] public float NormalDamage = 10;
        [Min(0)] public float JudgmentDamagePerHit = 5;
        [Min(0)] public float PerfectHeal = 6;

        internal enum Pattern { Light, Left, Right, Spin, Swords, Rush, Rain, Ambush, Judgment, Grab }
        enum Break { None, Echo, Eclipse, Reveal, LastStand }
        Dungeon game;
        BossTrialAudio audioBus;
        PaladinRig rig;
        SpriteRenderer shadow;
        Transform effects;
        CombatBody hitVolume;
        Transform hitAnchor;
        Vector2 facing = Vector2.down;
        bool enhanced, eclipseDone, sealedHp, bound, begun;
        internal bool FramingSlam { get; private set; }
        internal PaladinEchoBarrier EchoBarrier { get; private set; }
        internal const float ChaseSpeed = 3.5f;
        Break pending;
        static readonly int PatternCount = System.Enum.GetValues(typeof(Pattern)).Length;
        Pattern? previous;
        static readonly Color WarningColor = new Color(1,.075f,.115f,.53f);
        static readonly Color LightColor = new Color(1,.84f,.37f,.88f);
        static readonly Color CoreColor = new Color(1,.985f,.85f,.94f);
        Vector2 Feet
        {
            get => transform.position;
            set => transform.position = SecondPhase && !lastStand && ruinEnd>clockTwo ? ClampFloor(value,.55f) : value;
        }
        Vector2 Center => game.RoomCenter;
        public override bool Dead => base.Dead || (sealedHp && !TrueBodyOpen) || (begun && !rig.Visible);
        bool Playing => game && game.Running && game.Hero.Hp>0;
        internal void Begin(Dungeon dungeon, BossTrialAudio sound)
        {
            game = dungeon;
            audioBus = sound;
            MaxHp = Hp = PhaseHealth;
            Kind = 3;
            // HunterLocomotionAtlas normalizes the standing hunter to 1.9 world units.
            rig = PaladinRig.Create(transform,1.9f*HunterHeightRatio,Color.white);
            rig.Ground = Feet;
            shadow=Ink.Shape("Paladin shadow",transform,Vector2.down*.35f,new Vector2(2.05f,.9f),
                new Color(.10f,.07f,.11f,.43f),-415,true);
            shadow.sprite=Ink.SoftDisc;
            hitVolume = CombatBody.For(this);
            hitAnchor = new GameObject("Paladin torso hit volume").transform;
            hitAnchor.SetParent(transform,false);
            hitVolume.BodyAnchor = hitAnchor;
            hitVolume.BodySize = new Vector3(rig.Height*.43f,rig.Height*.85f,.9f);
            UpdateAnchor();
            CreateEffects();
            begun = true;
            StartCoroutine(Fight());
        }

        void CreateEffects()
        {
            effects = new GameObject("Sanctuary attack effects").transform;
            effects.SetParent(transform,false);
            // Effects use absolute ground coordinates. Keep the container independent of boss teleports.
            effects.SetParent(null,true);
            effects.position=Vector3.zero;
        }

        void UpdateAnchor()
        {
            var rotation = Quaternion.Euler(PaperWorld.Pitch,0,0);
            hitAnchor.SetPositionAndRotation(PaperWorld.Point(Feet+Vector2.down*.35f)+rotation*Vector3.up*
                (rig.Height*.50f+(SecondPhase?rig.Lift:0)),rotation);
        }

        public override void TakeStigmaDamage(float damage, StigmaOwner owner, bool slowed) => DamagePhase(damage);
        public override void TakeEnvironmentDamage(float damage) => DamagePhase(damage);
        void DamagePhase(float damage)
        {
            if (!begun || !Playing || Dead || damage<=0) return;
            if (SecondPhase) { DamageUnjudged(damage); return; }
            float floor = !enhanced ? MaxHp*.60f : !eclipseDone ? MaxHp*.20f : 0;
            Hp = Mathf.Max(floor,Hp-damage);
            Ink.Burst(Feet,Ink.Gold,4);
            if (Hp>floor) return;
            sealedHp = true;
            pending = !enhanced ? Break.Echo : !eclipseDone ? Break.Eclipse : Break.Reveal;
        }

        protected override void Update()
        {
            if (!begun || !Playing) return;
            Stigma.Tick(Time.deltaTime);
            rig.Tick(Time.deltaTime);
            if (SecondPhase) TickUnjudged(Time.deltaTime);
            if (pending != Break.None)
            {
                var ritual = pending;
                pending = Break.None;
                StopAllCoroutines();
                ClearEffects();
                CreateEffects();
                FreeHunter();
                rig.Motion();
                rig.Ignite(0);
                StartCoroutine(Ritual(ritual));
            }
        }
        protected override void LateUpdate()
        {
            if (!begun) return;
            if (EchoBarrier && Playing)
                game.Hero.transform.position=EchoBarrier.Clamp(game.Pos,.38f);
            rig.Ground = Feet;
            shadow.enabled=rig.Visible;
            UpdateAnchor();
        }
        void ClearEffects()
        {
            EchoBarrier=null;
            if (!effects) return;
            effects.gameObject.SetActive(false);
            Destroy(effects.gameObject);
        }
        void FreeHunter()
        {
            if (bound && game && game.Hero) game.Hero.ReleaseTrialBind();
            bound=false;
        }
        void OnDisable() { StopAllCoroutines(); ClearEffects(); FreeHunter(); ClearUnjudged(); }

        IEnumerator Wait(float duration, System.Action<float> tick=null)
        {
            float elapsed=0;
            while (elapsed<duration)
            {
                yield return null;
                if (!Playing || ShowingSummon) continue;
                elapsed+=Time.deltaTime;
                tick?.Invoke(Mathf.Clamp01(elapsed/duration));
            }
        }

        Vector2 Aim() => facing = PaladinArea.Unit(game.Pos-Feet);
        void PoseAt(Pose pose) => rig.Set(pose,facing);
        IEnumerator Fight()
        {
            while (!sealedHp)
            {
                float interval = eclipseDone ? FinalInterval : enhanced ? EnhancedInterval : BaseInterval;
                PoseAt(Pose.Guard);
                yield return Wait(interval,t=>
                {
                    Vector2 dir = Aim();
                    float distance=Vector2.Distance(Feet,game.Pos);
                    if (distance>2.35f)
                        Feet=Vector2.MoveTowards(Feet,game.Pos,Mathf.Min(distance-2.35f,Time.deltaTime*ChaseSpeed));
                    PoseAt(Pose.Guard);
                    rig.Motion(0,distance>2.35f ? Mathf.Sin(t*interval*9)*2 : 0);
                });
                Pattern next;
                do
                {
                    next=(Pattern)Random.Range(0,PatternCount);
                }
                while (next==previous);
                previous=next;
                yield return Attack(next);
                rig.Motion(); rig.Ignite(0);
            }
        }

        internal IEnumerator Attack(Pattern pattern)
        {
            Aim(); rig.Motion();
            switch (pattern)
            {
                case Pattern.Light: yield return LightShot(); break;
                case Pattern.Left: case Pattern.Right: yield return SideCut(pattern==Pattern.Left); break;
                case Pattern.Spin: yield return DoubleSpin(); break;
                case Pattern.Swords: yield return SummonSwords(); break;
                case Pattern.Rush: yield return Rush(); break;
                case Pattern.Rain: yield return Rain(); break;
                case Pattern.Ambush: yield return Ambush(); break;
                case Pattern.Judgment: yield return Judgment(); break;
                case Pattern.Grab: yield return Grab(); break;
            }
        }

        PaladinDecal Decal(PaladinArea area, Color color,int order=-3800,bool clip=true)
        {
            var d = new GameObject("Combat footprint").AddComponent<PaladinDecal>();
            d.transform.SetParent(effects,false);
            d.Paint(area,color,order,Center,clip);
            return d;
        }
        static void Retire(Component item)
        {
            if (item) { item.gameObject.SetActive(false); Destroy(item.gameObject); }
        }
        IEnumerator Warning(PaladinArea area,float duration=1)
        {
            var d=Decal(area,WarningColor);
            yield return Wait(duration);
            Retire(d);
        }
        bool Strike(PaladinArea area,float damage,bool sequence=false)
        {
            return area.Contains(game.Pos) && HitHunter(damage,sequence);
        }

        bool HitHunter(float damage,bool sequence=false)
        {
            if (!Playing) return false;
            var hero=game.Hero;
            if (hero.Invulnerable || hero.Dodging) return false;
            if (sequence) hero.HurtOverTime(damage,0);
            else hero.Hurt(damage);
            return true;
        }

        void Flash(PaladinArea area,Cue cue)
        {
            audioBus.Play(cue);
            StartCoroutine(Glow(area));
            if (cue==Cue.Cut && !area.Rectangular)
                StartCoroutine(SwordArc(area));
        }

        IEnumerator SwordArc(PaladinArea area)
        {
            float width=Mathf.Min(45,area.HalfAngle*2);
            float radius=area.Outer*.92f;
            PaladinArea Edge(float t)
            {
                float angle=Mathf.Lerp(-area.HalfAngle+width*.5f,area.HalfAngle-width*.5f,t);
                return PaladinArea.Band(area.Center,radius-.21f,radius,
                    PaladinArea.Turn(area.Direction,angle),width);
            }
            var arc=Decal(Edge(0),CoreColor,3200);
            yield return Wait(.18f,t=>arc.Paint(Edge(t),CoreColor,3200,Center));
            Retire(arc);
        }
        IEnumerator Glow(PaladinArea area)
        {
            Color from=SecondPhase ? new Color(.94f,.82f,1,.87f) : CoreColor;
            Color to=SecondPhase ? new Color(.58f,.19f,1,0) : new Color(1,.67f,.12f,0);
            var d=Decal(area,from,-3700);
            yield return Wait(.22f,t=>d.Paint(area,Color.Lerp(from,to,t),-3700,Center));
            Retire(d);
        }

        IEnumerator Swing(bool reversed=false)
        {
            PoseAt(Pose.Slash);
            float sign=reversed ? -1 : 1;
            yield return Wait(.23f,t=>rig.Motion(0,sign*Mathf.Sin(t*Mathf.PI)*12,.14f*Mathf.Sin(t*Mathf.PI),sign*Mathf.Lerp(-62,28,t)));
            rig.Motion();
        }

        IEnumerator LightShot()
        {
            for (int i=0;i<(enhanced?2:1);i++)
            {
                Vector2 dir=Aim();
                var lane=PaladinArea.Lane(Feet,Feet+dir*PaladinArea.ToWall(Feet,dir,Center),1.25f);
                PoseAt(Pose.Call); rig.Ignite(1);
                audioBus.Play(Cue.Charge);
                var spark=Star(Feet, .42f);
                yield return Warning(lane,.5f);
                Retire(spark);
                Flash(lane,Cue.Miracle); Strike(lane,NormalDamage);
                StartCoroutine(Glow(PaladinArea.Lane(Feet,Feet+dir*PaladinArea.ToWall(Feet,dir,Center),.32f)));
                yield return Wait(.18f);
            }
        }

        PaladinArea Sector(Vector2 origin,Vector2 toward,bool left,float radius)
            => PaladinArea.Band(origin,0,radius,PaladinArea.Turn(toward,left ? 45 : -45),90);

        IEnumerator SideCut(bool left)
        {
            var area=Sector(Feet,Aim(),left,7.4f);
            PoseAt(Pose.Windup);
            yield return Warning(area);
            Strike(area,NormalDamage); Flash(area,Cue.Cut);
            yield return Swing(!left);
        }

        IEnumerator DoubleSpin()
        {
            bool outerFirst=enhanced && Random.value<.5f;
            for (int i=0;i<2;i++)
            {
                bool outer=(i==0)==outerFirst;
                var area=PaladinArea.Band(Feet,outer?4.125f:0,outer?8.25f:4.125f);
                PoseAt(Pose.Windup); yield return Warning(area);
                Strike(area,NormalDamage); Flash(area,Cue.Cut);
                yield return SpinMotion();
            }
        }

        IEnumerator SpinMotion()
        {
            PoseAt(Pose.Slash);
            yield return Wait(.26f,t=>
            {
                rig.Set(t<.55f?Pose.Thrust:Pose.Slash,t<.5f?-facing:facing);
                rig.Motion(.08f,6,0,360*t);
            });
            rig.Motion();
        }

        IEnumerator Rush()
        {
            Vector2 start=Feet, dest=Rules.ClampToRoom(game.Pos,.5f);
            var lane=PaladinArea.Lane(start,dest,1.35f);
            var end=PaladinArea.Band(dest,0,2.65f);
            var circle=Decal(end,WarningColor);
            PoseAt(Pose.Windup); yield return Warning(lane);
            Flash(lane,Cue.Wave); PoseAt(Pose.Thrust);
            bool hit=false;
            yield return Wait(Mathf.Clamp(Vector2.Distance(start,dest)/40,.09f,.4f),t=>
            {
                Vector2 old=Feet; Feet=Vector2.Lerp(start,dest,t);
                if (!hit) hit=Strike(PaladinArea.Lane(old,Feet,1.35f),NormalDamage);
            });
            Retire(circle); Flash(end,Cue.Cut); Strike(end,NormalDamage);
            yield return SpinMotion();
        }

        IEnumerator Grab()
        {
            Vector2 dir=Aim();
            var lane=PaladinArea.Lane(Feet,Feet+dir*5.3f,2.24f);
            PoseAt(Pose.Windup);
            yield return Warning(lane,.5f);
            PoseAt(Pose.Thrust); Flash(lane,Cue.Cut);
            if (!Strike(lane,NormalDamage) || game.Hero.Hp<=0) yield break;
            bound=true;
            game.Hero.BindForTrial(.6f);
            PoseAt(Pose.Call);
            yield return Wait(.5f,t=>game.Hero.TrialLift(Mathf.Sin(t*Mathf.PI*.5f)*1.35f));
            FreeHunter();
            game.Hero.HurtOverTime(NormalDamage,0);
            game.Hero.ApplyChargeKnockback(dir,3.3f,.36f,.8f);
            audioBus.Play(Cue.Ground);
        }

        IEnumerator Judgment()
        {
            PoseAt(Pose.Windup); rig.Ignite(1); audioBus.Play(Cue.Charge);
            Vector2 safe=Center+new Vector2(Random.Range(-8f,8f),Random.Range(-4.4f,4.4f));
            // Keep the safe island reachable within the three-second tell from any corner.
            safe=Vector2.MoveTowards(game.Pos,safe,9f);
            var area=PaladinArea.Band(safe,2.15f,(PaladinArea.ArenaHalfSize + new Vector2(Mathf.Abs(safe.x-Center.x),Mathf.Abs(safe.y-Center.y))).magnitude);
            var rim=Decal(PaladinArea.Band(safe,2.04f,2.15f),LightColor);
            yield return Warning(area,3);
            Retire(rim); Flash(area,Cue.Ground); Strike(area,NormalDamage*2);
            yield return Swing();
        }
    }
}

