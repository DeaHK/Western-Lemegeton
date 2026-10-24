using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace WesternLemegeton
{
    /// <summary>Development-only tests against real Coordinator/Provider/Bridge and project assets.</summary>
    public sealed class RunStageEntryPreview : MonoBehaviour
    {
        public RunStageSelectionController Controller;
        public StageLayoutProvider Provider;
        public StageRoomDataBridge Bridge;
        private void Start()
        {
            Button("[DEV] Complete current", -140, () => Controller.Coordinator.CompleteCurrentStage());
            Button("[DEV] Run integration checks", 140, Verify);
        }

        [ContextMenu("Verify stage entry in Play Mode")]
        public void Verify()
        {
            if (!Application.isPlaying) return;
            var c = Controller.Coordinator;
            var v = Controller.View;
            var regionField = typeof(StageLayoutProvider).GetField("region", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalRegion = regionField.GetValue(Provider);
            int ready = 0, layoutEvents = 0;
            bool coherent = true, reentryBlocked = true;
            UnityEngine.Events.UnityAction<int> onReady = id => ready++;
            Action<StageLayout> onLayout = layout =>
            {
                layoutEvents++;
                coherent &= c.CurrentMap != null && c.Progress.CurrentNode != null &&
                    layout == c.CurrentStageLayout && layout.StageType == c.Progress.CurrentNode.Type &&
                    layout.Seed == ExpeditionStageCoordinator.GetStageSeed(c.CurrentMap.Seed, c.Progress.CurrentNodeId);
                reentryBlocked &= !c.StartNewRun(99);
            };
            Controller.OnStageReady.AddListener(onReady);
            Provider.OnLayoutGenerated += onLayout;
            try
            {
                Check(Controller.StartNewRun(12345), "Start real run");
                Check(ready == 1 && v.NodeCount == 9, "First stage ready exactly once, nine UI nodes");
                CheckStage();
                string first = Fingerprint(c.CurrentStageLayout);
                var map = c.CurrentMap; var progress = c.Progress; var layout = c.CurrentStageLayout;
                Check(!Controller.TrySelectNode(8) && c.Progress.CurrentNodeId == 0, "Locked selection rejected");
                regionField.SetValue(Provider, "__missing_region_for_failure_test__");
                Check(!Controller.StartNewRun(99), "Generation failure returned");
                Check(c.CurrentMap == map && c.Progress == progress && c.CurrentStageLayout == layout && ready == 1, "Failed restart preserves old run and data");
                regionField.SetValue(Provider, originalRegion);
                Check(c.CompleteCurrentStage(), "Complete first stage");
                int next = c.GetAvailableNextNodes()[0].Id;
                int oldNode = c.Progress.CurrentNodeId;
                var rooms = Controller.PreparedRooms;
                regionField.SetValue(Provider, "__missing_region_for_failure_test__");
                Check(!Controller.TrySelectNode(next), "Next stage failure returned");
                Check(c.Progress.CurrentNodeId == oldNode && c.Progress.CurrentStageCompleted && c.CurrentStageLayout == layout &&
                    Controller.PreparedRooms == rooms && ready == 1, "Failed advance preserves node, completion, layout and handoff");
                regionField.SetValue(Provider, originalRegion);
                int countBefore = ready;
                Check(v.TrySelectNode(next), "UI selection accepted");
                Check(c.Progress.CurrentNodeId == next && ready == countBefore + 1, "UI event reaches real Coordinator once");
                CheckStage();
                Check(!Controller.TrySelectNode(next) && ready == countBefore + 1, "Duplicate/current click rejected");
                Controller.enabled = false; Controller.enabled = true;
                Check(ready == countBefore + 1, "Enable cycle does not duplicate ready event");
                int guard = 8;
                while (!c.Progress.RunCompleted && guard-- > 0)
                {
                    Check(c.CompleteCurrentStage(), "Complete current");
                    if (c.Progress.RunCompleted) break;
                    next = c.GetAvailableNextNodes()[0].Id;
                    countBefore = ready;
                    Check(v.TrySelectNode(next) && ready == countBefore + 1, "UI prepares next stage once");
                    CheckStage();
                }
                Check(c.Progress.RunCompleted && c.Progress.CompletedNodeIds.Count == 5 && c.Progress.CurrentNode.Type == StageType.Boss, "Five stages end at Boss");
                Check(!Controller.TrySelectNode(0), "Completed run cannot advance");
                Check(Controller.StartNewRun(12345) && Fingerprint(c.CurrentStageLayout) == first, "Same run seed reproduces first stage");
                Check(coherent && reentryBlocked && layoutEvents == ready, "Observer ordering and reentrant mutation protection");
                Debug.Log("Stage entry integration checks PASSED: real UI/Coordinator/Provider/Bridge, 6-room data, type/seed, failure preservation, retry, event count, 5 stages, determinism.", this);
            }
            catch (Exception ex) { Debug.LogError("Stage entry integration checks FAILED: " + ex, this); }
            finally
            {
                regionField.SetValue(Provider, originalRegion);
                Controller.OnStageReady.RemoveListener(onReady);
                Provider.OnLayoutGenerated -= onLayout;
                Controller.StartNewRun(12345);
            }
        }

        private void CheckStage()
        {
            var c = Controller.Coordinator;
            var layout = c.CurrentStageLayout;
            Check(Controller.HasPreparedStage && Controller.PreparedLayout == layout && Controller.PreparedRooms.Count == 6, "Six-room handoff ready");
            Check(layout.StageType == c.Progress.CurrentNode.Type && layout.Seed == ExpeditionStageCoordinator.GetStageSeed(c.CurrentMap.Seed, c.Progress.CurrentNodeId), "Type and node-specific seed");
            Check(Bridge.TryGetAllRoomSpawnData(out var rooms) && rooms.Count == 6, "Bridge returns all rooms");
            int entry = 0, exit = 0;
            foreach (var room in rooms)
            {
                Check(room.Prefab && !string.IsNullOrEmpty(room.PrefabId), "Assigned prefab");
                if (room.IsEntry) entry++;
                if (room.IsExit) exit++;
                Check(room.Connections.Count == layout.Rooms[room.RoomIndex].Neighbors.Count, "Connection handoff");
            }
            Check(entry == 1 && exit == 1, "Unique Entry and Exit");
        }
        private static string Fingerprint(StageLayout layout)
        {
            var result = new StringBuilder();
            foreach (var room in layout.Rooms) result.Append(room.Index).Append(room.Role).Append(room.GridPosition).Append(room.PrefabId).Append(string.Join(",", room.Neighbors));
            return result.ToString();
        }
        private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
        private void Button(string title, float x, UnityEngine.Events.UnityAction action)
        {
            var rect = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            rect.SetParent(Controller.View.transform, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(x, 15); rect.sizeDelta = new Vector2(260, 27);
            rect.GetComponent<Image>().color = new Color(.22f, .25f, .3f); rect.GetComponent<Button>().onClick.AddListener(action);
            var text = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(rect, false); text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = title; text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        }
    }
}
