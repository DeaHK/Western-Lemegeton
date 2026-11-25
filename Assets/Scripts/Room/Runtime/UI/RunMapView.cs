using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WesternLemegeton
{
    /// <summary>Presentation only. Selection emits intent; it never advances Dungeon or the run.</summary>
    // Layout follows Assets/UI/Beta/References/Journey/StageSelection.png (1920 x 1080).
    // The shared frame (header, board, status panel, footer) is BetaJourneyShell; this class places the stage nodes.
    [RequireComponent(typeof(RectTransform))]
    public sealed class RunMapView : MonoBehaviour
    {
        [Serializable] public sealed class NodeEvent : UnityEvent<int> { }
        [SerializeField] private ExpeditionStageCoordinator coordinator;
        // These authored values belonged to the first map layout. They stay serialized so existing
        // scenes and prefabs keep loading unchanged, but the mockup layout below no longer reads them.
#pragma warning disable 0414
        [SerializeField] private Font font;
        [SerializeField] private Vector2 nodeSize = new Vector2(156, 88);
        [SerializeField] private Vector2 spacing = new Vector2(224, 128);
        [SerializeField] private Color currentColor = new Color(1f, .72f, .28f);
        [SerializeField] private Color completedColor = new Color(.38f, .82f, .55f);
        [SerializeField] private Color availableColor = new Color(.3f, .85f, .95f);
        [SerializeField] private Color lockedColor = new Color(.36f, .39f, .45f);
#pragma warning restore 0414
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
            public Image Art, Emblem, Lock, Plate;
            public Text Title, State;
            public bool Boss;
        }
        private sealed class LinkVisual { public int From, To; public List<Image> Dashes; }

        // Centre of the board area the stages may use, and the widest spacing from the mockup.
        private static readonly Vector2 BoardCenter = new Vector2(688, 606);
        private const float MaxStepX = 270, MaxStepY = 224, BoardWidth = 1100, BoardHeight = 520;
        private static readonly Color Selectable = new Color32(31,128,118,255), Travelled = new Color32(247,191,77,255),
            Locked = new Color32(122,94,72,255), TravelledRoute = new Color32(190,134,38,255);

        private readonly Dictionary<int, NodeVisual> nodes = new Dictionary<int, NodeVisual>();
        private readonly List<LinkVisual> links = new List<LinkVisual>();
        private RunMapLayout boundMap, renderedMap;
        private RunExpeditionProgress boundProgress;
        private bool explicitBinding;
        private BetaJourneyShell shell;
        private RectTransform routeLayer, stageLayer;
        private Sprite stageArt, currentArt, bossArt, plateArt, currentPlateArt;
        private int pending = -1, layerCount = 1;
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

        private static string StageTitle(RunMapNode node) =>
            node.Layer >= 0 && node.Layer < Dungeon.RoomNames.Length ? Dungeon.RoomNames[node.Layer] : BetaUi.StageName(node.Type) + " 스테이지";

        private static string StageNotes(StageType type) => type switch
        {
            StageType.Basic => "황야를 가로지르는 전투 스테이지입니다.\n방 여섯 곳을 돌파하면 다음 경로가 열립니다.",
            StageType.Boss => "보스가 기다리는 스테이지입니다.\n준비를 마친 뒤 입장하세요.",
            StageType.Sigil => "성흔 제단이 있는 스테이지입니다.",
            StageType.Shop => "상점이 있는 스테이지입니다.",
            StageType.Event => "특별한 사건이 일어나는 스테이지입니다.",
            _ => "인장을 고를 수 있는 스테이지입니다."
        };

        public void Refresh()
        {
            if (!Application.isPlaying) return;
            if (shell == null) BuildShell();
            shell.Fit();
            var map = Map;
            var progress = Progress;
            if (renderedMap != map)
            {
                ClearGraph();
                renderedMap = map;
                SelectedNodeId = -1;
                pending = -1;
                if (map != null) BuildGraph(map);
            }
            shell.ShowRun(Dungeon.I);
            if (map == null || progress == null || progress.Map != map)
            {
                string waiting = StatusOverride ?? "원정 시작을 기다리는 중";
                foreach (var node in nodes.Values) node.Button.interactable = false;
                shell.SetAction("스테이지 입장", false);
                shell.ChapterNumber.text = "00"; shell.ChapterTotal.text = "/ 00"; shell.ChapterName.text = "황야";
                shell.PlateKicker.text = "TERRITORY 01"; shell.PlateName.text = Dungeon.RoomNames[0];
                shell.Kicker.text = "원정 대기"; shell.Title.text = "황야의 여정"; shell.Sub.text = "히르바  →  " + Dungeon.RoomNames[0];
                shell.BoxKicker.text = "안내"; shell.BoxTitle.text = "원정 준비"; shell.BoxBody.text = waiting; shell.BoxFooter.text = "";
                shell.CounterLabelA.text = "클리어"; shell.CounterValueA.text = "- / -";
                shell.CounterLabelB.text = "입장"; shell.CounterValueB.text = "- / -";
                shell.Hint.text = waiting;
                return;
            }
            if (SelectedNodeId >= 0 && !CanSelectNode(SelectedNodeId)) SelectedNodeId = -1;
            if (!CanSelectNode(pending)) pending = -1;

            foreach (var pair in nodes)
            {
                bool current = pair.Key == progress.CurrentNodeId;
                bool done = progress.CompletedNodeIds.Contains(pair.Key);
                bool available = CanSelectNode(pair.Key);
                bool spotlight = current || pair.Key == pending;
                var visual = pair.Value;
                visual.Button.interactable = available;
                // The teal medallion marks where you are and what you may pick; it is larger for the spotlighted stage.
                if (spotlight) SetArt(visual.Art, currentArt, 138, 138);
                else if (available) SetArt(visual.Art, currentArt, 108, 108);
                else if (visual.Boss) SetArt(visual.Art, bossArt, 118, 121);
                else SetArt(visual.Art, stageArt, 104, 107);
                visual.Art.color = done && !spotlight ? new Color(1, .86f, .52f) : Color.white;
                visual.Emblem.enabled = !spotlight && !available;   // the teal medallion already carries a skull
                visual.Lock.enabled = !available && !done && !current;
                float plateY = spotlight ? 152 : 137;
                visual.Plate.sprite = spotlight ? currentPlateArt : plateArt;
                visual.Plate.rectTransform.anchoredPosition = new Vector2(0, -plateY);
                visual.Title.color = spotlight ? BetaUi.Cream : BetaUi.Ink;
                visual.State.text = current ? "현재 위치" : pair.Key == pending ? "선택됨" : "";
                visual.State.rectTransform.anchoredPosition = new Vector2(-30, -(plateY + 40));
            }
            foreach (var link in links)
            {
                bool travelled = progress.CompletedNodeIds.Contains(link.From) &&
                    (progress.CompletedNodeIds.Contains(link.To) || link.To == progress.CurrentNodeId);
                var color = travelled ? TravelledRoute : link.From == progress.CurrentNodeId && CanSelectNode(link.To) ? Selectable : Locked;
                foreach (var dash in link.Dashes) dash.color = color;
            }

            var here = progress.CurrentNode;
            var shown = pending >= 0 ? map.Find(pending) : here;
            var flow = GetComponentInParent<RunStageSelectionController>();
            bool retry = flow && flow.HasPreparedStage && !string.IsNullOrEmpty(flow.LastError);
            bool failedEntry = flow && flow.HasPreparedStage && StatusOverride != null && StatusOverride.StartsWith("Entry failed:");
            string status = retry || failedEntry ? StatusOverride : progress.RunCompleted ? "원정을 완료했습니다."
                : pending >= 0 ? "스테이지 입장을 누르면 선택한 스테이지로 출발합니다."
                : progress.CurrentStageCompleted ? "연결된 목적지를 선택한 뒤 출발하세요." : "현재 스테이지를 완료하면 다음 경로가 열립니다.";

            if (here != null)
            {
                shell.ChapterNumber.text = (here.Layer + 1).ToString("00");
                shell.ChapterName.text = BetaUi.StageName(here.Type);
                shell.PlateKicker.text = $"TERRITORY {here.Layer + 1:00}";
                shell.PlateName.text = StageTitle(here);
            }
            shell.ChapterTotal.text = "/ " + layerCount.ToString("00");
            if (shown != null)
            {
                shell.Kicker.text = (pending >= 0 ? "선택한 스테이지" : "현재 스테이지") + $"  /  STAGE {shown.Layer + 1:00}";
                shell.Title.text = StageTitle(shown);
                shell.Sub.text = pending >= 0 && here != null ? StageTitle(here) + "  →  " + StageTitle(shown)
                    : BetaUi.StageName(shown.Type) + " 스테이지";
                shell.BoxKicker.text = "스테이지 정보  /  " + BetaUi.StageName(shown.Type);
                shell.BoxTitle.text = $"{shown.Layer + 1:00}  ·  {BetaUi.StageName(shown.Type)}";
                shell.BoxBody.text = StageNotes(shown.Type);
                shell.BoxFooter.text = pending >= 0 ? "입장 준비 완료" : progress.CurrentStageCompleted ? "다음 경로 선택 가능" : "진행 중";
                shell.CounterValueB.text = $"{shown.Layer + 1} / {layerCount}";
            }
            shell.CounterLabelA.text = "클리어";
            shell.CounterValueA.text = $"{progress.CompletedNodeIds.Count} / {layerCount}";
            shell.CounterLabelB.text = "입장";
            shell.SetAction(retry || failedEntry ? "다시 입장" : "스테이지 입장", pending >= 0 || retry || failedEntry);
            shell.Hint.text = status;
        }

        private static void SetArt(Image image, Sprite sprite, float width, float height)
        {
            image.sprite = sprite;
            image.rectTransform.sizeDelta = new Vector2(width, height);
            // Node boxes are 150 wide and the medallion is centred 75 below the top.
            image.rectTransform.anchoredPosition = new Vector2((150 - width) * .5f, -(75 - height * .5f));
        }

        private void BuildShell()
        {
            stageArt = BetaUi.Art("Journey/StageNode"); currentArt = BetaUi.Art("Journey/CurrentStageNode");
            bossArt = BetaUi.Art("Journey/BossStageNode");
            plateArt = BetaUi.Art("Journey/StageNameplate"); currentPlateArt = BetaUi.Art("Journey/CurrentStageNameplate");
            shell = new BetaJourneyShell((RectTransform)transform, Enter, Close);
            shell.Subtitle.text = "다음 스테이지를 선택하세요";
            shell.SetLegend("경로 안내", (Selectable, "선택 가능"), (Travelled, "선택 / 지나온 경로"), (Locked, "잠김"));
            routeLayer = BetaUi.Box(shell.Graph, "Routes", 0, 0, 1920, 1080);
            stageLayer = BetaUi.Box(shell.Graph, "Stages", 0, 0, 1920, 1080);
        }

        private void Enter()
        {
            if (pending >= 0) { TrySelectNode(pending); return; }
            var flow = GetComponentInParent<RunStageSelectionController>();
            if (flow) flow.RequestStageEntry();
        }

        private void Close()
        {
            var flow = GetComponentInParent<RunStageSelectionController>();
            if (flow) flow.HideMap();
        }

        private void ClearGraph()
        {
            if (shell != null)
            {
                foreach (Transform child in routeLayer) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                foreach (Transform child in stageLayer) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            }
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
            layerCount = maxLayer + 1;
            // Stages spread across the board at the mockup spacing, and tighten only when the run is larger.
            float stepX = maxLayer > 0 ? Mathf.Min(MaxStepX, BoardWidth / maxLayer) : 0;
            float stepY = maxSlots > 1 ? Mathf.Min(MaxStepY, BoardHeight / (maxSlots - 1)) : 0;
            float scale = Mathf.Min(1, maxLayer > 0 ? stepX / 190f : 1, maxSlots > 1 ? stepY / 170f : 1);
            var positions = new Dictionary<int, Vector2>();
            foreach (var node in map.Nodes)
                positions[node.Id] = BoardCenter + new Vector2((node.Layer - maxLayer * .5f) * stepX,
                    (node.Slot - (slots[node.Layer] - 1) * .5f) * stepY);

            foreach (var node in map.Nodes)
                foreach (int next in node.NextIds)
                {
                    if (!positions.TryGetValue(next, out var end)) continue;
                    links.Add(new LinkVisual { From = node.Id, To = next,
                        Dashes = Dashed($"Link_{node.Id}_{next}", positions[node.Id], end, scale) });
                }

            var skull = BetaUi.Art("Journey/SkullIcon");
            var padlock = BetaUi.Art("Journey/LockIcon");
            foreach (var node in map.Nodes)
            {
                int id = node.Id;
                var rect = BetaUi.Box(stageLayer, $"Node_{id}_{node.Type}", 0, 0, 150, 150);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(positions[id].x, -positions[id].y);
                rect.localScale = Vector3.one * scale;
                var area = rect.gameObject.AddComponent<Image>();
                area.color = Color.clear;
                var button = BetaUi.Press(area, () => { if (CanSelectNode(id)) { pending = id; Refresh(); } });
                var visual = new NodeVisual { Button = button, Boss = node.Type == StageType.Boss };
                visual.Art = BetaUi.Picture(rect, "Medallion", 23, 22, 104, 107, stageArt);
                visual.Emblem = BetaUi.Picture(rect, "Skull", 53, 51, 44, 44, skull);
                visual.Lock = BetaUi.Picture(rect, "Lock", 96, 92, 30, 30, padlock);
                visual.Plate = BetaUi.Picture(rect, "Nameplate", 0, 137, 150, 38, plateArt);
                visual.Title = BetaUi.Label(visual.Plate.transform, "Type", 4, 0, 142, 37, 21, BetaUi.Ink, true, TextAnchor.MiddleCenter);
                visual.Title.text = $"{node.Layer + 1:00}  ·  {BetaUi.StageName(node.Type)}";
                visual.State = BetaUi.Label(rect, "State", -30, 177, 210, 24, 16, BetaUi.Ink, true, TextAnchor.MiddleCenter);
                nodes.Add(id, visual);
            }
        }

        // A dashed route between two stages. The dashes stop short of both medallions.
        private List<Image> Dashed(string name, Vector2 from, Vector2 to, float scale)
        {
            var dashes = new List<Image>();
            Vector2 delta = to - from;
            float length = delta.magnitude, inset = 58 * scale, dash = 20 * scale, gap = 13 * scale, thickness = 5 * scale;
            var holder = BetaUi.Box(routeLayer, name, from.x, from.y, length, thickness);
            holder.pivot = new Vector2(0, .5f);
            holder.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            for (float at = inset; at + dash <= length - inset; at += dash + gap)
                dashes.Add(BetaUi.Block(holder, "Dash", at, 0, dash, thickness, Locked));
            return dashes;
        }
    }
}
