using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WesternLemegeton.EditorTools
{
    /// <summary>
    /// 기존 Main 씬을 MainMenu / Lobby / Game / Debug 씬과 공용 GameSession 프리팹으로 나누는 일회성 도구입니다.
    /// Main.unity는 열기만 하고 저장하지 않으므로 원본은 그대로 남습니다.
    /// 마을·방·헌터·UI는 복사본을 새로 만들지 않고 원래 오브젝트를 그대로 옮겨서 프리팹 연결과 수작업 값을 보존합니다.
    /// </summary>
    public static class SceneSeparationSetup
    {
        public const string MainPath = "Assets/Scenes/Main.unity";
        public const string SessionPath = "Assets/Resources/Session/GameSession.prefab";
        public static readonly string[] Paths = Enum.GetNames(typeof(GameSpace)).Select(n => "Assets/Scenes/" + n + ".unity").ToArray();
        public static bool Separated => File.Exists(SessionPath);

        [MenuItem("Western Lemegeton/Scenes/Create Separated Scenes (Once)")]
        public static void Build()
        {
            if (File.Exists(SessionPath) || Paths.Any(File.Exists))
                throw new InvalidOperationException("Separated content already exists. Edit it directly; this migration never overwrites it.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var created = new List<string>();
            var previousBuildScenes = EditorBuildSettings.scenes;
            bool createdFolder = !Directory.Exists(Path.GetDirectoryName(SessionPath));
            try
            {
                Extract(created);
            }
            catch
            {
                // 중간에 실패하면 이번 실행이 만든 파일만 지우고, 저장하지 않은 Main을 다시 엽니다.
                EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
                foreach (var path in created) AssetDatabase.DeleteAsset(path);
                if (createdFolder && Directory.Exists(Path.GetDirectoryName(SessionPath)) &&
                    !Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(SessionPath)).Any())
                    AssetDatabase.DeleteAsset(Path.GetDirectoryName(SessionPath));
                EditorBuildSettings.scenes = previousBuildScenes;
                AssetDatabase.SaveAssets();
                throw;
            }
            Debug.Log("WNN_SCENE_MIGRATION_PASSED");
        }

        static void Extract(List<string> created)
        {
            var source = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
            var game = UnityEngine.Object.FindFirstObjectByType<Dungeon>();
            Require(game && game.Scene, "Main contains Dungeon and GameSceneBindings");
            var bindings = game.Scene;
            Require(bindings.Hirva && bindings.Rooms != null && bindings.Rooms.Length == 6 && bindings.Rooms.All(r => r), "Main binds the town and six rooms");
            var town = bindings.Hirva;
            var rooms = bindings.Rooms.ToArray();
            var linked = new Dictionary<GameObject, bool>();
            foreach (var go in rooms.Select(r => r.gameObject).Concat(new[] { town.gameObject, bindings.Hunter.gameObject, bindings.Raven.gameObject, bindings.UI.gameObject }))
                linked[go] = PrefabUtility.IsPartOfPrefabInstance(go);
            var world = town.transform.parent;
            var light = new Lighting();

            // 세션이 들고 있던 콘텐츠 참조는 씬을 넘나들 수 없으므로 먼저 비웁니다. 실행 중에는 SceneContent가 다시 연결합니다.
            bindings.Hirva = null;
            bindings.Rooms = Array.Empty<RoomAuthoring>();
            if (bindings.EnemyContainer) UnityEngine.Object.DestroyImmediate(bindings.EnemyContainer.gameObject);
            bindings.EnemyContainer = null;

            foreach (GameSpace space in Enum.GetValues(typeof(GameSpace)))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                light.Apply();
                var content = new GameObject(space + " Content").AddComponent<SceneContent>();
                content.Space = space;
                content.EnemyContainer = new GameObject("Spawned_Enemies").transform;
                if (space == GameSpace.Lobby) content.Town = Move(town, scene);
                if (space == GameSpace.Game) content.Rooms = rooms.Select(r => Move(r, scene)).ToArray();
                content.Validate();
                var outside = ExternalReferences(scene.GetRootGameObjects());
                Require(outside.Count == 0, space + " scene has no references to objects outside itself:\n" + string.Join("\n", outside));
                string path = "Assets/Scenes/" + space + ".unity";
                Require(EditorSceneManager.SaveScene(scene, path), "Saved " + path);
                created.Add(path);
                SceneManager.SetActiveScene(source);
                EditorSceneManager.CloseScene(scene, true);
            }

            foreach (var pair in linked)
                if (pair.Key) Require(PrefabUtility.IsPartOfPrefabInstance(pair.Key) == pair.Value, "Prefab link preserved: " + pair.Key.name);
            if (world)
            {
                Require(world.childCount == 0, "Every child of World was moved to a content scene. Left behind: " +
                    string.Join(", ", world.Cast<Transform>().Select(t => t.name)));
                UnityEngine.Object.DestroyImmediate(world.gameObject);
            }

            var session = new GameObject("GameSession");
            var manager = session.AddComponent<GameManager>();
            manager.Session = game;
            foreach (var root in source.GetRootGameObjects())
                if (root != session) root.transform.SetParent(session.transform, true);
            game.ManagedSession = true;
            AddButton(bindings.UI.PausePanel, bindings.UI.PausePanel, "Pause_Settings", "Pause_MainMenu", "메인 화면으로", GameUIAction.ReturnToMainMenu, new Rect(430, 522, 420, 48));
            AddButton(bindings.UI.ResultsPanel, bindings.UI.ResultsPanel, "Results_Town", "Results_MainMenu", "메인 화면으로", GameUIAction.ReturnToMainMenu, new Rect(330, 484, 620, 48));
            AddButton(bindings.UI.TitlePanel, bindings.UI.PausePanel, "Pause_Settings", "Title_Quit", "게임 종료", GameUIAction.QuitGame, new Rect(1010, 650, 240, 48));

            var external = ExternalReferences(new[] { session });
            Require(external.Count == 0, "Session has no references to objects outside itself:\n" + string.Join("\n", external));
            foreach (var go in new[] { bindings.Hunter.gameObject, bindings.Raven.gameObject, bindings.UI.gameObject })
                Require(PrefabUtility.IsPartOfPrefabInstance(go) == linked[go], "Prefab link preserved: " + go.name);

            Directory.CreateDirectory(Path.GetDirectoryName(SessionPath));
            PrefabUtility.SaveAsPrefabAsset(session, SessionPath, out bool saved);
            Require(saved, "Saved " + SessionPath);
            created.Add(SessionPath);
            EditorBuildSettings.scenes = Paths.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
            // Main은 저장하지 않고 닫습니다.
            EditorSceneManager.OpenScene(Paths[0], OpenSceneMode.Single);
            Validate();
        }

        static RoomAuthoring Move(RoomAuthoring room, Scene scene)
        {
            room.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(room.gameObject, scene);
            // 기존과 같이 필요한 방만 실행 중에 켭니다(방 안 오브젝트의 Start가 방에 처음 들어갈 때 실행되던 순서를 유지).
            room.gameObject.SetActive(false);
            return room;
        }

        // Main에서 쓰던 조명·안개 설정을 새 씬에도 그대로 둡니다.
        sealed class Lighting
        {
            readonly Material skybox = RenderSettings.skybox;
            readonly UnityEngine.Rendering.AmbientMode mode = RenderSettings.ambientMode;
            readonly Color sky = RenderSettings.ambientSkyColor, equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor;
            readonly float intensity = RenderSettings.ambientIntensity, density = RenderSettings.fogDensity, start = RenderSettings.fogStartDistance, end = RenderSettings.fogEndDistance;
            readonly bool fog = RenderSettings.fog;
            readonly Color fogColor = RenderSettings.fogColor, shadow = RenderSettings.subtractiveShadowColor;
            readonly FogMode fogMode = RenderSettings.fogMode;
            public void Apply()
            {
                RenderSettings.skybox = skybox; RenderSettings.ambientMode = mode;
                RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientIntensity = intensity; RenderSettings.fog = fog; RenderSettings.fogColor = fogColor; RenderSettings.fogMode = fogMode;
                RenderSettings.fogDensity = density; RenderSettings.fogStartDistance = start; RenderSettings.fogEndDistance = end;
                RenderSettings.subtractiveShadowColor = shadow;
            }
        }

        // 기존 버튼을 복제해 같은 모양의 버튼을 만듭니다. 위치는 1280x720 기준 왼쪽 위 좌표입니다.
        static void AddButton(GameObject panel, GameObject templatePanel, string templateName, string name, string label, GameUIAction action, Rect rect)
        {
            Require(panel && templatePanel, "UI panel for " + name);
            var template = templatePanel.transform.Find(templateName);
            Require(template && template.GetComponent<Button>() && template.GetComponent<UICommand>(), "Button template " + templateName);
            var copy = UnityEngine.Object.Instantiate(template.gameObject, panel.transform, false);
            copy.name = name;
            var area = (RectTransform)copy.transform;
            area.anchorMin = area.anchorMax = new Vector2(0, 1); area.pivot = new Vector2(0, 1);
            area.anchoredPosition = new Vector2(rect.x, -rect.y); area.sizeDelta = rect.size;
            var command = copy.GetComponent<UICommand>();
            command.Action = action; command.Parameter = -1;
            var button = copy.GetComponent<Button>();
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, command.Execute);
            var text = copy.GetComponentInChildren<Text>(true);
            Require(text, "Button label " + name);
            text.name = name + "Text"; text.text = label;
            var labelArea = text.rectTransform;
            labelArea.anchorMin = labelArea.anchorMax = new Vector2(0, 1); labelArea.pivot = new Vector2(0, 1);
            labelArea.anchoredPosition = new Vector2(5, -4); labelArea.sizeDelta = new Vector2(rect.width - 10, rect.height - 8);
        }

        [MenuItem("Western Lemegeton/Scenes/Validate Separated Scenes")]
        public static void Validate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SessionPath);
            Require(prefab, "GameSession prefab exists");
            var manager = prefab.GetComponent<GameManager>();
            Require(manager && manager.Session && manager.Session.ManagedSession, "Managed session binding");
            Require(prefab.GetComponentsInChildren<GameManager>(true).Length == 1 && prefab.GetComponentsInChildren<Dungeon>(true).Length == 1, "One GameManager and one Dungeon");
            var binding = manager.Session.Scene;
            Require(binding && binding.Hunter && binding.Raven && binding.GameCamera && binding.UI, "Shared actors, camera and UI");
            Require(!binding.Hirva && binding.Rooms.Length == 0 && !binding.EnemyContainer, "No scene references stored in session prefab");
            Require(prefab.GetComponentsInChildren<EventSystem>(true).Length == 1, "One persistent EventSystem");
            Require(prefab.GetComponentsInChildren<AudioListener>(true).Length == 1, "One persistent AudioListener");
            Require(prefab.GetComponentInChildren<StageDungeonBuilder>(true) && prefab.GetComponentInChildren<ExpeditionStageCoordinator>(true), "Generated run managers stay in the session");
            // 분리 전 EditableValidation이 Main에서 하던 UI·몬스터 프리팹 검사를 그대로 유지합니다.
            var ui = binding.UI;
            Require(ui.TitlePanel && ui.HudPanel && ui.PausePanel && ui.SettingsPanel && ui.ResultsPanel, "UI panels persist");
            Require(ui.GetComponentsInChildren<Button>(true).Length >= 25, "Native Canvas contains interactive buttons");
            Require(ui.References.Select(r => r.Key).Distinct().Count() == ui.References.Count, "UI binding keys are unique");
            foreach (var reference in ui.References) Require(reference.Target, "UI binding target persists: " + reference.Key);
            var commands = ui.GetComponentsInChildren<UICommand>(true);
            Require(commands.Count(c => c.Action == GameUIAction.ReturnToMainMenu && c.GetComponent<Button>()) == 2, "Pause and results panels have a main-menu button");
            Require(commands.Count(c => c.Action == GameUIAction.QuitGame && c.transform.IsChildOf(ui.TitlePanel.transform)) == 1, "Title has a quit button");
            foreach (var button in ui.GetComponentsInChildren<Button>(true))
                if (button.GetComponent<UICommand>())
                    Require(button.onClick.GetPersistentEventCount() > 0 && button.onClick.GetPersistentTarget(0) == button.GetComponent<UICommand>(), "Button runs its own command: " + button.name);
            Require(binding.EnemyPrefabs.All(p => p && p.AuthoredArt && p.AuthoredSprite && p.HealthFill && p.AuthoredWarning), "Enemies use editable prefab visuals");
            CheckObjects(prefab);

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var path in Paths)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var content = SceneContent.Find(scene);
                    Require(content, "Content binding: " + path);
                    Require("Assets/Scenes/" + content.Space + ".unity" == path, "Content space matches scene: " + path);
                    content.Validate();
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        Require(!root.GetComponentInChildren<Dungeon>(true) && !root.GetComponentInChildren<GameManager>(true), "No duplicate session in " + path);
                        Require(!root.GetComponentInChildren<EventSystem>(true), "No duplicate EventSystem in " + path);
                        Require(!root.GetComponentInChildren<AudioListener>(true) && !root.GetComponentInChildren<Camera>(true), "No camera or AudioListener in " + path);
                        CheckObjects(root);
                    }
                    if (content.Space == GameSpace.Lobby)
                        Require(content.Town.Town && content.Town.GetComponentsInChildren<WorldProp>(true).Length > 20 &&
                            content.Town.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("NPC - ")), "Lobby town keeps scenery and NPCs");
                    if (content.Space == GameSpace.Game)
                        for (int i = 0; i < content.Rooms.Length; i++)
                        {
                            var room = content.Rooms[i];
                            Require(room.RoomIndex == i, "Room order persists: " + room.name);
                            Require(room.Doors.Length > 0 && room.InitialSpawn && room.ServicePoint, "Room authoring anchors persist");
                            foreach (var door in room.Doors) Require(door && door.Owner == room && door.Arrival && door.Marker, "Door and arrival links persist");
                            Require(room.GetComponentsInChildren<WorldProp>(true).Length > 20, "Room contains editable scenery");
                        }
                }
            }
            // 배치 실행처럼 저장된 씬 없이 시작한 경우에는 되돌릴 구성이 없으므로 MainMenu를 열어 둡니다.
            finally
            {
                if (setup.Any(s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.OpenScene(Paths[0], OpenSceneMode.Single);
            }
            Require(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).SequenceEqual(Paths), "Four enabled scenes in startup order");
            Debug.Log("WNN_SEPARATED_SCENES_VALIDATED");
        }

        // 기존 몬스터·투사체 검사를 한 번에 실행합니다. 이 검사들은 씬이 하나 열려 있어야 하므로 기존 Main을 먼저 엽니다.
        // 하나가 실패해도 나머지를 계속 실행하고, 마지막에 실패 목록으로 예외를 던집니다.
        [MenuItem("Western Lemegeton/Tests/Run Legacy Editor Checks (Main)")]
        public static void RunLegacyChecks()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var checks = new (string name, Action run)[]
            {
                ("ProjectileSpace", ProjectileSpaceChecks.Run), ("CrowdControl", EnemyCrowdControlChecks.Run), ("EnemyModule", EnemyModuleChecks.Run),
                ("SlowMovement", SlowMovementSkillChecks.Run), ("Logan", LoganChecks.Run), ("EnemyPair", EnemyPairChecks.Run), ("RobeMan", RobeManChecks.Run),
            };
            var failed = new List<string>();
            foreach (var check in checks)
            {
                EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
                try { check.run(); Debug.Log("LEGACY_CHECK_PASSED: " + check.name); }
                catch (Exception ex) { failed.Add(check.name); Debug.LogError("LEGACY_CHECK_FAILED: " + check.name + " :: " + ex.Message); }
            }
            EditorSceneManager.OpenScene(Separated ? Paths[0] : MainPath, OpenSceneMode.Single);
            if (failed.Count > 0) throw new InvalidOperationException("LEGACY_CHECKS_FAILED: " + string.Join(", ", failed));
            Debug.Log("LEGACY_CHECKS_ALL_PASSED");
        }

        // roots 밖의 씬 오브젝트를 가리키는 직렬화 참조를 모두 찾습니다. 저장하면 조용히 비워지는 참조입니다.
        static List<string> ExternalReferences(GameObject[] roots)
        {
            var found = new List<string>();
            foreach (var root in roots)
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (!component) continue;
                    var property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var target = property.objectReferenceValue;
                        if (!target || EditorUtility.IsPersistent(target)) continue;
                        var transform = target is GameObject go ? go.transform : target is Component c ? c.transform : null;
                        if (!transform || roots.Any(r => transform.IsChildOf(r.transform))) continue;
                        found.Add(Describe(component.transform) + " [" + component.GetType().Name + "." + property.propertyPath + "] -> " + Describe(transform));
                    }
                }
            return found;
        }

        static string Describe(Transform t)
        {
            string path = t.name;
            for (var p = t.parent; p; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        static void CheckObjects(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "No missing scripts: " + Describe(transform));
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component is SpriteRenderer renderer)
                        Require(renderer.sprite && EditorUtility.IsPersistent(renderer.sprite), "Renderer references an exported sprite asset: " + Describe(transform));
                    var property = new SerializedObject(component).GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference)
                            Require(property.objectReferenceValue || property.objectReferenceInstanceIDValue == 0,
                                "No missing reference: " + Describe(transform) + "." + property.propertyPath);
                }
            }
        }

        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("SCENE_CHECK_FAILED: " + message); }
    }
}
