using System.Collections;
using System.IO;
using UnityEngine;

namespace WesternLemegeton
{
    // Opt-in executable checks exercise the same stepping method as normal input.
    public sealed class MotionSmoke:MonoBehaviour
    {
        int checks;Dungeon game;Player hero;Enemy target;string output;
        void Check(bool pass,string message)
        {if(!pass){Debug.LogError("MOTION_FAILED: "+message);Application.Quit(2);throw new System.Exception(message);}checks++;Debug.Log("MOTION_PASS: "+message);}
        void Reset(int weapon,Vector2 direction)
        {
            game.State=RunState.Combat;hero.SafeEntry();hero.Weapon=weapon;hero.Skill2Cd=hero.Skill3Cd=hero.TagCd=hero.DashCd=0;
            hero.Ammo=6;hero.Reload=hero.Fury=0;hero.Combo=0;hero.Facing=hero.LastMove=direction;hero.transform.position=new Vector2(0,-1.5f);
            game.Crow.ResetAt(hero.transform.position);target.Hp=target.MaxHp=5000;target.transform.position=(Vector2)hero.transform.position+direction*1.1f;
            hero.AdvanceCombat(0,Vector2.zero);
        }
        void Step(float time)=>hero.AdvanceCombat(time,Vector2.zero);
        IEnumerator Start()
        {
            Application.runInBackground=true;output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Logs"));Directory.CreateDirectory(output);
            yield return new WaitForSeconds(.4f);game=Dungeon.I;game.StartRun();yield return null;game.Cinematics.Skip();
            hero=game.Hero;hero.enabled=false;game.Crow.enabled=false;
            foreach(var e in game.Enemies){e.enabled=false;e.transform.position=new Vector2(12,7);}
            target=game.Enemies[0];
            Reset(0,Vector2.right);var start=hero.transform.position;
            Check(hero.TrySkill(0),"Slash starts");Step(.10f);
            Check(target.Hp==5000&&hero.transform.position==start,"Slash anticipation has no early movement or damage");
            Step(.015f);Check(target.Hp==4964&&hero.MotionFrame>=2,"Slash starts collision during action pose");
            Step(.185f);Check(Mathf.Abs(hero.transform.position.x-start.x-2.85f)<.02f,"Slash travels 2.85 units only during its 0.19 second active interval");
            Check(target.Hp==4964,"Swept slash hits one target only once");Step(.09f);
            Check(hero.SkillBusy&&hero.Phase==MotionPhase.Recovery&&hero.Skill2Cd==0,"Slash keeps a visible recovery before cooldown");Step(.10f);
            Check(!hero.SkillBusy&&hero.Skill2Cd>7.9f,"Slash finishes into eight second cooldown");
            Reset(0,Vector2.right);Check(hero.TrySkill(1),"Slam starts");start=hero.transform.position;hero.AdvanceCombat(.79f,Vector2.right);
            Check(target.Hp==5000&&hero.transform.position==start&&hero.ActionLocked,"Slam airborne anticipation cannot damage or walk");Step(.011f);
            Check(target.Hp==4980&&hero.MotionFrame==4&&Mathf.Abs(hero.Presentation.transform.localPosition.y+.35f)<.001f,"Slam landing pose, ground contact and first impact agree");Step(.25f);
            Check(target.Hp==4946,"Slam second impact occurs at 1.05 seconds");Step(.95f);
            Check(!hero.ActionLocked&&!hero.SkillBusy,"Slam releases control after recovery");
            Reset(1,Vector2.up);hero.LastMove=Vector2.left;Check(hero.TrySkill(0),"Shot starts");Step(.119f);
            Check(target.Hp==5000,"Shot anticipation does not deal damage");Step(.002f);
            Check(target.Hp==4958&&hero.MotionDirection==Vector2.up,"Shot fires toward captured aim, not previous movement");Step(.10f);
            Check(target.Hp==4958,"Shot cannot repeat its hit during recoil");
            Reset(1,Vector2.right);Check(hero.TrySkill(1),"Barrage starts");Step(.199f);Check(target.Hp==5000,"Barrage windup has no instant radial damage");Step(.81f);
            Check(target.Hp==4945,"Barrage processes exactly five pulses even across a long frame");Step(.5f);Check(target.Hp==4945&&hero.Phase==MotionPhase.Recovery,"Barrage recovery applies no extra pulses");
            Reset(0,Vector2.right);Check(hero.TrySkill(1),"Pause test starts");Step(.30f);float elapsed=hero.SkillElapsed;int frame=hero.MotionFrame;game.State=RunState.Paused;Step(.5f);
            Check(hero.SkillElapsed==elapsed&&hero.MotionFrame==frame&&target.Hp==5000,"Pause freezes pose, movement and pending skill damage");game.State=RunState.Combat;
            Reset(1,Vector2.right);Check(hero.TryAttack()&&hero.Ammo==6,"Basic shot reserves its animation before consuming ammo");Check(hero.TryDodge(Vector2.up),"Dodge cancels basic anticipation");Step(.3f);
            Check(hero.Ammo==6&&target.Hp==5000,"Canceled basic shot consumes no ammunition and cannot hit later");
            Reset(0,Vector2.right);Check(hero.TryAttack(),"Basic melee starts");Step(.05f);Check(target.Hp==5000,"Basic melee has anticipation");Step(.02f);Check(target.Hp==4983,"Basic melee strikes on its action frame");
            hero.SafeEntry();Step(.5f);Check(target.Hp==4983,"Room safe entry clears pending animation attacks");
            Reset(0,Vector2.right);
            for(int i=0;i<4;i++){Check(hero.TryAttack(),"Four hit chain starts stage "+(i+1));Step(.26f);}
            Check(game.Crow.CurrentState==RavenState.ComboWindup&&target.Hp==4919,"Animated four hit sequence still triggers crow combo on real hits");
            float scale=HunterAtlas.Scale;
            for(int row=0;row<4;row++)for(int column=0;column<6;column++){HunterAtlas.Pose(hero.Presentation,row,column);Check(Mathf.Abs(hero.Presentation.transform.localScale.x-scale)<.0001f,"Constant character scale for pose "+row+"/"+column);}
            // Capture actual rendered key poses, not an isolated marketing illustration.
            float[][] samples={new[]{0f,.07f,.14f,.21f,.28f,.40f},new[]{0f,.16f,.45f,.70f,.85f,1.40f},new[]{0f,.07f,.13f,.18f,.27f,.37f},new[]{0f,.12f,.21f,.41f,.61f,1.40f}};
            for(int row=0;row<4;row++)
            {
                Reset(row<2?0:1,Vector2.right);target.transform.position=(Vector2)hero.transform.position+Vector2.right*6;
                game.ClearTransient();Check(hero.TrySkill(row%2),"Visual skill starts "+row);float last=0;
                for(int column=0;column<6;column++)
                {
                    Step(samples[row][column]-last);last=samples[row][column];
                    yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                    var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,$"motion-{row}-{column}.png"),image.EncodeToPNG());Destroy(image);
                    yield return new WaitForSeconds(.08f);
                }
            }
            File.WriteAllText(Path.Combine(output,"motion-checks.txt"),"PASS: "+checks+" motion checks / 24 key pose captures / collision, cancellation, pause and combo timing");
            Debug.Log("WNN_MOTION_SMOKE_SUCCESS: "+checks);Application.Quit(0);
        }
    }
}
