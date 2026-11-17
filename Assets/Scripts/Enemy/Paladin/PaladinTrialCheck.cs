#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace WesternLemegeton
{
    // Run on the Main scene in Play Mode; the batch entry uses the same path.
    public sealed class PaladinTrialCheck : MonoBehaviour
    {
        static bool Batch => Array.IndexOf(Environment.GetCommandLineArgs(),"-checkPaladin") >= 0;
        [MenuItem("Western Lemegeton/Tests/Run Paladin Trial (Play Mode)")]
        public static void Run()
        {
            if (!Application.isPlaying || !Dungeon.I)
                throw new InvalidOperationException("Open Main and enter Play Mode to run the paladin trial.");
            Dungeon.I.gameObject.AddComponent<PaladinTrialCheck>();
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachBatch() { if (Batch && Dungeon.I) Run(); }
        bool failed, done;
        float started;
        int assertions;
        void OnEnable()
        {
            started=Time.realtimeSinceStartup;
            Application.logMessageReceived+=OnLog;
        }
        void OnDisable() { Application.logMessageReceived-=OnLog; Time.timeScale=1; }
        void OnLog(string message,string trace,LogType type)
        {
            // A fresh, headless editor can race its Search index at startup, outside the game scene.
            if (trace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")) return;
            if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert) failed=true;
        }
        void Update()
        {
            if (done || (!failed && Time.realtimeSinceStartup-started<235)) return;
            done=true; Time.timeScale=1;
            Debug.LogError("PALADIN_TRIAL_FAILED: exception or timeout");
            if (Batch) EditorApplication.Exit(1);
        }
        void Check(bool ok,string message)
        {
            if (!ok) throw new InvalidOperationException("PALADIN_TRIAL_FAILED: "+message);
            assertions++;
            Debug.Log("PALADIN_CHECK: "+message);
        }

        IEnumerator Start()
        {
            var game=Dungeon.I;
            if (Batch && game.State == RunState.Title)
            {
                yield return null;
                Check(game.Scene.UI.TitlePanel.activeInHierarchy, "title artwork visible");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    Capture(game, "Trial-Title");
            }
            game.StartRun();
            yield return null;
            yield return CheckTrainingStations(game);
            game.EnterTown();
            var west=GameObject.Find("West Gate - Boss Trials");
            Check(west && west.transform.position.x<game.RoomCenter.x,"west portal");
            game.Hero.transform.position=Rules.OutsideCover((Vector2)west.transform.position+Vector2.right*1.45f);
            yield return new WaitForSeconds(.5f);
            if (Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(game,"Paladin-WestGate");
            game.Hero.transform.position=west.transform.position;
            Check(game.TryTravel() && game.InBossTrial,"town to gallery");
            Check(game.TrialBossName=="심판관","default portrait");
            yield return new WaitForSeconds(.45f);
            if (Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(game,"Paladin-Gallery");
            yield return CheckTrainingStations(game);
            game.Hero.transform.position = game.RoomCenter + new Vector2(-13,1);
            game.Crow.ResetAt(game.Pos);
            yield return new WaitForSeconds(.75f);
            if (Batch && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(game, "Trial-Seals");
            game.CycleTrialBoss(-1);
            Check(game.TrialBossName=="Logan","previous registered boss");
            game.Hero.transform.position=game.RoomCenter+Vector2.up*6.8f;
            Check(game.TryTravel() && game.TrialBoss.GetComponent<LoganBossAttack>(),"selected Logan enters arena");
            Check(Array.Exists(game.GetComponents<AudioSource>(),s=>s.loop && s.clip && s.clip.name=="SanctuaryHymn"),
                "Logan keeps the existing sanctuary music");
            Check(game.Hero.TrainingDamageProtection && game.Passives.Count == game.PassiveOffers.Seals.Count &&
                game.Wallet.Copper > 0, "training loadout carries into combat");
            game.Hero.BonusHp.SetSource("trial-check",15);
            game.Hero.BonusHp.Grant("trial-check",15);
            float protectedHp = game.Hero.Hp;
            game.Hero.Hurt(25,2);
            game.Hero.HurtOverTime(7,0);
            Check(game.Hero.Hp == protectedHp && game.Hero.BonusHp.Current == 15 && game.Hero.Stunned,
                "zero direct and periodic damage preserves shield and applies stun");
            game.Hero.ApplyChargeKnockback(Vector2.right,2);
            Check(game.Hero.KnockedBack, "protected hunter can be knocked back");
            game.Hero.transform.position=game.RoomCenter+Vector2.down*7;
            Check(game.TryTravel(),"leave arena");
            var switchPlate = GameObject.Find("Training stations/무적 켜짐");
            game.Hero.transform.position = switchPlate.transform.position;
            Check(game.UseTrainingStation() && !game.Hero.TrainingDamageProtection, "second press disables damage protection");
            game.Hero.HurtOverTime(7,0);
            Check(game.Hero.Hp < game.Hero.MaxHealth,"taking damage resumes");
            // Exercise the base encounter at its normal balance after checking the debug loadout.
            game.Passives.ResetRun();
            game.Stigmas.ResetRun();
            game.CycleTrialBoss(1);
            game.StartTrialBattle();
            var knight=(PaladinEncounter)game.TrialBoss;
            var rig=knight.GetComponentInChildren<PaladinRig>();
            Vector2 court=PaladinArea.ArenaHalfSize;
            Check(court==new Vector2(22.5f,13.5f) && Rules.ClampToRoom(game.RoomCenter+court*2,0)==game.RoomCenter+court &&
                Mathf.Approximately(PaladinArea.ToWall(game.RoomCenter,Vector2.left,game.RoomCenter),court.x),
                "expanded court shares movement and projectile boundaries");
            var sw=game.Cam.WorldToViewportPoint(PaperWorld.Point(game.RoomCenter-court));
            var nw=game.Cam.WorldToViewportPoint(PaperWorld.Point(game.RoomCenter+new Vector2(-court.x,court.y)));
            Check(game.Cam.orthographic && Mathf.Abs(sw.x-nw.x)<.001f,"arena sidelines project parallel");
            Check(knight.BaseInterval==3 && knight.EnhancedInterval==2 && knight.FinalInterval==1.5f,
                "phase intervals accelerate from three to two to one and a half seconds");
            Vector2 chaseStart=knight.transform.position;
            float chaseAt=Time.time;
            yield return new WaitForSeconds(.4f);
            float chaseRate=Vector2.Distance(chaseStart,knight.transform.position)/(Time.time-chaseAt);
            Check(Mathf.Abs(chaseRate-PaladinEncounter.ChaseSpeed)<.65f && chaseRate<5.6f,
                "ground pursuit closes distance at 3.5 units per second below hunter walking speed");
            Check(Mathf.Abs(rig.Height/1.9f-1.75f)<.01f,"standing scale");
            game.Hero.transform.position=(Vector2)knight.transform.position+Vector2.left*2.2f;
            game.Crow.ResetAt(game.Pos);
            yield return new WaitForSeconds(.60f);
            if (Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(game,"Paladin-Scale");
            var ui=game.Scene.UI;
            Check(ui.Element<RectTransform>("Boss").gameObject.activeInHierarchy,"boss HUD visible");
            var label = ui.Element<UnityEngine.UI.Text>("BossHPText");
            Check(label.text == "심판관" && label.GetComponent<UnityEngine.UI.Outline>() &&
                label.rectTransform.anchorMin.y == 1 && label.transform.parent.Find("Engraved brass bezel"),
                "outlined boss name above illustrated frame");
            var music=Array.Find(game.GetComponents<AudioSource>(),source=>source.loop && source.clip);
            var intro=Array.Find(game.GetComponents<AudioSource>(),s=>!s.loop && s.clip && s.clip.name=="ReversalIntro");
            Check(music && music.clip.name=="ReversalLoop" && music.clip.samples==4468364 && music.clip.frequency==48000 &&
                intro && intro.clip.samples==574865 && intro.clip.frequency==48000 && music.clip.channels==2 &&
                Mathf.Approximately(music.volume,.16f) && Mathf.Approximately(intro.volume,music.volume),
                "Judicator schedules a stereo Reversal intro and 64-bar loop at the original channel volume");
            foreach(BossTrialAudio.Cue cue in Enum.GetValues(typeof(BossTrialAudio.Cue)))
                Check(Resources.Load<AudioClip>("Paladin/Audio/"+cue),"sound cue "+cue);
            game.State=RunState.Paused;
            Vector3 saved=knight.transform.position;
            yield return new WaitForSecondsRealtime(.15f);
            Check(knight.transform.position==saved,"pause");
            game.State=RunState.Combat;
            var sector=PaladinArea.Band(Vector2.zero,0,7.4f,PaladinArea.Turn(Vector2.right,45),90);
            Check(sector.Contains(new Vector2(1,1)) && !sector.Contains(Vector2.down),"left sector");
            var annulus=PaladinArea.Band(Vector2.zero,4.125f,8.25f);
            Check(!annulus.Contains(Vector2.right*4) && annulus.Contains(Vector2.right*8) && !annulus.Contains(Vector2.right*8.3f),"ring boundaries");
            Check(PaladinArea.WaveSwept(new Vector2(1,0),Vector2.zero,Vector2.right,2,1.2f) &&
                !PaladinArea.WaveSwept(new Vector2(1,1),Vector2.zero,Vector2.right,2,1.2f),"swept crescent wave");
            var fringe=new Vector2(1,.65f);
            Check(!PaladinArea.WaveSwept(fringe,Vector2.zero,Vector2.right,2,1.15f) &&
                PaladinArea.WaveSwept(fringe,Vector2.zero,Vector2.right,2,1.15f,1.25f),"larger eclipse wave hits its new fringe");

            knight.StopAllCoroutines();
            Time.timeScale=3;
            var torso=CombatBody.For(knight);
            float initialHp=knight.Hp;
            Bullet.SpawnWorld(torso.Center-Vector3.right*2.5f,Vector3.right*18,17,false,.6f);
            yield return new WaitForSeconds(.55f);
            Check(knight.Hp<initialHp,"player projectile hits visible torso");
            Check(Mathf.Approximately(ui.Element<UnityEngine.UI.Image>("BossHP").fillAmount, knight.Hp / knight.MaxHp),
                "illustrated health bar tracks damage");
            game.Hero.TrainingDamageProtection = true;
            game.Hero.transform.position=(Vector2)knight.transform.position+Vector2.right*1.8f;
            float protectedGrabHp=game.Hero.Hp;
            var protectedGrab=knight.StartCoroutine(knight.Attack(PaladinEncounter.Pattern.Grab));
            yield return new WaitForSeconds(.68f);
            Check(game.Hero.Stunned && game.Hero.KnockbackHeight>0 && game.Hero.Hp==protectedGrabHp,
                "damage-only mode retains actual boss grab and lift");
            yield return protectedGrab;
            yield return new WaitForSeconds(.5f);
            Check(game.Hero.Hp==protectedGrabHp && Vector2.Distance(knight.transform.position,game.Pos)>4,
                "damage-only mode retains actual boss throw");
            game.Hero.TrainingDamageProtection = false;
            game.Hero.transform.position=(Vector2)knight.transform.position+Vector2.right*1.8f;
            yield return new WaitForSeconds(.65f);
            float beforeGrab=game.Hero.Hp;
            var grab=knight.StartCoroutine(knight.Attack(PaladinEncounter.Pattern.Grab));
            yield return new WaitForSeconds(.68f);
            Check(game.Hero.Stunned && game.Hero.KnockbackHeight>0 && game.Hero.Hp<beforeGrab,"grab hit, lift and movement lock");
            yield return grab;
            yield return new WaitForSeconds(.45f);
            Check(Mathf.Approximately(game.Hero.Hp,beforeGrab-2*knight.NormalDamage) &&
                Vector2.Distance(knight.transform.position,game.Pos)>4,"throw damage and knockback");
            game.Hero.DebugInvincibility=DebugInvincibilityMode.HealthOnly;
            foreach (PaladinEncounter.Pattern pattern in Enum.GetValues(typeof(PaladinEncounter.Pattern)))
            {
                game.Hero.transform.position=game.RoomCenter+Vector2.down*2;
                Time.timeScale=1;
                float start=Time.time;
                var routine=knight.StartCoroutine(knight.Attack(pattern));
                if (pattern==PaladinEncounter.Pattern.Light || pattern==PaladinEncounter.Pattern.Left ||
                    pattern==PaladinEncounter.Pattern.Right || pattern==PaladinEncounter.Pattern.Spin)
                {
                    yield return null;
                    var red=WarningRenderer();
                    Check(red,"pattern displays a red footprint: "+pattern);
                    if (pattern==PaladinEncounter.Pattern.Light)
                    {
                        while (red && red.gameObject.activeSelf) yield return null;
                        Check(Mathf.Abs(Time.time-start-.5f)<.13f,"light charge lasts half a second");
                    }
                    else if (pattern==PaladinEncounter.Pattern.Spin)
                    {
                        Check(Mathf.Approximately(red.transform.localScale.x,8.25f),"first spin radius is 4.125");
                        while (red && red.gameObject.activeSelf) yield return null;
                        while (!(red=WarningRenderer())) yield return null;
                        Check(Mathf.Approximately(red.transform.localScale.x,16.5f),"outer spin reaches radius 8.25");
                    }
                    else Check(Mathf.Approximately(red.transform.localScale.x,14.8f),"side slash radius doubled: "+pattern);
                }
                if (pattern==PaladinEncounter.Pattern.Swords)
                {
                    yield return new WaitForSeconds(.65f);
                    var sword=GameObject.Find("Sword of light").transform;
                    Vector3 before=sword.position;
                    float angle=sword.eulerAngles.z;
                    game.Hero.transform.position=game.RoomCenter+new Vector2(5,-4);
                    while (!WarningRenderer()) yield return null;
                    var red=WarningRenderer();
                    float lockedAt=Time.time;
                    Vector2 locked=game.Pos;
                    Check(lockedAt-start>=1.18f && Vector3.Distance(sword.position,before)>2 &&
                        Mathf.Abs(Mathf.DeltaAngle(angle,sword.eulerAngles.z))>5,"summoned blade turns and tracks for 1.2 seconds");
                    game.Hero.transform.position=game.RoomCenter+Vector2.left*7;
                    var card=sword.GetComponent<PaperCard>();
                    Vector3 hover=card.RenderOffset;
                    while (card.RenderOffset==hover) yield return null;
                    float launch=Time.time;
                    Check(launch-lockedAt>=.47f && launch-lockedAt<.65f &&
                        Vector2.Distance(new Vector2(red.transform.position.x,red.transform.position.z),locked)<.01f,
                        "sword locks its location and waits half a second");
                    while (red && red.gameObject.activeSelf) yield return null;
                    Check(Time.time-launch<.15f && Vector2.Distance(sword.position,locked)<.01f,"fast sword lands at locked point");
                }
                if (pattern==PaladinEncounter.Pattern.Rain)
                {
                    MeshRenderer red;
                    while (!(red=WarningRenderer())) yield return null;
                    float warned=Time.time;
                    Check(Mathf.Approximately(red.transform.localScale.x,4.05f),"rain pillar radius grows to 2.025");
                    while (red && red.gameObject.activeSelf) yield return null;
                    Check(Mathf.Abs(Time.time-warned-.35f)<.12f,"rain falls after a 0.35-second warning");
                }
                if (pattern==PaladinEncounter.Pattern.Ambush)
                {
                    while (rig.Visible) yield return null;
                    yield return new WaitForSeconds(1.55f);
                    Check(!rig.Visible && WarningRenderer(),"ambush previews while the boss remains hidden");
                    game.Hero.DebugInvincibility=DebugInvincibilityMode.Off;
                    float hp=game.Hero.Hp;
                    Vector2 origin=knight.transform.position;
                    game.Hero.transform.position=origin+PaladinArea.Turn((game.Pos-origin).normalized,120)*9;
                    while (!rig.Visible) yield return null;
                    Check(Mathf.Abs(Time.time-start-2)<.13f && game.Hero.Hp==hp-knight.NormalDamage,
                        "270-degree ambush strikes its new flank at radius nine on the reveal frame");
                    game.Hero.DebugInvincibility=DebugInvincibilityMode.HealthOnly;
                }
                Time.timeScale=3;
                if (pattern==PaladinEncounter.Pattern.Rush && Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    yield return new WaitForSeconds(.4f);
                    Capture(game,"Paladin-Rush");
                }
                yield return routine;
                Check(game.TrialBoss==knight,"completed "+pattern);
            }
            Check(!intro.isPlaying && music.isPlaying && music.timeSamples>0,
                "Reversal hands off from the one-shot intro to its scheduled loop");
            music.timeSamples=music.clip.samples-music.clip.frequency/4;
            yield return new WaitForSecondsRealtime(.6f);
            Check(music.isPlaying && music.timeSamples<music.clip.frequency*2,
                "the imported Reversal clip wraps to its first bar in the live mixer");
            Time.timeScale=1;
            game.Hero.transform.position=game.RoomCenter+new Vector2(20,11);
            knight.TakeDamage(knight.MaxHp*3);
            Check(Mathf.Approximately(knight.Hp,knight.MaxHp*.6f),"overkill stops at 60%");
            knight.TakeDamage(999);
            Check(Mathf.Approximately(knight.Hp,knight.MaxHp*.6f),"locked threshold");
            game.Hero.Hp=50;
            yield return new WaitForSeconds(.7f);
            Check(game.EchoBarrier && game.EchoBarrier.Closure>0 && game.EchoBarrier.Closure<1 &&
                game.Hero.Stunned && rig.Visible && game.LocalPos.magnitude>1 && game.LocalPos.magnitude<21,
                "floating judge and closing walls push a distant hunter toward center");
            for (int n=1;n<=3;n++)
            {
                string name="Echo preview "+n;
                GameObject preview;
                while (!(preview=GameObject.Find(name))) yield return null;
                float shown=Time.time;
                Check(!rig.Visible && game.Hero.Hp==50 && !game.Hero.Stunned,"harmless echo "+n);
                if (n==1)
                {
                    Check(game.LocalPos.magnitude<.1f && Mathf.Abs(game.EchoBarrier.Radius-2.9f)<.01f &&
                        Rules.ResolveDash(game.Pos,game.Pos+Vector2.right*20,.38f).x<=game.RoomCenter.x+2.53f &&
                        Rules.ClampToRoom(game.Pos+Vector2.up*20,.38f).y<=game.RoomCenter.y+4.43f,
                        "closed court contains walking and dashes on both axes");
                }
                if (n==2 && Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    yield return new WaitForSeconds(.45f);
                    Capture(game,"Paladin-Echo");
                }
                while (preview && preview.activeSelf) yield return null;
                Check(Time.time-shown>=.94f,"one-second ghost demonstration "+n);
            }
            yield return new WaitForSeconds(2.55f);
            Check(game.Hero.TryDodge(Vector2.left),"hunter dodges the real echo sequence inside the barrier");
            while (!game.Hero.Stunned && knight.Dead) yield return null;
            float bindStart=Time.time;
            yield return new WaitForSeconds(2.6f);
            Check(game.Hero.Stunned && game.Hero.Hp==50,"echo ring holds the hunter before detonation");
            while (game.Hero.Stunned) yield return null;
            Check(Mathf.Abs(Time.time-bindStart-3)<.25f,"echo judgment resolves after three seconds");
            Check(Mathf.Approximately(game.Hero.Hp,50+knight.PerfectHeal),"perfect memory heal");
            while (knight.Dead) yield return null;
            Check(!game.EchoBarrier && Rules.ClampToRoom(game.RoomCenter+court,0)==game.RoomCenter+court,
                "judgment releases the full arena");
            Time.timeScale=3;
            knight.StopAllCoroutines();
            foreach (var pattern in new[]{PaladinEncounter.Pattern.Light,PaladinEncounter.Pattern.Spin,PaladinEncounter.Pattern.Swords,PaladinEncounter.Pattern.Rain})
                yield return knight.StartCoroutine(knight.Attack(pattern));
            knight.TakeEnvironmentDamage(knight.MaxHp*3);
            Check(Mathf.Approximately(knight.Hp,knight.MaxHp*.2f),"overkill stops at 20%");
            yield return new WaitForSeconds(1.9f);
            if (Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(game,"Paladin-Eclipse");
            while (!knight.FramingSlam) yield return null;
            game.Hero.transform.position=game.RoomCenter+new Vector2(8,-3);
            yield return new WaitForSeconds(.65f);
            var eclipse=GameObject.Find("Eclipse spotlights").GetComponent<MeshRenderer>();
            var properties=new MaterialPropertyBlock();
            eclipse.GetPropertyBlock(properties);
            var lights=properties.GetVector("_Lights");
            Check(rig.Visible && properties.GetColor("_Tint").a>.95f && properties.GetVector("_LightRadii").y>3 &&
                Vector2.Distance(new Vector2(lights.x,lights.y),game.Pos+Vector2.up*.85f)<.01f &&
                Vector2.Distance(new Vector2(lights.z,lights.w),game.RoomCenter+Vector2.up*1.65f)<.01f,
                "player and judge retain separate spotlights until the central slam");
            if (Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(game,"Judicator-EclipseSlam");
            game.Hero.DebugInvincibility=DebugInvincibilityMode.Off;
            float hpBeforeSlam=game.Hero.Hp;
            while (eclipse && eclipse.gameObject.activeSelf && game.Hero.Hp==hpBeforeSlam) yield return null;
            Check((!eclipse || !eclipse.gameObject.activeSelf) && game.Hero.Hp==hpBeforeSlam-knight.NormalDamage*2,
                "arena relights exactly when slam damage lands");
            game.Hero.DebugInvincibility=DebugInvincibilityMode.HealthOnly;
            while(knight.Dead) yield return null;
            knight.TakeDamage(knight.MaxHp*3);
            while(!knight.SecondPhase) yield return null;
            yield return null;
            Check(game.State==RunState.Combat && knight.Hp==knight.UnjudgedHealth && game.TrialBossName=="심판받지 못한 자" &&
                label.text==game.TrialBossName,"armor transition starts second health bar and renames the boss");
            Check(music.isPlaying && music.clip==knight.IdentityMusic && music.clip.name=="The_identity" &&
                !intro.isPlaying && music.volume<.03f,"phase two uses the identity recording with matched music gain");
            Check(GameObject.Find("Sanctuary Arena").GetComponent<TrialInterior>().Dusk == 1,
                "phase transition replaces joined wall and floor artwork");
            game.Hero.transform.position=game.RoomCenter+Vector2.left*3;
            game.Crow.ResetAt(game.Pos);
            yield return new WaitForSeconds(.8f);
            if (Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(game,"Paladin-Reveal");
            yield return CheckUnjudged(game,knight);
            game.Hero.transform.position=game.RoomCenter+Vector2.down*(court.y-2);
            Check(game.TryTravel() && !game.Cam.orthographic,"new southern portal returns to perspective gallery");
            game.StartTrialBattle();
            Check(game.TrialBoss && game.TrialBoss.Hp==game.TrialBoss.MaxHp,"fresh retry");
            knight=(PaladinEncounter)game.TrialBoss;
            game.Hero.DebugInvincibility=DebugInvincibilityMode.Off;
            game.Hero.transform.position=game.RoomCenter;
            float beforeEcho=game.Hero.Hp;
            knight.TakeDamage(knight.MaxHp);
            while(knight.Dead) yield return null;
            Check(Mathf.Approximately(game.Hero.Hp,beforeEcho-3*(knight.NormalDamage+knight.JudgmentDamagePerHit)),
                "three real echo hits apply three judgment penalties");
            game.OpenTrialGallery(); game.StartTrialBattle();
            ((PaladinEncounter)game.TrialBoss).TakeEnvironmentDamage(1800);
            yield return new WaitForSeconds(.4f);
            Check(game.EchoBarrier && game.Hero.Stunned,"interrupted ritual starts with an active barrier");
            game.BeginDeath(); yield return null; yield return null;
            Check(!game.TrialBoss && !game.EchoBarrier && !game.Hero.Stunned && game.Hero.Hp==game.Hero.MaxHealth,
                "death during closure releases the walls and bind when returning to gallery");
            game.Hero.transform.position=game.RoomCenter+Vector2.down*7;
            Check(game.TryTravel() && game.IsTown && !game.InBossTrial,"town restored");
            Time.timeScale=1;
            if (failed) yield break;
            done=true;
            Debug.Log("PALADIN_TRIAL_PASSED: "+assertions+" checks");
            if(Batch) EditorApplication.Exit(0);
            else Destroy(this);
        }

        IEnumerator CheckUnjudged(Dungeon game,PaladinEncounter knight)
        {
            game.Hero.DebugInvincibility=DebugInvincibilityMode.Full;
            var rig=knight.GetComponentInChildren<PaladinRig>();
            while (!knight.IsHandMode) yield return null;
            Check(!rig.Armed && GameObject.Find("Grounded sword"),"live rotation enters unarmed combat");
            while (knight.IsHandMode) yield return null;
            Check(rig.Armed && !GameObject.Find("Grounded sword"),"live rotation retrieves the sword");
            knight.StopAllCoroutines();
            foreach (var pose in Enum.GetValues(typeof(PaladinRig.Action)))
                Check(Resources.Load<Sprite>("Paladin/Unjudged/"+pose),"authored animation cel "+pose);
            foreach (PaladinEncounter.Move move in Enum.GetValues(typeof(PaladinEncounter.Move)))
            {
                if (move==PaladinEncounter.Move.BeamPunch)
                {
                    yield return knight.ChangeWeapon(true);
                    Check(knight.IsHandMode && !rig.Armed && GameObject.Find("Grounded sword"),"throw plants the sword and enters fist mode");
                }
                if (move==PaladinEncounter.Move.DrawCircle)
                {
                    yield return knight.ChangeWeapon(false);
                    Check(!knight.IsHandMode && rig.Armed && !GameObject.Find("Grounded sword"),"recall returns to armed combat");
                }
                knight.transform.position=game.RoomCenter;
                game.Hero.transform.position=game.RoomCenter+Vector2.right*(move==PaladinEncounter.Move.Pillars?9:3);
                float grabHealth=game.Hero.Hp;
                if (move==PaladinEncounter.Move.SweepGrab)
                {
                    // Isolate the grab from the planted sword's persistent damage.
                    Vector2 sword=GameObject.Find("Grounded sword").transform.position;
                    Vector2 away=Vector2.right*Mathf.Sign(game.RoomCenter.x-sword.x);
                    knight.transform.position=game.RoomCenter+away*11;
                    game.Hero.transform.position=(Vector2)knight.transform.position+away*3;
                    game.Hero.DebugInvincibility=DebugInvincibilityMode.Off;
                    game.Hero.Hp=grabHealth=game.Hero.MaxHealth;
                    yield return new WaitForSeconds(1.1f);
                }
                var attack=knight.StartCoroutine(knight.SecondAttack(move));
                if (move==PaladinEncounter.Move.Ricochet)
                {
                    while (!GameObject.Find("Four-wall ricochet")) yield return null;
                    float before=knight.Hp;
                    knight.TakeEnvironmentDamage(20);
                    Check(Mathf.Approximately(before-knight.Hp,10),"ricochet applies half damage during flight");
                }
                if (move==PaladinEncounter.Move.QuakeLeap)
                {
                    yield return null;
                    Check(knight.InQuake(game.RoomCenter),"earthquake installs the slowing ground field");
                }
                yield return attack;
                if (move==PaladinEncounter.Move.SweepGrab)
                {
                    Check(Mathf.Approximately(game.Hero.Hp,grabHealth-knight.UnjudgedDamage*2) && game.Hero.KnockedBack,
                        "unarmed grab deals exactly two hits and ends in knockback");
                    game.Hero.DebugInvincibility=DebugInvincibilityMode.Full;
                }
                if (move==PaladinEncounter.Move.DeathRay)
                {
                    yield return null; // Let the painted floor publish the breach before capturing it.
                    var ruin=knight.RuinForPainting;
                    Vector2 tip=game.RoomCenter+new Vector2(ruin.x,ruin.y);
                    Vector2 lost=tip+Vector2.right*ruin.z*10;
                    Check(ruin.w==1 && Mathf.Approximately(Mathf.Abs(ruin.x),13.5f) &&
                        Vector2.Distance(Rules.ClampToRoom(lost),lost)>2,
                        "beam destroys a thirty-degree wedge and shares the movement boundary");
                    if (Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                        Capture(game,"Unjudged-RuinedFloor");
                }
                Check(game.TrialBoss==knight && rig.Visible,"completed phase-two move "+move);
            }
            // Breach duration is measured in combat time, including subsequent attacks.
            yield return new WaitForSeconds(21.1f);
            Check(knight.RuinForPainting.w==0,"temporary beam breach recovers");
            for (int i=0;i<2;i++)
            {
                knight.TakeEnvironmentDamage(knight.Hp-knight.MaxHp*(i==0?.59f:.29f));
                yield return null;
                Check(knight.ShowingSummon && knight.HunterCount==i,"summon marker appears before the blade");
                Vector2 marker=knight.SummonPosition;
                yield return new WaitForSeconds(.5f);
                Check(knight.ShowingSummon && knight.HunterCount==i,"summon point remains visible during the warning");
                while (knight.ShowingSummon) yield return null;
                var summoned=GameObject.Find("Pursuing verdict "+(i+1));
                Check(knight.HunterCount==i+1 && summoned &&
                    Vector2.Distance(marker,summoned.transform.position)<.8f,"blade materializes at its marked position");
            }
            yield return new WaitForSeconds(.35f);
            knight.TakeEnvironmentDamage(knight.MaxHp*5);
            knight.TakeDamage(knight.MaxHp*5);
            Check(Mathf.Approximately(knight.Hp,knight.MaxHp*.01f),"overkill and repeat hits stop at one percent");
            for(int round=0;round<2;round++)
            {
                while (!knight.TrueBodyOpen) yield return null;
                yield return null;
                Check(knight.HunterCount==0 && knight.ApparitionCount==(round==0?4:8) && knight.IslandRadius==6.5f &&
                    Vector2.Distance(Rules.ClampToRoom(game.RoomCenter+Vector2.right*20),game.RoomCenter)<6.5f,
                    "real attack window on the final island, round "+(round+1));
                foreach (var other in FindObjectsByType<PaladinRig>(FindObjectsSortMode.None))
                    if (other!=rig) Check(!other.Visible,"real charge arrives alone after the scatter");
                float locked=knight.Hp;
                knight.TakeEnvironmentDamage(9000);
                Check(knight.TrueBodyOpen && knight.Hp==locked,"incidental damage cannot clear a final round");
                if (Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                    Capture(game,"Unjudged-Final"+(round+1));
                var torso=CombatBody.For(knight).Center;
                var origin=CombatBody.For(game.Hero).Muzzle;
                var direction=(torso-origin).normalized;
                Check(ProjectileSpace.Sweep(game,origin,torso,false,.05f,out _,out var body,out _) && body==knight,
                    "projectile can reach the real silhouette from the hunter's muzzle");
                Bullet.SpawnWorld(origin,direction*85,12,false,.5f);
                yield return new WaitForSeconds(.4f);
                Check(!knight.TrueBodyOpen,"projectile registers before the incoming attack");
            }
            while(game.TrialBoss) yield return null;
            Check(game.State==RunState.Exit && knight.Hp==0 && knight.IslandRadius==0 &&
                knight.RuinForPainting.w==0 && GameObject.Find("Unjudged abandoned sword") && !rig.Visible,
                "judge escapes, leaves his sword and restores the arena");
            if (Batch && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                Capture(game,"Unjudged-Departure");
        }

        static MeshRenderer WarningRenderer()
        {
            var effects=GameObject.Find("Sanctuary attack effects");
            foreach (var surface in effects.GetComponentsInChildren<MeshRenderer>())
                if (surface.sortingOrder==-3800) return surface;
            return null;
        }

        IEnumerator CheckTrainingStations(Dungeon game)
        {
            var root = GameObject.Find("Training stations").transform;
            Check(root.childCount == game.PassiveOffers.Seals.Count + 3, "one station per seal and three utilities");
            foreach (var seal in game.PassiveOffers.Seals)
            {
                var plate = root.Find(seal.NameStringKey.Replace("인장 · ",""));
                var point = (Vector2)plate.position;
                Check(Vector2.Distance(Rules.OutsideCover(point),point)<.02f, "accessible " + seal.name);
                game.Hero.transform.position = point + Vector2.down * .3f;
                Check(game.AtTrainingInteraction && game.UseTrainingStation() && game.UseTrainingStation() &&
                    game.Passives.TryGetPassive(seal.PassiveID,out var owned) && owned.Stack == 2,
                    "repeat grant " + seal.name);
            }
            game.Hero.transform.position = root.Find("동전 +10,000").position;
            int before = game.Wallet.Copper;
            Check(game.UseTrainingStation() && game.UseTrainingStation() && game.Wallet.Copper == before+20000,
                "repeat copper grant");
            game.Hero.transform.position = root.Find("성흔 강화").position;
            int clears = game.MapsCleared;
            Check(game.UseTrainingStation() && game.Stigmas.Room.IsOpen, "open training altar");
            yield return null;
            var altar = game.Scene.UI.StigmaView;
            Check(altar.gameObject.activeInHierarchy, "altar panel opens in the HUD");
            var card = game.Stigmas.Room.GetOffers()[0];
            altar.OfferCards[0].Button.onClick.Invoke();
            altar.EquipButton.onClick.Invoke();
            Check(game.Stigmas.Room.HasClaimedCard, "claim training stigma through the altar UI");
            before = game.Wallet.Copper;
            Check(game.Stigmas.Room.Quote(card.Owner,null,true,out int cost,out _) && altar.UpgradeButton.interactable,
                "upgrade available in the altar UI");
            altar.UpgradeButton.onClick.Invoke();
            Check(game.Stigmas.Loadout.Get(card.Owner).Level == 2 && game.Wallet.Copper == before-cost,
                "training upgrade spends actual quoted price");
            altar.CloseButton.onClick.Invoke();
            Check(game.InTrainingRoom && game.State == RunState.Exit && game.MapsCleared == clears,
                "altar closes without completing expedition room");
            game.Hero.transform.position = root.Find("무적 꺼짐").position;
            Check(game.UseTrainingStation() && game.Hero.TrainingDamageProtection, "damage-only floor switch");
        }
        static void Capture(Dungeon game,string name)
        {
            var target=RenderTexture.GetTemporary(1280,720,24);
            var original=game.Cam.targetTexture;
            var active=RenderTexture.active;
            var canvas=game.Scene.UI.GetComponentInParent<Canvas>().rootCanvas;
            RenderMode mode=canvas.renderMode;
            int sortingOrder=canvas.sortingOrder;
            Camera canvasCamera=canvas.worldCamera;
            float plane=canvas.planeDistance;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                game.Cam.targetTexture=target;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;
                canvas.sortingOrder=short.MaxValue;
                canvas.worldCamera=game.Cam;
                canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();
                game.Cam.Render();
                RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../Logs",name+".png"),image.EncodeToPNG());
                if (name == "Paladin-Scale")
                {
                    // Sample an unobstructed section of the nave. A flat camera
                    // backdrop can otherwise pass every movement and combat check.
                    float low=1, high=0;
                    for (int y=235; y<=450; y+=11)
                        for (int x=215; x<=465; x+=11)
                        {
                            float light=image.GetPixel(x,y).grayscale;
                            low=Mathf.Min(low,light); high=Mathf.Max(high,light);
                        }
                    if (high-low < .12f) throw new InvalidOperationException("Sanctuary painting is missing from the combat camera");
                }
            }
            finally
            {
                game.Cam.targetTexture=original; RenderTexture.active=active;
                canvas.worldCamera=canvasCamera; canvas.planeDistance=plane;
                canvas.renderMode=mode;
                canvas.sortingOrder=sortingOrder;
                RenderTexture.ReleaseTemporary(target); Destroy(image);
            }
        }
    }
}
#endif

