using System.Collections;
using UnityEngine;
using ActionPose = WesternLemegeton.PaladinRig.Action;
using Cue = WesternLemegeton.BossTrialAudio.Cue;

namespace WesternLemegeton
{
    public sealed partial class PaladinEncounter
    {
        IEnumerator SlashMotion(float sweep,bool stab=false)
        {
            Act(stab?ActionPose.Lunge:ActionPose.Sweep);
            yield return Wait(stab?.13f:.22f,t=>rig.Motion(0,7*Mathf.Sin(t*Mathf.PI),
                .24f*Mathf.Sin(t*Mathf.PI),stab?0:Mathf.Lerp(-sweep*.5f,sweep*.5f,t)));
            rig.Motion();
        }

        IEnumerator CrossScars()
        {
            Vector2 origin=Feet, dir=Aim();
            var first=PaladinArea.Lane(origin,origin+dir*12,2.2f);
            Act(ActionPose.Coil);
            var horn=SignalLight(true,Violet);
            yield return Warning(first,.65f);
            Retire(horn); Blow(first,UnjudgedDamage);
            var scar=Decal(first,new Color(.64f,.17f,1,.53f));
            yield return SlashMotion(120);
            yield return Wait(.42f);
            Act(ActionPose.Coil);
            horn=SignalLight(false,Violet);
            dir=Aim();
            var second=PaladinArea.Lane(Feet,Feet+dir*12,2.2f);
            yield return Warning(second,.53f);
            Retire(horn); Retire(scar);
            Flash(first,Cue.Cut); Flash(second,Cue.Cut); Sparks("Arc",Feet,dir,4);
            if (first.Contains(game.Pos)||second.Contains(game.Pos)) HitHunter(UnjudgedDamage);
            yield return SlashMotion(-135);
        }

        SpriteRenderer SignalLight(bool left,Color color,bool hand=false)
        {
            var signal=Ink.Shape(hand?"Glowing glove":"Horn signal",effects,Feet,Vector2.one*(hand?.85f:.48f),color,3400,true);
            signal.sprite=Ink.SoftDisc;
            var card=signal.GetComponent<PaperCard>(); card.Flat=false;
            var lift=hand?new Vector3(.65f*Mathf.Sign(facing.x),1.45f,0):
                new Vector3(left?-.19f:.22f,rig.Height+.02f,0);
            card.RenderOffset=Quaternion.Euler(PaperWorld.Pitch,0,0)*lift;
            return signal;
        }

        IEnumerator ReturningBlade()
        {
            Vector2 start=Feet, dir=Aim();
            float distance=Mathf.Min(PaladinArea.ToWall(start,dir,Center)-.25f,
                Mathf.Max(9,Vector2.Distance(game.Pos,start)+6));
            Vector2 end=start+dir*Mathf.Max(.1f,distance);
            var lane=PaladinArea.Lane(start,end,ReturningBladeWidth);
            Act(ActionPose.Coil); yield return Warning(lane,.58f);
            Act(ActionPose.Lunge); rig.Armed=false;
            var blade=Blade("Piercing throw",effects,start,2.1f);
            audioBus.Play(Cue.Wave);
            yield return TravelBlade(blade,start,end,Mathf.Max(.18f,distance/26),true,false,launch:true);
            Vector2 step=Vector2.Perpendicular(dir)*(Random.value<.5f?1:-1);
            Vector2 side=Rules.ClampToRoom(start+step*3.4f,.6f);
            if (Vector2.Distance(start,side)<1.5f) side=Rules.ClampToRoom(start-step*3.4f,.6f);
            Act(ActionPose.Ready);
            yield return Wait(.62f,t=>Feet=Rules.ClampToRoom(Vector2.Lerp(start,side,Mathf.SmoothStep(0,1,t)),.6f));
            var returnLane=PaladinArea.Lane(end,Feet,ReturningBladeWidth);
            var tell=Decal(returnLane,WarningColor);
            var thread=Decal(PaladinArea.Lane(end,Feet,.09f),Violet);
            Act(ActionPose.Palm); yield return Wait(.34f);
            Retire(tell); Retire(thread);
            Act(ActionPose.Coil); audioBus.Play(Cue.Recall);
            yield return CatchBlade(blade,.115f,true);
        }

