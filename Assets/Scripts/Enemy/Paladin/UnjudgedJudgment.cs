using System.Collections;
using UnityEngine;
using ActionPose = WesternLemegeton.PaladinRig.Action;
using Cue = WesternLemegeton.BossTrialAudio.Cue;

namespace WesternLemegeton
{
    public sealed partial class PaladinEncounter
    {
        IEnumerator DeathRay()
        {
            Vector2 start=Feet;
            float sign=Random.value<.5f?1:-1;
            const float insetFromWall=.20f; // Fraction of the full room width.
            Vector2 caster=Center+Vector2.right*(sign*PaladinArea.ArenaHalfSize.x*(1-2*insetFromWall));
            facing=Vector2.right*-sign;
            Act(ActionPose.Leap);
            yield return Wait(.58f,t=>{Feet=Vector2.Lerp(start,caster,t);rig.Motion(Mathf.Sin(t*Mathf.PI)*2);});
            Act(ActionPose.Palm); rig.Motion();
            var ray=PaladinArea.Band(caster,0,52,facing,30);
            var signal=SignalLight(false,Violet,true);
            audioBus.Play(Cue.Charge);
            yield return Warning(ray,1.4f);
            Retire(signal);
            var beam=Decal(ray,Violet,3100);
            audioBus.Play(Cue.Beam);
            float nextHit=0;
            yield return Wait(1.35f,t=>
            {
                beam.PaintBeam(ray,Center,t);
                rig.Motion(0,-7+Mathf.Sin(t*65)*2);
                if (t>=nextHit)
                {
                    Strike(ray,DangerDamage);
                    nextHit=t+.13f;
                }
            });
            Retire(beam); audioBus.Play(Cue.Ground);
            ruinTip=caster; raySign=-sign;
            ruinEnd=clockTwo+21; nextRay=clockTwo+31;
            game.Hero.transform.position=ClampFloor(game.Pos,.38f);
            Feet=Rules.ClampToRoom(Feet-facing*.9f,.6f);
        }

        IEnumerator LastStand()
        {
            armoredThrow=false; TrueBodyOpen=false; identified=false;
            rig.Visible=true; rig.Armed=true; rig.BladeTint=Violet;
            Retire(planted); planted=null; Retire(pool); pool=null; quakeEnd=0;
            ruinEnd=0;
            game.Tell("발악 · 짙은 본체를 두 번 맞히세요");
            Act(ActionPose.Ready);
            yield return DismissHunters();

            Vector2 start=Feet;
            float fullRadius=PaladinArea.ArenaHalfSize.magnitude+1;
            audioBus.Play(Cue.Ground); Act(ActionPose.Leap);
            yield return Wait(2.6f,t=>
            {
                float p=Mathf.SmoothStep(0,1,t);
                IslandRadius=Mathf.Lerp(fullRadius,Island,p);
                Feet=Vector2.Lerp(start,Center,p);
                game.Hero.transform.position=ClampFloor(game.Pos,.38f);
                rig.Motion(Mathf.Sin(t*Mathf.PI)*1.6f,-5*Mathf.Sin(t*Mathf.PI));
            });
            IslandRadius=Island;
            Aim(); Act(ActionPose.Coil);
            yield return Wait(1f,t=>rig.Motion(0,-9*Mathf.SmoothStep(0,1,t)));
            rig.Motion();
            for(int round=0;round<2;round++)
            {
                int count=round==0?4:8;
                bool won=false;
                while (!won)
                {
                    yield return Apparitions(count);
                    won=identified;
                    if (!won) { game.Tell("짙은 본체를 조준하세요"); yield return Wait(.55f); }
                }
                rig.Visible=true; Act(ActionPose.Recoil);
                audioBus.Play(Cue.Armor); Sparks("Impact",Feet,facing,2.6f);
                yield return Wait(1f,t=>rig.Motion(.14f*Mathf.Sin(t*Mathf.PI),-15*(1-t)));
            }
            departed=true; TrueBodyOpen=false;
            var exitStart=Feet;
            yield return Wait(.42f,t=>Feet=Vector2.Lerp(exitStart,Center,t));
            facing=Vector2.right; Act(ActionPose.Slam); rig.Armed=false;
            var memorial=Blade("Unjudged abandoned sword",lasting,Center+Vector2.right*.68f,2.1f);
            memorial.transform.rotation=Quaternion.Euler(0,0,177);
            memorial.GetComponent<PaperCard>().RenderOffset=Vector3.up*1.85f;
            audioBus.Play(Cue.Ground);
            Act(ActionPose.Kneel);
            game.Tell("심판이 끝났다");
            yield return Wait(2.15f,t=>
            {
                rig.Motion(.06f*Mathf.Sin(t*25),4+2*Mathf.Sin(t*25));
                audioBus.SetMusicFade(MusicTrim*(1-t));
            });
            Act(ActionPose.Leap);
            yield return Wait(.73f,t=>
            {
                Feet=Center+new Vector2(9,23)*t;
                rig.Motion(5*Mathf.Sin(t*Mathf.PI),-10*t);
            });
            rig.Visible=false; Hp=0;
            yield return Wait(.85f,t=>IslandRadius=Mathf.Lerp(Island,27,Mathf.SmoothStep(0,1,t)));
            IslandRadius=0;
            game.EndTrialBattle();
        }

