using System.Collections;
using UnityEngine;
using Pose = WesternLemegeton.PaladinRig.Pose;
using Cue = WesternLemegeton.BossTrialAudio.Cue;

namespace WesternLemegeton
{
    public sealed partial class PaladinEncounter
    {
        struct EchoStep
        {
            public int Move;
            public Vector2 Origin, Direction;
            public PaladinArea Footprint()
            {
                switch (Move)
                {
                    case 0: return PaladinArea.Lane(Origin,Origin+Direction*7,1.86f);
                    case 1: case 2: return PaladinArea.Band(Origin,0,5.3f,
                        PaladinArea.Turn(Direction,Move==1?45:-45),90);
                    default: return PaladinArea.Band(Origin,0,4.16f);
                }
            }
            public Pose ActionPose => Move==0 ? Pose.Thrust : Pose.Slash;
        }

        IEnumerator Ritual(Break ritual)
        {
            if (ritual==Break.LastStand) { yield return LastStand(); yield break; }
            if (ritual==Break.Reveal)
            {
                yield return Unmask();
                BeginUnjudged();
                yield break;
            }
            if (ritual==Break.Echo) { yield return Echoes(); enhanced=true; }
            else { yield return Eclipse(); eclipseDone=true; }
            rig.Visible=true;
            rig.Motion(); rig.Ignite(0);
            sealedHp=false;
            StartCoroutine(Fight());
        }

        IEnumerator Echoes()
        {
            game.Tell("빛의 장벽이 닫힙니다");
            EchoBarrier=new GameObject("Echo court").AddComponent<PaladinEchoBarrier>();
            EchoBarrier.Build(effects,Center);
            Vector2 heroStart=game.Pos, bossStart=Feet;
            const float closingTime=1.45f;
            bound=true;
            game.Hero.BindForTrial(closingTime+.15f);
            rig.Visible=true; PoseAt(Pose.Call);
            audioBus.Play(Cue.Miracle);
            yield return Wait(closingTime,t=>
            {
                float progress=Mathf.SmoothStep(0,1,t);
                EchoBarrier.SetClosure(progress);
                game.Hero.transform.position=EchoBarrier.Clamp(Vector2.Lerp(heroStart,Center,progress),.38f);
                Feet=Vector2.Lerp(bossStart,Center+Vector2.up*1.5f,progress);
                rig.Motion(1.75f*progress,-6*progress);
                rig.Ignite(progress);
            });
            FreeHunter();
            rig.Visible=false;
            game.Tell("빛의 잔상 · 세 번의 공격을 기억하세요");
            EchoStep[] sequence=new EchoStep[3];
            var ghosts=new PaladinRig[3];
            float angle=Random.Range(0f,360f);
            Vector2 target=Center;
            for (int i=0;i<3;i++)
            {
                var offset=PaladinArea.Turn(Vector2.right,angle+i*120)*1.85f;
                var point=Rules.ClampToRoom(target+offset,.6f);
                sequence[i]=new EchoStep { Move=Random.Range(0,4), Origin=point, Direction=PaladinArea.Unit(target-point) };
                ghosts[i]=PaladinRig.Create(effects,rig.Height,new Color(.84f,.93f,1,.67f));
                ghosts[i].Visible=false;
                ghosts[i].Ground=point; ghosts[i].Set(Pose.Windup,sequence[i].Direction);
            }
            for(int i=0;i<3;i++)
            {
                EchoStep step=sequence[i];
                var ghost=ghosts[i];
                ghost.Visible=true;
                ghost.Ignite(.75f);
                var tell=Decal(step.Footprint(),new Color(.16f,.40f,.66f,.54f));
                tell.MarkEchoPreview();
                tell.name="Echo preview "+(i+1);
                audioBus.Play(Cue.Wave);
                float sweep=step.Move==3?360:step.Move==2?-80:65;
                yield return Wait(1,t=>
                {
                    if (t<.27f) ghost.Motion(.46f,-9*t/.27f,0,-20*t/.27f);
                    else
                    {
                        float stroke=Mathf.SmoothStep(0,1,(t-.27f)/.65f);
                        ghost.Set(step.ActionPose,step.Direction);
                        ghost.Motion(.46f+.13f*Mathf.Sin(Mathf.PI*stroke),Mathf.Lerp(-9,8,stroke),
                            step.Move==0 ? .38f*stroke : 0,step.Move==0 ? 0 : Mathf.Lerp(-20,sweep,stroke));
                    }
                });
                Retire(tell); ghost.Visible=false;
                if (i<2) yield return Wait(.25f);
            }
            yield return Wait(3);
            int hits=0;
            foreach (var step in sequence)
            {
                Feet=step.Origin; facing=step.Direction;
                rig.Visible=true; rig.Motion(.46f); PoseAt(step.ActionPose);
                var area=step.Footprint();
                // The white slash appears on the damage frame. The clones supplied the entire warning.
                if (Strike(area,NormalDamage,true)) hits++;
                Flash(area,Cue.Cut);
                yield return Wait(.30f,t=>rig.Motion(.46f+.10f*Mathf.Sin(t*Mathf.PI),8,0,
                    (step.Move==3?360:step.Move==2?-75:60)*t));
            }
            foreach (var ghost in ghosts) Retire(ghost);
            Feet=Center; facing=PaladinArea.Unit(game.Pos-Center); PoseAt(Pose.Call);
            const float judgmentDelay=3;
            bound=true; game.Hero.BindForTrial(judgmentDelay+.15f);
            var ring=Decal(PaladinArea.Band(game.Pos,1.0f,1.13f),LightColor,3800);
            var glyphs=new PaladinDecal[8];
            for (int n=0;n<glyphs.Length;n++)
                glyphs[n]=Decal(PaladinArea.Band(game.Pos,1.22f,1.40f,PaladinArea.Turn(Vector2.right,n*45),17),CoreColor,3801);
            audioBus.Play(Cue.Miracle);
            yield return Wait(judgmentDelay,t=>
            {
                rig.Motion(.55f+.10f*Mathf.Sin(t*Mathf.PI*3));
                for(int n=0;n<glyphs.Length;n++) glyphs[n].Paint(PaladinArea.Band(game.Pos,1.22f,1.40f,
                    PaladinArea.Turn(Vector2.right,n*45+t*t*220),17),CoreColor,3801,Center);
            });
            Flash(PaladinArea.Band(game.Pos,0,1.4f),Cue.Miracle);
            if (hits>0) game.Hero.HurtOverTime(JudgmentDamagePerHit*hits,0);
            else game.Hero.Hp=Mathf.Min(game.Hero.MaxHealth,game.Hero.Hp+PerfectHeal);
            game.Tell(hits>0 ? $"심판 · 피격 {hits}회" : "완전 회피 · 체력 회복");
            FreeHunter();
            Retire(ring);
            foreach(var glyph in glyphs) Retire(glyph);
            yield return Wait(.75f,t=>
            {
                float open=Mathf.SmoothStep(0,1,t);
                EchoBarrier.SetClosure(1-open);
                rig.Motion(.55f*(1-open));
            });
            Retire(EchoBarrier);
            EchoBarrier=null;
        }

