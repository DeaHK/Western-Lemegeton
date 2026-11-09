using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WesternLemegeton.Editor
{
    public static class StageDungeonSetup
    {
        [MenuItem("Tools/Western Lemegeton/Generated Dungeon/Connect Builder in Main")]
        public static void Connect()
        {
            if (EditorApplication.isPlaying) return;
            var scene = SceneManager.GetActiveScene();
            Dungeon dungeon = null; RunStageSelectionController controller = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var d = root.GetComponentInChildren<Dungeon>(true); if (d) dungeon = d;
                var c = root.GetComponentInChildren<RunStageSelectionController>(true); if (c) controller = c;
            }
            if (!dungeon || !controller) { Debug.LogError("Open Main with Dungeon and connected RunMap UI."); return; }
            var builder = dungeon.GetComponent<StageDungeonBuilder>();
            if (!builder) builder = Undo.AddComponent<StageDungeonBuilder>(dungeon.gameObject);
            Undo.RecordObject(builder, "Configure generated Dungeon"); builder.Configure(controller, dungeon);
            EditorUtility.SetDirty(builder); EditorSceneManager.MarkSceneDirty(scene); Selection.activeObject = builder;
            Debug.Log("Generated Dungeon builder connected to Main. Save the scene.");
        }
        [MenuItem("Tools/Western Lemegeton/Generated Dungeon/Run Integration Checks (Play Mode)")]
        public static void Verify()
        {
            if (!EditorApplication.isPlaying) return;
            var builder = Object.FindAnyObjectByType<StageDungeonBuilder>();
            if (!builder || Object.FindAnyObjectByType<GeneratedDungeonSmoke>()) return;
            builder.gameObject.AddComponent<GeneratedDungeonSmoke>().Builder = builder;
        }
        [MenuItem("Tools/Western Lemegeton/Generated Dungeon/Prepare Door Test at Entry (Play Mode)")]
        public static void PrepareEntryDoor()
        {
            var d = Dungeon.I;
            if (!EditorApplication.isPlaying || !d || !d.UsesGeneratedStage || d.CurrentKind != ExplorationRoomKind.Entry) return;
            var door = d.CurrentRoom.Doors.Find(item => !item.StageExit);
            if (door) d.Hero.transform.position = door.Position;
            Debug.Log("Entry door test ready: focus Game and press F to enter the connected combat room.");
        }
    }
}
