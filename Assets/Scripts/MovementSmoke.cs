using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace WesternLemegeton
{
    public sealed class MovementSmoke:MonoBehaviour
    {
        Dungeon game;Player hero;int checks;string output;
        void Check(bool pass,string message)
        {if(!pass){Debug.LogError("MOVEMENT_FAILED: "+message);Application.Quit(2);throw new System.Exception(message);}checks++;Debug.Log("MOVEMENT_PASS: "+message);}
        void Reset(Vector2 direction)
        {hero.LastMove=hero.Facing=direction;hero.SafeEntry();hero.Skill2Cd=hero.Skill3Cd=hero.DashCd=hero.TagCd=0;hero.Weapon=0;hero.transform.position=new Vector2(-2,-2);hero.Locomotion.Reset(direction);hero.AdvanceCombat(0,Vector2.zero);}
        void Move(Vector2 direction,int frames=30,float dt=1f/60)
        {for(int i=0;i<frames;i++)hero.AdvanceCombat(dt,direction);}
        IEnumerator Start()
        {
            Application.runInBackground=true;output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Logs"));Directory.CreateDirectory(output);
            yield return new WaitForSeconds(.3f);game=Dungeon.I;game.StartRun();yield return null;game.Cinematics.Skip();hero=game.Hero;hero.enabled=false;game.Crow.enabled=false;
            foreach(var e in game.Enemies){e.enabled=false;e.transform.position=new Vector2(12,7);}
            var obstacles=Dungeon.Obstacles;var solids=Dungeon.SolidObstacles;Dungeon.Obstacles=System.Array.Empty<Rect>();Dungeon.SolidObstacles=System.Array.Empty<Rect>();
            var directions=new[]{Vector2.right,new Vector2(1,1),Vector2.up,new Vector2(-1,1),Vector2.left,new Vector2(-1,-1),Vector2.down,new Vector2(1,-1)};
            int[] rows={1,2,3,2,1,1,0,1};
            for(int i=0;i<8;i++)
            {
                Reset(directions[i]);Vector2 start=hero.transform.position;Move(directions[i],10);
                Check(hero.Locomotion.Sector==i&&hero.Locomotion.Row==rows[i],"Movement selects direction sector "+i);
                Check(hero.Presentation.flipX==(i>=3&&i<=5),"Leftward directions mirror the correct view "+i);
                Check(Mathf.Abs(Vector2.Distance(start,hero.transform.position)-5.6f/6)<.002f,"Diagonal movement keeps cardinal speed "+i);
                Check(hero.Presentation.sprite.name.StartsWith("HunterMove_"),"Direction uses actual locomotion atlas "+i);
            }
            Reset(Vector2.right);var framesSeen=new HashSet<int>();
            for(int i=0;i<60;i++){Move(Vector2.right,1);framesSeen.Add(hero.Locomotion.Frame);}
            Check(framesSeen.Count==6,"Travel cycles through six distinct stride poses");
            hero.AdvanceCombat(.02f,Vector2.zero);Check(hero.Locomotion.State==LocomotionState.Settling&&hero.Locomotion.Frame==7,"Release plays stopping pose");
            Move(Vector2.zero,9);Check(hero.Locomotion.State==LocomotionState.Idle&&hero.Locomotion.Frame==0,"Stop returns to directional idle");
            Reset(Vector2.right);Vector2 runStart=hero.transform.position;Move(Vector2.right);float run=Vector2.Distance(runStart,hero.transform.position),runCycle=hero.Locomotion.Cycle;
            Reset(Vector2.right);Vector2 walkStart=hero.transform.position;Move(Vector2.right);float walk=Vector2.Distance(walkStart,hero.transform.position);
            Check(Mathf.Abs(run-walk*2)<.002f,"Shift walk is half the original run speed");
            Check(Mathf.Abs(hero.Locomotion.Cycle-walk/HunterLocomotion.StrideLength)<.001f,"Walk gait tracks travelled distance");
            Reset(Vector2.right);Move(Vector2.right,12);float cycle=hero.Locomotion.Cycle;int frame=hero.Locomotion.Frame;Vector3 pos=hero.transform.position;
            game.State=RunState.Paused;Move(Vector2.right);Check(hero.transform.position==pos&&hero.Locomotion.Cycle==cycle&&hero.Locomotion.Frame==frame,"Pause freezes movement and gait together");game.State=RunState.Combat;
            Dungeon.Obstacles=Dungeon.SolidObstacles=new[]{new Rect(0,-3,2,6)};Reset(Vector2.right);hero.transform.position=new Vector2(-.5f,0);Move(Vector2.right,60);
            Check(hero.transform.position.x<0&&hero.Locomotion.State==LocomotionState.Idle,"Holding movement against a wall stops foot cycling");
            Dungeon.Obstacles=System.Array.Empty<Rect>();Dungeon.SolidObstacles=System.Array.Empty<Rect>();
            Reset(Vector2.right);Move(Vector2.right,10);Check(hero.TrySkill(0),"Skill can interrupt running");Move(Vector2.right,10);
            Check(hero.Presentation.sprite.name.StartsWith("HunterMotion_"),"Skill pose has priority over locomotion");Move(Vector2.right,30);
            Check(!hero.SkillBusy&&hero.Presentation.sprite.name.StartsWith("HunterMove_")&&hero.Locomotion.State==LocomotionState.Moving,"Held movement resumes its gait after recovery");
            Reset(Vector2.right);Move(Vector2.right,10);Check(hero.TryDodge(Vector2.right),"Dodge can interrupt running");Move(Vector2.right,20);
            Check(hero.Presentation.sprite.name.StartsWith("HunterMove_"),"Locomotion resumes after dodge");
            Reset(Vector2.right);Move(Vector2.right,10);Check(hero.TryTag(),"Weapon tag remains available while moving");Move(Vector2.right,5);
            Check(hero.Weapon==1&&hero.Presentation.sprite.name.StartsWith("HunterMove_"),"Both weapons share consistent travel frames");
            hero.Facing=Vector2.left;Move(Vector2.right,5);Check(hero.Facing==Vector2.left&&!hero.Presentation.flipX,"Travel direction does not overwrite gun aim");
            hero.SafeEntry();Check(hero.Locomotion.State==LocomotionState.Idle&&hero.Locomotion.Cycle==0,"Room entry clears stale gait");
            for(int row=0;row<4;row++)
            {
                var pivot=HunterLocomotionAtlas.Frame(row,0).pivot;
                for(int col=0;col<8;col++){HunterLocomotionAtlas.Pose(hero.Presentation,row,col);Check(hero.Presentation.sprite.pivot.y==pivot.y&&Mathf.Abs(hero.Presentation.transform.localScale.x-HunterLocomotionAtlas.Scale)<.00001f,"Fixed ground baseline and scale "+row+"/"+col);}
            }
            Vector2[] views={Vector2.down,Vector2.right,new Vector2(1,1).normalized,Vector2.up};
            for(int row=0;row<4;row++)
            {
                Reset(views[row]);game.Crow.ResetAt(hero.transform.position);
                for(int phase=0;phase<8;phase++)
                {
                    if(phase>=1&&phase<=6)hero.AdvanceCombat(phase==1?.02f:HunterLocomotion.StrideLength/6/5.6f,views[row]);
                    if(phase==7)hero.AdvanceCombat(.02f,Vector2.zero);
                    yield return new WaitForEndOfFrame();yield return new WaitForEndOfFrame();
                    var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,$"movement-{row}-{phase}.png"),image.EncodeToPNG());Destroy(image);
                }
            }
            Dungeon.Obstacles=obstacles;Dungeon.SolidObstacles=solids;
            File.WriteAllText(Path.Combine(output,"movement-checks.txt"),"PASS: "+checks+" movement checks / 8 direction sectors / six-frame gait / walk, wall, pause, combat and entry transitions / 32 screenshots");
            Debug.Log("WNN_MOVEMENT_SMOKE_SUCCESS: "+checks);Application.Quit(0);
        }
    }
}
