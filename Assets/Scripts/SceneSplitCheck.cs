#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace WesternLemegeton
{
    // 분리된 씬을 실제 Play 모드에서 오가며 확인하는 자동 검사입니다.
    // 배치 실행: Unity -batchmode -executeMethod WesternLemegeton.SceneSplitCheck.RunBatch -checkSceneSplit
    // 전투의 재미나 그림은 판단하지 못합니다. 씬 전환·상태·중복 오브젝트·오류 로그만 확인합니다.
    public sealed class SceneSplitCheck : MonoBehaviour
    {
        static bool Batch => Array.IndexOf(Environment.GetCommandLineArgs(), "-checkSceneSplit") >= 0;
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            EditorApplication.EnterPlaymode();
        }
        [MenuItem("Western Lemegeton/Tests/Run Scene Split Check (Play Mode, from MainMenu)")]
        public static void Run()
        {
            if (!Application.isPlaying || !GameManager.I) throw new InvalidOperationException("Open MainMenu and enter Play Mode first.");
            var go = new GameObject("Scene Split Check"); DontDestroyOnLoad(go); go.AddComponent<SceneSplitCheck>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachBatch() { if (Batch && GameManager.I) Run(); }

        int checks; string failure; bool finished; float started;
        void OnEnable() { started = Time.realtimeSinceStartup; Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }
        void OnLog(string message, string trace, LogType type)
        {
            if (trace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")) return;
            if (failure == null && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && !message.StartsWith("SCENE_SPLIT"))
                failure = "logged " + type + ": " + message;
        }
        void Update()
        {
            if (finished) return;
            if (failure == null && Time.realtimeSinceStartup - started > 300) failure = "timeout";
            if (failure == null) return;
            finished = true;
            Debug.LogError("SCENE_SPLIT_CHECK_FAILED: " + failure);
            if (Batch) EditorApplication.Exit(1);
        }
        void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
            checks++; Debug.Log("SCENE_SPLIT_PASS: " + message);
        }
        IEnumerator Ready()
        {
            float limit = Time.realtimeSinceStartup + 60;
            yield return null;
            while (GameManager.I.IsLoading && GameManager.I.LastError == null && Time.realtimeSinceStartup < limit) yield return null;
            if (GameManager.I.IsLoading) throw new InvalidOperationException("loading did not finish: " + GameManager.I.LastError);
            yield return null; yield return null;
        }
        void Globals(GameSpace space)
        {
            var m = GameManager.I;
            Check(m.Content && m.Content.Space == space && SceneManager.GetActiveScene() == m.Content.gameObject.scene, space + " is the bound and active scene");
            Check(SceneManager.sceneCount == 1, space + ": exactly one content scene loaded (" + SceneManager.sceneCount + ")");
            Check(FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, space + ": one EventSystem");
            Check(FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, space + ": one AudioListener");
            Check(FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1 &&
                  FindObjectsByType<Dungeon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, space + ": one session");
            Check(Time.timeScale == 1, space + ": time scale restored");
        }
        void HeroVisible(Dungeon g, string where)
        {
            Check(g.Hero.gameObject.activeInHierarchy && g.Crow.gameObject.activeInHierarchy, where + ": hunter and raven active");
            foreach (var card in g.Hero.GetComponentsInChildren<PaperCard>(true).Concat(g.Crow.GetComponentsInChildren<PaperCard>(true)))
                Check(card.Visible, where + ": actor card survives scene change (" + card.name + ")");
            Check(!g.Cam.orthographic, where + ": perspective camera");
        }
        void NothingLeft(Dungeon g, string where)
        {
            Check(g.Enemies.Count == 0 && FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0, where + ": no enemies left");
            Check(FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length == 0, where + ": no bullets left");
            Check(!g.InBossTrial && !g.InPractice && !g.InsideHouse, where + ": no trial, practice or house state left");
            Check(!FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a => a.isPlaying && a.loop), where + ": no looping music left");
        }
        void Title(Dungeon g)
        {
            Globals(GameSpace.MainMenu);
            Check(g.State == RunState.Title && g.Scene.UI.TitlePanel.activeInHierarchy && !g.Scene.UI.HudPanel.activeInHierarchy, "title panel shown, HUD hidden");
            Check(!g.Hero.gameObject.activeSelf && !g.Crow.gameObject.activeSelf, "actors hidden on title");
            Check(g.Scene.UI.GetComponentsInChildren<UICommand>(true).Any(c => c.Action == GameUIAction.QuitGame && c.gameObject.activeInHierarchy), "quit button visible on title");
            NothingLeft(g, "title");
        }
        IEnumerator Town(Dungeon g, string where)
        {
            yield return Ready();
            Globals(GameSpace.Lobby);
            Check(g.State == RunState.Town && g.IsTown && g.Scene.Hirva && g.Scene.Hirva.gameObject.activeInHierarchy, where + ": town active");
            Check(Dungeon.Obstacles.Length > 0 && g.Wallet.Copper == 0, where + ": town footprints installed, wallet reset");
            HeroVisible(g, where); NothingLeft(g, where);
        }

        IEnumerator Start()
        {
            var steps = Steps(); bool more = true;
            while (more && failure == null)
            {
                try { more = steps.MoveNext(); }
                catch (Exception ex) { failure = ex.Message; more = false; }
                if (more) yield return steps.Current;
            }
            if (failure != null) yield break;
            finished = true;
            Debug.Log("WNN_SCENE_SPLIT_PLAYCHECK_PASSED: " + checks);
            if (Batch) EditorApplication.Exit(0);
        }

        IEnumerator Steps()
        {
            var m = GameManager.I; var g = m.Session;
            yield return Ready();
            Title(g);

            // 메인 ↔ 로비 3회 왕복, 로비 기능(NPC·집)
            for (int i = 0; i < 3; i++)
            {
                g.EnterTown();
                Check(m.IsLoading && !g.Running, "loading blocks play while entering town #" + i);
                g.EnterTown(); g.StartRun();
                yield return Town(g, "lobby #" + i);
                if (i == 0)
                {
                    Vector2 spawn = g.Pos;
                    var npc = g.Scene.Hirva.GetComponentsInChildren<Transform>(false).First(t => t.name.StartsWith("NPC - "));
                    g.Hero.transform.position = new Vector2(npc.position.x, npc.position.z) + Vector2.down * .5f;
                    Check(g.TryInteractTownNpc() && g.State == RunState.Dialogue, "NPC dialogue opens");
                    g.NpcDialogue.Close(); Check(g.State == RunState.Town, "NPC dialogue closes");
                    Check(g.ShowHouseSign, "house entrance marker exists");
                    g.Hero.transform.position = g.HouseDoor;
                    Check(g.TryTravel() && g.InsideHouse, "house interior entered");
                    yield return null;
                    Check(g.TryTravel() && !g.InsideHouse && g.Scene.Hirva.gameObject.activeInHierarchy, "house exited to town");
                    g.Hero.transform.position = spawn;
                    yield return null;
                }
                m.ReturnToMenu();
                yield return Ready();
                Title(g);
            }

            // 로비 → 디버그(보스 선택실) → 전투 → 재도전 → 로비
            g.EnterTown(); yield return Town(g, "lobby before trial");
            g.OpenTrialGallery(); yield return Ready();
            Globals(GameSpace.Debug);
            Check(g.InTrialGallery && g.State == RunState.Exit && g.InTrainingRoom, "boss selection gallery built");
            HeroVisible(g, "gallery");
            g.Wallet.Add(77);
            string boss = g.TrialBossName;
            g.StartTrialBattle(); yield return null; yield return null;
            Check(g.InBossTrial && !g.InTrialGallery && g.TrialBoss && g.TrialBoss.gameObject.scene == m.Content.gameObject.scene, "boss arena and boss live in the Debug scene");
            Check(SceneManager.sceneCount == 1 && !m.IsLoading, "gallery to arena stays in the same scene without loading");
            yield return new WaitForSeconds(1.5f);
            g.OpenTrialGallery(); yield return null; yield return null;
            Check(g.InTrialGallery && g.TrialBossName == boss && g.Wallet.Copper == 77 && !m.IsLoading, "retry returns to the gallery with boss selection and coins kept");
            Check(FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length == 0, "boss removed on retry");
            g.Hero.transform.position = new Vector2(-180, -187);
            Check(g.TryTravel() && m.IsLoading, "south portal starts the return to town");
            yield return Town(g, "lobby after trial");
            Check(g.NearTrialGate || Vector2.Distance(g.Pos, RoomAuthoring.Point(g.Scene.Hirva.InitialSpawn)) > 1, "returned next to the west gate");

            // 보스전 도중 메인으로 이탈
            g.OpenTrialGallery(); yield return Ready();
            g.StartTrialBattle(); yield return new WaitForSeconds(2f);
            m.ReturnToMenu(); yield return Ready();
            Title(g);
            g.EnterTown(); yield return Town(g, "lobby after leaving a boss fight");

            // 로비 → 생성형 원정(Game) → 연습실 → 첫 방 → 마을 귀환 → 두 번째 원정
            for (int run = 0; run < 2; run++)
            {
                g.Hero.transform.position = RoomAuthoring.Point(g.Scene.Hirva.TownExit);
                Check(g.TryTravel(), "east gate starts expedition #" + run);
                yield return Ready();
                Globals(GameSpace.Game);
                var builder = g.GetComponent<StageDungeonBuilder>();
                Check(g.UsesGeneratedStage && g.InPractice && g.State == RunState.Exit && !g.RunMapInputBlocked, "run #" + run + ": generated stage installed, weapon practice first");
                Check(builder.ActiveRoot && builder.ActiveRoot.scene == m.Content.gameObject.scene, "run #" + run + ": generated rooms belong to the Game scene");
                Check(g.PracticeRoom.gameObject.scene == m.Content.gameObject.scene, "run #" + run + ": practice room belongs to the Game scene");
                Check(g.Rooms != null && g.Rooms.Length == 6 && g.Wallet.Copper == 0, "run #" + run + ": six rooms bound, new run reset");
                HeroVisible(g, "practice #" + run);
                RuntimeSmoke.PassWeaponRoom(g);
                yield return null; g.Cinematics.Skip(); yield return null;
                Check(!g.InPractice && g.CurrentRoom != null && (g.State == RunState.Combat || g.State == RunState.Exit), "run #" + run + ": first room entered (" + g.State + ")");
                Check(g.Enemies.All(e => e && e.gameObject.scene == m.Content.gameObject.scene), "run #" + run + ": enemies belong to the Game scene");
                g.Wallet.Add(5);
                yield return new WaitForSeconds(1f);
                if (run == 0) { g.BeforePause = g.State; g.State = RunState.Paused; g.EnterTown(); }
                else { m.ReturnToMenu(); yield return Ready(); Title(g); Check(!builder.ActiveRoot, "generated stage released on menu return"); g.EnterTown(); }
                yield return Town(g, "lobby after run #" + run);
                Check(!builder.ActiveRoot && !g.UsesGeneratedStage && g.Rooms == null, "run #" + run + ": generated stage released in town");
            }
            m.ReturnToMenu(); yield return Ready(); Title(g);
        }
    }
}
#endif
