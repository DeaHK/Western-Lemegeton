using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WesternLemegeton.EditorTools
{
    public static class LoganChecks
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception("LOGAN_FAILED: "+message); }
        [MenuItem("Western Lemegeton/Tests/Check Logan")]
        public static void Run()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Stop Play before checking.");
            var previous=SceneManager.GetActiveScene(); var previousGame=Dungeon.I;
            var obstacles=Dungeon.Obstacles; var origin=Rules.RoomOrigin; var random=UnityEngine.Random.state;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var game=new GameObject("Logan check").AddComponent<Dungeon>(); Dungeon.I=game;
                game.State=RunState.Combat; Rules.RoomOrigin=Vector2.zero; Dungeon.Obstacles=Array.Empty<Rect>();
                game.Hero=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Player>("Assets/Editable/Actors/Hunter.prefab"));
                game.Cam=new GameObject("Camera").AddComponent<Camera>(); PaperWorld.Configure(game.Cam);
                var enemy=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/Logan.prefab"));
                enemy.Init(3,9); enemy.transform.position=Vector2.zero;
                var attack=enemy.GetComponent<LoganBossAttack>();
                Check(enemy.Hp==350 && enemy.MaxHp==350,"Health remains 350 in later rooms");
                game.Hero.transform.position=Vector2.right*5;
                var movement=enemy.GetComponent<EnemyKeepDistanceMovement>();
                Check(movement && !movement.UseAttackRange && movement.ApproachRange==4 && movement.ApproachRangeFactor==1,
                    "Movement stop distance is independent from skill ranges");
                enemy.GetComponent<EnemyBasicBrain>().Tick(game,1,.1f);
                Check(enemy.transform.position.x>0,"Phase-one aim permits chase movement");
                enemy.transform.position=Vector2.zero;
                movement.ApproachRange=6;
                enemy.GetComponent<EnemyBasicBrain>().Tick(game,1,.1f);
                Check(enemy.transform.position.x==0 && attack.ActivePattern==LoganBossAttack.Pattern.Barrage,
                    "Independent stop range stops movement without cancelling in-range attack");
                movement.ApproachRange=4;
                Check(attack.ActivePattern==LoganBossAttack.Pattern.Barrage,"Barrage has priority when ready");
                Check(attack.Tick(game,1,1),"Fire transition blocks chase");
                Check(attack.Action==LoganBossAttack.ActionState.Fire,"One-second aim starts fire");
                Vector3 target=attack.LockedAim;
                game.Hero.transform.position=Vector2.up*5;
                attack.Tick(game,1,.5f);
                Check(attack.LockedAim==target,"Fire retains the locked aim point");
                attack.Tick(game,1,2.5f);
                Check(attack.Action==LoganBossAttack.ActionState.Recovery && attack.BarrageCooldownRemaining==10,"Barrage enters recovery and starts cooldown");
                Check(!attack.SignalMachineGun() && !attack.SignalBarrage(),"Recovery blocks external pattern signals");
                Check(attack.Tick(game,1,.25f) && attack.Action==LoganBossAttack.ActionState.Recovery,"Recovery blocks movement before it expires");
                Check(Mathf.Abs(attack.BarrageCooldownRemaining-9.75f)<.001f,"Cooldown continues during recovery");
                attack.Tick(game,1,attack.RecoveryRemaining);
                Check(attack.ActivePattern==LoganBossAttack.Pattern.None,"Recovery releases current pattern");
                attack.Tick(game,1,0);
                Check(attack.ActivePattern==LoganBossAttack.Pattern.MachineGun,"Machine gun fills barrage cooldown");
                typeof(LoganBossAttack).GetField("barrageCooldown",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(attack,0f);
                int beforeCancel=UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length;
                attack.Tick(game,1,0);
                Check(attack.ActivePattern==LoganBossAttack.Pattern.Barrage && attack.Action==LoganBossAttack.ActionState.Aim,"Ready skill cancels basic aim and starts a fresh aim");
                Check(UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length==beforeCancel,"Cancelled basic emits no bullet");
                Check(!attack.SignalBarrage() && !attack.SignalMachineGun(),"Active skill cannot be restarted or replaced");
                attack.Cancel();
                attack.Tick(game,1,0);
                attack.Tick(game,1,1);
                typeof(LoganBossAttack).GetField("barrageCooldown",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(attack,0f);
                Check(!attack.SignalBarrage(),"External signal cannot interrupt machine-gun fire");
                attack.Tick(game,1,attack.MachineGun.Duration);
                Check(attack.Action==LoganBossAttack.ActionState.Recovery,"Machine gun has recovery");
                attack.Tick(game,1,attack.RecoveryRemaining);
                attack.Tick(game,1,0);
                Check(attack.ActivePattern==LoganBossAttack.Pattern.Barrage,"Ready skill starts after basic attack finishes");
                attack.Cancel(); attack.EnableBarrage=false; attack.EnableMachineGun=false;
                Check(!attack.Tick(game,1,0),"Disabled patterns permit chase without firing");
                Dungeon.Obstacles=new[]{new Rect(-2,-1,.3f,2)};
                Vector2 destination=attack.FindLeapTarget(Vector2.right);
                Check(Vector2.Distance(destination,Vector2.zero)<=attack.Barrage.LeapDistance+.001f,"Leap stays within range");
                for(int i=1;i<=100;i++) Check(!HauntedBullAttack.Blocked(Vector2.Lerp(Vector2.zero,destination,i/100f),attack.BodyRadius),"Leap path avoids wall");
                enemy.Hp=150; attack.Tick(game,1,0);
                Check(game.State==RunState.Cinematic && attack.Action==LoganBossAttack.ActionState.Transition,"Threshold freezes combat for transition");
                attack.SkipTransition();
                Check(game.State==RunState.Combat && attack.Phase==2,"Skip restores combat and enters phase two");
                Dungeon.Obstacles=Array.Empty<Rect>();
                Check(Mathf.Abs(attack.SkillMovementMultiplier-1.2f)<.001f,"Phase two accelerates basic movement");
                game.Hero.transform.position=Vector2.right*2;
                attack.Tick(game,1,0);
                Check(attack.ActivePattern==LoganBossAttack.Pattern.Whip,"Whip wins close-range priority");
                game.Hero.transform.position=Vector2.right*12;
                float stop=movement.ApproachRange;
                attack.Tick(game,1,1);
                Check(attack.Action==LoganBossAttack.ActionState.Active && Mathf.Abs(attack.SkillMovementMultiplier-1.8f)<.001f,
                    "Whip uses negative slow to accelerate");
                Check(movement.ApproachRange==0 && movement.RetreatDistance==0 && !movement.UseAttackRange,"Active whip can chase into melee range");
                attack.Cancel();
                Check(movement.ApproachRange==stop && movement.ApproachRangeFactor==1 && !movement.UseAttackRange,"Whip restores independent movement settings");
                attack.Tick(game,1,0);
                Check(attack.ActivePattern==LoganBossAttack.Pattern.Dynamite,"Dynamite wins when whip is unavailable");
                attack.Cancel(); attack.EnableDynamite=false; attack.EnableWhip=false;
                game.Hero.transform.position=Vector2.right*6;
                attack.Tick(game,1,0);
                Check(attack.ActivePattern==LoganBossAttack.Pattern.Shotgun,"Shotgun fills skill cooldowns inside range eight");
                int bullets=UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length;
                attack.Tick(game,1,1);
                Check(UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length==bullets+7,"Shotgun emits seven pellets at once");
                Check(attack.Action==LoganBossAttack.ActionState.Recovery && !attack.SignalWhip(),"Shotgun waits in recovery after pellets");
                attack.Tick(game,1,attack.RecoveryRemaining);
                attack.Tick(game,1,0);
                attack.EnableWhip=true; game.Hero.transform.position=Vector2.right;
                typeof(LoganBossAttack).GetField("whipCooldown",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(attack,0f);
                beforeCancel=UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length;
                attack.Tick(game,1,0);
                Check(attack.ActivePattern==LoganBossAttack.Pattern.Whip && attack.Action==LoganBossAttack.ActionState.Windup,"Ready whip cancels shotgun aim");
                Check(UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length==beforeCancel,"Cancelled shotgun emits no pellets");
                Check(!attack.SignalShotgun() && !attack.SignalWhip(),"Active whip cannot be replaced or restarted");
                attack.Cancel(); game.Hero.transform.position=Vector2.right*20;
                game.Enemies.Add(enemy); enemy.Hp=150;
                var dynamite=new LoganBossAttack.DynamiteSettings { ScatterRadius=0, FuseSeconds=3 };
                var volley=LoganDynamiteVolley.Spawn(Vector2.left,Vector2.zero,dynamite);
                Check(volley.BombCount==3,"Default volley contains three bombs");
                volley.Advance(game,.2f);
                Check(enemy.Hp==150,"Landed dynamite waits for fuse");
                volley.Advance(game,3);
                Check(enemy.Hp==60,"Three overlapping explosions damage their caster only once");
                enemy.transform.position=Vector2.zero;
                var danger=LoganDynamiteVolley.Spawn(Vector2.left,Vector2.zero,dynamite);
                Check(danger.Threatens(Vector2.zero,0),"Airborne dynamite exposes its landing danger area");
                var patternBefore=attack.ActivePattern;
                attack.Tick(game,1,.2f);
                Check(attack.IsDodgingDynamite && attack.Action==LoganBossAttack.ActionState.Dodge,"Logan leaps out of dynamite danger");
                Check(!danger.Threatens(attack.DodgeTarget,attack.BodyRadius+attack.DodgeSafetyMargin),"Dodge destination clears every overlapping bomb");
                Check(!attack.SignalShotgun() && !attack.SignalWhip(),"Dodge rejects new attack signals");
                attack.Tick(game,1,.2f);
                Check(!attack.IsDodgingDynamite && attack.ActivePattern==patternBefore && !danger.Threatens(enemy.transform.position,0),"Dodge lands safely and retains original pattern");
                RobeManPoisonZone.Remove(danger.gameObject);
                Debug.Log("LOGAN_CHECKS_PASSED");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene,true); SceneManager.SetActiveScene(previous);
                Dungeon.I=previousGame; Dungeon.Obstacles=obstacles; Rules.RoomOrigin=origin; UnityEngine.Random.state=random;
            }
        }
    }
}
