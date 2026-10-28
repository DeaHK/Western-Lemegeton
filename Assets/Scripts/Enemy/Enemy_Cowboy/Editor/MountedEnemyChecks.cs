using System;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WesternLemegeton.Stigmas;

namespace WesternLemegeton.EditorTools
{
    public static class MountedEnemyChecks
    {
        // 공통 검사 도우미: 실패 보고, 적 생성, 예약된 적 정리와 공격 준비 진행을 제공합니다.
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static int count;
        static void Check(bool value, string name)
        { if (!value) throw new Exception("MOUNTED_ENEMY_FAILED: " + name); count++; }
        static void Flush(Dungeon game) => typeof(Dungeon).GetMethod("RemoveDefeatedEnemies", Private).Invoke(game, null);
        static Enemy Make(string path, Dungeon game)
        {
            var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>(path));
            enemy.Init(enemy.Kind, 0); enemy.transform.position = Vector2.zero; game.Enemies.Add(enemy); return enemy;
        }
        static void WindUp(HauntedBullAttack bull, Dungeon game)
        { bull.Tick(game, 1, 0); bull.Tick(game, 1, bull.StompSeconds + .01f); bull.Tick(game, 1, bull.LockSeconds + .01f); }

        // 원본 원거리 모듈과 같은 설정으로 실제 탄환을 비교하고, 준비 중 수치 변경과 탑승 사격을 확인합니다.
        static void CheckCowboyProjectiles(Dungeon game)
        {
            var enemy = Make(MountedEnemySetup.CowboyPath, game);
            var cowboy = enemy.GetComponent<CowboyAttack>();
            var reference = Make("Assets/Editable/Actors/Enemy_2.prefab", game).GetComponent<EnemyRangedAttack>();
            cowboy.ShootRange = reference.TriggerRange = 8;
            cowboy.ShotWindup = reference.WindupSeconds = .2f;
            cowboy.ShotCooldown = reference.CooldownSeconds = .7f;
            cowboy.ShotDamage = reference.Damage = 23;
            cowboy.BulletSpeed = reference.ProjectileSpeed = 9;
            cowboy.ProjectileLifetime = reference.ProjectileLifetime = 2.5f;
            cowboy.SpreadAngle = reference.SpreadAngle = 14;
            cowboy.WarningRadius = reference.WarningRadius = 1.3f;
            cowboy.InitialCooldownRange = reference.InitialCooldownRange = new Vector2(.25f, .25f);
            foreach (int count in new[] { 1, 4, 5 })
            {
                cowboy.ProjectileCount = reference.ProjectileCount = count;
                cowboy.Initialize(enemy); reference.Initialize(reference.GetComponent<Enemy>());
                game.Hero.transform.position = Vector2.right * 4;
                var before = UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.InstanceID);
                cowboy.Tick(game, 1, .1f); reference.Tick(game, 1, .1f);
                Check(cowboy.Action == CowboyAttack.ActionState.Shoot &&
                    UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length == before.Length,
                    "Configured initial cooldown delays shooting");
                cowboy.Tick(game, 1, .16f); reference.Tick(game, 1, .16f);
                Check(enemy.AuthoredWarning.enabled && Mathf.Abs(enemy.AuthoredWarning.transform.localScale.x - 2.6f) < .001f,
                    "Configured shot warning radius is used");
                game.Hero.transform.position = Vector2.up * 4;
                reference.Tick(game, 1, .21f);
                var referenceShots = UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.InstanceID).Except(before).ToArray();
                var beforeCowboy = UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.InstanceID);
                cowboy.Tick(game, 1, .21f);
                var shots = UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.InstanceID).Except(beforeCowboy).ToArray();
                Check(shots.Length == count && referenceShots.Length == count, "Cowboy configurable projectile count matches ranged module");
                for (int i = 0; i < shots.Length; i++)
                {
                    foreach (string field in new[] { "velocity", "damage", "life", "hostile" })
                    {
                        var data = typeof(Bullet).GetField(field, Private);
                        Check(data.GetValue(shots[i]).Equals(data.GetValue(referenceShots[i])),
                            "Cowboy matches ranged projectile " + field + " for count " + count);
                    }
                }
                cowboy.Tick(game, 1, .69f);
                Check(cowboy.Action == CowboyAttack.ActionState.Shoot, "Custom shot cooldown is retained");
                cowboy.Tick(game, 1, .02f);
                Check(cowboy.Action == CowboyAttack.ActionState.ShotWindup, "Custom cooldown permits next shot");
            }

            // 재초기화 없이 준비 도중 바꾼 수치는 다음 발사에 적용합니다.
            cowboy.ProjectileCount = 2; cowboy.ShotDamage = 31; cowboy.BulletSpeed = 11;
            cowboy.ProjectileLifetime = 6; cowboy.SpreadAngle = 20;
            var existing = UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.InstanceID);
            cowboy.Tick(game, 1, .21f);
            var editedShots = UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.InstanceID).Except(existing).ToArray();
            Check(editedShots.Length == 2, "Changing count during windup affects the next volley");
            foreach (var shot in editedShots)
            {
                Check((float)typeof(Bullet).GetField("damage", Private).GetValue(shot) == 31 &&
                    (float)typeof(Bullet).GetField("life", Private).GetValue(shot) == 6 &&
                    Mathf.Abs(((Vector2)typeof(Bullet).GetField("velocity", Private).GetValue(shot)).magnitude - 11) < .001f,
                    "Changing damage, lifetime and speed during windup affects the next volley");
            }

            var bull = Make(MountedEnemySetup.BullPath, game).GetComponent<HauntedBullAttack>();
            Check(bull.Mount(cowboy), "Configured Cowboy can still mount bull");
            existing = UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.InstanceID);
            bull.Tick(game, 1, 0); bull.Tick(game, 1, 0);
            var mountedShots = UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.InstanceID).Except(existing).ToArray();
            Check(mountedShots.Length == 2 && mountedShots.All(shot =>
                (float)typeof(Bullet).GetField("damage", Private).GetValue(shot) == 31),
                "Mounted shooting uses the same configurable volley");
        }

        // Wild/Ride 범위를 다르게 설정해 시작 거리·표시 크기·접촉 피해와 탑승/충돌 전환을 검증합니다.
        static void CheckBullRanges(Dungeon game)
        {
            Dungeon.Obstacles = Array.Empty<Rect>();
            var enemy = Make(MountedEnemySetup.BullPath, game);
            var bull = enemy.GetComponent<HauntedBullAttack>();
            var cowboy = Make(MountedEnemySetup.CowboyPath, game).GetComponent<CowboyAttack>();
            var warning = enemy.AuthoredWarning;
            bull.ChargeCooldown = 0;
            bull.WildTriggerRange = 2; bull.RideTriggerRange = 4;
            bull.WildHitRadius = .5f; bull.RideHitRadius = 1.5f;
            bull.WildWarningLength = 9; bull.RideWarningLength = 7;
            game.Hero.transform.position = Vector2.right * 3;
            bull.Tick(game, 1, 0);
            Check(bull.Action == HauntedBullAttack.ActionState.Chase && bull.AttackRange == 2,
                "Wild uses its own trigger range");
            game.Hero.transform.position = Vector2.up;
            bull.Tick(game, 1, 0);
            Check(warning.enabled && warning.sprite == Ink.Square && warning.transform.localScale == new Vector3(9, 1, 1),
                "Wild warning is a rectangle with configured length and hit diameter");
            Check(Vector2.Distance(warning.transform.position, Vector2.up * 4.5f) < .001f &&
                Mathf.Abs(Mathf.DeltaAngle(warning.transform.eulerAngles.z, 90)) < .001f,
                "Rectangle starts at bull and rotates toward aim");
            bull.Tick(game, 1, bull.StompSeconds + .01f);
            bull.WildWarningLength = 10;
            game.Hero.transform.position = Vector2.right;
            bull.Tick(game, 1, 0);
            Check(bull.Aim.y > .99f && warning.transform.localScale.x == 10,
                "Wild locked aim stays fixed while edited size updates");
            bull.Tick(game, 1, bull.LockSeconds + .01f);
            typeof(Player).GetField("invulnerable", Private).SetValue(game.Hero, 0f);
            float hp = game.Hero.Hp;
            game.Hero.transform.position = Vector2.right * .8f;
            bull.Tick(game, 1, 0);
            Check(game.Hero.Hp == hp, "Wild excludes target outside configured contact radius");
            game.Hero.transform.position = Vector2.right * .4f;
            bull.Tick(game, 1, 0);
            Check(Mathf.Approximately(game.Hero.Hp, hp - bull.Damage), "Wild configured contact radius deals damage");
            bull.Cancel(); game.Hero.transform.position = Vector2.right;
            bull.Tick(game, 1, 0);
            bull.Tick(game, 1, bull.StompSeconds + .01f);
            Check(bull.Mount(cowboy), "Mount during locked warning succeeds");
            Check(bull.AttackRange == 4 && warning.sprite == Ink.Square && warning.transform.localScale == new Vector3(7, 3, 1),
                "Mount immediately switches warning and range to Ride values");
            bull.Cancel(); game.Hero.transform.position = Vector2.right * 3;
            WindUp(bull, game);
            Check(bull.Action == HauntedBullAttack.ActionState.Charge, "Ride starts attack beyond Wild trigger range");
            typeof(Player).GetField("invulnerable", Private).SetValue(game.Hero, 0f);
            hp = game.Hero.Hp; game.Hero.transform.position = Vector2.up * 1.2f;
            bull.Tick(game, 1, 0);
            Check(Mathf.Approximately(game.Hero.Hp, hp - bull.Damage), "Ride uses its independent contact radius");
            Dungeon.Obstacles = new[] { new Rect(.55f, -2, .2f, 4) };
            bull.Tick(game, 1, .1f);
            Check(bull.State == HauntedBullAttack.BullState.Wild && warning.sprite == Ink.Disc,
                "Crash restores Wild state and round stun indicator");
            Dungeon.Obstacles = Array.Empty<Rect>();
            bull.Tick(game, 1, bull.StunSeconds + .01f);
            game.Hero.transform.position = enemy.transform.position + Vector3.right;
            bull.Tick(game, 1, 0);
            Check(warning.sprite == Ink.Square && warning.transform.localScale == new Vector3(10, 1, 1),
                "Next Wild attack restores rectangular warning and Wild settings");
        }

        // 임시 씬에서 황소 돌진·Cowboy 사격/올가미·탑승·구속 해제를 검증한 뒤 원래 환경을 복원합니다.
        [MenuItem("Western Lemegeton/Tests/Check HauntedBull and Cowboy")]
        public static void Run()
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var previous = SceneManager.GetActiveScene(); var previousGame = Dungeon.I;
            var obstacles = Dungeon.Obstacles; var origin = Rules.RoomOrigin; var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            AudioClip clip = null;
            try
            {
                // 실제 전투를 대신할 게임·플레이어·소리를 준비하고 두 프리팹을 초기화합니다.
                count = 0; MountedEnemySetup.Create();
                var game = new GameObject("Test game").AddComponent<Dungeon>(); Dungeon.I = game;
                game.State = RunState.Combat;
                game.Hero = new GameObject("Hunter").AddComponent<Player>(); game.Hero.Hp = 1000;
                var audio = game.gameObject.AddComponent<AudioSource>(); clip = AudioClip.Create("Silent hit", 64, 1, 44100, false);
                typeof(Dungeon).GetField("sound", Private).SetValue(game, audio);
                typeof(Dungeon).GetField("hitClip", Private).SetValue(game, clip);
                Rules.RoomOrigin = Vector2.zero; Dungeon.Obstacles = Array.Empty<Rect>();
                var bullEnemy = Make(MountedEnemySetup.BullPath, game); var bull = bullEnemy.GetComponent<HauntedBullAttack>();
                var cowboyEnemy = Make(MountedEnemySetup.CowboyPath, game); var cowboy = cowboyEnemy.GetComponent<CowboyAttack>();
                cowboyEnemy.transform.position = Vector2.left * 10;
                Check(bull.State == HauntedBullAttack.BullState.Wild, "Bull spawns Wild");
                game.Hero.transform.position = Vector2.right * 10;
                bullEnemy.GetComponent<EnemyBasicBrain>().Tick(game, 1, .1f);
                Check(bullEnemy.transform.position.x > 0, "Wild bull chases Hunter");
                bullEnemy.transform.position = Vector2.zero; game.Hero.transform.position = Vector2.right * 4;
                bull.Tick(game, 1, 0);
                Check(bull.Action == HauntedBullAttack.ActionState.Stomp, "Nearby Hunter starts stomp");
                game.Hero.transform.position = Vector2.up * 4;
                bull.Tick(game, 1, 2.9f);
                Check(bull.Action == HauntedBullAttack.ActionState.Stomp && bull.Aim.y > .99f, "Three-second stomp tracks Hunter");
                bull.Tick(game, 1, .11f); var locked = bull.Aim;
                game.Hero.transform.position = Vector2.left * 8;
                bull.Tick(game, 1, .9f);
                Check(bull.Action == HauntedBullAttack.ActionState.Lock && bull.Aim == locked, "One-second lock no longer tracks");
                bull.Tick(game, 1, .11f);
                Check(bull.Action == HauntedBullAttack.ActionState.Charge, "Lock transitions into charge");
                Dungeon.Obstacles = new[] { new Rect(-2, 3, 4, .3f) };
                bull.Tick(game, 1, 2);
                Check(bull.Action == HauntedBullAttack.ActionState.Stunned && bullEnemy.transform.position.y < 2,
                    "Swept charge hits thin obstacle and recoils");
                bull.Tick(game, 1, 2.9f);
                Check(bull.Action == HauntedBullAttack.ActionState.Stunned, "Impact stun lasts three seconds");
                bull.Tick(game, 1, .11f);
                Check(bull.Action == HauntedBullAttack.ActionState.Chase, "Bull resumes after stun");
                Dungeon.Obstacles = Array.Empty<Rect>(); bullEnemy.transform.position = Vector2.zero;
                cowboyEnemy.Hp = 37;
                Check(bull.Mount(cowboy), "Wild bull accepts Cowboy");
                Check(bull.State == HauntedBullAttack.BullState.Ride && bull.RiderHealth == 37 &&
                    !cowboyEnemy.gameObject.activeSelf && !game.Enemies.Contains(cowboyEnemy), "Mount stores current health and hides independent rider");
                cowboyEnemy.TakeStigmaDamage(1000, StigmaOwner.Raven, false);
                Check(cowboyEnemy.Hp == 37, "Stale Raven target cannot damage the hidden mounted Cowboy");
                game.Hero.transform.position = Vector2.right * 5;
                bull.RideDistance = 2; bull.ChargeCooldown = 0;
                int shots = UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length;
                bull.Tick(game, 1, 0); bull.Tick(game, 1, bull.StompSeconds + .01f);
                game.Hero.transform.position = Vector2.up * 5;
                bull.Tick(game, 1, .9f);
                Check(bull.Action == HauntedBullAttack.ActionState.Lock && bull.Aim.y > .99f,
                    "Ride keeps tracking during final preparation second");
                game.Hero.transform.position = Vector2.right * 5;
                bull.Tick(game, 1, .11f);
                Check(bull.Action == HauntedBullAttack.ActionState.Charge && bull.Aim.x > .99f,
                    "Ride uses Hunter position at the end of preparation");
                Check(UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length > shots, "Mounted rider fires during stomp");
                bull.Tick(game, 1, 1);
                Check(bull.Action == HauntedBullAttack.ActionState.Chase && Mathf.Abs(bullEnemy.transform.position.x - 2) < .01f &&
                    bull.State == HauntedBullAttack.BullState.Ride, "Ride charge stops at configured distance without dismount");
                bullEnemy.transform.position = Vector2.right * 13; game.Hero.transform.position = Vector2.right * 14;
                bull.RideDistance = 8; WindUp(bull, game); bull.Tick(game, 1, 1);
                Flush(game);
                Check(bull.State == HauntedBullAttack.BullState.Wild && bull.Action == HauntedBullAttack.ActionState.Stunned,
                    "Wall crash returns mounted bull to Wild and stuns it");
                Check(cowboyEnemy.gameObject.activeSelf && cowboyEnemy.Hp == 37 && game.Enemies.Contains(cowboyEnemy) &&
                    cowboy.Action == CowboyAttack.ActionState.Stunned, "Crash releases rider with preserved health and stun");
                cowboy.Tick(game, 1, 2.9f);
                Check(cowboy.Action == CowboyAttack.ActionState.Stunned, "Released rider remains stunned for full duration");
                cowboy.Tick(game, 1, .11f);
                Check(cowboy.Action == CowboyAttack.ActionState.Shoot, "Released rider resumes");
                bull.Mount(cowboy);
                var died = (Action<Enemy>)typeof(Enemy).GetField("Dying", Private).GetValue(bullEnemy);
                died.Invoke(bullEnemy); bullEnemy.Hp = 0; Flush(game);
                Check(cowboyEnemy.Hp == 37 && cowboyEnemy.gameObject.activeSelf && game.Enemies.Contains(cowboyEnemy),
                    "Enemy death callback releases rider before wave-empty check");

                bullEnemy.Hp = 120; game.Enemies.Add(bullEnemy); bull.Initialize(bullEnemy);
                bullEnemy.transform.position = Vector2.left * 12;
                cowboy.Initialize(cowboyEnemy); cowboyEnemy.transform.position = Vector2.zero;
                game.Hero.transform.position = Vector2.right * 4; cowboy.Tick(game, 1, 0);
                Check(cowboy.Action == CowboyAttack.ActionState.ShotWindup, "Cowboy pauses before shooting");
                game.Hero.transform.position = Vector2.right * 10;
                cowboy.Tick(game, 1, 0);
                Check(cowboy.Action == CowboyAttack.ActionState.Spin && cowboy.Slowed, "Exiting attack range interrupts shot and starts slowed spin");
                var spin = (SpriteRenderer)typeof(CowboyAttack).GetField("spinVisual", Private).GetValue(cowboy);
                Check(spin && spin.transform.localScale.x < 1 && spin.color.a == 1 &&
                    spin.sprite.texture.GetPixel(32, 32).a == 0, "Spin uses small hollow ring instead of range disc");
                Vector3 spinStart = spin.transform.localPosition;
                cowboy.Tick(game, 1, .1f);
                Check(Vector3.Distance(spinStart, spin.transform.localPosition) > .1f,
                    "Preparing lasso orbits around Cowboy");
                cowboyEnemy.GetComponent<EnemyBasicBrain>().Tick(game, 1, 1);
                Check(Mathf.Abs(cowboyEnemy.transform.position.x - .77f) < .001f, "Lasso preparation applies configured movement slow");
                cowboy.Tick(game, 1, 1.8f);
                Check(cowboy.Action == CowboyAttack.ActionState.Spin, "Lasso spins for three seconds");
                cowboy.Tick(game, 1, .11f);
                Check(cowboy.Action == CowboyAttack.ActionState.LassoOut, "Spin ends by throwing lasso");
                Check(!spin, "Overhead ring is removed when lasso is thrown");
                cowboy.Tick(game, 1, 1);
                Check(game.Hero.Tether && cowboy.Action == CowboyAttack.ActionState.Shoot, "Hunter catch creates rope and resumes shooting");
                var rope = game.Hero.Tether; Flush(game);
                float before = game.Hero.transform.position.x; rope.Pull(1);
                Check(game.Hero.transform.position.x < before && game.Hero.transform.position.x > cowboyEnemy.transform.position.x,
                    "Rope slowly pulls Hunter toward Cowboy");
                Check(game.Enemies.Contains(rope.Targets[0]) && rope.Targets[0].AuxiliaryTarget, "Rope is registered as attackable enemy segments");
                int kills = game.Kills;
                rope.Targets[0].TakeStigmaDamage(1, StigmaOwner.Player, false);
                Check(!game.Hero.Tether && game.Kills == kills, "Hunter damage cuts rope without kill rewards");
                rope = CowboyRope.Bind(cowboy, game.Hero, 1, 18); Flush(game);
                rope.Targets[1].TakeStigmaDamage(1, StigmaOwner.Raven, false);
                Check(!game.Hero.Tether, "Raven damage also cuts rope");

                cowboy.Initialize(cowboyEnemy); cowboyEnemy.transform.position = Vector2.zero;
                game.Hero.transform.position = Vector2.right * 4; cowboy.Tick(game, 1, 0);
                game.Hero.transform.position = Vector2.right * 10; cowboy.Tick(game, 1, 0);
                game.State = RunState.Paused; cowboy.Tick(game, 1, 30);
                Check(cowboy.Action == CowboyAttack.ActionState.Spin, "Pause freezes lasso preparation");
                game.State = RunState.Combat; cowboy.Tick(game, 1, 3.01f);
                game.Hero.transform.position = Vector2.up * 8;
                cowboy.Tick(game, 1, 2);
                Check(cowboy.Action == CowboyAttack.ActionState.LassoReturn, "Missed lasso returns at maximum range");
                cowboy.Tick(game, 1, 2);
                Check(cowboy.Action == CowboyAttack.ActionState.Shoot, "Completed retrieval resumes shooting state");
                rope = CowboyRope.Bind(cowboy, game.Hero, 1, 18); Flush(game);
                cowboy.Cancel();
                Check(!game.Hero.Tether, "Cancel cleans up rope and restores Hunter movement");

                cowboy.Initialize(cowboyEnemy); cowboyEnemy.transform.position = Vector2.zero;
                bullEnemy.transform.position = Vector2.right * 3;
                game.Hero.transform.position = Vector2.right * 4; cowboy.Tick(game, 1, 0);
                game.Hero.transform.position = Vector2.right * 10; cowboy.Tick(game, 1, 0); cowboy.Tick(game, 1, 3.01f);
                cowboy.Tick(game, 1, .4f);
                Check(cowboy.Action == CowboyAttack.ActionState.ApproachBull, "Lasso selects nearer Wild bull instead of Hunter");
                cowboy.Tick(game, 1, .4f);
                Check(bull.State == HauntedBullAttack.BullState.Ride && cowboy.Action == CowboyAttack.ActionState.Mounted,
                    "Cowboy rapidly reaches captured bull and mounts");
                // 저장된 씬의 생성 슬롯 연결과 추가된 상태별 범위 검사를 확인합니다.
                foreach (var root in previous.GetRootGameObjects())
                {
                    var bindings = root.GetComponentInChildren<GameSceneBindings>(true);
                    if (!bindings) continue;
                    Check(bindings.EnemyPrefabs[1] && bindings.EnemyPrefabs[1].GetComponent<HauntedBullAttack>() &&
                        bindings.EnemyPrefabs[2] && bindings.EnemyPrefabs[2].GetComponent<CowboyAttack>(), "Main scene resolves new spawn prefab references");
                }
                CheckBullRanges(game);
                CheckCowboyProjectiles(game);
                Debug.Log("MOUNTED_ENEMY_CHECKS_PASSED: " + count);
            }
            // 검사 실패 시에도 전역 참조·난수·장애물을 복원하고 임시 씬을 닫습니다.
            finally
            {
                if (clip) UnityEngine.Object.DestroyImmediate(clip);
                Dungeon.I = previousGame; Dungeon.Obstacles = obstacles; Rules.RoomOrigin = origin; UnityEngine.Random.state = random;
                SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
