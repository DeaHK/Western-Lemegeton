using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WesternLemegeton.Passives;

namespace WesternLemegeton
{
    // Opt-in executable integration checks; never active in an ordinary game run.
    public class RuntimeSmoke:MonoBehaviour
    {
        int checks;string output;
        void Check(bool condition,string name)
        {
            if(!condition){Debug.LogError("SMOKE_FAILED: "+name);Application.Quit(2);throw new System.Exception(name);}
            checks++;Debug.Log("SMOKE_PASS: "+name);
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Logs"));Directory.CreateDirectory(output);
            yield return new WaitForSeconds(.5f);var g=Dungeon.I;
            Check(g.State==RunState.Title&&!g.Cam.orthographic,"Title and perspective camera initialize");Capture("title.png");yield return new WaitForEndOfFrame();
            g.EnterTown();yield return new WaitForSeconds(.3f);Capture("town.png");yield return new WaitForEndOfFrame();
            Check(g.IsTown&&g.Enemies.Count==0,"Hirva remains peaceful");g.Hero.transform.position=new Vector2(13,0);
            Check(g.TryTravel()&&g.State==RunState.Route,"Town gate opens departure route");Check(g.ChooseRoom(0),"Select first segment");yield return null;
            yield return EntranceChecks(g);
            g.OpenRoute(true);Check(!g.RouteTravel&&!g.ChooseRoom(1),"Map preview cannot unlock next segment early");g.CloseRoute();
            g.Hero.transform.position=Vector2.zero;yield return new WaitForSeconds(.3f);Capture("combat.png");yield return new WaitForEndOfFrame();
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--visual-only")>=0)
            {yield return new WaitForSeconds(.6f);Capture("combat.png");yield return new WaitForEndOfFrame();Debug.Log("WNN_VISUAL_SMOKE_SUCCESS");Application.Quit(0);yield break;}
            yield return DeathChecks(g);
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--cinematic-only")>=0)
            {
                foreach(var enemy in g.Enemies.ToArray())if(enemy)Destroy(enemy.gameObject);g.Enemies.Clear();
                var bossObject=new GameObject("Cinematic review boss");bossObject.transform.position=g.RoomCenter+new Vector2(1,2);var boss=bossObject.AddComponent<Enemy>();boss.Init(3,4);g.Enemies.Add(boss);
                Check(g.Cinematics.Play(CinematicKind.BossEntrance,boss),"Boss Timeline review starts");yield return BossChecks(g);
                Debug.Log("WNN_CINEMATIC_SMOKE_SUCCESS: "+checks);Application.Quit(0);yield break;
            }
            for(int segment=0;segment<5;segment++)
            {
                Check(g.Room==segment&&g.MapIndex==0&&g.Rooms.Length==6,"Stage starts with six persistent rooms");
                for(int i=0;i<6;i++)Check(g.Rooms[i].Center==RoomGraph.Centers[i],"Room has its own world coordinates");
                yield return ClearCombat(g);
                Travel(g,1);yield return null;yield return ClearCombat(g);
                Travel(g,2);yield return null;
                g.Hero.transform.position=g.RoomCenter;Check(g.TryTravel()&&g.State==RunState.SigilChoice,"Central altar opens sigil tree");
                if(segment==0){Capture("sigil-choice.png");yield return new WaitForEndOfFrame();}
                g.CloseService();Check(!g.Progress.MapComplete,"Closing altar without choice keeps room unfinished");
                Travel(g,4);yield return null;yield return ClearCombat(g);
                Travel(g,5);yield return null;yield return ClearCombat(g);
                var exit=g.CurrentRoom.Doors.Find(d=>d.StageExit);g.Hero.transform.position=(Vector3)exit.Position;
                Check(!g.TryTravel()&&g.State==RunState.Exit&&g.MapsCleared==4,"Exit stays locked when boss room cleared before side rooms");
                g.OpenRoute(true);Check(!g.RouteTravel&&!g.ChooseRoom(segment+1),"Preview cannot bypass incomplete service rooms");g.CloseRoute();
                Travel(g,4);yield return null;Check(g.Enemies.Count==0&&g.Progress.MapComplete,"Revisited combat room never respawns enemies");
                Travel(g,2);yield return null;g.Hero.transform.position=g.RoomCenter;
                Check(g.TryTravel(),"Altar remains available after backtracking");int before=g.Build.Stack((SeongheunType)(segment%3));
                Check(g.ClaimSigil((SeongheunType)(segment%3))&&g.Build.Stack((SeongheunType)(segment%3))==before+1,"Altar choice grants exactly one stack");
                Check(!g.ClaimSigil(SeongheunType.Fire)&&g.MapsCleared==5,"Altar cannot be claimed twice");
                Travel(g,3);yield return new WaitForSeconds(.15f);
                if(segment==0){Capture("card-room.png");yield return new WaitForEndOfFrame();}
                g.Hero.transform.position=g.RoomCenter;Check(g.TryTravel()&&g.State==RunState.CardChoice,"Card stand opens three choices");
                if(segment==0){Capture("devil-cards.png");yield return new WaitForEndOfFrame();}
                var offer=g.PassiveOffers.GetOffer(0);Check(offer&&g.PassiveOffers.OfferCount==3,"Card choices contain database definitions");
                int cards=g.Passives.Count;bool alreadyOwned=g.Passives.TryGetPassive(offer.PassiveID,out var owned);
                int previousStack=alreadyOwned?owned.Stack:0,cardsAfterChoice=cards+(alreadyOwned?0:1);
                float bonus=offer.ValueType==PassiveValueType.Raw?g.PassiveStats.GetRawBonus(offer.StatType):g.PassiveStats.GetPercentBonus(offer.StatType);
                Check(g.ClaimCard(0)&&g.Passives.TryGetPassive(offer.PassiveID,out var claimed)&&claimed.Stack==previousStack+1&&g.Passives.Count==cardsAfterChoice,"Selected card adds or stacks its database passive");
                float updatedBonus=offer.ValueType==PassiveValueType.Raw?g.PassiveStats.GetRawBonus(offer.StatType):g.PassiveStats.GetPercentBonus(offer.StatType);
                Check(Mathf.Approximately(updatedBonus,bonus+offer.Value)&&!g.PassiveOffers.HasOffer,"Card applies one definition value and clears the offer");
                Check(!g.ClaimCard(1)&&g.Passives.Count==cardsAfterChoice&&g.Progress.SegmentComplete,"All six rooms complete, no repeated card claim");
                Check(g.State==RunState.Exit,"Final room completion never automatically opens route popup");
                Travel(g,2);yield return null;Travel(g,4);yield return null;Travel(g,5);yield return null;
                g.Hero.transform.position=g.RoomCenter;g.OpenRoute();Check(!g.RouteTravel,"M map is read-only even after all rooms are cleared");g.CloseRoute();
                exit=g.CurrentRoom.Doors.Find(d=>d.StageExit);Check(!g.TryDoor(exit),"Stage exit requires physical proximity");
                g.Hero.transform.position=(Vector3)exit.Position;Check(g.TryTravel()&&g.State==RunState.Route&&g.RouteTravel,"Exit interaction opens next-stage route after full exploration");
                if(segment==0){Capture("route.png");yield return new WaitForEndOfFrame();}
                if(segment<4)
                {
                    Check(!g.ChooseRoom(segment+2),"Cannot skip locked stage");
                    float hp=g.Hero.Hp;Check(g.ChooseRoom(segment+1),"Choose next unlocked stage");yield return null;
                    Check(g.State==RunState.Cinematic&&g.Cinematics.Kind==CinematicKind.DungeonEntrance,"New stage starts entrance Timeline");g.Cinematics.Skip();
                    Check(g.Passives.Count==cardsAfterChoice&&g.Passives.TryGetPassive(offer.PassiveID,out var retained)&&retained.Stack==previousStack+1&&g.Hero.Hp==hp,"Passives, stacks and health persist across stages");
                }
                else{g.FinishExpedition();Check(g.State==RunState.Victory,"Final victory also requires the exit interaction");}
            }
            Check(completedRooms==20&&completedWaves==50,"Entire run has twenty combat rooms and fifty waves plus ten service rooms");
            g.EnterTown();yield return null;Check(g.Rooms==null&&g.MapIndex==0&&g.MapsCleared==0&&Rules.RoomOrigin==Vector2.zero,"New run resets all room state and world bounds");
            yield return BuildAndComboChecks(g);
            File.WriteAllText(Path.Combine(output,"runtime-checks.txt"),"PASS: "+checks+" runtime integration checks\n30 rooms / 50 combat waves completed\n");
            Debug.Log("WNN_RUNTIME_SMOKE_SUCCESS: "+checks);yield return new WaitForSeconds(.3f);Application.Quit(0);
        }
        IEnumerator WaitForCinematic(Dungeon g)
        {
            float until=Time.realtimeSinceStartup+(float)g.Cinematics.Duration+1;
            while(g.Cinematics.IsPlaying&&Time.realtimeSinceStartup<until)yield return null;
            Check(!g.Cinematics.IsPlaying,"Timeline completes within its duration");
        }
        IEnumerator EntranceChecks(Dungeon g)
        {
            var c=g.Cinematics;
            Check(c.IsPlaying&&g.State==RunState.Cinematic&&!g.Running&&c.Director.playableAsset is UnityEngine.Timeline.TimelineAsset,"Entrance uses real Timeline and suspends combat");
            float hp=g.Hero.Hp;Vector2 pos=g.Pos;int wave=g.WaveIndex;
            g.Hero.Hurt(1000);Check(g.Hero.Hp==hp&&!g.Hero.TryAttack()&&!g.Hero.TryTag()&&!g.Hero.TryDodge(Vector2.right),"Cutscene blocks damage and combat inputs");
            g.OpenRoute(true);Check(g.State==RunState.Cinematic&&!g.TryTravel(),"Cutscene cannot open map or travel through doors");
            yield return new WaitForSecondsRealtime(.65f);Capture("cinematic-entrance.png");yield return new WaitForEndOfFrame();
            Check(c.Letterbox>.8f&&c.TitleOpacity>.4f&&g.Cam.fieldOfView<38,"Timeline drives camera and title presentation");
            c.SetPaused(true);double paused=c.Time;yield return new WaitForSecondsRealtime(.2f);Check(System.Math.Abs(c.Time-paused)<.01,"Timeline pause freezes director time");c.SetPaused(false);
            Time.timeScale=0;double before=c.Time;yield return new WaitForSecondsRealtime(.2f);Time.timeScale=1;Check(c.Time>before+.1,"Timeline runs on unscaled time");
            Check(g.Pos==pos&&g.WaveIndex==wave,"Cutscene leaves gameplay position and progress unchanged");
            yield return WaitForCinematic(g);Check(g.State==RunState.Combat&&Mathf.Abs(g.Cam.fieldOfView-38)<.1&&c.Shade==0,"Entrance restores gameplay camera and controls");
        }
        IEnumerator DeathChecks(Dungeon g)
        {
            foreach(var enemy in g.Enemies)enemy.enabled=false;
            yield return new WaitForSecondsRealtime(1.1f);g.Hero.Hp=1;g.Hero.Hurt(2);
            Check(g.Hero.Hp==0&&g.State==RunState.Cinematic&&g.Cinematics.Kind==CinematicKind.PlayerDeath,"Lethal damage triggers death Timeline before results");
            yield return new WaitForSecondsRealtime(.85f);
            Check(Quaternion.Angle(g.Hero.Presentation.transform.localRotation,Quaternion.identity)>70&&g.Cinematics.Shade==0,"Hunter finishes collapsing before fade begins");
            Capture("cinematic-death.png");yield return new WaitForEndOfFrame();
            // PNG encoding takes wall time. Observe the running Timeline instead of sleeping
            // another fixed 1.6 seconds and potentially sampling after its 2.7 second end.
            bool sawFade=false;float deadline=Time.realtimeSinceStartup+5;
            while(g.Cinematics.IsPlaying&&Time.realtimeSinceStartup<deadline)
            {sawFade|=g.Cinematics.Shade>.8&&g.State==RunState.Cinematic;yield return null;}
            Check(sawFade,"Death fades to black before results");
            yield return WaitForCinematic(g);Check(g.State==RunState.Dead,"Death Timeline ends on results screen");Capture("cinematic-death-results.png");yield return new WaitForEndOfFrame();
            g.EnterTown();g.StartRun();Check(g.Cinematics.IsPlaying,"Fresh run starts entrance again");
            g.EnterTown();yield return new WaitForSecondsRealtime(3);Check(g.State==RunState.Town&&!g.Cinematics.IsPlaying,"Reset cancels Timeline without stale completion callbacks");
            g.StartRun();yield return null;g.Cinematics.Skip();Check(g.State==RunState.Combat,"Skipping entrance resumes combat immediately");
        }
        IEnumerator BossChecks(Dungeon g)
        {
            var c=g.Cinematics;Check(c.IsPlaying&&c.Kind==CinematicKind.BossEntrance,"Final boss spawn triggers its Timeline");
            var boss=g.Enemies.Find(e=>e&&e.Kind==3);Check(boss,"Cinematic has a living boss target");float hp=boss.Hp;var scale=boss.PresentationRoot.localScale;
            boss.TakeDamage(10000,false);Check(boss.Hp==hp&&!g.Hero.TryAttack(),"Boss cannot be damaged during reveal");
            yield return new WaitForSecondsRealtime(1.05f);Capture("cinematic-boss.png");yield return new WaitForEndOfFrame();
            Check(c.TitleOpacity>.8&&g.Cam.fieldOfView<32,"Boss close-up and name appear on Timeline");
            int finished=c.CompletedCount;c.Skip();Check(g.State==RunState.Combat&&!c.IsPlaying&&c.CompletedCount==finished+1,"Boss skip restores combat once");
            c.Skip();Check(c.CompletedCount==finished+1&&Mathf.Abs(g.Cam.fieldOfView-38)<.1,"Repeated skip is safe and camera resets");
            Check(c.Play(CinematicKind.BossEntrance,boss),"Director can replay after skip without stale bindings");yield return WaitForCinematic(g);
            Check(g.State==RunState.Combat&&Mathf.Abs(boss.PresentationRoot.localScale.x-1.9f)<.02,"Natural boss completion restores actor and combat");
        }
        int completedRooms,completedWaves;
        void Travel(Dungeon g,int next)
        {
            var door=g.CurrentRoom.Doors.Find(d=>!d.StageExit&&d.Destination==next);Check(door,"Connected door exists");
            var previous=g.CurrentRoom;g.Hero.transform.position=(Vector3)door.Position;Check(g.TryTravel()&&g.MapIndex==next,"Door interaction moves to connected room");
            Check(g.CurrentRoom!=previous&&Rules.RoomOrigin==g.RoomCenter&&Vector2.Distance(g.Pos,g.RoomCenter)<15,"Room transition updates world origin and entrance");
            Check(!g.Cinematics.IsPlaying,"Local room travel does not replay stage entrance");
            Check(!previous.Layout.Root.gameObject.activeSelf&&g.CurrentRoom.Layout.Root.gameObject.activeSelf,"Only the current room renders while room objects persist");
        }
        IEnumerator ClearCombat(Dungeon g)
        {
            Check(g.CurrentKind==ExplorationRoomKind.Combat&&!g.Progress.MapComplete,"Uncleared combat room entered");
            int total=g.WavesInMap;Check(total>=2&&total<=3,"Combat room has two or three waves");
            if(g.Room==0){yield return new WaitForSeconds(.16f);Capture("map-"+(g.MapIndex+1)+".png");yield return new WaitForEndOfFrame();}
            for(int wave=0;wave<total;wave++)
            {
                if(g.Cinematics.IsPlaying)yield return BossChecks(g);
                Check(g.State==RunState.Combat&&g.WaveIndex==wave&&g.Enemies.Count>0,"Wave has living enemies");
                var door=g.CurrentRoom.Doors[0];g.Hero.transform.position=(Vector3)door.Position;Check(!g.TryDoor(door),"Live combat locks connecting doors");
                foreach(var e in g.Enemies)foreach(var rect in Dungeon.Obstacles)Check(!rect.Contains(e.transform.position),"Enemies spawn outside world-offset scenery");
                Check(g.Enemies.Exists(e=>e&&e.Kind==3)==(g.Room==4&&g.MapIndex==5&&wave==total-1),"Boss appears only at final combat wave");
                foreach(var e in g.Enemies)if(e&&!e.Dead)e.TakeDamage(100000,false);
                yield return null;yield return null;completedWaves++;
                if(wave<total-1)
                {
                    Check(g.State==RunState.WaveBreak&&!g.Progress.MapComplete,"Intermediate wave does not clear room");
                    Check(!g.TryTravel(),"Wave break cannot bypass locked doors");
                    if(g.Room==0&&g.MapIndex==0&&wave==0)
                    {
                        Capture("wave-break.png");yield return new WaitForEndOfFrame();float countdown=g.WaveCountdown;
                        g.OpenRoute();yield return new WaitForSeconds(.2f);Check(g.WaveCountdown==countdown,"Exploration popup pauses countdown");g.CloseRoute();
                    }
                    yield return new WaitForSeconds(2.15f);
                }
            }
            Check(g.Progress.MapComplete&&g.State==RunState.Exit,"Combat clear unlocks doors without opening any popup");completedRooms++;
        }        IEnumerator BuildAndComboChecks(Dungeon g)
        {
            var build=g.Build;var hud=g.GetComponent<GameHUD>();
            build.SetThreshold(SeongheunType.Fire,3);g.ApplySeongheunStack(SeongheunType.Fire,2);
            Check(build.Stack(SeongheunType.Fire)==2&&!build.IsActive(SeongheunType.Fire)&&g.BurnLevel==0,"Below threshold UI stack does not enable damage effect");
            g.ApplySeongheunStack(SeongheunType.Fire,1);
            Check(build.Stack(SeongheunType.Fire)==3&&g.BurnLevel==3&&hud.StackPulse(SeongheunType.Fire)>0,"Stack addition immediately updates effect and HUD punch");
            yield return new WaitForSeconds(.3f);Check(hud.StackPulse(SeongheunType.Fire)==0,"HUD punch ends after quarter second");
            g.ApplySeongheunStack(SeongheunType.Fire,-2);Check(!build.IsActive(SeongheunType.Fire)&&g.BurnLevel==0&&hud.StackPulse(SeongheunType.Fire)==0,"Removing stacks deactivates effect without gain punch");
            g.ApplySeongheunStack(SeongheunType.Fire,2);
            Check(g.Passives.AcquirePassiveById(1,PassiveAcquisitionSource.MonsterDrop).WasAdded,"Drop uses the new passive acquisition entry");
            Check(g.Passives.AcquirePassiveById(4,PassiveAcquisitionSource.ShopPurchase).WasAdded,"Shop uses the new passive acquisition entry");
            Check(g.Passives.AcquirePassiveById(5,PassiveAcquisitionSource.EventReward).WasAdded,"Event uses the new passive acquisition entry");
            var inventory=new List<PassiveInstance>();g.Passives.CopyActivePassives(inventory);
            Check(inventory.Count==3&&inventory[0].PassiveID==1&&inventory[1].PassiveID==4&&inventory[2].PassiveID==5&&inventory[0].InitialSource==PassiveAcquisitionSource.MonsterDrop&&inventory[2].Rarity==g.Passives.Database.GetById(5).Rarity,"Inventory preserves acquisition order, source and definition rarity");
            Check(build.Stack(SeongheunType.Fire)==3,"Passive acquisition never changes stigma stacks");
            Check(!g.Passives.AcquirePassiveById(int.MaxValue,PassiveAcquisitionSource.EventReward).Success&&g.Passives.Count==3,"Unknown ID rejected without mutation");
            Check(hud.PendingPassiveNotices==3,"Rapid acquisitions queue all notices");
            var stacked=g.Passives.AcquirePassiveById(1,PassiveAcquisitionSource.EventReward);
            Check(stacked.WasStacked&&stacked.PreviousStack==1&&stacked.NewStack==2&&g.Passives.Count==3&&stacked.Instance.InitialSource==PassiveAcquisitionSource.MonsterDrop&&stacked.Instance.LastSource==PassiveAcquisitionSource.EventReward&&Mathf.Approximately(g.PassiveStats.GetPercentBonus(WesternPassiveStatKeys.PlayerAttack),30)&&hud.PendingPassiveNotices==4,"Duplicate acquisition updates one stack, modifier and notification");
            yield return new WaitForSeconds(.12f);Capture("build-passives.png");yield return new WaitForSeconds(.2f);
            hud.OpenBuildTree();Check(g.State==RunState.Paused,"Build tree pauses play");yield return new WaitForSeconds(.1f);Capture("build-tree.png");yield return new WaitForEndOfFrame();hud.CloseBuildTree();
            build.SetThreshold(SeongheunType.Fire,1);g.StartRun();yield return null;g.Cinematics.Skip();
            Check(g.Passives.Count==0&&!g.PassiveOffers.HasOffer&&g.PassiveStats.GetPercentBonus(WesternPassiveStatKeys.PlayerAttack)==0&&g.PassiveStats.GetRawBonus(WesternPassiveStatKeys.MoveSpeed)==0&&g.PassiveStats.GetRawBonus(WesternPassiveStatKeys.RavenAttack)==0&&build.Stack(SeongheunType.Fire)==0&&hud.PendingPassiveNotices==0,"New run clears passives, modifiers, offers, stigma and notification queue");
            var foes=g.Enemies.ToArray();g.Hero.transform.position=Vector2.zero;g.Crow.ResetAt(Vector2.zero);g.Crow.transform.position=new Vector2(-3,0);
            for(int i=0;i<foes.Length;i++){foes[i].enabled=false;foes[i].Hp=foes[i].MaxHp=500;foes[i].transform.position=new Vector2(i+1,0);}
            for(int i=1;i<=4;i++)g.Hero.Hit(foes[0],1,i,900+i);
            Check(g.Crow.AttackTarget==foes[0]&&g.Crow.CurrentState==RavenState.ComboWindup,"Four collisions designate target and begin windup");
            Check(foes[0].Hp==496,"Combo does not apply instantaneous dash damage");
            yield return new WaitForSeconds(.23f);Capture("crow-dash.png");yield return new WaitForEndOfFrame();
            Check(g.Crow.ComboDashStarts>0&&g.Crow.ComboBusy,"Crow enters visible timed dash");
            Vector2 crowAt=g.Crow.transform.position;g.BeforePause=g.State;g.State=RunState.Paused;yield return new WaitForSeconds(.2f);
            Check((Vector2)g.Crow.transform.position==crowAt,"Pause freezes combo dash");g.State=RunState.Combat;
            yield return new WaitForSeconds(.9f);
            Check(g.Crow.LastComboHits==2&&foes[0].Hp==478&&foes[1].Hp==482&&foes[2].Hp==500,"Dash damages exactly two distinct targets at arrival");
            Check(g.Crow.ComboEffects>0&&!g.Crow.ComboBusy,"Dash emits dedicated effects and finishes");
            foes[0].Hp=1;
            for(int i=1;i<=4;i++)g.Hero.Hit(foes[0],i==4?2:0,i,1000+i);
            Check(g.Crow.AttackTarget==foes[1],"Lethal fourth hit selects nearest living replacement inside player range");
            yield return new WaitForSeconds(.95f);
            Check(g.Crow.LastComboHits==2,"Replacement chain also respects two-target cap");
            foreach(var e in foes)if(e)e.transform.position=new Vector2(14,8);
            g.Crow.ResetAt(g.Pos);int dashes=g.Crow.ComboDashStarts,effects=g.Crow.ComboEffects;
            g.Crow.ComboFollowup(null);
            Check(g.Crow.ComboFlash>0&&!g.Crow.ComboBusy&&GameObject.Find("Crow combo indicator"),"No target still spawns PNG indicator");
            yield return new WaitForSeconds(.35f);
            Check(g.Crow.ComboDashStarts==dashes&&g.Crow.ComboEffects==effects,"No target produces no dash or dedicated effects");
            g.EnterTown();yield return null;
        }        void Capture(string filename){StartCoroutine(SaveFrame(filename));}
        IEnumerator SaveFrame(string filename)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            if(texture){File.WriteAllBytes(Path.Combine(output,filename),texture.EncodeToPNG());Destroy(texture);}
            else Debug.LogError("Frame capture failed: "+filename);
        }
    }
}






