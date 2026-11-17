using System.Collections;
using UnityEngine;
using ActionPose = WesternLemegeton.PaladinRig.Action;
using Cue = WesternLemegeton.BossTrialAudio.Cue;

namespace WesternLemegeton
{
    public sealed partial class PaladinEncounter
    {
        IEnumerator PunchDash(Vector2 target,float width,float duration)
        {
            Vector2 start=Feet, dir=PaladinArea.Unit(target-start), old=start;
            bool hit=false;
            Act(ActionPose.Punch); audioBus.Play(Cue.Wave);
            yield return Wait(duration,t=>
            {
                Feet=Rules.ClampToRoom(Vector2.Lerp(start,target,t),.48f);
                if (!hit) hit=Strike(PaladinArea.Lane(old-dir*.25f,Feet+dir*.65f,width),UnjudgedDamage);
                rig.Motion(.12f,12,.16f);
                old=Feet;
            });
            Sparks("Impact",Feet,dir,2.8f); rig.Motion();
        }

        IEnumerator BeamPunch()
        {
            Vector2 dir=Aim();
            var beam=PaladinArea.Lane(Feet,Feet+dir*PaladinArea.ToWall(Feet,dir,Center),1.55f);
            Act(ActionPose.Palm);
            yield return Warning(beam,.38f);
            Blow(beam,UnjudgedDamage,"Impact");
            yield return Wait(.20f);
            Vector2 target=Rules.ClampToRoom(game.Pos,.5f);
            facing=PaladinArea.Unit(target-Feet); Act(ActionPose.Coil);
            yield return Warning(PaladinArea.Lane(Feet,target,1.7f),.32f);
            yield return PunchDash(target,1.7f,Mathf.Clamp(Vector2.Distance(Feet,target)/32,.12f,.46f));
        }

        IEnumerator HandGrab()
        {
            var sweep=PaladinArea.Band(Feet,0,5);
            Act(ActionPose.Catch);
            yield return Warning(sweep,.67f);
            Flash(sweep,Cue.Wave);
            yield return Wait(.12f,t=>rig.Motion(0,Mathf.Sin(t*6)*10,.12f*t));
            if (sweep.Contains(game.Pos) && !game.Hero.Invulnerable && !game.Hero.Dodging) yield return SlamHunter(true);
        }

        IEnumerator SlamHunter(bool punch)
        {
            bound=true; game.Hero.BindForTrial(1.35f); Act(ActionPose.Catch);
            Vector2 caught=game.Pos, dir=PaladinArea.Unit(caught-Feet);
            Vector2 grip=Rules.ClampToRoom(Feet+dir*1.1f,.38f);
            yield return Wait(.33f,t=>
            {
                game.Hero.transform.position=Vector2.Lerp(caught,grip,t);
                game.Hero.TrialLift(1.35f*Mathf.Sin(t*Mathf.PI*.5f)); rig.Motion(.23f*t);
            });
            Act(ActionPose.Slam);
            yield return Wait(.12f,t=>game.Hero.TrialLift((1-t)*1.35f));
            Sparks("Impact",game.Pos,dir,3.5f); audioBus.Play(Cue.Ground);
            game.Hero.HurtOverTime(UnjudgedDamage,0);
            if (punch && game.Hero.Hp>0)
            {
                Act(ActionPose.Coil); yield return Wait(.26f);
                Act(ActionPose.Punch); audioBus.Play(Cue.Fist); Sparks("Impact",game.Pos,dir,3);
                game.Hero.HurtOverTime(UnjudgedDamage,0);
            }
            FreeHunter();
            if (punch && game.Hero.Hp>0) game.Hero.ApplyChargeKnockback(dir,5.4f,.45f,.75f);
        }

