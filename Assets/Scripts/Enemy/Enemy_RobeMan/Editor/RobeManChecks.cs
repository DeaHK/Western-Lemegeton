using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WesternLemegeton.EditorTools
{
    public static class RobeManChecks
    {
        // 실패한 조건을 예외로 보고하고 통과한 검사 수를 집계합니다.
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static int checks;
        static void Check(bool value, string name)
        {
            if (!value) throw new Exception("ROBEMAN_FAILED: " + name);
            checks++;
        }

        // 임시 전투 환경에서 프리팹 연결, 체류 조건, 고정 조준, 비행과 주기 피해를 검증합니다.
        [MenuItem("Western Lemegeton/Tests/Check RobeMan")]
        public static void Run()
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var previousScene = SceneManager.GetActiveScene();
            var previousGame = Dungeon.I;
            var random = UnityEngine.Random.state;
            var previousOrigin = Rules.RoomOrigin;
            var previousObstacles = Dungeon.Obstacles;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            AudioClip clip = null;
            try
            {
                // 게임·플레이어·소리와 장애물 없는 임시 환경을 구성합니다.
                checks = 0;
                RobeManSetup.CreatePrefab();
                var game = new GameObject("RobeMan test game").AddComponent<Dungeon>();
                Dungeon.I = game;
                game.State = RunState.Combat;
                game.Hero = new GameObject("Hunter test").AddComponent<Player>();
                game.Hero.Hp = 1000;
                var sound = game.gameObject.AddComponent<AudioSource>();
                clip = AudioClip.Create("Silent hit", 64, 1, 44100, false);
                typeof(Dungeon).GetField("sound", Private).SetValue(game, sound);
                typeof(Dungeon).GetField("hitClip", Private).SetValue(game, clip);
                Rules.RoomOrigin = Vector2.zero;
                Dungeon.Obstacles = Array.Empty<Rect>();

                // 프리팹의 부품 구성과 추적, 체류 초기화 및 조준 중 행동 제한을 검사합니다.
                var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>(RobeManSetup.PrefabPath));
                enemy.Init(0, 0);
                var attack = enemy.GetComponent<RobeManAttack>();
                var brain = enemy.GetComponent<EnemyBasicBrain>();
                Check(attack && enemy.GetComponents<EnemyAttackModule>().Length == 1, "Prefab has exactly one dedicated attack");
                enemy.transform.position = Vector2.zero;
                game.Hero.transform.position = Vector2.right * 8;
                brain.Tick(game, 1, .1f);
                Check(enemy.transform.position.x > 0, "Spawned RobeMan chases Hunter through existing Brain");
                // 추적 확인 중 누적된 체류 시간을 비워 독병 조건을 독립적으로 검사합니다.
                attack.Initialize(enemy);
                enemy.transform.position = Vector2.zero;
                attack.MeleeRange = 0;
                game.Hero.transform.position = Vector2.right * 2;
                attack.Tick(game, 1, 4.9f);
                Check(attack.CurrentPhase == RobeManAttack.Phase.Chase, "No poison before five seconds");
                game.Hero.transform.position = Vector2.right * (attack.DetectionRadius + .01f);
                attack.Tick(game, 1, .1f);
                Check(attack.StaySeconds == 0, "Leaving radius resets incomplete dwell");
                game.Hero.transform.position = Vector2.right * attack.DetectionRadius;
                attack.Tick(game, 1, 5);
                Check(attack.CurrentPhase == RobeManAttack.Phase.Aim, "Inclusive radius and five seconds begin aim");
                var locked = attack.LockedTarget;
                attack.MeleeRange = 1.7f;
                game.Hero.transform.position = Vector2.right;
                float hp = game.Hero.Hp;
                Check(!attack.Tick(game, 1, .5f) && attack.CurrentPhase == RobeManAttack.Phase.Aim,
                    "Aim allows skill movement without starting melee while Hunter is nearby");
                Check(attack.LockedTarget == locked && game.Hero.Hp == hp, "Aim does not track or apply melee damage");
                game.State = RunState.Paused;
                attack.Tick(game, 1, 20);
                Check(attack.CurrentPhase == RobeManAttack.Phase.Aim, "Pause freezes aim timer");
                game.State = RunState.Combat;
                attack.Tick(game, 1, .5f);
                var bottles = UnityEngine.Object.FindObjectsByType<RobeManPoisonBottle>(FindObjectsSortMode.None);
                Check(bottles.Length == 1 && bottles[0].Target == locked, "Exactly one bottle launches toward captured point");
                // 독병의 포물선과 착지, 독 구역의 피해 간격·재진입·수명 만료를 검사합니다.
                var bottle = bottles[0];
                bottle.Advance(.4f);
                Check(Mathf.Abs(bottle.VisualHeight - 3) < .001f && Vector2.Distance(bottle.transform.position, locked * .5f) < .001f,
                    "Bottle follows planar interpolation with an arc apex");
                bottle.Advance(.4f);
                var zones = UnityEngine.Object.FindObjectsByType<RobeManPoisonZone>(FindObjectsSortMode.None);
                Check(!bottle && zones.Length == 1 && (Vector2)zones[0].transform.position == locked, "Landing replaces bottle with zone at target");
                var zone = zones[0];
                Check(zone.Radius == attack.PoisonRadius && !zone.GetComponent<Collider>() && !zone.GetComponent<Collider2D>(), "Circular danger zone is passable");
                game.Hero.transform.position = locked;
                zone.Advance(game, .5f);
                Check(game.Hero.Hp == hp, "Poison waits a full second after entry");
                zone.Advance(game, .5f);
                Check(game.Hero.Hp == hp - 5, "First poison tick damages Hunter");
                typeof(Player).GetField("invulnerable", Private).SetValue(game.Hero, 1f);
                zone.Advance(game, 2);
                Check(game.Hero.Hp == hp - 15, "Two periodic ticks survive large delta and normal hit immunity");
                game.Hero.transform.position = locked + Vector2.right * 5;
                zone.Advance(game, .5f);
                game.Hero.transform.position = locked;
                zone.Advance(game, .5f);
                Check(game.Hero.Hp == hp - 15, "Exit and re-entry reset poison exposure clock");
                zone.Advance(game, .5f);
                Check(game.Hero.Hp == hp - 20, "Re-entry damages after one complete second");
                game.Hero.transform.position = locked + Vector2.right * 5;
                zone.Advance(game, 100);
                Check(!zone && game.Hero.Hp == hp - 20, "Zone expires without out-of-range damage");

                attack.Initialize(enemy);
                // 근접 공격 중 달성한 독병 조건이 후딜 종료까지 보존되는지 검사합니다.
                attack.RequiredStaySeconds = .2f;
                attack.MeleeWindup = .5f;
                game.Hero.transform.position = Vector2.right;
                attack.Tick(game, 1, 0);
                Check(attack.CurrentPhase == RobeManAttack.Phase.MeleeWindup, "Nearby Hunter starts melee");
                attack.Tick(game, 1, .2f);
                Check(attack.CurrentPhase == RobeManAttack.Phase.MeleeWindup, "Qualifying dwell does not interrupt melee");
                attack.Tick(game, 1, .31f);
                Check(attack.CurrentPhase == RobeManAttack.Phase.MeleeRecovery, "Melee finishes its strike before poison aim");
                game.Hero.transform.position = Vector2.right * 8;
                attack.Tick(game, 1, .25f);
                Check(attack.CurrentPhase == RobeManAttack.Phase.Aim && attack.LockedTarget == game.Pos,
                    "Queued poison locks Hunter position immediately when melee ends");
                attack.Cancel();
                Check(!attack.Telegraphing && attack.StaySeconds == 0, "Cancel clears pending aim and dwell");
                Check(!enemy.AuthoredWarning.enabled, "Cancel clears melee warning");
                attack.Initialize(enemy);
                attack.RequiredStaySeconds = 5;
                attack.MeleeRange = 1.7f;
                enemy.transform.position = Vector2.zero;
                // Hunter가 매 프레임 다른 위치로 움직여도 반지름 3 안이면 체류 시간이 누적됩니다.
                for (int frame = 0; frame < 301; frame++)
                {
                    float angle = frame * .03f;
                    game.Hero.transform.position = enemy.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.5f;
                    brain.Tick(game, 1, 1f / 60);
                }
                Check(attack.CurrentPhase == RobeManAttack.Phase.Aim,
                    "Moving Hunter inside radius for five seconds still triggers aim through Brain");
                Vector2 movingTarget = attack.LockedTarget;
                game.Hero.transform.position = enemy.transform.position + Vector3.left;
                brain.Tick(game, 1, 1.1f);
                var movingBottles = UnityEngine.Object.FindObjectsByType<RobeManPoisonBottle>(FindObjectsSortMode.None);
                Check(movingBottles.Length == 1 && movingBottles[0].Target == movingTarget,
                    "Moving after lock still launches bottle at the fixed target");
                Debug.Log("ROBEMAN_CHECKS_PASSED: " + checks);
            }
            // 실패 여부와 관계없이 전역 상태와 활성 씬을 복원하고 임시 리소스를 정리합니다.
            finally
            {
                if (clip) UnityEngine.Object.DestroyImmediate(clip);
                Dungeon.I = previousGame;
                Rules.RoomOrigin = previousOrigin;
                Dungeon.Obstacles = previousObstacles;
                UnityEngine.Random.state = random;
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
