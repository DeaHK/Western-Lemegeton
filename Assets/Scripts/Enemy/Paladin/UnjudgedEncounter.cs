using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ActionPose = WesternLemegeton.PaladinRig.Action;
using Cue = WesternLemegeton.BossTrialAudio.Cue;
using Random = UnityEngine.Random;

namespace WesternLemegeton
{
    public sealed partial class PaladinEncounter
    {
        [Min(100)] public float UnjudgedHealth=2400;
        [Min(1)] public float UnjudgedDamage=12, DangerDamage=32;
        public AudioClip IdentityMusic;
        internal bool SecondPhase { get; private set; }
        internal bool TrueBodyOpen { get; private set; }
        internal float IslandRadius { get; private set; }
        internal enum Move { Scars, Return, Slices, Flurry, Ricochet, Vortex, BeamPunch, SweepGrab, QuakeLeap,
            Feint, DashChain, OneTwo, Pillars, DeathRay, HandRing, Storm, DrawCircle, Verdict }
        static readonly Move[] swordMoves={Move.Scars,Move.Return,Move.Slices,Move.Flurry,Move.Ricochet,Move.Vortex};
        static readonly Move[] fistMoves={Move.BeamPunch,Move.SweepGrab,Move.QuakeLeap,Move.Feint,Move.DashChain,Move.OneTwo};
        readonly List<Move> choices=new List<Move>(8);
        static readonly Color Violet=new Color(.77f,.37f,1,.9f), Ice=new Color(.16f,.52f,1,.60f);
        const float Island=6.5f, SecondSpeed=4f, MusicTrim=.155f;
        const float RicochetRadius=4.5f, ElectricPoolRadius=9.45f;
        const float ReturningBladeWidth=2.3625f;
        bool handMode, armoredThrow, lastStand, identified, departed;
        int seekers;
        Move? previousMove;
        float clockTwo, ruinEnd, nextRay, poolTick, quakeEnd, lastMoved;
        Vector2 ruinTip, poolPlace, quakePlace, lastHunter;
        float raySign;
        Transform lasting;
        SpriteRenderer planted;
        PaladinDecal pool;
        PaladinArea poolArea;
        PaladinDecal summonMarker, summonShade;
        Vector2 summonPosition;
        float summonElapsed;
        internal bool ShowingSummon => summonMarker;
        internal Vector2 SummonPosition => summonPosition;
        readonly SpriteRenderer[] hunters=new SpriteRenderer[2];
        readonly float[] bladeHitAt=new float[2];
        internal Vector4 RuinForPainting => ruinEnd>clockTwo ? new Vector4(ruinTip.x-Center.x,ruinTip.y-Center.y,raySign,1) : Vector4.zero;
        internal bool InQuake(Vector2 p) => SecondPhase && quakeEnd>clockTwo && (p-quakePlace).sqrMagnitude<16;
        internal int HunterCount => seekers;
        internal bool IsHandMode => handMode;

        void BeginUnjudged()
        {
            SecondPhase=true; sealedHp=false;
            MaxHp=Hp=UnjudgedHealth;
            lasting=new GameObject("Unjudged persistent effects").transform;
            lastHunter=game.Pos;
            Act(ActionPose.Ready);
            rig.Motion(); rig.Ignite(0);
            audioBus.StartMusic(IdentityMusic);
            audioBus.SetMusicFade(MusicTrim);
            game.Tell("심판받지 못한 자");
            StartCoroutine(SecondFight());
        }

        internal Vector2 ClampFloor(Vector2 point,float clearance)
        {
            if (IslandRadius>0) return Center+Vector2.ClampMagnitude(point-Center,Mathf.Max(.01f,IslandRadius-clearance));
            if (ruinEnd<=clockTwo) return point;
            Vector2 local=point-ruinTip;
            float forward=local.x*raySign;
            if (forward < -clearance) return point;
            float edge=forward*.258819f-Mathf.Abs(local.y)*.965926f+clearance;
            if (edge>0) point.y+=(local.y<0?-1:1)*(edge/.965926f+.005f);
            return point;
        }

        void DamageUnjudged(float damage)
        {
            if (lastStand) return;
            float floor=MaxHp*.01f;
            Hp=Mathf.Max(floor,Hp-damage*(armoredThrow?.5f:1));
            Ink.Burst(Feet,Violet,4);
            if (Hp<=floor)
            {
                CloseSummonPreview();
                lastStand=true; sealedHp=true; pending=Break.LastStand;
            }
        }

