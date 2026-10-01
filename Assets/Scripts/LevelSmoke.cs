using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace WesternLemegeton
{
    // Opt-in visual review also walks every doorway using the real player collision solver.
    public sealed class LevelSmoke:MonoBehaviour
    {
        Dungeon game;int checks;string output;
        void Check(bool ok,string label){if(!ok){Debug.LogError("LEVEL_FAILED: "+label);Application.Quit(2);throw new System.Exception(label);}checks++;Debug.Log("LEVEL_PASS: "+label);}
        IEnumerator Start()
        {
            Application.runInBackground=true;output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Logs"));
            yield return new WaitForSeconds(.4f);game=Dungeon.I;game.StartRun();yield return null;game.Cinematics.Skip();game.Hero.enabled=false;game.Crow.enabled=false;
            int[] itinerary={0,1,2,3,2,4,5};
            for(int visit=0;visit<itinerary.Length;visit++)
            {
                int index=itinerary[visit];Check(game.MapIndex==index,"Review reached room "+index);game.State=RunState.Exit;
                foreach(var e in game.Enemies)if(e)Destroy(e.gameObject);game.Enemies.Clear();
                WalkTo(Vector2.zero);game.Hero.AdvanceCombat(.2f,Vector2.zero);
                yield return Capture("level-room-"+index);
                foreach(var door in game.CurrentRoom.Doors)
                {
                    game.Hero.transform.position=game.RoomCenter;
                    WalkTo(door.LocalPosition);
                    Check(Vector2.Distance(game.Pos,(Vector3)door.Position)<.55f,"Walk reaches door "+index+" -> "+door.Destination);
                    game.Crow.ResetAt(game.Pos);game.Hero.AdvanceCombat(.2f,Vector2.zero);
                    yield return Capture("level-gate-"+index+"-"+door.Destination);
                }
                if(game.CurrentKind==ExplorationRoomKind.Combat){while(!game.Progress.MapComplete)game.Progress.CompleteWave();}
                else game.Progress.CompleteService();
                if(visit+1<itinerary.Length)
                {
                    var door=game.CurrentRoom.Doors.Find(d=>d.Destination==itinerary[visit+1]);game.Hero.transform.position=(Vector3)door.Position;
                    Check(game.TryDoor(door),"Completed room permits connected travel");yield return null;
                }
            }
            var exit=game.CurrentRoom.Doors.Find(d=>d.StageExit);game.Hero.transform.position=(Vector3)exit.Position;
            Check(game.Progress.SegmentComplete&&game.TryDoor(exit)&&game.State==RunState.Route,"Six rooms still require exit interaction for route popup");
            File.WriteAllText(Path.Combine(output,"level-checks.txt"),"PASS: "+checks+" layout checks; collision-driven walks to every door; all six rooms and final route interaction.");
            Debug.Log("WNN_LEVEL_SMOKE_SUCCESS: "+checks);Application.Quit(0);
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForSeconds(.35f);yield return new WaitForEndOfFrame();
            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image);
        }
        void WalkTo(Vector2 target)
        {
            const int w=57,h=33;const float step=.5f;
            Vector2 At(int x,int y)=>new Vector2(-14+x*step,-8+y*step);
            bool[,] blocked=new bool[w,h];int[,] previous=new int[w,h];var queue=new Queue<int>();
            for(int x=0;x<w;x++)for(int y=0;y<h;y++)
            {
                previous[x,y]=-2;var p=At(x,y)+game.RoomCenter;
                foreach(var r in Dungeon.Obstacles)if(Rect.MinMaxRect(r.xMin-.65f,r.yMin-.65f,r.xMax+.65f,r.yMax+.65f).Contains(p)){blocked[x,y]=true;break;}
            }
            int sx=Mathf.Clamp(Mathf.RoundToInt((game.LocalPos.x+14)/step),0,w-1),sy=Mathf.Clamp(Mathf.RoundToInt((game.LocalPos.y+8)/step),0,h-1);
            previous[sx,sy]=-1;queue.Enqueue(sy*w+sx);int end=-1;
            var dirs=new[]{Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down};
            while(queue.Count>0)
            {
                int id=queue.Dequeue(),x=id%w,y=id/w;if(Vector2.Distance(At(x,y),target)<.36f){end=id;break;}
                foreach(var d in dirs){int nx=x+d.x,ny=y+d.y;if(nx<0||ny<0||nx>=w||ny>=h||blocked[nx,ny]||previous[nx,ny]!=-2)continue;previous[nx,ny]=id;queue.Enqueue(ny*w+nx);}
            }
            Check(end>=0,"Wide path exists from entry to "+target);
            var path=new List<Vector2>();for(int id=end;id>=0;id=previous[id%w,id/w])path.Add(At(id%w,id/w)+game.RoomCenter);path.Reverse();
            foreach(var point in path)
            {
                for(int i=0;i<40&&Vector2.Distance(game.Pos,point)>.025f;i++)
                {var delta=point-game.Pos;float dt=Mathf.Min(1f/60,delta.magnitude/5.6f);game.Hero.AdvanceCombat(dt,delta.normalized);}
                Check(Vector2.Distance(game.Pos,point)<.08f,"Player physically traverses route waypoint");
            }
        }
    }
}