        IEnumerator Eclipse()
        {
            rig.Visible=false;
            game.Tell("암전 · 빛나는 검기를 피하세요");
            var mask=Decal(PaladinArea.Band(Center,0,100),Color.clear,11500,false);
            mask.name="Eclipse spotlights";
            void Spotlight(float opacity) => mask.PaintSpotlights(game.Pos,Center,FramingSlam,opacity);
            Spotlight(0);
            yield return Wait(.55f,t=>Spotlight(t));
            int last=-1;
            int inFlight=0;
            IEnumerator CornerWave(Vector2 origin,Vector2 direction)
            {
                inFlight++;
                yield return Wave(origin,direction,12.5f,1.15f,NormalDamage,1.25f);
                inFlight--;
            }
            Vector2 corners=PaladinArea.ArenaHalfSize-Vector2.one*.8f;
            for(int i=0;i<15;i++)
            {
                int corner=Random.Range(0,last<0?4:3);
                if(last>=0 && corner>=last) corner++;
                last=corner;
                Feet=Center+new Vector2((corner%2==0?-1:1)*corners.x,(corner<2?-1:1)*corners.y);
                facing=PaladinArea.Unit(game.Pos-Feet);
                var blade=FlyingSword(Feet);
                blade.sortingOrder=1220;
                blade.color=LightColor;
                blade.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(facing.y,facing.x)*Mathf.Rad2Deg-90);
                blade.GetComponent<PaperCard>().Flat=true;
                yield return Wait(.20f,t=>Spotlight(1));
                audioBus.Play(Cue.Wave);
                StartCoroutine(CornerWave(Feet,facing));
                Retire(blade);
                yield return Wait(.43f,t=>Spotlight(1));
            }
            // Let the last wave leave the room before the central slam.
            while (inFlight>0) { Spotlight(1); yield return null; }
            FramingSlam=true; Feet=Center;
            rig.Visible=true; facing=Vector2.right; PoseAt(Pose.Call); Spotlight(1);
            game.Tell("중앙으로 · 심판관 곁이 안전합니다");
            var blast=PaladinArea.Band(Center,2.65f,PaladinArea.ArenaHalfSize.magnitude+.1f);
            var lamp=Decal(PaladinArea.Band(Center,2.54f,2.65f),CoreColor);
            var tell=Decal(blast,WarningColor);
            float reach=Mathf.Max(1.15f,(Vector2.Distance(game.Pos,Center)-2.65f)/5f+.4f);
            yield return Wait(.75f+reach,t=>
            {
                Spotlight(1);
                rig.Motion(Mathf.SmoothStep(0,.85f,t),-8*t);
            });
            PoseAt(Pose.Miracle);
            yield return Wait(.14f,t=>{ Spotlight(1); rig.Motion((1-t)*.85f,Mathf.Lerp(-8,6,t)); });
            Retire(tell); Retire(lamp); Retire(mask);
            FramingSlam=false; rig.Motion();
            Flash(blast,Cue.Ground); Strike(blast,NormalDamage*2);
            Ink.Ring(Center,2.65f,LightColor,.35f);
            yield return Wait(.35f);
        }