        IEnumerator DismissHunters()
        {
            if (seekers==0) yield break;
            audioBus.Play(Cue.Miracle);
            foreach (var hunter in hunters)
                if (hunter) Ink.Burst(hunter.transform.position,Violet,5);
            yield return Wait(.75f,t=>
            {
                float fade=Mathf.SmoothStep(0,1,t);
                foreach (var hunter in hunters)
                {
                    if (!hunter) continue;
                    hunter.color=new Color(Violet.r,Violet.g,Violet.b,Violet.a*(1-fade));
                    hunter.GetComponent<PaperCard>().RenderOffset=Vector3.up*(fade*.45f);
                }
            });
            for (int i=0;i<seekers;i++) { Retire(hunters[i]); hunters[i]=null; }
            seekers=0;
        }

        internal int ApparitionCount { get; private set; }
        IEnumerator Apparitions(int count)
        {
            ApparitionCount=count; identified=false; TrueBodyOpen=false;
            Feet=Center; rig.Visible=false;
            var ghosts=new PaladinRig[count];
            var outside=new Vector2[count];
            var order=new int[count];
            for(int n=0;n<count;n++)
            {
                order[n]=n;
                outside[n]=Center+PaladinArea.Turn(Vector2.right,n*360f/count)*9.8f;
                var ghost=PaladinRig.Create(effects,rig.Height,new Color(.75f,.64f,1,.62f));
                ghost.Ground=Center;
                ghost.Armed=true;
                ghost.Act(ActionPose.Leap,outside[n]-Center);
                ghosts[n]=ghost;
            }
            audioBus.Play(Cue.Wave);
            yield return Wait(.9f,t=>
            {
                float progress=Mathf.SmoothStep(0,1,t);
                float alpha=.62f*(1-Mathf.InverseLerp(.70f,1,t));
                for(int n=0;n<count;n++)
                {
                    ghosts[n].Ground=Vector2.Lerp(Center,outside[n],progress);
                    ghosts[n].Motion(.55f*Mathf.Sin(t*Mathf.PI),-8);
                    ghosts[n].SetOpacity(alpha);
                }
            });
            foreach (var ghost in ghosts) ghost.Visible=false;
            yield return Wait(.45f);

            for (int i=count-1;i>0;i--)
            {
                int pick=Random.Range(0,i+1);
                (order[i],order[pick])=(order[pick],order[i]);
            }
            int realSlot=Random.Range(0,count);
            for (int slot=0;slot<count && !identified;slot++)
            {
                int direction=order[slot];
                bool real=slot==realSlot;
                yield return IncomingApparition(real?rig:ghosts[direction],outside[direction],real);
                if (!identified && slot+1<count) yield return Wait(.16f);
            }
            foreach (var ghost in ghosts) Retire(ghost);
        }

        IEnumerator IncomingApparition(PaladinRig actor,Vector2 start,bool real)
        {
            Vector2 target=game.Pos;
            Vector2 dir=PaladinArea.Unit(target-start);
            Vector2 end=target+dir*1.3f;
            actor.Ground=start;
            actor.Armed=Random.value<.5f;
            actor.Act(actor.Armed?ActionPose.Sweep:ActionPose.Punch,dir);
            actor.Motion();
            actor.Visible=true;
            if (real)
            {
                Feet=start; facing=dir; UpdateAnchor();
                TrueBodyOpen=true;
            }
            else actor.SetOpacity(.30f);

            Vector2 previous=start;
            float elapsed=0;
            const float flightTime=1f;
            while (!identified && elapsed<flightTime)
            {
                yield return null;
                if (!Playing || identified) continue;
                elapsed+=Time.deltaTime;
                Vector2 position=Vector2.Lerp(start,end,elapsed/flightTime);
                actor.Ground=position;
                if (real)
                {
                    Feet=position;
                    var cut=PaladinArea.Lane(previous-dir*.25f,position+dir*.65f,1.75f);
                    if (TrueBodyOpen && Strike(cut,DangerDamage))
                    {
                        TrueBodyOpen=false;
                        Sparks("Impact",position,dir,3);
                    }
                }
                previous=position;
            }
            TrueBodyOpen=false;
            actor.Visible=real && identified;
        }
    }
}
