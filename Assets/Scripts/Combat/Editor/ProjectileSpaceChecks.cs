using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WesternLemegeton.EditorTools
{
    public static class ProjectileSpaceChecks
    {
        static int count;
        static void Check(bool ok, string text)
        { if (!ok) throw new Exception("PROJECTILE_SPACE_FAILED: " + text); count++; }

        [MenuItem("Western Lemegeton/Tests/Check Projectile Space")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before running this test.");
            var previous = SceneManager.GetActiveScene(); var previousGame = Dungeon.I;
            var obstacles = Dungeon.Obstacles; var room = Rules.RoomOrigin;
            var random = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            RenderTexture target = null;
            try
            {
                count = 0; Rules.RoomOrigin = Vector2.zero; Dungeon.Obstacles = Array.Empty<Rect>();
                var game = new GameObject("Projectile test").AddComponent<Dungeon>(); Dungeon.I = game;
                game.State = RunState.Combat;
                game.Hero = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Player>("Assets/Editable/Actors/Hunter.prefab"));
                game.Hero.transform.position = Vector2.left * 4;
                game.Cam = new GameObject("Camera").AddComponent<Camera>();
                PaperWorld.Configure(game.Cam); game.Cam.transform.position = PaperWorld.CameraPosition(Vector2.zero);
                target = new RenderTexture(1280,720,0); game.Cam.targetTexture = target;
                var enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Editable/Actors/Enemy_0.prefab"));
                enemy.Init(0,0); enemy.transform.position = Vector2.right * 3; game.Enemies.Add(enemy);
                var body = CombatBody.For(enemy);
                foreach (Vector2 position in new[] { new Vector2(3,0), new Vector2(-2,4), new Vector2(4,-4) })
                {
                    enemy.transform.position = position;
                    Vector3 center = body.Center, screen = game.Cam.WorldToScreenPoint(center);
                    Vector3 aim = ProjectileSpace.Aim(game, screen);
                    Vector3 projected = game.Cam.WorldToScreenPoint(aim);
                    Check(Vector2.Distance(projected, screen) < .1f, "Body aim projects back to cursor across screen");
                    Vector3 muzzle = CombatBody.For(game.Hero).Muzzle;
                    Check(ProjectileSpace.Sweep(game, muzzle, aim, false, .05f, out _, out var hit, out _) && hit == enemy,
                        "Shot from displayed muzzle reaches displayed target");
                }
                enemy.transform.position = Vector2.right * 3;
                Vector3 start = CombatBody.For(game.Hero).Muzzle, end = body.Center;
                Dungeon.Obstacles = new[] { new Rect(0,-2,.02f,4) };
                Check(ProjectileSpace.Sweep(game,start,end,false,.05f,out var wall,out var blockedEnemy,out _) &&
                    !blockedEnemy && wall.x <= 0, "Thin wall blocks before body even in one long step");
                Dungeon.Obstacles = Array.Empty<Rect>();
                Check(!ProjectileSpace.Sweep(game,start+Vector3.up*5,end+Vector3.up*5,false,.05f,out _,out _,out _),
                    "Passing above body does not hit its ground footprint");
                var bodyRay = new Ray(body.Center, Vector3.right);
                Check(body.Cast(bodyRay, 1, .05f, out float inside) && inside == 0,
                    "Shot starting inside hurtbox is not missed");
                Check(ProjectileSpace.Sweep(game,end,start,true,.05f,out _,out _,out bool hunter) && hunter,
                    "Hostile shot uses Hunter displayed hurtbox");
                Vector3 emptyScreen = new Vector3(1200,650,0);
                Vector3 emptyAim = ProjectileSpace.Aim(game,emptyScreen);
                Check(Mathf.Abs(emptyAim.y-start.y) < .001f &&
                    Vector2.Distance(game.Cam.WorldToScreenPoint(emptyAim), emptyScreen) < .1f,
                    "Empty cursor aims on muzzle-height plane instead of ground");
                var bullet = Bullet.SpawnWorld(start,(end-start).normalized*21,20,false,1);
                Check(bullet.WorldPosition == start && ProjectileSpace.Ground(start) == (Vector2)bullet.transform.position,
                    "Projectile logical position is derived from its world position");
                var anchor = new GameObject("Body anchor").transform;
                anchor.position = new Vector3(2,3,1); body.BodyAnchor = anchor;
                Check(body.Center == anchor.position, "Custom hurtbox anchor overrides sprite-derived body");
                enemy.gameObject.SetActive(false);
                Check(!ProjectileSpace.Sweep(game,body.Center-Vector3.right,body.Center+Vector3.right,false,.05f,out _,out _,out _),
                    "Inactive enemies cannot be shot");
                Debug.Log("PROJECTILE_SPACE_CHECKS_PASSED: " + count);
            }
            finally
            {
                if (target) UnityEngine.Object.DestroyImmediate(target);
                Dungeon.I = previousGame; Dungeon.Obstacles = obstacles; Rules.RoomOrigin = room; UnityEngine.Random.state = random;
                SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true);
            }
        }
    }
}