        IEnumerator Feint()
        {
            Act(ActionPose.Coil); yield return Wait(.34f);
            rig.Visible=false;
            float baseAngle=Random.Range(0,360);
            for(int i=0;i<2;i++)
            {
                Vector2 offset=PaladinArea.Turn(Vector2.right,baseAngle+i*129)*2.75f;
                var ghost=PaladinRig.Create(effects,rig.Height,new Color(.75f,.55f,1,.33f));
                ghost.Armed=false; ghost.Ground=Rules.ClampToRoom(game.Pos+offset,.55f);
                ghost.Act(ActionPose.Punch,game.Pos-ghost.Ground);
                audioBus.Play(Cue.Wave);
                yield return Wait(.27f,t=>ghost.Motion(.12f,12,.25f*t));
                Retire(ghost); yield return Wait(.14f);
            }
            Feet=Rules.ClampToRoom(game.Pos+PaladinArea.Turn(Vector2.right,baseAngle+251)*2.55f,.55f);
            var area=PaladinArea.Lane(Feet,Feet+Aim()*4.2f,2.2f);
            yield return Warning(area,.23f);
            rig.Visible=true; Act(ActionPose.Punch);
            Blow(area,UnjudgedDamage,"Impact"); yield return Wait(.22f);
        }

        IEnumerator DashChain()
        {
            Act(ActionPose.Coil); Sparks("Bolt",Feet,facing,3.2f);
            var marks=new PaladinDecal[3]; var ends=new Vector2[3];
            Vector2 start=Feet;
            float side=Random.value<.5f?-1:1;
            for(int i=0;i<3;i++)
            {
                Vector2 diff=game.Pos-start;
                if (i==2) ends[i]=Rules.ClampToRoom(game.Pos,.5f);
                else
                {
                    Vector2 dir=PaladinArea.Turn(PaladinArea.Unit(diff),side*(i==0?55:-85));
                    ends[i]=Rules.ClampToRoom(start+dir*Mathf.Clamp(diff.magnitude*.65f+2,4.5f,9),.6f);
                    if (Vector2.Distance(ends[i],start)<2) ends[i]=Rules.ClampToRoom(start-dir*5,.6f);
                }
                marks[i]=Decal(PaladinArea.Lane(start,ends[i],1.8f),WarningColor);
                start=ends[i];
                yield return Wait(i==2?.32f:.16f);
            }
            for(int i=0;i<3;i++)
            {
                Retire(marks[i]); facing=PaladinArea.Unit(ends[i]-Feet);
                yield return PunchDash(ends[i],1.8f,.14f);
            }
        }

        IEnumerator OneTwo()
        {
            Vector2 dir=Aim();
            var wind=PaladinArea.Band(Feet,0,6.4f,dir,72);
            var close=PaladinArea.Band(Feet,0,3.0f,dir,72);
            var outer=Decal(wind,new Color(.56f,.72f,1,.34f));
            Act(ActionPose.Coil); yield return Warning(close,.48f);
            Retire(outer);
            bool pushed=false;
            for (int i=0;i<2;i++)
            {
                Act(ActionPose.Punch); rig.Motion(0,i==0?-9:9,.22f);
                Flash(wind,Cue.Wave); Sparks("Impact",Feet+dir*2,dir,2.8f);
                if (!pushed && wind.Contains(game.Pos) && !game.Hero.Invulnerable && !game.Hero.Dodging)
                {
                    if (close.Contains(game.Pos)) HitHunter(UnjudgedDamage,true);
                    if (game.Hero.Hp>0)
                    {
                        game.Hero.ApplyChargeKnockback(dir,2.1f,.20f,.28f);
                        pushed=game.Hero.KnockedBack;
                    }
                }
                yield return Wait(.10f);
            }
            if (!pushed) yield break;
            var target=Rules.ClampToRoom(game.Pos+dir*.6f,.6f);
            Vector2 start=Feet;
            Act(ActionPose.Kick);
            yield return Wait(.14f,t=>Feet=Rules.ClampToRoom(Vector2.Lerp(start,target,t),.6f));
            Blow(PaladinArea.Band(Feet,0,2.15f),UnjudgedDamage,"Impact",true);
        }

