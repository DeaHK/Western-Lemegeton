using System.Collections;
using UnityEngine;
using Pose = WesternLemegeton.PaladinRig.Pose;
using Cue = WesternLemegeton.BossTrialAudio.Cue;

namespace WesternLemegeton
{
    public sealed partial class PaladinEncounter
    {
        SpriteRenderer Star(Vector2 point,float size)
        {
            var halo=Ink.Shape("Hand light",effects,point,Vector2.one*size*2,LightColor,750,true);
            halo.sprite=Ink.SoftDisc;
            halo.GetComponent<PaperCard>().RenderOffset=rig.CastingHandOffset;
            return halo;
        }

        SpriteRenderer FlyingSword(Vector2 point)
        {
            var sword=Dungeon.TrialArt(effects,"Sword of light",Resources.Load<Sprite>("Paladin/Sword"),point,
                new Vector2(.55f,2.1f),780,false);
            sword.transform.rotation=Quaternion.Euler(0,0,180);
            sword.color=CoreColor;
            return sword;
        }

        IEnumerator SummonSwords()
        {
            PoseAt(Pose.Call); audioBus.Play(Cue.Charge);
            int count=enhanced ? 3 : 1;
            Coroutine last=null;
            for (int i=0;i<count;i++)
            {
                last=StartCoroutine(FallingSword());
                if (i<count-1) yield return Wait(.5f);
            }
            yield return last;
        }

        IEnumerator FallingSword()
        {
            Vector2 start=Feet;
            var sword=FlyingSword(start);
            var card=sword.GetComponent<PaperCard>();
            Vector3 hand=rig.CastingHandOffset;
            card.RenderOffset=hand;
            Vector2 hover=start;
            const float altitude=4.3f;
            float BladeAngle(Vector2 from, Vector2 aimPoint)
            {
                Vector3 flight=PaperWorld.Point(aimPoint)-PaperWorld.Point(from,altitude);
                float vertical=Vector3.Dot(flight,Quaternion.Euler(PaperWorld.Pitch,0,0)*Vector3.up);
                return Mathf.Atan2(vertical,flight.x)*Mathf.Rad2Deg-90;
            }
            yield return Wait(1.2f,t=>
            {
                Vector2 approach=PaladinArea.Unit(game.Pos-start);
                hover=Vector2.Lerp(start,Rules.ClampToRoom(game.Pos-approach*2.1f),Mathf.SmoothStep(0,1,t/.65f));
                sword.transform.position=hover;
                card.RenderOffset=Vector3.Lerp(hand,Vector3.up*altitude,Mathf.SmoothStep(0,1,t/.65f));
                float turn=BladeAngle(hover,game.Pos)+Mathf.Lerp(150,18,t);
                sword.transform.rotation=Quaternion.Euler(0,0,turn);
            });
            Vector2 target=Rules.ClampToRoom(game.Pos);
            float angle=BladeAngle(hover,target);
            var area=PaladinArea.Band(target,0,1.32f);
            var warning=Decal(area,WarningColor);
            yield return Wait(.5f,t=>sword.transform.rotation=Quaternion.Euler(0,0,angle+18*(1-Mathf.SmoothStep(0,1,t))));
            float tipLength=sword.sprite.bounds.max.y*sword.transform.localScale.y;
            Vector3 tip=Quaternion.Euler(PaperWorld.Pitch,0,0)*Quaternion.Euler(0,0,angle)*Vector3.up*tipLength;
            yield return Wait(.08f,t=>
            {
                sword.transform.position=Vector2.Lerp(hover,target,t);
                card.RenderOffset=Vector3.Lerp(Vector3.up*altitude,-tip,t);
            });
            Retire(warning); Strike(area,NormalDamage); Flash(area,Cue.Miracle);
            Ink.Burst(target,LightColor,7);
            yield return Wait(.22f);
            Retire(sword);
        }