        IEnumerator Unmask()
        {
            game.Tell("갑주가 무너진다");
            Feet=Center; facing=Vector2.right; rig.Visible=true; PoseAt(Pose.Guard);
            yield return Wait(.55f,t=>{ rig.Motion(0,Mathf.Sin(t*30)*2); rig.Ignite(t); });
            PoseAt(Pose.Revealed);
            StartCoroutine(DropArmor("Helm",new Vector2(-1.3f,.45f),2.9f,.75f,-56));
            StartCoroutine(DropArmor("Pauldron",new Vector2(1.6f,.4f),2.3f,.62f,72));
            StartCoroutine(DropArmor("Greave",new Vector2(-1.7f,-.55f),1.1f,.59f,101));
            audioBus.Play(Cue.Ground);
            yield return Wait(1.45f,t=>
            {
                game.SetSanctuaryDusk(Mathf.SmoothStep(0,1,t));
                audioBus.SetMusicFade(1-Mathf.SmoothStep(0,1,t));
                rig.Motion(.12f*Mathf.Sin(t*Mathf.PI),Mathf.Lerp(-6,0,t));
            });
            rig.Motion(); rig.Ignite(1);
            yield return Wait(.6f);
        }

        IEnumerator DropArmor(string key,Vector2 offset,float height,float duration,float angle)
        {
            var sprite=Resources.Load<Sprite>("Paladin/"+key);
            float h=key=="Helm"?.68f:key=="Pauldron"?.78f:.72f;
            var piece=Dungeon.TrialArt(effects,key,sprite,Center,new Vector2(h*sprite.rect.width/sprite.rect.height,h),250,false);
            var card=piece.GetComponent<PaperCard>();
            yield return Wait(duration,t=>
            {
                piece.transform.position=Center+offset*t;
                piece.transform.rotation=Quaternion.Euler(0,0,t*angle);
                card.RenderOffset=Vector3.up*(height*(1-t*t)+.32f);
            });
            audioBus.Play(Cue.Armor);
            yield return Wait(.18f,t=>card.RenderOffset=Vector3.up*(.30f+Mathf.Sin(t*Mathf.PI)*.15f));
            yield return Wait(.30f);
            var dust=new Color(.74f,.60f,.86f);
            Ink.Burst(Center+offset,dust,4);
            Vector3 settled=piece.transform.localScale;
            yield return Wait(.65f,t=>
            {
                float fade=Mathf.SmoothStep(0,1,t);
                var tint=Color.Lerp(Color.white,dust,fade);
                tint.a=1-fade;
                piece.color=tint;
                piece.transform.localScale=settled*(1-.22f*fade);
            });
            Retire(piece);
        }
    }
}