        IEnumerator SpinningSlices()
        {
            var blade=Blade("Whirling verdict",effects,Feet,2.3f);
            Act(ActionPose.Lunge); rig.Armed=false;
            yield return TravelBlade(blade,Feet,Center,.36f,false,true,launch:true);
            var sectors=new PaladinArea[4];
            var warnings=new PaladinDecal[4];
            float rotation=Random.Range(0,360f);
            yield return Wait(.50f,t=>blade.transform.rotation=Quaternion.Euler(0,0,t*800));
            for(int i=0;i<4;i++)
            {
                Vector2 dir=PaladinArea.Turn(Vector2.right,rotation+i*90+Random.Range(-18f,18f));
                sectors[i]=PaladinArea.Band(Center,0,PaladinArea.ArenaHalfSize.magnitude,dir,Random.Range(28f,48f));
                warnings[i]=Decal(sectors[i],WarningColor);
            }
            yield return Wait(.84f,t=>blade.transform.rotation=Quaternion.Euler(0,0,t*1100));
            bool hit=false;
            for (int i=0;i<4;i++) { Retire(warnings[i]); StartCoroutine(Glow(sectors[i])); hit|=sectors[i].Contains(game.Pos); }
            if (hit) HitHunter(UnjudgedDamage);
            audioBus.Play(Cue.Ground); Sparks("Impact",Center,Vector2.right,4);
            yield return CatchBlade(blade,.23f);
        }

        IEnumerator QuickCuts()
        {
            bool left=Random.value<.5f;
            Vector2 dir=Aim();
            for (int i=0;i<2;i++)
            {
                var area=Sector(Feet,dir,left,9.6f);
                Act(ActionPose.Coil); rig.Motion(0,left?-12:12,0,left?-55:55);
                yield return Warning(area,i==0?.53f:.22f);
                Blow(area,UnjudgedDamage,"Arc",true);
                yield return SlashMotion(left?130:-130);
                left=!left;
            }
            dir=Aim();
            var thrust=PaladinArea.Lane(Feet,Feet+dir*10.95f,3.45f);
            yield return Warning(thrust,.4f);
            for (int n=0;n<3;n++)
            {
                Blow(thrust,UnjudgedDamage*.5f,"Impact",true);
                yield return SlashMotion(0,true);
            }
        }

        IEnumerator Ricochet()
        {
            Act(ActionPose.Coil);
            Vector2 dir=PaladinArea.Turn(Aim(),Random.Range(-33f,33f));
            if (Mathf.Abs(dir.x)<.18f) dir=PaladinArea.Unit(new Vector2(.24f,dir.y));
            if (Mathf.Abs(dir.y)<.18f) dir=PaladinArea.Unit(new Vector2(dir.x,.24f));
            Vector2 point=Feet;
            Vector2 firstWall=point+dir*PaladinArea.ToWall(point,dir,Center);
            var startCap=Decal(PaladinArea.Band(point,0,RicochetRadius,-dir,180),WarningColor);
            var endCap=Decal(PaladinArea.Band(firstWall,0,RicochetRadius,dir,180),WarningColor);
            yield return Warning(PaladinArea.Lane(point,firstWall,RicochetRadius*2),.6f);
            Retire(startCap); Retire(endCap);
            var blade=Blade("Four-wall ricochet",effects,point,2.1f);
            rig.Armed=false; armoredThrow=true;
            var ward=Decal(PaladinArea.Band(Feet,1.1f,1.32f),Violet);
            var disc=Decal(PaladinArea.Band(point,0,RicochetRadius),RicochetTint,3100);
            disc.MarkRicochet();
            Act(ActionPose.Ready);
            for (int n=0;n<4;n++)
            {
                float length=PaladinArea.ToWall(point,dir,Center);
                Vector2 wall=point+dir*length, local=wall-Center;
                yield return TravelBlade(blade,point,wall,Mathf.Max(.06f,length/24),true,true,disc,n==0);
                Sparks("Impact",wall,-dir,2); audioBus.Play(Cue.Armor);
                if (Mathf.Abs(Mathf.Abs(local.x)-PaladinArea.ArenaHalfSize.x)<.05f) dir.x=-dir.x;
                if (Mathf.Abs(Mathf.Abs(local.y)-PaladinArea.ArenaHalfSize.y)<.05f) dir.y=-dir.y;
                point=wall+dir*.015f;
            }
            yield return CatchBlade(blade,.28f,true,disc);
            Retire(ward); Retire(disc); armoredThrow=false;
        }

