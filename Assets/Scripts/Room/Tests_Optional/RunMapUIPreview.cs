using System;
using UnityEngine;
using UnityEngine.UI;

namespace WesternLemegeton
{
    /// <summary>Development-only fixture. Never attach to the production Main scene.</summary>
    public sealed class RunMapUIPreview : MonoBehaviour
    {
        public RunMapView View;
        private RunExpeditionProgress progress;
        private void Start()
        {
            // Overlay Canvas still needs a camera to avoid Unity's "No cameras rendering" overlay.
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            ResetPreview();
            View.OnNodeSelected.AddListener(AdvancePreview);
            AddButton("Complete current (PREVIEW ONLY)", -190, CompletePreview);
            AddButton("Reset preview", 70, ResetPreview);
            AddButton("Run checks", 270, Verify);
        }
        private void OnDestroy() { if (View) View.OnNodeSelected.RemoveListener(AdvancePreview); }
        private void ResetPreview()
        {
                var map = RunMapGenerator.Generate(12345, new[] { 1, 2, 3, 1, 1 }, new[] { StageType.Basic, StageType.Shop, StageType.Sigil });
            // Ensure the visual fixture demonstrates all four requested type labels.
            map.Nodes[1].Type = StageType.Shop;
            progress = new RunExpeditionProgress();
            progress.Start(map);
            View.Bind(map, progress);
        }
        private void CompletePreview() { progress.CompleteCurrentStage(); View.Refresh(); }
        private void AdvancePreview(int id) { progress.TrySelectNextNode(id); View.Refresh(); }

        [ContextMenu("Verify UI in Play Mode")]
        public void Verify()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Run the preview scene in Play Mode first."); return; }
            View.OnNodeSelected.RemoveListener(AdvancePreview);
            int delivered = -1;
            UnityEngine.Events.UnityAction<int> record = id => delivered = id;
            View.OnNodeSelected.AddListener(record);
            try
            {
                ResetPreview();
                Check(View.NodeCount == 9, "Nine nodes");
                int expectedLinks = 0;
                foreach (var node in progress.Map.Nodes) expectedLinks += node.NextIds.Count;
                Check(View.LinkCount == expectedLinks, "All links");
                Check(!View.TrySelectNode(0) && !View.TrySelectNode(-1) && !View.TrySelectNode(8), "Initial and invalid selections blocked");
                for (int layer = 0; layer < 5; layer++)
                {
                    Check(progress.CompleteCurrentStage(), "Complete stage");
                    View.Refresh();
                    foreach (var node in progress.Map.Nodes)
                        Check(View.IsNodeInteractable(node.Id) == View.CanSelectNode(node.Id), "Button state matches permission");
                    if (progress.RunCompleted) break;
                    int next = progress.GetAvailableNextNodes()[0].Id;
                    int old = progress.CurrentNodeId;
                    delivered = -1;
                    Check(View.TrySelectNode(next) && delivered == next, "Selection event");
                    Check(progress.CurrentNodeId == old, "View does not advance progress");
                    Check(progress.TrySelectNextNode(next), "Fixture advances progress");
                    // Before refresh: button still represents the old state, but handler must reject it.
                    Check(!View.TrySelectNode(next), "Stale click blocked");
                    View.Refresh();
                }
                Check(progress.RunCompleted && progress.CompletedNodeIds.Count == 5, "Complete five-layer run");
                foreach (var node in progress.Map.Nodes) Check(!View.IsNodeInteractable(node.Id), "Finished run locked");
                View.gameObject.SetActive(false);
                View.gameObject.SetActive(true);
                Check(View.NodeCount == 9 && View.LinkCount == expectedLinks, "Enable cycle has no duplicates");
                ResetPreview();
                Check(View.NodeCount == 9 && View.SelectedNodeId == -1 && progress.CurrentNodeId == 0, "New map resets presentation");
                Debug.Log("RunMap UI checks PASSED: nodes, links, permissions, events, stale clicks, 5 stages, reset, enable cycle.", this);
            }
            catch (Exception ex) { Debug.LogError("RunMap UI checks FAILED: " + ex, this); }
            finally
            {
                View.OnNodeSelected.RemoveListener(record);
                View.OnNodeSelected.AddListener(AdvancePreview);
                ResetPreview();
            }
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private void AddButton(string title, float x, UnityEngine.Events.UnityAction action)
        {
            var rect = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(View.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(x, -70);
            rect.sizeDelta = new Vector2(title.StartsWith("Complete") ? 260 : 150, 28);
            rect.GetComponent<Image>().color = new Color(.25f, .28f, .35f);
            rect.GetComponent<Button>().onClick.AddListener(action);
            var text = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(rect, false);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14; text.text = title; text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }
    }
}
