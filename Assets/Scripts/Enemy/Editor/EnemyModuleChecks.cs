using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WesternLemegeton.Stigmas;

namespace WesternLemegeton.EditorTools
{
    public static class EnemyModuleChecks
    {
        static int checks;
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("ENEMY_MODULE_FAILED: " + message);
            checks++;
        }

        [MenuItem("Western Lemegeton/Tests/Check Basic Enemy Modules")]
        public static void Run()
        {
            checks = 0;
            // 배치 실행의 첫 화면은 저장되지 않은 빈 씬이므로 검사 전에 저장된 씬을 엽니다.
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var previousScene = SceneManager.GetActiveScene();
            var previousGame = Dungeon.I;
            var previousObstacles = Dungeon.Obstacles;
            var previousOrigin = Rules.RoomOrigin;
            var randomState = UnityEngine.Random.state;
            var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(testScene);
            try
            {
                var game = new GameObject("Module check game").AddComponent<Dungeon>();
                Dungeon.I = game;
                game.Hero = new GameObject("Module check target").AddComponent<Player>();
                game.State = RunState.Combat;
                Dungeon.Obstacles = Array.Empty<Rect>();
                Rules.RoomOrigin = Vector2.zero;

                var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/Enemy_0.prefab");
                Check(prefab && prefab.GetComponent<EnemyHealth>() && prefab.GetComponent<EnemyBasicBrain>() &&
                    prefab.GetComponent<EnemyMeleeAttack>() && prefab.GetComponent<EnemyChaseMovement>(), "Saved prefab has all four modules");
                var enemy = UnityEngine.Object.Instantiate(prefab);
                enemy.Init(0, 2);
                var health = enemy.GetComponent<EnemyHealth>();
                var brain = enemy.GetComponent<EnemyBasicBrain>();
                var movement = enemy.GetComponent<EnemyChaseMovement>();
                var attack = enemy.GetComponent<EnemyMeleeAttack>();
                Check(enemy.Hp == 81 && enemy.MaxHp == 81, "Default health scales with room");
                health.BaseHealth = 100;
                health.HealthPerRoom = 5;
                enemy.Init(0, 2);
                Check(enemy.Hp == 110, "Inspector health settings survive initialization");
                enemy.TakeStigmaDamage(20, StigmaOwner.Player, false);
                Check(enemy.Hp == 90 && health.Current == 90, "Existing damage API reaches health module");
                enemy.Hp = enemy.MaxHp = 500;
                Check(health.Current == 500 && health.Maximum == 500, "Existing debug setters stay compatible");
                health.ApplyDamage(1000);
                Check(enemy.Dead && enemy.Hp == 0, "Lethal health clamps to zero");
                enemy.Init(0, 2);

                enemy.transform.position = Vector2.zero;
                game.Hero.transform.position = new Vector2(5, 0);
                movement.Speed = 3;
                brain.Tick(game, .5f, 1);
                Check(Mathf.Abs(enemy.transform.position.x - 1.5f) < .001f, "Custom speed and slow multiplier affect chase");
                movement.enabled = false;
                brain.Tick(game, 1, 1);
                Check(Mathf.Abs(enemy.transform.position.x - 1.5f) < .001f, "Disabled movement stays stationary");
                movement.enabled = true;
                enemy.transform.position = Vector2.zero;
                Dungeon.Obstacles = new[] { new Rect(1, -1, 1, 2) };
                movement.Tick(enemy, game, game.Pos, 1, .5f);
                Check(enemy.transform.position.x == 0, "Chase respects obstacle resolution");
                Dungeon.Obstacles = Array.Empty<Rect>();

                game.Hero.transform.position = Vector2.right;
                attack.Damage = 27;
                attack.WindupSeconds = .4f;
                attack.Initialize(enemy);
                brain.Tick(game, 1, 3);
                Check(enemy.Telegraphing && enemy.AuthoredWarning.enabled && enemy.AttackPower == 27, "Attack uses configured damage and warning");
                Check(enemy.transform.position == Vector3.zero, "Windup blocks movement");
                brain.Tick(game, 1, .2f);
                Check(enemy.Telegraphing, "Windup waits before executing");
                game.State = RunState.Paused;
                typeof(Enemy).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(enemy, null);
                Check(enemy.Telegraphing, "Pause does not advance attack");
                game.State = RunState.Combat;
                game.Hero.transform.position = new Vector2(5, 0);
                float hpBefore = game.Hero.Hp;
                brain.Tick(game, 1, .21f);
                Check(!enemy.Telegraphing && !enemy.AuthoredWarning.enabled && game.Hero.Hp == hpBefore, "Escaping during windup avoids the hit");
                game.Hero.transform.position = Vector2.right;
                brain.Tick(game, 1, .1f);
                Check(!enemy.Telegraphing, "Cooldown prevents immediate repeat attack");
                brain.Tick(game, 1, 2);
                attack.enabled = false;
                // Edit Mode는 일반 MonoBehaviour 생명주기를 실행하지 않으므로 콜백을 명시적으로 검사합니다.
                typeof(EnemyMeleeAttack).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(attack, null);
                Check(!enemy.Telegraphing && !enemy.AuthoredWarning.enabled && enemy.AttackPower == 0, "Disabling attack cancels pending hit and warning");
                game.Hero.transform.position = new Vector2(5, 0);
                brain.Tick(game, 1, .1f);
                Check(enemy.transform.position.x > 0, "Movement works with attack disabled");

                var sound = game.gameObject.AddComponent<AudioSource>();
                var clip = AudioClip.Create("Module check silent hit", 64, 1, 44100, false);
                typeof(Dungeon).GetField("sound", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, sound);
                typeof(Dungeon).GetField("hitClip", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, clip);
                attack.enabled = true;
                attack.Initialize(enemy);
                game.Hero.Hp = 100;
                game.Hero.transform.position = enemy.transform.position + Vector3.right;
                brain.Tick(game, 1, 3);
                Check(game.Hero.Hp == 100, "Starting windup deals no early damage");
                brain.Tick(game, 1, .41f);
                Check(game.Hero.Hp == 73, "Melee hit applies configured damage through Player.Hurt");
                brain.Tick(game, 1, .1f);
                Check(game.Hero.Hp == 73, "Cooldown does not repeat damage");
                for (int kind = 1; kind <= 3; kind++)
                {
                    string name = kind == 3 ? "Boss" : "Enemy_" + kind;
                    var legacy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/" + name + ".prefab"));
                    legacy.Init(kind, 2);
                    Check(legacy.GetComponent<EnemyBasicBrain>() &&
                        legacy.MaxHp == (kind == 3 ? 650 : kind == 1 ? 106 : 81), "Kind " + kind + " retains health and expected behavior path");
                }
                EnemyPatternChecks.Run(game);
                UnityEngine.Object.DestroyImmediate(clip);
                Debug.Log("ENEMY_MODULE_CHECKS_PASSED: " + checks);
            }
            finally
            {
                Dungeon.I = previousGame;
                Dungeon.Obstacles = previousObstacles;
                Rules.RoomOrigin = previousOrigin;
                UnityEngine.Random.state = randomState;
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(testScene, true);
            }
        }
    }
}
