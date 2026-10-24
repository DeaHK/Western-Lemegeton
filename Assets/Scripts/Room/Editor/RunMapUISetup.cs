using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WesternLemegeton.Editor
{
    public static class RunMapUISetup
    {
        private const string PreviewPath = "Assets/Scripts/Room/Tests_Optional/RunMapUIPreview.unity";
        private const string PrefabPath = "Assets/Scripts/Room/Runtime/UI/RunMapCanvas.prefab";

        [MenuItem("Tools/Western Lemegeton/RunMap UI/Create Preview Scene and Prefab")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode before creating assets."); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath))
            { Debug.LogWarning("RunMap assets already exist; they were not overwritten. Open the preview scene from Project."); return; }
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var canvas = CreateCanvas();
                // Save a production prefab before adding any preview-only component.
                PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath);
                var preview = canvas.AddComponent<RunMapUIPreview>();
                preview.View = canvas.GetComponentInChildren<RunMapView>(true);
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                EditorSceneManager.SaveScene(scene, PreviewPath);
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("RunMap UI assets created. Open RunMapUIPreview.unity and press Play. Production Main was not modified.");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath);
        }

        [MenuItem("Tools/Western Lemegeton/RunMap UI/Run Preview Checks (Play Mode)")]
        public static void RunChecks()
        {
            var preview = Object.FindAnyObjectByType<RunMapUIPreview>();
            if (!preview || !EditorApplication.isPlaying) { Debug.LogWarning("Open RunMapUIPreview scene and press Play first."); return; }
            preview.Verify();
        }

        private static GameObject CreateCanvas()
        {
            var root = new GameObject("RunMapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("RunMapPanel", typeof(RectTransform));
            var rect = panel.GetComponent<RectTransform>();
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.AddComponent<RunMapView>();
            return root;
        }
    }
}
