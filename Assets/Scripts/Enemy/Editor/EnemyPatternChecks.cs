using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WesternLemegeton.EditorTools
{
    // 기존 Enemy의 실제 행동 경로와 새 블록을 같은 입력으로 프레임마다 비교합니다.
    public static class EnemyPatternChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static int checks;
        static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("ENEMY_PATTERN_FAILED: " + message);
            checks++;
        }
        static void Near(float a, float b, string label) => Check(Mathf.Abs(a - b) < .00001f, label + ": " + a + " / " + b);
        static void Near(Vector3 a, Vector3 b, string label) => Check((a - b).sqrMagnitude < .00000001f, label + ": " + a + " / " + b);

        struct Shot
        {
            public Vector3 Position;
            public Vector2 Velocity;
            public float Damage, Life;
            public bool Hostile;
        }
        static List<Shot> CollectShots()
        {
            var result = new List<Shot>();
            foreach (var bullet in UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.InstanceID))
            {
                if (bullet.gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) continue;
                result.Add(new Shot { Position = bullet.transform.position, Velocity = Read<Vector2>(bullet, "velocity"),
                    Damage = Read<float>(bullet, "damage"), Life = Read<float>(bullet, "life"), Hostile = Read<bool>(bullet, "hostile") });
                UnityEngine.Object.DestroyImmediate(bullet.gameObject);
            }
            return result;
        }

        public static void Run(Dungeon game)
        {
            checks = 0;
            for (int kind = 1; kind <= 3; kind++) ComparePattern(game, kind);
            CheckReplacement(game);
            for (int kind = 1; kind <= 2; kind++) CheckAttackRangeFollowing(game, kind);
            CheckBossOptions(game);
            Debug.Log("ENEMY_PATTERN_CHECKS_PASSED: " + checks);
        }

        static void ComparePattern(Dungeon game, int kind)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/" + (kind == 3 ? "Boss" : "Enemy_" + kind) + ".prefab");
            var modular = UnityEngine.Object.Instantiate(prefab);
            var legacy = UnityEngine.Object.Instantiate(prefab);
            UnityEngine.Object.DestroyImmediate(legacy.GetComponent<EnemyBasicBrain>());
            UnityEngine.Object.DestroyImmediate(legacy.GetComponent<EnemyAttackModule>());
            UnityEngine.Object.DestroyImmediate(legacy.GetComponent<EnemyMovementModule>());
            UnityEngine.Object.DestroyImmediate(legacy.GetComponent<EnemyHealth>());
            var brain = modular.GetComponent<EnemyBasicBrain>();
            var attack = modular.GetComponent<EnemyAttackModule>();
            Check(brain && modular.GetComponent<EnemyKeepDistanceMovement>() && attack, "Saved prefab has modules: " + kind);
            Check(kind == 1 ? attack is EnemyChargeAttack : kind == 2 ? attack is EnemyRangedAttack : attack is EnemyBossAttack, "Correct attack module: " + kind);
            if (attack is EnemyBossAttack boss)
                Check(!boss.EnableMelee && boss.EnableCharge && boss.EnableRanged, "Boss prefab retains ranged/ranged/charge configuration");

            UnityEngine.Random.InitState(1729 + kind);
            legacy.Init(kind, 2);
            float expectedNextRandom = UnityEngine.Random.value;
            UnityEngine.Random.InitState(1729 + kind);
            modular.Init(kind, 2);
            Near(UnityEngine.Random.value, expectedNextRandom, "Initialization preserves random sequence");
            Near(modular.Hp, legacy.Hp, "Health");
            Near(modular.AttackPower, legacy.AttackPower, "Raven attack power");
            Near(Read<float>(attack, "cooldown"), Read<float>(legacy, "cooldown"), "Initial random cooldown");

            var neighbour = new GameObject("Separation neighbour").AddComponent<Enemy>();
            neighbour.Hp = 100;
            int shotCount = 0, chargeFrames = 0, damageFrames = 0;
            var tick = typeof(Enemy).GetMethod("TickLegacyBehavior", Private);
            CollectShots();
            // 이동 경계, 추적, 후퇴, 고정 조준, 장애물, 감속, 공격 후 재시작을 포함합니다.
            for (int frame = 0; frame < (kind == 3 ? 840 : 420); frame++)
            {
                if (kind == 3 && (frame == 140 || frame == 280 || frame == 420))
                {
                    float hp = frame == 140 ? 325 : frame == 280 ? 324 : 650;
                    legacy.Hp = modular.Hp = hp;
                }
                float dt = new[] { .016f, .08f, .21f, .033f }[frame % 4];
                float targetDistance = kind == 1 ? 3 : 5;
                if (frame < 30) targetDistance = new[] { 10f, 8f, 6.4f, 6f, 4.8f, 4f, 3.99f, 0f }[frame % 8];
                if (kind != 2 && Read<float>(attack, "chargeTime") > 0)
                {
                    targetDistance = .9f;
                    chargeFrames++;
                }
                game.Hero.transform.position = modular.transform.position +
                    (frame % 5 == 0 ? Vector3.up : Vector3.right) * targetDistance;
                neighbour.transform.position = modular.transform.position + new Vector3(.7f, .2f);
                Dungeon.Obstacles = frame >= 160 && frame < 260 ? new[] { new Rect(1, -2, 1, 4) } : Array.Empty<Rect>();
                if (frame == 100)
                {
                    legacy.Stigma.Debuff(.35f, 8, false);
                    modular.Stigma.Debuff(.35f, 8, false);
                }
                legacy.Stigma.Tick(dt);
                modular.Stigma.Tick(dt);
                // Enemy.Update에서 계산하는 감속 배율도 같은 상태에서 비교합니다.
                float multiplier = (float)typeof(Enemy).GetProperty("StigmaMoveMultiplier", Private).GetValue(legacy);
                var frameRandom = UnityEngine.Random.state;
                game.Enemies.Clear(); game.Enemies.Add(legacy);
                if (frame < 30 || frame >= 160) game.Enemies.Add(neighbour);
                game.Hero.Hp = 1000; Set(game.Hero, "invulnerable", 0f);
                tick.Invoke(legacy, new object[] { game, dt });
                float expectedHp = game.Hero.Hp;
                var expectedShots = CollectShots();
                UnityEngine.Random.state = frameRandom;
                game.Enemies.Clear(); game.Enemies.Add(modular);
                if (frame < 30 || frame >= 160) game.Enemies.Add(neighbour);
                game.Hero.Hp = 1000; Set(game.Hero, "invulnerable", 0f);
                brain.Tick(game, multiplier, dt);
                var actualShots = CollectShots();
                string label = "kind=" + kind + " frame=" + frame;
                Near(modular.transform.position, legacy.transform.position, "Position " + label);
                Near(game.Hero.Hp, expectedHp, "Contact damage " + label);
                if (expectedHp < 1000) damageFrames++;
                Near(Read<float>(attack, "cooldown"), Read<float>(legacy, "cooldown"), "Cooldown " + label);
                Near(Read<float>(attack, "windup"), Read<float>(legacy, "windup"), "Windup " + label);
                Check(modular.Telegraphing == legacy.Telegraphing, "Telegraphing " + label);
                Check(modular.AuthoredWarning.enabled == legacy.AuthoredWarning.enabled, "Warning visibility " + label);
                Near(modular.AuthoredWarning.transform.position, legacy.AuthoredWarning.transform.position, "Warning position " + label);
                Near(modular.AuthoredWarning.transform.localScale, legacy.AuthoredWarning.transform.localScale, "Warning scale " + label);
                Near(Quaternion.Angle(modular.AuthoredWarning.transform.rotation, legacy.AuthoredWarning.transform.rotation), 0, "Warning angle " + label);
                if (kind != 2) Near(Read<float>(attack, "chargeTime"), Read<float>(legacy, "chargeTime"), "Charge duration " + label);
                if (kind == 3) Check(Read<int>(attack, "cycle") == Read<int>(legacy, "cycle"), "Boss cycle " + label);
                Check(actualShots.Count == expectedShots.Count, "Shot count " + label);
                for (int i = 0; i < actualShots.Count; i++)
                {
                    Near(actualShots[i].Position, expectedShots[i].Position, "Shot origin " + label);
                    Near(actualShots[i].Velocity, expectedShots[i].Velocity, "Shot aim and speed " + label);
                    Near(actualShots[i].Damage, expectedShots[i].Damage, "Shot damage " + label);
                    Near(actualShots[i].Life, expectedShots[i].Life, "Shot lifetime " + label);
                    Check(actualShots[i].Hostile == expectedShots[i].Hostile, "Shot allegiance " + label);
                }
                shotCount += actualShots.Count;
            }
            Check(kind == 1 ? chargeFrames > 5 && damageFrames > 0 : kind == 2 ? shotCount >= 9 :
                chargeFrames > 5 && damageFrames > 0 && shotCount >= 36, "Repeated pattern exercised: " + kind);

            // Inspector 값 보존, 공격 중 정지, 비활성화 시 준비/돌진 취소.
            var health = modular.GetComponent<EnemyHealth>();
            health.BaseHealth = 120; health.HealthPerRoom = 7;
            modular.Init(kind, 3);
            Near(modular.MaxHp, 141, "Custom health survives initialization");
            Set(attack, "windup", .5f);
            game.State = RunState.Paused;
            typeof(Enemy).GetMethod("Update", Private).Invoke(modular, null);
            Near(Read<float>(attack, "windup"), .5f, "Paused attack does not advance");
            game.State = RunState.Combat;
            if (kind != 2) Set(attack, "chargeTime", .3f);
            attack.enabled = false;
            attack.GetType().GetMethod("OnDisable", Private).Invoke(attack, null);
            Check(!modular.Telegraphing && !modular.AuthoredWarning.enabled && modular.AttackPower == 0, "Disabled attack cancels warning");
            if (kind != 2) Near(Read<float>(attack, "chargeTime"), 0, "Disabled charge cancels motion");
            game.Enemies.Clear();
            Dungeon.Obstacles = Array.Empty<Rect>();
            UnityEngine.Object.DestroyImmediate(neighbour.gameObject);
            UnityEngine.Object.DestroyImmediate(legacy.gameObject);
            UnityEngine.Object.DestroyImmediate(modular.gameObject);
        }

        static void CheckBossOptions(Dungeon game)
        {
            var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/Boss.prefab"));
            var attack = enemy.GetComponent<EnemyBossAttack>();
            var brain = enemy.GetComponent<EnemyBasicBrain>();
            attack.InitialCooldownRange = Vector2.zero;
            Dungeon.Obstacles = Array.Empty<Rect>();
            game.Enemies.Clear(); game.Enemies.Add(enemy);
            try
            {
                for (int mask = 0; mask < 8; mask++)
                {
                    attack.EnableMelee = (mask & 1) != 0;
                    attack.EnableCharge = (mask & 2) != 0;
                    attack.EnableRanged = (mask & 4) != 0;
                    enemy.Init(3, 0);
                    enemy.transform.position = Vector3.zero;
                    var starts = new List<EnemyBossAttack.Pattern>();
                    bool meleeHit = false, chargeHit = false;
                    int shots = 0;
                    for (int frame = 0; frame < 180; frame++)
                    {
                        game.Hero.transform.position = enemy.transform.position + Vector3.right;
                        game.Hero.Hp = 1000; Set(game.Hero, "invulnerable", 0f);
                        float beforeWindup = Read<float>(attack, "windup");
                        var beforePattern = Read<EnemyBossAttack.Pattern>(attack, "activePattern");
                        brain.Tick(game, 1, .1f);
                        if (beforeWindup < 0 && Read<float>(attack, "windup") >= 0)
                            starts.Add(Read<EnemyBossAttack.Pattern>(attack, "activePattern"));
                        if (game.Hero.Hp < 1000)
                        {
                            if (beforePattern == EnemyBossAttack.Pattern.Melee)
                            { meleeHit = true; Near(game.Hero.Hp, 1000 - attack.Melee.Damage, "Boss melee configured damage"); }
                            if (beforePattern == EnemyBossAttack.Pattern.Charge)
                            { chargeHit = true; Near(game.Hero.Hp, 1000 - attack.Charge.Damage, "Boss charge configured damage"); }
                        }
                        shots += CollectShots().Count;
                    }
                    var expected = new List<EnemyBossAttack.Pattern>();
                    if (attack.EnableRanged) { expected.Add(EnemyBossAttack.Pattern.Ranged); expected.Add(EnemyBossAttack.Pattern.Ranged); }
                    if (attack.EnableCharge) expected.Add(EnemyBossAttack.Pattern.Charge);
                    if (attack.EnableMelee) expected.Add(EnemyBossAttack.Pattern.Melee);
                    Check(expected.Count == 0 ? starts.Count == 0 : starts.Count >= expected.Count, "Enabled pattern cycle executes: " + mask);
                    for (int i = 0; i < starts.Count; i++)
                        Check(starts[i] == expected[i % expected.Count], "Disabled slots skipped in sequence: " + mask);
                    Check(meleeHit == attack.EnableMelee && chargeHit == attack.EnableCharge && (shots > 0) == attack.EnableRanged,
                        "Only enabled attacks deal damage or spawn projectiles: " + mask);
                    Near(enemy.AttackPower, mask == 0 ? 0 : 25, "Boss threat query with enabled patterns");
                }

                // 준비 도중 해당 종류를 끄면 발사/피해 없이 취소합니다.
                for (int bit = 1; bit <= 4; bit *= 2)
                {
                    attack.EnableMelee = bit == 1; attack.EnableCharge = bit == 2; attack.EnableRanged = bit == 4;
                    enemy.Init(3, 0);
                    game.Hero.transform.position = enemy.transform.position + Vector3.right;
                    game.Hero.Hp = 1000; Set(game.Hero, "invulnerable", 0f);
                    brain.Tick(game, 1, .01f);
                    Check(enemy.Telegraphing && enemy.AuthoredWarning.enabled, "Enabled boss pattern begins warning");
                    attack.EnableMelee = attack.EnableCharge = attack.EnableRanged = false;
                    brain.Tick(game, 1, 1);
                    Check(!enemy.Telegraphing && !enemy.AuthoredWarning.enabled && CollectShots().Count == 0 && game.Hero.Hp == 1000,
                        "Toggle cancels pending attack without damage");
                }

                attack.EnableCharge = true;
                enemy.Init(3, 0);
                game.Hero.transform.position = enemy.transform.position + Vector3.right;
                brain.Tick(game, 1, .01f); brain.Tick(game, 1, 1);
                Check(Read<float>(attack, "chargeTime") > 0, "Boss charge is active before toggle");
                var position = enemy.transform.position;
                attack.EnableCharge = false;
                brain.Tick(game, 1, .1f);
                Near(enemy.transform.position, position, "Disabling charge cancels its motion");
                Near(Read<float>(attack, "chargeTime"), 0, "Disabling charge clears remaining time");

                // 발사 시점의 체력으로 일반/강화 개수를 선택하고, 사용자 설정 탄환 수도 반영합니다.
                attack.EnableRanged = true;
                attack.Ranged.ProjectileCount = 7;
                attack.Ranged.LowHealthProjectileCount = 11;
                foreach (float hp in new[] { 650f, 325f, 324f })
                {
                    enemy.Init(3, 0);
                    game.Hero.transform.position = enemy.transform.position + Vector3.right;
                    brain.Tick(game, 1, .01f);
                    enemy.Hp = hp;
                    brain.Tick(game, 1, 1);
                    var shots = CollectShots();
                    Check(shots.Count == (hp < 325 ? 11 : 7), "Boss Inspector projectile count and strict half-health boundary");
                    foreach (var shot in shots)
                    {
                        Near(shot.Velocity.magnitude, hp < 325 ? 6 : 4.5f, "Boss projectile speed by health at execution");
                        Near(shot.Damage, 17, "Boss projectile damage");
                    }
                }

                attack.EnableRanged = false; attack.EnableMelee = true;
                enemy.Init(3, 0);
                game.Hero.transform.position = enemy.transform.position + Vector3.right;
                game.Hero.Hp = 1000; Set(game.Hero, "invulnerable", 0f);
                brain.Tick(game, 1, .01f);
                game.Hero.transform.position = enemy.transform.position + Vector3.right * 5;
                brain.Tick(game, 1, 1);
                Near(game.Hero.Hp, 1000, "Escaping boss melee windup avoids hit");
                attack.Melee.TriggerRange = 10;
                Near(attack.AttackRange, 10, "Boss movement follows next enabled pattern range");

                // 다시 초기화하면 순서, 준비, 돌진도 새 생성 상태로 돌아갑니다.
                attack.EnableCharge = attack.EnableRanged = true; attack.EnableMelee = false;
                enemy.Init(3, 0);
                Check(Read<int>(attack, "cycle") == 0 && Read<float>(attack, "chargeTime") == 0 && !attack.Telegraphing,
                    "Boss initialization resets pattern state");
            }
            finally
            {
                CollectShots();
                game.Enemies.Clear();
                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            }

            var rangedEnemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/Enemy_2.prefab"));
            try
            {
                rangedEnemy.Init(2, 0);
                var ranged = rangedEnemy.GetComponent<EnemyRangedAttack>();
                ranged.ProjectileCount = 5;
                Set(ranged, "cooldown", 0f);
                game.Hero.transform.position = rangedEnemy.transform.position + Vector3.right * 5;
                rangedEnemy.GetComponent<EnemyBasicBrain>().Tick(game, 1, .01f);
                rangedEnemy.GetComponent<EnemyBasicBrain>().Tick(game, 1, 1);
                Check(CollectShots().Count == 5, "Enemy_2 Inspector projectile count controls actual shots");
            }
            finally { UnityEngine.Object.DestroyImmediate(rangedEnemy.gameObject); }
        }

        static void CheckAttackRangeFollowing(Dungeon game, int kind)
        {
            var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/Enemy_" + kind + ".prefab"));
            try
            {
                enemy.Init(kind, 0);
                var brain = enemy.GetComponent<EnemyBasicBrain>();
                var movement = enemy.GetComponent<EnemyKeepDistanceMovement>();
                var attack = enemy.GetComponent<EnemyAttackModule>();
                Check(movement.UseAttackRange, "Prefab follows attack range: " + kind);
                game.Enemies.Clear();
                game.Enemies.Add(enemy);
                game.Hero.transform.position = Vector3.right * 7;
                Dungeon.Obstacles = Array.Empty<Rect>();
                Set(attack, "cooldown", 100f);
                var trigger = attack.GetType().GetField("TriggerRange");

                // Init 후 공격거리만 수정합니다. 이동 설정이나 초기화를 다시 건드리지 않습니다.
                trigger.SetValue(attack, 10f);
                Near(attack.AttackRange, 10, "Common attack range reflects Inspector value");
                enemy.transform.position = Vector3.zero;
                brain.Tick(game, 1, .1f);
                Near(enemy.transform.position, Vector3.zero, "Range 10 stops at distance 7 without reinitialization");
                trigger.SetValue(attack, 5f);
                brain.Tick(game, 1, .1f);
                Near(enemy.transform.position.x, .22f, "Range 5 approaches at distance 7 without reinitialization");

                enemy.transform.position = Vector3.zero;
                attack.enabled = false;
                trigger.SetValue(attack, 10f);
                brain.Tick(game, 1, .1f);
                Near(enemy.transform.position, Vector3.zero, "Disabled attack retains configured movement range");

                movement.UseAttackRange = false;
                movement.ApproachRange = 5;
                brain.Tick(game, 1, .1f);
                Near(enemy.transform.position.x, .22f, "Manual range override remains available");

                movement.UseAttackRange = true;
                UnityEngine.Object.DestroyImmediate(attack);
                enemy.transform.position = Vector3.zero;
                brain.Tick(game, 1, .1f);
                Near(enemy.transform.position.x, .22f, "Removed attack falls back to manual range");

                var replacement = enemy.gameObject.AddComponent<EnemyMeleeAttack>();
                replacement.TriggerRange = 10;
                enemy.Init(kind, 0);
                replacement.enabled = false;
                enemy.transform.position = Vector3.zero;
                brain.Tick(game, 1, .1f);
                Near(enemy.transform.position, Vector3.zero, "Replacement attack range is bound through common contract");
            }
            finally
            {
                game.Enemies.Clear();
                UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            }
        }

        static void CheckReplacement(Dungeon game)
        {
            var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/Enemy_0.prefab"));
            UnityEngine.Object.DestroyImmediate(enemy.GetComponent<EnemyMeleeAttack>());
            UnityEngine.Object.DestroyImmediate(enemy.GetComponent<EnemyChaseMovement>());
            var attack = enemy.gameObject.AddComponent<EnemyRangedAttack>();
            var movement = enemy.gameObject.AddComponent<EnemyKeepDistanceMovement>();
            attack.Damage = 31; movement.Speed = 4;
            enemy.Init(0, 0);
            Near(enemy.AttackPower, 31, "Brain discovers replacement attack by common base");
            Near(movement.Speed, 4, "Replacement settings preserved");
            attack.enabled = false;
            enemy.transform.position = Vector3.zero;
            game.Hero.transform.position = Vector3.right * 10;
            enemy.GetComponent<EnemyBasicBrain>().Tick(game, .5f, .25f);
            Near(enemy.transform.position.x, .5f, "Brain executes replacement movement and slow multiplier");
            UnityEngine.Object.DestroyImmediate(attack);
            UnityEngine.Object.DestroyImmediate(movement);
            enemy.Init(0, 0);
            var position = enemy.transform.position;
            enemy.GetComponent<EnemyBasicBrain>().Tick(game, 1, 1);
            Near(enemy.transform.position, position, "Missing optional modules remain absent");
            UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        }
    }
}
