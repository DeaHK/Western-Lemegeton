using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WesternLemegeton.EditorTools
{
    public static class EnemyCrowdControlChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static int checks;
        static void Check(bool ok, string message)
        { if (!ok) throw new Exception("CROWD_CONTROL_FAILED: " + message); checks++; }
        static void Set(object target, string name, object value)
            => target.GetType().GetField(name, Private).SetValue(target, value);

        [MenuItem("Western Lemegeton/Tests/Check Crowd Control")]
        public static void Run()
        {
            var previous = SceneManager.GetActiveScene(); var previousGame = Dungeon.I;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            AudioClip clip = null;
            try
            {
                checks = 0;
                var game = new GameObject("Crowd control checks").AddComponent<Dungeon>(); Dungeon.I = game;
                game.State = RunState.Combat;
                var sound = game.gameObject.AddComponent<AudioSource>();
                clip = AudioClip.Create("Silent check", 64, 1, 44100, false);
                Set(game, "sound", sound); Set(game, "hitClip", clip);
                var cowboy = new GameObject("Cowboy").AddComponent<CowboyAttack>();
                foreach (DebugInvincibilityMode mode in Enum.GetValues(typeof(DebugInvincibilityMode)))
                foreach (bool invulnerable in new[] { false, true })
                for (int cc = 0; cc <= 2; cc++)
                {
                    Player Fresh()
                    {
                        if (game.Hero) UnityEngine.Object.DestroyImmediate(game.Hero.gameObject);
                        var hero = new GameObject("Hunter").AddComponent<Player>(); game.Hero = hero;
                        hero.Hp = 100; hero.DebugInvincibility = mode;
                        Set(hero, "invulnerable", invulnerable ? 1f : 0f);
                        return hero;
                    }
                    bool control = mode != DebugInvincibilityMode.Full && cc > 0 && (!invulnerable || cc == 2);
                    float hp = mode == DebugInvincibilityMode.Off && !invulnerable ? 90 : 100;
                    var p = Fresh();
                    Check(p.CanReceiveCrowdControl(cc) == control, "CC gate matrix");
                    p.Hurt(10, cc);
                    Check(p.Hp == hp, "Damage is independent of CC and health-only protection");
                    p = Fresh();
                    Check(p.TryApplyImpactControl(Vector2.right, 3, .5f, cc) == control, "Shotgun control matrix");
                    Check(p.KnockedBack == control && p.Stunned == control, "Actual knockback and stun state");
                    p = Fresh();
                    bool accepted = p.TryReceiveChargeHit(10, cc, out bool applyControl);
                    Check(applyControl == control, "Charge checks control before damage grants invulnerability");
                    Check(accepted == (mode != DebugInvincibilityMode.Full && (!invulnerable || control)), "Charge hit acceptance");
                    Check(p.Hp == hp, "Charge CC2 does not bypass damage invulnerability");
                    p = Fresh(); cowboy.LassoCrowdControl = cc;
                    var rope = CowboyRope.Bind(cowboy, p, 1, 5);
                    Check((rope != null) == control, "Lasso binding matrix");
                    if (rope) rope.Release();
                }
                var logan = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/Logan.prefab").GetComponent<LoganBossAttack>();
                var bull = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/HauntedBull.prefab").GetComponent<HauntedBullAttack>();
                var cow = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/Cowboy.prefab").GetComponent<CowboyAttack>();
                Check(logan.Shotgun.SwingCrowdControl == 2 && logan.Shotgun.CrowdControl == 1, "Shotgun prefab settings");
                Check(bull.FullChargeCrowdControl == 2 && bull.CrowdControl == 1, "Full/short charge prefab settings");
                Check(cow.LassoCrowdControl == 2 && cow.CrowdControl == 1, "Lasso/shooting prefab settings");
                Debug.Log("CROWD_CONTROL_CHECKS_PASSED: " + checks);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                Dungeon.I = previousGame;
                if (clip) UnityEngine.Object.DestroyImmediate(clip);
            }
        }
    }
}