        IEnumerator Dive(bool sword)
        {
            if (!sword)
            {
                Act(ActionPose.Slam); quakePlace=Feet; quakeEnd=clockTwo+3.8f;
                var slow=Decal(PaladinArea.Band(Feet,0,4),new Color(.52f,.43f,.94f,.44f));
                StartCoroutine(FadeQuake(slow)); audioBus.Play(Cue.Ground);
            }
            Vector2 start=Feet, target=Rules.ClampToRoom(game.Pos,.65f);
            var impact=PaladinArea.Band(target,0,sword?3.1f:3.7f);
            var tell=Decal(impact,WarningColor);
            Act(ActionPose.Leap);
            yield return Wait(.76f,t=>
            {
                Feet=Vector2.Lerp(start,target,t);
                rig.Motion(Mathf.Sin(t*Mathf.PI)*3.6f,8*t);
            });
            Retire(tell); rig.Motion(); Act(ActionPose.Slam);
            if (!sword)
            {
                Flash(impact,Cue.Ground); Sparks("Impact",target,facing,4);
                if (impact.Contains(game.Pos) && !game.Hero.Invulnerable) yield return SlamHunter(false);
                yield break;
            }
            Blow(impact,UnjudgedDamage,"Impact");
            var vortex=PaladinArea.Band(Feet,3,9.4f);
            Act(ActionPose.Coil); yield return Warning(vortex,.7f);
            Blow(vortex,UnjudgedDamage); yield return SlashMotion(360);
        }
        IEnumerator FadeQuake(PaladinDecal field) { yield return Wait(3.8f); Retire(field); }

        IEnumerator DrawCircle()
        {
            Vector2 origin=Rules.ClampToRoom(game.Pos,5.4f);
            const float r=5.1f;
            bool counter=Random.value<.5f;
            Vector2 start=Feet, edge=origin+Vector2.right*r;
            yield return Wait(.4f,t=>Feet=Rules.ClampToRoom(Vector2.Lerp(start,edge,t),.5f));
            Act(ActionPose.Sweep);
            var trace=Decal(PaladinArea.Band(origin,r-.16f,r),Color.clear);
            var danger=PaladinArea.Band(origin,counter?0:r,counter?r:58);
            var warn=Decal(danger,WarningColor);
            yield return Wait(1.10f,t=>
            {
                float turn=(counter?1:-1)*360*t;
                Vector2 radial=PaladinArea.Turn(Vector2.right,turn);
                Feet=Rules.ClampToRoom(origin+radial*r,.5f); facing=Vector2.Perpendicular(radial)*(counter?1:-1);
                Act(ActionPose.Sweep); rig.Motion(0,16,0,-25);
                trace.Paint(PaladinArea.Band(origin,r-.19f,r+.06f,PaladinArea.Turn(Vector2.right,turn/2),
                    Mathf.Max(1,Mathf.Abs(turn))),Violet,3100,Center);
            });
            Retire(trace); Retire(warn); Blow(danger,DangerDamage);
        }

        IEnumerator ColorVerdict()
        {
            Vector2 start=Feet, north=Center+Vector2.up*(PaladinArea.ArenaHalfSize.y-.65f);
            Act(ActionPose.Leap);
            yield return Wait(.65f,t=>{Feet=Vector2.Lerp(start,north,t);rig.Motion(Mathf.Sin(t*Mathf.PI)*2);});
            facing=Vector2.down; Act(ActionPose.Coil);
            bool still=Random.value<.5f;
            rig.BladeTint=still?new Color(1,.12f,.14f):Violet;
            // Locate the pivot beyond the northern wall so every playable point is in the semicircle.
            var zone=PaladinArea.Band(Center+Vector2.up*(PaladinArea.ArenaHalfSize.y+.2f),0,42,Vector2.down,180);
            var cue=Decal(zone,still?WarningColor:Ice);
            game.Tell(still?"붉은 검 · 멈추세요":"보라 검 · 움직이세요");
            yield return Wait(1.25f,t=>rig.Motion(0,-14*t,0,-80*t));
            Retire(cue); Act(ActionPose.Sweep); Flash(zone,Cue.Cut);
            Sparks("Arc",Feet,Vector2.down,12);
            bool moving=clockTwo-lastMoved<.09f || (game.Pos-lastHunter).sqrMagnitude>.000001f;
            if (moving==still) game.Hero.HurtOverTime(DangerDamage,0);
            yield return SlashMotion(220); rig.BladeTint=Violet;
        }
    }
}