        void BeginSummonPreview()
        {
            float sign=seekers==0 ? -1 : 1;
            summonPosition=Rules.ClampToRoom(Center+new Vector2(sign*11,sign*4));
            summonElapsed=0;
            summonShade=Decal(PaladinArea.Band(Center,0,90),Color.clear,11550,false);
            summonMarker=Decal(PaladinArea.Band(summonPosition,0,.46f),new Color(1,.05f,.10f,1),11560);
            game.Tell(seekers==0?"추적검 · 첫 번째 선고":"추적검 · 두 번째 선고");
            audioBus.Play(Cue.Charge);
        }

        void TickSummonPreview(float dt)
        {
            summonElapsed+=dt;
            if (summonElapsed<1)
            {
                float pulse=.5f+.5f*Mathf.Cos(summonElapsed*4*Mathf.PI);
                summonMarker.Paint(PaladinArea.Band(summonPosition,0,.43f+.12f*pulse),
                    new Color(1,.06f+.09f*pulse,.12f,1),11560,Center);
                summonShade.Paint(PaladinArea.Band(Center,0,90),
                    new Color(.10f,.025f,.18f,.43f*Mathf.SmoothStep(0,1,summonElapsed/.25f)),11550,Center,false);
                return;
            }
            CloseSummonPreview();
            var blade=Blade("Pursuing verdict "+(seekers+1),lasting,summonPosition,3.5f);
            blade.transform.rotation=Quaternion.Euler(0,0,seekers==0?26:-26);
            hunters[seekers++]=blade;
            audioBus.Play(Cue.Miracle);
        }

        void CloseSummonPreview()
        {
            Retire(summonMarker); summonMarker=null;
            Retire(summonShade); summonShade=null;
        }

        void TickUnjudged(float dt)
        {
            if (departed) return;
            clockTwo+=dt;
            if ((game.Pos-lastHunter).sqrMagnitude>.000001f) lastMoved=clockTwo;
            lastHunter=game.Pos;
            if (ruinEnd>clockTwo || IslandRadius>0) game.Hero.transform.position=ClampFloor(game.Pos,.38f);
            if (lastStand) return;
            if (ShowingSummon) { TickSummonPreview(dt); return; }
            if (!bound && seekers<2 && Hp<=MaxHp*(seekers==0?.60f:.30f))
            {
                BeginSummonPreview();
                return;
            }
            for (int n=0;n<seekers;n++)
            {
                var blade=hunters[n];
                if (!blade) continue;
                Vector2 old=blade.transform.position, dir=PaladinArea.Unit(game.Pos-old);
                Vector2 next=Vector2.MoveTowards(old,game.Pos,SecondSpeed*.5f*dt);
                blade.transform.position=next;
                blade.transform.rotation=Quaternion.Euler(0,0,Mathf.Sin(clockTwo*2+n)*14+18*dir.x);
                if ((next-game.Pos).sqrMagnitude<.95f*.95f && clockTwo>=bladeHitAt[n] && HitHunter(UnjudgedDamage))
                {
                    bladeHitAt[n]=clockTwo+1.1f;
                    game.Hero.ApplyChargeKnockback(dir,2.2f);
                }
            }
            if (pool && poolArea.Contains(game.Pos) && clockTwo>=poolTick)
            {
                poolTick=clockTwo+.75f;
                HitHunter(UnjudgedDamage*.5f,true);
            }
        }

        void ClearUnjudged()
        {
            if (lasting) { lasting.gameObject.SetActive(false); Destroy(lasting.gameObject); }
            IslandRadius=0; ruinEnd=quakeEnd=0; TrueBodyOpen=false;
        }

        internal bool HitTrueBody()
        {
            if (!TrueBodyOpen) return false;
            identified=true; TrueBodyOpen=false;
            return true;
        }

        IEnumerator SecondFight()
        {
            while (!lastStand)
            {
                for (int index=0;index<4 && !lastStand;index++)
                {
                    Act(ActionPose.Ready);
                    yield return Wait(1.7f,t=>
                    {
                        Aim(); Act(ActionPose.Ready);
                        float distance=Vector2.Distance(Feet,game.Pos);
                        if (distance>2.4f) Feet=Rules.ClampToRoom(Vector2.MoveTowards(Feet,game.Pos,
                            Mathf.Min(distance-2.4f,SecondSpeed*Time.deltaTime)),.6f);
                        rig.Motion(0,distance>2.4f?Mathf.Sin(t*13)*3:0);
                    });
                    choices.Clear();
                    if (index==2)
                    {
                        if (clockTwo>=nextRay) choices.Add(Move.DeathRay);
                        choices.Add(handMode?Move.HandRing:Move.DrawCircle);
                        choices.Add(handMode?Move.Storm:Move.Verdict);
                    }
                    else
                    {
                        choices.AddRange(handMode?fistMoves:swordMoves);
                        if (Vector2.Distance(Feet,game.Pos)>5) choices.Remove(Move.SweepGrab);
                        if (Vector2.Distance(Feet,game.Pos)>=7) choices.Add(Move.Pillars);
                    }
                    if (previousMove.HasValue && choices.Count>1) choices.Remove(previousMove.Value);
                    Move next=choices[Random.Range(0,choices.Count)];
                    previousMove=next;
                    yield return SecondAttack(next);
                    rig.Motion(); rig.Visible=true; rig.BladeTint=Violet;
                }
                if (!lastStand) yield return ChangeWeapon(!handMode);
            }
        }

