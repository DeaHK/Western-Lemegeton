using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace WesternLemegeton.Editor
{
    public static class RunStageEntrySetup
    {
        private const string PrefabPath = "Assets/Scripts/Room/Runtime/UI/RunMapCanvas.prefab";
        private const string ScenePath = "Assets/Scripts/Room/Tests_Optional/RunStageEntryPreview.unity";

        [MenuItem("Tools/Western Lemegeton/Stage Entry/Complete Current Stage for Testing (Play Mode)")]
        public static void CompleteForTesting()
        {
            if (!EditorApplication.isPlaying) return;
            var controller = Object.FindAnyObjectByType<RunStageSelectionController>();
            if (controller) controller.Coordinator.CompleteCurrentStage();
        }

        [MenuItem("Tools/Western Lemegeton/Stage Entry/Verify Main Isolation (Play Mode)")]
        public static void VerifyMainIsolation()
        {
            if (!EditorApplication.isPlaying) return;
            var controller = Object.FindAnyObjectByType<RunStageSelectionController>();
            var dungeon = Object.FindAnyObjectByType<Dungeon>();
            if (!controller || !dungeon) { Debug.LogError("Run this check in Main Play Mode."); return; }
            if (dungeon.GetComponent<StageDungeonBuilder>()) { Debug.LogWarning("Stage 3 builder is installed. Use Generated Dungeon integration checks instead of the data-only stage 2 isolation check."); return; }
            var oldProgress = dungeon.Progress;
            int segment = oldProgress.Segment, map = oldProgress.Map, wave = oldProgress.Wave;
            int cleared = oldProgress.ClearedMaps, visited = oldProgress.VisitedMaps, seed = dungeon.Seed;
            var state = dungeon.State;
            var heroPosition = dungeon.Hero.transform.position;
            bool success = controller.StartNewRun(12345) && controller.Coordinator.CompleteCurrentStage();
            var options = controller.Coordinator.GetAvailableNextNodes();
            success &= options.Count > 0 && controller.View.TrySelectNode(options[0].Id) && controller.HasPreparedStage;
            success &= dungeon.Progress == oldProgress && oldProgress.Segment == segment && oldProgress.Map == map &&
                oldProgress.Wave == wave && oldProgress.ClearedMaps == cleared && oldProgress.VisitedMaps == visited &&
                dungeon.Seed == seed && dungeon.State == state && dungeon.Hero.transform.position == heroPosition;
            if (success) Debug.Log("Main stage selection isolation PASSED: UI prepared next stage; Dungeon progress/state/player unchanged.");
            else Debug.LogError("Main stage selection isolation FAILED.");
        }

        [MenuItem("Tools/Western Lemegeton/Stage Entry/Create Connected Preview Scene")]
        public static void CreatePreview()
        {
            if (EditorApplication.isPlaying) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) { Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath); return; }
            var basic = AssetDatabase.LoadAssetAtPath<StageRoleConfig>("Assets/BasicStageConfig.asset");
            var boss = AssetDatabase.LoadAssetAtPath<StageRoleConfig>("Assets/BossStageConfig.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<RoomCatalog>("Assets/Wilderness Room Catalog.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!basic || !boss || !catalog || !prefab) { Debug.LogError("Stage Entry: original Basic/Boss config, Catalog or RunMapCanvas cannot be loaded."); return; }
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var systems = new GameObject("StageEntrySystems");
                var provider = systems.AddComponent<StageLayoutProvider>();
                var ps = new SerializedObject(provider);
                ps.FindProperty("roleConfig").objectReferenceValue = basic;
                ps.FindProperty("roomCatalog").objectReferenceValue = catalog; ps.ApplyModifiedPropertiesWithoutUndo();
                var coordinator = systems.AddComponent<ExpeditionStageCoordinator>();
                var cs = new SerializedObject(coordinator);
                cs.FindProperty("stageProvider").objectReferenceValue = provider;
                var types = cs.FindProperty("middleTypes"); types.arraySize = 1; types.GetArrayElementAtIndex(0).enumValueIndex = (int)StageType.Basic;
                var configs = cs.FindProperty("stageConfigs"); configs.arraySize = 2;
                configs.GetArrayElementAtIndex(0).FindPropertyRelative("Type").enumValueIndex = (int)StageType.Basic;
                configs.GetArrayElementAtIndex(0).FindPropertyRelative("Config").objectReferenceValue = basic;
                configs.GetArrayElementAtIndex(1).FindPropertyRelative("Type").enumValueIndex = (int)StageType.Boss;
                configs.GetArrayElementAtIndex(1).FindPropertyRelative("Config").objectReferenceValue = boss; cs.ApplyModifiedPropertiesWithoutUndo();
                var bridge = systems.AddComponent<StageRoomDataBridge>();
                var bs = new SerializedObject(bridge); bs.FindProperty("provider").objectReferenceValue = provider; bs.ApplyModifiedPropertiesWithoutUndo();
                var canvas = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var controller = canvas.AddComponent<RunStageSelectionController>();
                controller.Configure(coordinator, bridge, canvas.GetComponentInChildren<RunMapView>(true), true);
                var test = systems.AddComponent<RunStageEntryPreview>(); test.Controller = controller; test.Provider = provider; test.Bridge = bridge;
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                var camera = new GameObject("Preview Camera", typeof(Camera)).GetComponent<Camera>(); camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("Connected Stage Entry preview created using original project assets. Open it and press Play.");
        }

        [MenuItem("Tools/Western Lemegeton/Stage Entry/Connect UI in Current Scene")]
        public static void ConnectCurrentScene()
        {
            if (EditorApplication.isPlaying) return;
            var scene = SceneManager.GetActiveScene();
            ExpeditionStageCoordinator coordinator = null; StageRoomDataBridge bridge = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<RunStageSelectionController>(true)) { Debug.LogWarning("Stage Entry UI already connected in this scene."); return; }
                var found = root.GetComponentInChildren<ExpeditionStageCoordinator>(true); if (found) coordinator = found;
                var foundBridge = root.GetComponentInChildren<StageRoomDataBridge>(true); if (foundBridge) bridge = foundBridge;
            }
            if (!coordinator || !bridge || !AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) { Debug.LogError("Current scene needs Coordinator, Bridge and RunMapCanvas prefab."); return; }
            var canvas = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            Undo.RegisterCreatedObjectUndo(canvas, "Connect RunMap stage entry UI");
            var view = canvas.GetComponentInChildren<RunMapView>(true);
            var controller = Undo.AddComponent<RunStageSelectionController>(canvas);
            controller.Configure(coordinator, bridge, view, false);
            var vs = new SerializedObject(view); vs.FindProperty("coordinator").objectReferenceValue = coordinator; vs.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = canvas;
            Debug.Log("Stage Entry UI connected. Save the scene. Open RunMap launches data selection without changing Dungeon progression.");
        }
    }
}