        IEnumerator FivePillars()
        {
            Act(ActionPose.Slam); audioBus.Play(Cue.Ground);
            for (int n=0;n<5;n++)
            {
                var target=Rules.ClampToRoom(game.Pos+Random.insideUnitCircle*3.8f,.2f);
                StartCoroutine(Thunder(target,.42f,null));
                yield return Wait(.16f);
            }
            yield return Wait(.4f);
        }

        IEnumerator Thunder(Vector2 point,float duration,System.Action onHit)
        {
            var area=PaladinArea.Band(point,0,1.55f);
            yield return Warning(area,duration);
            Sparks("Bolt",point,Vector2.up,4.8f);
            audioBus.Play(Cue.Shock);
            StartCoroutine(Glow(area));
            if (Strike(area,onHit==null?UnjudgedDamage:5,true)) onHit?.Invoke();
        }

        IEnumerator RedHand()
        {
            // Centering the arena-wide ring keeps its safe side reachable from the furthest corner.
            Vector2 start=Feet;
            yield return Wait(.47f,t=>Feet=Vector2.Lerp(start,Center,t));
            const float radius=9; // pi * 9^2 = 254 of the 1215 square-unit arena.
            bool inside=Random.value<.5f;
            var danger=PaladinArea.Band(Feet,inside?0:radius,inside?radius:30);
            Act(ActionPose.Coil);
            var red=SignalLight(false,new Color(1,.05f,.08f),true);
            float range=Vector2.Distance(game.Pos,Feet);
            float travel=inside?radius-range:range-radius;
            yield return Warning(danger,Mathf.Max(1.05f,travel/4.7f+.38f));
            Retire(red); Act(ActionPose.Slam);
            Blow(danger,DangerDamage,"Impact");
        }

        IEnumerator StormAmbush()
        {
            Act(ActionPose.Coil); yield return Wait(.34f);
            rig.Visible=false;
            int strikes=0;
            for(int n=0;n<12;n++)
            {
                Vector2 spot=game.Pos+Random.insideUnitCircle*(n%3==0?.7f:4.8f);
                if (n%3==0) audioBus.Play(Cue.Armor);
                StartCoroutine(Thunder(Rules.ClampToRoom(spot,.35f),.41f,()=>
                {
                    if (++strikes==2)
                    {
                        bound=true; game.Hero.BindForTrial(5.5f); game.Tell("감전");
                    }
                }));
                yield return Wait(.23f);
            }
            yield return Wait(.45f);
            var mask=Decal(PaladinArea.Band(Center,0,100),Color.clear,11500,false);
            yield return Wait(.35f,t=>mask.PaintSpotlights(game.Pos,Center,false,t));
            Vector2 target=game.Pos;
            Vector2 start=Rules.ClampToRoom(target-PaladinArea.Unit(game.Hero.Facing)*4.25f,.5f);
            Feet=start; facing=PaladinArea.Unit(target-start);
            rig.Visible=true; Act(ActionPose.Leap);
            // Illuminate the silhouette above the blackout as it crosses into the player's spotlight.
            yield return Wait(.58f,t=>
            {
                Feet=Vector2.Lerp(start,target,t); rig.Motion(Mathf.Sin(t*Mathf.PI)*2.8f);
                mask.PaintSpotlights(game.Pos,Center,false,1);
            });
            Act(ActionPose.Slam); rig.Motion(); Retire(mask);
            var slam=PaladinArea.Band(target,0,3);
            if (strikes>=2)
            {
                Flash(slam,Cue.Ground); Sparks("Impact",target,facing,4);
                game.Hero.HurtOverTime(DangerDamage,0);
            }
            else Blow(slam,DangerDamage,"Impact");
            FreeHunter();
        }
    }
}