        internal IEnumerator SecondAttack(Move move)
        {
            Aim(); rig.Motion();
            switch(move)
            {
                case Move.Scars: yield return CrossScars(); break;
                case Move.Return: yield return ReturningBlade(); break;
                case Move.Slices: yield return SpinningSlices(); break;
                case Move.Flurry: yield return QuickCuts(); break;
                case Move.Ricochet: yield return Ricochet(); break;
                case Move.Vortex: yield return Dive(true); break;
                case Move.BeamPunch: yield return BeamPunch(); break;
                case Move.SweepGrab: yield return HandGrab(); break;
                case Move.QuakeLeap: yield return Dive(false); break;
                case Move.Feint: yield return Feint(); break;
                case Move.DashChain: yield return DashChain(); break;
                case Move.OneTwo: yield return OneTwo(); break;
                case Move.Pillars: yield return FivePillars(); break;
                case Move.DeathRay: yield return DeathRay(); break;
                case Move.HandRing: yield return RedHand(); break;
                case Move.Storm: yield return StormAmbush(); break;
                case Move.DrawCircle: yield return DrawCircle(); break;
                case Move.Verdict: yield return ColorVerdict(); break;
            }
        }

        void Act(ActionPose pose) => rig.Act(pose,facing);
        SpriteRenderer Blade(string label,Transform parent,Vector2 at,float size)
        {
            var b=Dungeon.TrialArt(parent,label,Resources.Load<Sprite>("Paladin/Sword"),at,
                new Vector2(size*.27f,size),785,false);
            b.color=Violet;
            return b;
        }

        static readonly Dictionary<string,Sprite[]> fxFrames=new Dictionary<string,Sprite[]>();
        void Sparks(string key,Vector2 at,Vector2 dir,float size)
        {
            StartCoroutine(EffectFrames(key,at,dir,size));
        }
        IEnumerator EffectFrames(string key,Vector2 at,Vector2 dir,float size)
        {
            if (!fxFrames.TryGetValue(key,out var frames))
            {
                frames=new Sprite[4];
                for (int i=0;i<4;i++) frames[i]=Resources.Load<Sprite>("Paladin/Unjudged/"+key+i);
                fxFrames.Add(key,frames);
            }
            var fx=Dungeon.TrialArt(effects,key,frames[0],at,new Vector2(size,size),3250,key=="Arc");
            if (key!="Bolt") fx.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg);
            yield return Wait(.23f,t=>fx.sprite=frames[Mathf.Min(3,(int)(t*4))]);
            Retire(fx);
        }

        bool Blow(PaladinArea area,float damage,string art="Arc",bool combo=false)
        {
            Flash(area,art=="Arc"?Cue.Cut:Cue.Fist);
            Sparks(art,area.Center,area.Direction,art=="Arc"?4:3);
            return Strike(area,damage,combo);
        }

        static readonly Color RicochetTint=new Color(.84f,.28f,1,.46f);

        void AdvanceBladeHit(Vector2 from,Vector2 to,PaladinDecal disc,ref bool hit)
        {
            Vector2 delta=to-from;
            bool overlaps;
            if (disc)
            {
                disc.Paint(PaladinArea.Band(to,0,RicochetRadius),RicochetTint,3100,Center);
                if (hit) return;
                float along=delta.sqrMagnitude>0 ? Mathf.Clamp01(Vector2.Dot(game.Pos-from,delta)/delta.sqrMagnitude) : 0;
                overlaps=(game.Pos-(from+delta*along)).sqrMagnitude<=RicochetRadius*RicochetRadius;
            }
            else
            {
                if (hit) return;
                Vector2 tip=PaladinArea.Unit(delta)*.32f;
                overlaps=PaladinArea.Lane(from-tip,to+tip,ReturningBladeWidth).Contains(game.Pos);
            }
            if (overlaps) hit=HitHunter(UnjudgedDamage);
        }