        IEnumerator Rain()
        {
            PoseAt(Pose.Call);
            yield return Wait(.4f,t=>rig.Motion(t*.95f));
            PoseAt(Pose.Miracle);
            audioBus.Play(Cue.Charge);
            for (int second=0;second<5;second++)
            {
                int rate=Random.Range(3,5)+(enhanced?Random.Range(2,4):0);
                for (int i=0;i<rate;i++)
                {
                    Vector2 reach=PaladinArea.ArenaHalfSize-Vector2.one*.8f;
                    var p=Center+new Vector2(Random.Range(-reach.x,reach.x),Random.Range(-reach.y,reach.y));
                    StartCoroutine(Pillar(p));
                    yield return Wait(1f/rate);
                }
            }
            yield return Wait(.48f,t=>rig.Motion(.95f*(1-t)));
        }

        IEnumerator Pillar(Vector2 point)
        {
            var area=PaladinArea.Band(point,0,2.025f);
            var warning=Decal(area,WarningColor);
            yield return Wait(.27f);
            var beam=Ink.Shape("Column of dawn",effects,point,new Vector2(1.05f,5.8f),LightColor,650);
            var core=Ink.Shape("White hot core",effects,point,new Vector2(.255f,5.8f),CoreColor,651);
            var beamCard=beam.GetComponent<PaperCard>();
            var coreCard=core.GetComponent<PaperCard>();
            Vector3 up=Quaternion.Euler(PaperWorld.Pitch,0,0)*Vector3.up;
            beamCard.RenderOffset=coreCard.RenderOffset=up*7;
            yield return Wait(.08f,t=>beamCard.RenderOffset=coreCard.RenderOffset=up*Mathf.Lerp(7,2.9f,t));
            Retire(warning); Strike(area,NormalDamage); Flash(area,Cue.Miracle);
            yield return Wait(.21f,t=>
            {
                beam.color=new Color(1,.81f,.22f,(1-t)*.90f);
                core.color=new Color(1,1,.92f,1-t);
            });
            Retire(beam); Retire(core);
        }

        IEnumerator Ambush()
        {
            rig.Visible=false;
            yield return Wait(1.25f);
            Feet=Rules.ClampToRoom(game.Pos-PaladinArea.Unit(game.Hero.Facing)*.95f,.55f);
            Vector2 dir=Aim();
            var cut=PaladinArea.Band(Feet,1.4f,10.35f,dir,270);
            Vector2 start=Feet+dir*1.4f;
            float distance=PaladinArea.ToWall(Feet,dir,Center);
            Vector2 end=Feet+dir*distance;
            const float waveWidth=2.475f, waveScale=1.75f;
            var path=distance>1.4f ? Decal(PaladinArea.Lane(start,end,waveWidth*waveScale),WarningColor) : null;
            yield return Warning(cut,.75f);
            rig.Visible=true; PoseAt(Pose.Slash);
            Retire(path); Strike(cut,NormalDamage); Flash(cut,Cue.Cut);
            if (distance>1.4f) StartCoroutine(Wave(start,dir,11.5f,waveWidth,NormalDamage,waveScale));
            yield return Swing();
        }

        IEnumerator Wave(Vector2 start,Vector2 dir,float speed,float width,float damage,float scale=1)
        {
            float total=PaladinArea.ToWall(start,dir,Center);
            var edge=Decal(PaladinArea.Band(start,0,.1f),LightColor,12500);
            edge.PaintWave(start,dir,width,Center,scale);
            float distance=0;
            bool hit=false;
            while(distance<total)
            {
                yield return null;
                if (!Playing) continue;
                float next=Mathf.Min(total,distance+Time.deltaTime*speed);
                Vector2 before=start+dir*distance, p=start+dir*next;
                if (!hit && PaladinArea.WaveSwept(game.Pos,before,dir,next-distance,width,scale)) hit=HitHunter(damage);
                edge.PaintWave(p,dir,width,Center,scale);
                distance=next;
            }
            Retire(edge);
        }
    }
}

