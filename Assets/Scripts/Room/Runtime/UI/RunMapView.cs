using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WesternLemegeton
{
    /// <summary>Presentation only. Selection emits intent; it never advances Dungeon or the run.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class RunMapView : MonoBehaviour
    {
        [Serializable] public sealed class NodeEvent : UnityEvent<int> { }
        [SerializeField] private ExpeditionStageCoordinator coordinator;
        [SerializeField] private Font font;
        [SerializeField] private Vector2 nodeSize = new Vector2(156, 88);
        [SerializeField] private Vector2 spacing = new Vector2(224, 128);
        [SerializeField] private Color currentColor = new Color(1f, .72f, .28f);
        [SerializeField] private Color completedColor = new Color(.38f, .82f, .55f);
        [SerializeField] private Color availableColor = new Color(.3f, .85f, .95f);
        [SerializeField] private Color lockedColor = new Color(.36f, .39f, .45f);
        [SerializeField] private NodeEvent onNodeSelected = new NodeEvent();

        public ExpeditionStageCoordinator Coordinator { get => coordinator; set { coordinator = value; UseCoordinator(); } }
        public NodeEvent OnNodeSelected => onNodeSelected;
        public int NodeCount => nodes.Count;
        public int LinkCount => links.Count;
        public int SelectedNodeId { get; private set; } = -1;
        public string StatusOverride { get; set; }
        /// <summary>Runtime RunMap is informational; route progression uses field markers.</summary>
        public bool SelectionEnabled { get; set; } = true;

        private sealed class NodeVisual
        {
            public Button Button;
            public Image Border;
            public Text State;
        }
        private sealed class LinkVisual { public int From, To; public Image Image; }
        private readonly Dictionary<int, NodeVisual> nodes = new Dictionary<int, NodeVisual>();
        private readonly List<LinkVisual> links = new List<LinkVisual>();
        private RunMapLayout boundMap, renderedMap;
        private RunExpeditionProgress boundProgress;
        private bool explicitBinding;
        private RectTransform ownedRoot, graph;
        private Text status;
        private float nextRefresh;

        private RunMapLayout Map => explicitBinding ? boundMap : coordinator ? coordinator.CurrentMap : null;
        private RunExpeditionProgress Progress => explicitBinding ? boundProgress : coordinator ? coordinator.Progress : null;

        /// <summary>Bind a read-only presentation source (also useful for isolated previews).</summary>
        public void Bind(RunMapLayout map, RunExpeditionProgress progress)
        {
            if (map == null || progress == null || progress.Map != map)
                throw new ArgumentException("Map and progress must refer to the same run.");
            explicitBinding = true;
            boundMap = map;
            boundProgress = progress;
            SelectedNodeId = -1;
            Refresh();
        }

        public void UseCoordinator()
        {
            explicitBinding = false;
            boundMap = null;
            boundProgress = null;
            SelectedNodeId = -1;
            if (isActiveAndEnabled) Refresh();
        }

        private void OnEnable() { Refresh(); }
        private void Update()
        {
            // Existing coordinator has no progress event. Poll only presentation, without allocations.
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f;
            Refresh();
        }

        public bool IsNodeInteractable(int id) => nodes.TryGetValue(id, out var node) && node.Button.interactable;

        public bool CanSelectNode(int id)
        {
            var p = Progress;
            return SelectionEnabled && Map != null && p != null && p.Map == Map && !p.RunCompleted &&
                p.CurrentStageCompleted && p.CurrentNode != null &&
                p.CurrentNode.NextIds.Contains(id) && Map.Find(id) != null && !p.CompletedNodeIds.Contains(id);
        }

        /// <summary>Rechecks live state, so an outdated enabled button cannot send an illegal request.</summary>
        public bool TrySelectNode(int id)
        {
            if (!CanSelectNode(id)) return false;
            SelectedNodeId = id;
            onNodeSelected.Invoke(id);
            Refresh();
            return true;
        }

        public void Refresh()
        {
            if (!Application.isPlaying) return;
            if (!ownedRoot) BuildShell();
            var map = Map;
            var progress = Progress;
            if (renderedMap != map)
            {
                ClearGraph();
                renderedMap = map;
                SelectedNodeId = -1;
                if (map != null) BuildGraph(map);
            }
            if (map == null || progress == null || progress.Map != map)
            {
                status.text = StatusOverride ?? "Waiting for run data — assign Coordinator and start a run.";
                foreach (var node in nodes.Values) node.Button.interactable = false;
                return;
            }
            if (SelectedNodeId >= 0 && !CanSelectNode(SelectedNodeId)) SelectedNodeId = -1;
            foreach (var pair in nodes)
            {
                bool current = pair.Key == progress.CurrentNodeId;
                bool done = progress.CompletedNodeIds.Contains(pair.Key);
                bool available = CanSelectNode(pair.Key);
                var visual = pair.Value;
                visual.Button.interactable = available;
                visual.Border.color = current ? currentColor : done ? completedColor : available ? availableColor : lockedColor;
                visual.State.text = current ? (done ? "CURRENT / COMPLETE" : "CURRENT") :
                    done ? "COMPLETE" : available ? (SelectedNodeId == pair.Key ? "SELECTED / AVAILABLE" : "AVAILABLE") : "LOCKED";
                visual.State.color = visual.Border.color;
            }
            foreach (var link in links)
                link.Image.color = progress.CompletedNodeIds.Contains(link.From) && progress.CompletedNodeIds.Contains(link.To)
                    ? completedColor : link.From == progress.CurrentNodeId && CanSelectNode(link.To) ? availableColor : lockedColor * new Color(1, 1, 1, .55f);
            status.text = StatusOverride ?? (progress.RunCompleted ? "RUN COMPLETE" : SelectedNodeId >= 0
                ? $"Node {SelectedNodeId} selected — stage entry is connected in the next implementation phase."
                : progress.CurrentStageCompleted ? "Choose an AVAILABLE node.  •  Selection only; stage entry is not connected."
                : $"Current node: {progress.CurrentNodeId}  •  Complete this stage to unlock the next route.");
        }

        private void BuildShell()
        {
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ownedRoot = Rect("RunMapContent", transform);
            Stretch(ownedRoot);
            var bg = ownedRoot.gameObject.AddComponent<Image>();
            bg.color = new Color(.055f, .065f, .085f, .98f);
            Label("Title", ownedRoot, "WESTERN LEMEGETON  /  RUN MAP", 26, new Vector2(.03f, .90f), new Vector2(.97f, .98f));
            Label("Legend", ownedRoot, "GOLD: CURRENT     GREEN: COMPLETE     CYAN: AVAILABLE     GRAY: LOCKED", 16, new Vector2(.03f, .83f), new Vector2(.97f, .90f));
            status = Label("Status", ownedRoot, "", 17, new Vector2(.03f, .02f), new Vector2(.97f, .12f));
            var viewport = Rect("Viewport", ownedRoot);
            viewport.anchorMin = new Vector2(.025f, .14f);
            viewport.anchorMax = new Vector2(.975f, .82f);
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            var background = viewport.gameObject.AddComponent<Image>();
            background.color = new Color(.08f, .095f, .12f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            graph = Rect("Graph", viewport);
            graph.anchorMin = graph.anchorMax = graph.pivot = new Vector2(.5f, .5f);
            scroll.content = graph;
            scroll.viewport = viewport;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
        }

        private void ClearGraph()
        {
            foreach (Transform child in graph) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            nodes.Clear();
            links.Clear();
        }

        private void BuildGraph(RunMapLayout map)
        {
            int maxLayer = 0, maxSlots = 1;
            var slots = new Dictionary<int, int>();
            foreach (var node in map.Nodes)
            {
                maxLayer = Mathf.Max(maxLayer, node.Layer);
                slots[node.Layer] = Mathf.Max(slots.TryGetValue(node.Layer, out int count) ? count : 0, node.Slot + 1);
                maxSlots = Mathf.Max(maxSlots, slots[node.Layer]);
            }
            Vector2 gap = new Vector2(Mathf.Max(spacing.x, nodeSize.x + 32), Mathf.Max(spacing.y, nodeSize.y + 24));
            graph.sizeDelta = new Vector2(maxLayer * gap.x + nodeSize.x + 64, (maxSlots - 1) * gap.y + nodeSize.y + 64);
            graph.anchoredPosition = Vector2.zero;
            var positions = new Dictionary<int, Vector2>();
            foreach (var node in map.Nodes)
                positions[node.Id] = new Vector2((node.Layer - maxLayer * .5f) * gap.x, ((slots[node.Layer] - 1) * .5f - node.Slot) * gap.y);
            foreach (var node in map.Nodes)
                foreach (int next in node.NextIds)
                {
                    if (!positions.TryGetValue(next, out var end)) continue;
                    Vector2 start = positions[node.Id] + Vector2.right * nodeSize.x * .5f;
                    end -= Vector2.right * nodeSize.x * .5f;
                    Vector2 delta = end - start;
                    var rect = Rect($"Link_{node.Id}_{next}", graph);
                    rect.anchoredPosition = (start + end) * .5f;
                    rect.sizeDelta = new Vector2(delta.magnitude, 3);
                    rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    var line = rect.gameObject.AddComponent<Image>();
                    line.raycastTarget = false;
                    links.Add(new LinkVisual { From = node.Id, To = next, Image = line });
                }
            foreach (var node in map.Nodes)
            {
                int id = node.Id;
                var rect = Rect($"Node_{id}_{node.Type}", graph);
                rect.anchoredPosition = positions[id];
                rect.sizeDelta = nodeSize;
                var border = rect.gameObject.AddComponent<Image>();
                var inner = Rect("Face", rect);
                Stretch(inner);
                inner.offsetMin = Vector2.one * 3;
                inner.offsetMax = Vector2.one * -3;
                var face = inner.gameObject.AddComponent<Image>();
                face.color = new Color(.12f, .14f, .18f);
                face.raycastTarget = false;
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                var colors = button.colors;
                colors.normalColor = face.color;
                colors.highlightedColor = new Color(.22f, .29f, .36f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(.16f, .24f, .3f);
                colors.disabledColor = new Color(.095f, .11f, .14f);
                button.colors = colors;
                button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                button.onClick.AddListener(() => TrySelectNode(id));
                Label("Type", rect, node.Type.ToString().ToUpperInvariant(), 21, new Vector2(0, .49f), Vector2.one);
                Label("Id", rect, $"{id:00}  /  STAGE {node.Layer + 1}", 13, new Vector2(0, .27f), new Vector2(1, .52f));
                var state = Label("State", rect, "", 12, Vector2.zero, new Vector2(1, .28f));
                nodes.Add(id, new NodeVisual { Button = button, Border = border, State = state });
            }
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private Text Label(string name, Transform parent, string value, int size, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter; text.color = new Color(.91f, .89f, .83f);
            text.raycastTarget = false;
            return text;
        }
    }
}