        IEnumerator TravelBlade(SpriteRenderer blade,Vector2 from,Vector2 to,float duration,bool hurt,bool spin,
            PaladinDecal disc=null, bool launch=false)
        {
            Vector2 dir=PaladinArea.Unit(to-from), previousPoint=from;
            bool hit=false;
            var card=blade.GetComponent<PaperCard>();
            card.Flat=true;
            var release=launch ? rig.HeldBlade.transform.position-PaperWorld.Point(from,.015f) : Vector3.zero;
            card.RenderOffset=release;
            float startAngle=blade.transform.eulerAngles.z;
            yield return Wait(duration,t=>
            {
                Vector2 point=Vector2.Lerp(from,to,t);
                blade.transform.position=point;
                card.RenderOffset=release*(1-Mathf.Clamp01(t*duration/.14f));
                blade.transform.rotation=Quaternion.Euler(0,0,spin?startAngle+t*duration*980:Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg-90);
                if (hurt) AdvanceBladeHit(previousPoint,point,disc,ref hit);
                previousPoint=point;
            });
        }

        IEnumerator CatchBlade(SpriteRenderer blade,float duration,bool hurt=false,PaladinDecal disc=null)
        {
            var card=blade.GetComponent<PaperCard>();
            Vector2 start=blade.transform.position, previousPoint=start;
            var held=rig.HeldBlade;
            var visible=card.Visible;
            var flight=new GameObject("Returning sword").AddComponent<SpriteRenderer>();
            flight.transform.SetParent(effects,false);
            flight.sprite=blade.sprite;
            flight.sharedMaterial=blade.sharedMaterial;
            Vector3 origin=PaperWorld.Point(start,card.Flat?.015f:0)+card.RenderOffset;
            Quaternion rotation=Quaternion.Euler(card.Flat?90:PaperWorld.Pitch,0,0)*blade.transform.rotation;
            Vector3 scale=blade.transform.lossyScale;
            Color tint=visible ? visible.color : blade.color;
            flight.transform.SetPositionAndRotation(origin,rotation);
            flight.transform.localScale=scale;
            flight.color=tint;
            flight.sortingOrder=visible ? visible.sortingOrder : held.sortingOrder;
            card.OverrideHidden=true;
            rig.Motion();
            Act(ActionPose.Ready);
            bool hit=false;
            yield return Wait(duration,t=>
            {
                Vector2 point=Vector2.Lerp(start,Feet,t);
                flight.transform.SetPositionAndRotation(Vector3.Lerp(origin,held.transform.position,t),
                    Quaternion.Slerp(rotation,held.transform.rotation,t));
                flight.transform.localScale=Vector3.Lerp(scale,held.transform.lossyScale,t);
                flight.color=Color.Lerp(tint,rig.BladeTint,t);
                if (t>.8f) flight.sortingOrder=held.sortingOrder;
                if (hurt) AdvanceBladeHit(previousPoint,point,disc,ref hit);
                previousPoint=point;
            });
            Retire(flight); Retire(blade); rig.Armed=true;
        }

        internal IEnumerator ChangeWeapon(bool setHands)
        {
            Act(ActionPose.Coil);
            if (setHands)
            {
                poolPlace=Rules.ClampToRoom(Center+new Vector2((Random.value<.5f?-1:1)*8,Random.Range(-4f,4f)),.6f);
                poolArea=PaladinArea.Band(poolPlace,0,ElectricPoolRadius);
                var pre=Decal(poolArea,WarningColor);
                yield return Wait(.55f);
                planted=Blade("Grounded sword",lasting,Feet,2.15f);
                rig.Armed=false;
                yield return TravelBlade(planted,Feet,poolPlace,.28f,false,true,launch:true);
                planted.GetComponent<PaperCard>().Flat=false;
                planted.transform.rotation=Quaternion.Euler(0,0,170);
                planted.GetComponent<PaperCard>().RenderOffset=Vector3.up*1.85f;
                Retire(pre); pool=Decal(poolArea,new Color(.66f,.18f,1,.44f));
                pool.Electrify();
                audioBus.Play(Cue.Shock);
                Sparks("Bolt",poolPlace,Vector2.up,5);
            }
            else if (planted)
            {
                Retire(pool); pool=null;
                audioBus.Play(Cue.Recall);
                yield return CatchBlade(planted,.18f);
                planted=null;
            }
            handMode=setHands;
            game.Tell(setHands?"전기장 · 맨손 전투":"검 회수");
            Act(ActionPose.Ready);
        }
    }
}
