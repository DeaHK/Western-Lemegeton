using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WesternLemegeton.EditorTools
{
    public static class SlowMovementSkillChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static int checks;

        // 공통 Brain 변경이 기존 공격 패턴과 각 몬스터의 상태 전환에 영향을 주는지도 함께 검사합니다.
        public static void RunAll()
        {
            Run();
            MountedEnemyChecks.Run();
            RobeManChecks.Run();
            EnemyModuleChecks.Run();
        }

        static void Check(bool value, string message)
        {
            if (!value) throw new Exception("SLOW_MOVEMENT_FAILED: " + message);
            checks++;
        }

        // 시간과 조준 상태를 고정해 이동 모듈 종류·스킬 상태·배율만 바꿔 실제 이동 거리를 비교합니다.
        static void SetState(EnemyAttackModule attack, string state)
        {
            var property = attack.GetType().GetProperty(attack is CowboyAttack ? "Action" : "CurrentPhase");
            property.SetValue(attack, Enum.Parse(property.PropertyType, state));
            attack.GetType().GetField("timer", Private).SetValue(attack, 100f);
            if (attack is CowboyAttack)
            {
                typeof(CowboyAttack).GetField("lassoPosition", Private).SetValue(attack, Vector2.left * 10);
                typeof(CowboyAttack).GetField("distance", Private).SetValue(attack, 0f);
            }
        }

        static void CheckDistance(Enemy enemy, Dungeon game, float expected, string message)
        {
            enemy.transform.position = Vector2.zero;
            enemy.GetComponent<EnemyBasicBrain>().Tick(game, .5f, .1f);
            Check(Vector2.Distance(enemy.transform.position, Vector2.right * expected) < .0001f, message);
        }

        static void CheckSkill(Dungeon game, bool cowboy, bool keepDistance)
        {
            var path = cowboy ? MountedEnemySetup.CowboyPath : RobeManSetup.PrefabPath;
            var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>(path));
            try
            {
                foreach (var old in enemy.GetComponents<EnemyMovementModule>()) UnityEngine.Object.DestroyImmediate(old);
                if (keepDistance)
                {
                    var movement = enemy.gameObject.AddComponent<EnemyKeepDistanceMovement>();
                    movement.Speed = 2; movement.UseAttackRange = false; movement.ApproachRange = 6;
                    movement.ApproachRangeFactor = 1; movement.RetreatDistance = 0; movement.SeparationStrength = 0;
                }
                else
                {
                    var movement = enemy.gameObject.AddComponent<EnemyChaseMovement>();
                    movement.Speed = 2; movement.StopDistance = 6; movement.SeparationStrength = 0;
                }
                enemy.Init(enemy.Kind, 0);
                var attack = enemy.GetComponent<EnemyAttackModule>();
                var skill = attack as ISlowMovementSkill;
                Check(skill != null, "Attack supplies common slow movement contract");
                if (attack is RobeManAttack robe) { robe.DetectionRadius = 0; robe.MeleeRange = 0; }
                var states = cowboy ? new[] { "Spin", "LassoOut", "LassoReturn" } : new[] { "Aim", "ThrowRecovery" };
                foreach (string state in states)
                {
                    foreach (float factor in new[] { 0f, .25f, 1f })
                    {
                        SetState(attack, state);
                        if (attack is CowboyAttack cow) cow.LassoMoveMultiplier = factor;
                        else ((RobeManAttack)attack).PoisonMoveMultiplier = factor;
                        CheckDistance(enemy, game, .1f * factor,
                            attack.GetType().Name + "/" + state + "/" + keepDistance + ": skill and external slow multiply once");
                    }
                }

                // 감속 상태를 빠져나가거나 공격 부품을 끄면 이동 속도가 정상으로 복구되어야 합니다.
                attack.Cancel();
                CheckDistance(enemy, game, .1f, "Cancel restores normal movement");
                attack.enabled = false;
                SetState(attack, states[0]);
                CheckDistance(enemy, game, .1f, "Disabled skill cannot slow movement");
                attack.enabled = true;
                SetState(attack, cowboy ? "ShotWindup" : "MeleeWindup");
                CheckDistance(enemy, game, 0, "Stationary attack still blocks movement");

                // 일시 정지 중에는 스킬 타이머뿐 아니라 공통 이동도 진행하지 않습니다.
                SetState(attack, states[0]);
                game.State = RunState.Paused;
                CheckDistance(enemy, game, 0, "Pause freezes skill movement");
                game.State = RunState.Combat;
            }
            finally { UnityEngine.Object.DestroyImmediate(enemy.gameObject); }
        }

        [MenuItem("Western Lemegeton/Tests/Check Slow Movement Skills")]
        public static void Run()
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var previous = SceneManager.GetActiveScene();
            var previousGame = Dungeon.I; var obstacles = Dungeon.Obstacles; var origin = Rules.RoomOrigin;
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                checks = 0;
                var game = new GameObject("Slow movement test").AddComponent<Dungeon>();
                Dungeon.I = game; game.State = RunState.Combat;
                game.Hero = new GameObject("Hunter").AddComponent<Player>(); game.Hero.Hp = 1000;
                game.Hero.transform.position = Vector2.right * 10;
                Dungeon.Obstacles = Array.Empty<Rect>(); Rules.RoomOrigin = Vector2.zero;
                CheckSkill(game, true, true);
                CheckSkill(game, true, false);
                CheckSkill(game, false, true);
                CheckSkill(game, false, false);
                CheckPoisonTransition(game);
                Debug.Log("SLOW_MOVEMENT_CHECKS_PASSED: " + checks);
            }
            finally
            {
                Dungeon.I = previousGame; Dungeon.Obstacles = obstacles; Rules.RoomOrigin = origin;
                UnityEngine.Random.state = random;
                SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true);
            }
        }

        // 실제 독병 조준 진입과 투척을 진행해 이동하면서도 목표가 고정되고 후딜 종료 시 감속이 풀리는지 확인합니다.
        static void CheckPoisonTransition(Dungeon game)
        {
            var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>(RobeManSetup.PrefabPath));
            enemy.Init(enemy.Kind, 0); enemy.transform.position = Vector2.zero;
            var attack = enemy.GetComponent<RobeManAttack>();
            var brain = enemy.GetComponent<EnemyBasicBrain>();
            var movement = enemy.GetComponent<EnemyChaseMovement>();
            movement.Speed = 2; movement.SeparationStrength = 0;
            attack.RequiredStaySeconds = 0; attack.DetectionRadius = 20; attack.MeleeRange = 0;
            attack.PoisonMoveMultiplier = .25f; attack.AimDelay = .2f; attack.ThrowRecovery = .2f;
            brain.Tick(game, 1, .1f);
            Check(attack.CurrentPhase == RobeManAttack.Phase.Aim && Mathf.Abs(enemy.transform.position.x - .05f) < .0001f,
                "Entering poison aim immediately applies slow movement");
            var target = attack.LockedTarget;
            game.Hero.transform.position = Vector2.right * 8;
            brain.Tick(game, 1, .21f);
            var bottles = UnityEngine.Object.FindObjectsByType<RobeManPoisonBottle>(FindObjectsSortMode.None);
            Check(bottles.Length == 1 && bottles[0].Target == target && attack.CurrentPhase == RobeManAttack.Phase.ThrowRecovery,
                "Moving throw keeps the original locked target");
            float before = enemy.transform.position.x;
            brain.Tick(game, 1, .1f);
            Check(Mathf.Abs(enemy.transform.position.x - before - .05f) < .0001f, "Throw recovery retains skill slow");
            before = enemy.transform.position.x;
            brain.Tick(game, 1, .11f);
            Check(attack.CurrentPhase == RobeManAttack.Phase.Chase && Mathf.Abs(enemy.transform.position.x - before - .22f) < .0001f && bottles[0],
                "Recovery end restores movement while bottle still exists");
        }
    }
}
