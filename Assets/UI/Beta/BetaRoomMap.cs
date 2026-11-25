using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WesternLemegeton
{
    // The room map opened with M inside a stage: the six rooms of the current stage and the doors between them.
    // Layout follows References/Journey/RoomMap.png (1920 x 1080).
    internal sealed class BetaRoomMap
    {
        const float NodeWidth = 337, NodeHeight = 167, StepX = 440, StepY = 222;
        // The area of the board the rooms may use, below the stage name plate.
        static readonly Rect Board = new Rect(62, 286, 1284, 660);
        static readonly Color Door = new Color32(139,107,79,255), Earth = new Color32(84,62,46,255);
        static readonly Color CurrentMark = new Color32(78,205,196,255), DoneMark = new Color32(242,184,75,255),
            SeenMark = new Color32(245,224,178,255), UnseenMark = new Color32(140,123,117,255);

        sealed class RoomVisual
        {
            public RectTransform Rect;
            public Image Art, Lock;
            public Text Number, Name, Kind, State;
        }

        readonly BetaJourneyShell shell;
        readonly RoomVisual[] rooms = new RoomVisual[6];
        readonly List<GameObject> doors = new List<GameObject>();
        readonly RectTransform doorLayer;
        readonly Sprite roomArt, currentArt;
        StageLayout shownLayout;
        bool laidOut;

        public BetaRoomMap(GameUIView ui)
        {
            var panel = (RectTransform)ui.RoutePanel.transform;
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            // The authored controls are replaced by this screen. Only the two buttons that carry
            // game commands (next stage, close) are kept and moved into the new layout.
            var travel = ui.Element<RectTransform>("RouteTravel");
            var close = ui.Element<RectTransform>("Route_Close");
            foreach (Transform child in panel)
                if (child != travel && child != close) child.gameObject.SetActive(false);

            shell = new BetaJourneyShell(panel, () => { if (Dungeon.I) Dungeon.I.CloseRoute(); }, null);
            shell.Subtitle.text = "연결된 방의 문에서 F 키로 이동합니다.";
            shell.SetLegend("룸 상태", (CurrentMark, "현재 위치"), (DoneMark, "완료"), (SeenMark, "방문"), (UnseenMark, "미방문"));

            roomArt = BetaUi.Art("Journey/RoomNode");
            currentArt = BetaUi.Art("Journey/CurrentRoomNode");
            doorLayer = BetaUi.Box(shell.Graph, "Doors", 0, 0, 1920, 1080);
            for (int i = 0; i < rooms.Length; i++)
            {
                var room = new RoomVisual();
                room.Rect = BetaUi.Box(shell.Graph, "Room " + (i + 1), 0, 0, NodeWidth, NodeHeight);
                room.Rect.pivot = new Vector2(.5f, .5f);
                room.Art = room.Rect.gameObject.AddComponent<Image>();
                room.Art.raycastTarget = false;
                room.Number = BetaUi.Label(room.Rect, "Number", 0, 12, NodeWidth, 44, 32, Earth, true, TextAnchor.MiddleCenter, true);
                room.Lock = BetaUi.Picture(room.Rect, "Lock", 177, 21, 26, 26, BetaUi.Art("Journey/LockIcon"));
                room.Name = BetaUi.Label(room.Rect, "Name", 26, 56, NodeWidth - 52, 40, 27, BetaUi.Ink, true, TextAnchor.MiddleCenter);
                room.Kind = BetaUi.Label(room.Rect, "Kind", 26, 95, NodeWidth - 52, 28, 21, Earth, false, TextAnchor.MiddleCenter);
                room.State = BetaUi.Label(room.Rect, "State", 26, 122, NodeWidth - 52, 26, 18, Earth, true, TextAnchor.MiddleCenter);
                rooms[i] = room;
            }

            if (travel)
            {
                Adopt(travel, shell.Root, 1430, 896, 438, 70);
                var art = travel.GetComponent<Image>();
                if (art) { art.sprite = BetaUi.Art("Journey/TravelButton"); art.type = Image.Type.Simple; art.color = Color.white; }
                var label = travel.GetComponentInChildren<Text>(true);
                if (label)
                {
                    label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = new Vector2(20, 8); label.rectTransform.offsetMax = new Vector2(-20, 0);
                    label.font = BetaUi.Body; label.fontSize = 30; label.fontStyle = FontStyle.Bold;
                    label.color = BetaUi.Ink; label.alignment = TextAnchor.MiddleCenter;
                }
            }
            if (close)
            {
                // The footer already draws "M / ESC  × 닫기"; the old button becomes its invisible click area.
                shell.Close.gameObject.SetActive(false);
                Adopt(close, shell.Root, 1622, 994, 280, 52);
                var art = close.GetComponent<Image>();
                if (art) { art.sprite = null; art.color = Color.clear; }
                var label = close.GetComponentInChildren<Text>(true);
                if (label) label.enabled = false;
            }
        }

        static void Adopt(RectTransform rect, Transform parent, float x, float y, float w, float h)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();
        }

        // Room names come from Dungeon so the HUD, door prompts and this map always agree.
        static string RoomName(Dungeon g, StageLayout layout, int index) => g.GetRoomName(index);

        static string KindName(ExplorationRoomKind kind) =>
            kind == ExplorationRoomKind.Sigil ? "성흔 강화" : kind == ExplorationRoomKind.DevilCards ? "인장 선택" :
            kind == ExplorationRoomKind.Entry ? "입구" : "전투";

        static bool Linked(StageLayout layout, int a, int b) =>
            layout == null ? RoomGraph.Adjacent(a, b) : layout.Rooms[a].Neighbors.Contains(b);

        // Rooms sit on a grid: the generated layout gives each room a cell, the authored stage uses RoomGraph.
        void Layout(StageLayout layout)
        {
            laidOut = true;
            shownLayout = layout;
            foreach (var door in doors) Object.Destroy(door);
            doors.Clear();

            var cells = new Vector2[6];
            for (int i = 0; i < 6; i++)
                cells[i] = layout != null ? (Vector2)layout.Rooms[i].GridPosition
                    : new Vector2(RoomGraph.Centers[i].x / 60f, RoomGraph.Centers[i].y / 44f);
            Vector2 min = cells[0], max = cells[0];
            foreach (var cell in cells) { min = Vector2.Min(min, cell); max = Vector2.Max(max, cell); }
            // Shrink the whole graph only when it would not fit the board at full size.
            float scale = Mathf.Min(1, Board.width / ((max.x - min.x) * StepX + NodeWidth),
                Board.height / ((max.y - min.y) * StepY + NodeHeight));
            var points = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                // Grid y grows upward, screen y grows downward.
                points[i] = new Vector2(Board.center.x + (cells[i].x - (min.x + max.x) * .5f) * StepX * scale,
                    Board.center.y - (cells[i].y - (min.y + max.y) * .5f) * StepY * scale);
                rooms[i].Rect.anchoredPosition = new Vector2(points[i].x, -points[i].y);
                rooms[i].Rect.localScale = Vector3.one * scale;
            }
            for (int a = 0; a < 6; a++)
                for (int b = a + 1; b < 6; b++)
                    if (Linked(layout, a, b))
                        doors.Add(BetaUi.Line(doorLayer, $"Door {a + 1}-{b + 1}", points[a], points[b], 4, Door).gameObject);
        }

        public void Refresh(Dungeon g)
        {
            shell.Fit();
            var layout = g.UsesGeneratedStage ? g.Progress.GeneratedLayout : null;
            if (!laidOut || layout != shownLayout) Layout(layout);

            bool town = g.IsTown;
            int stage = Mathf.Clamp(g.Room, 0, Dungeon.RoomNames.Length - 1);
            int here = town ? -1 : g.MapIndex;
            string stageName = Dungeon.RoomNames[stage];

            for (int i = 0; i < 6; i++)
            {
                var room = rooms[i];
                bool current = i == here, done = !town && g.Progress.Cleared(i), seen = !town && g.Progress.Visited(i);
                room.Art.sprite = current ? currentArt : roomArt;
                // Cleared rooms take a gold tint; the lock badge and the state line tell visited from unvisited.
                room.Art.color = !current && done ? new Color(1, .90f, .60f) : Color.white;
                room.Number.text = (i + 1).ToString("00");
                room.Number.color = current ? BetaUi.Gold : Earth;
                // Unvisited rooms carry a lock beside their number, so the number shifts left to stay centred as a pair.
                bool locked = !current && !seen && !done;
                room.Lock.enabled = locked;
                room.Number.rectTransform.anchoredPosition = new Vector2(locked ? -15 : 0, -12);
                room.Name.text = RoomName(g, layout, i);
                room.Name.color = current ? BetaUi.Cream : BetaUi.Ink;
                room.Kind.text = KindName(g.Progress.KindAt(i));
                room.Kind.color = current ? BetaUi.Cream : BetaUi.Ink;
                room.State.text = current ? "현재 위치" : done ? "완료" : seen ? "방문" : "미방문";
                room.State.color = current ? new Color32(150,235,225,255) : done ? new Color32(120,76,10,255) : BetaUi.Ink;
            }

            shell.ChapterNumber.text = (stage + 1).ToString("00");
            shell.ChapterTotal.text = "/ " + ExpeditionProgress.SegmentCount.ToString("00");
            shell.ChapterName.text = layout != null ? BetaUi.StageName(layout.StageType) : "황야";
            shell.PlateKicker.text = town ? "NEXT STAGE" : $"STAGE {stage + 1:00}";
            shell.PlateName.text = stageName;

            if (town)
            {
                shell.Kicker.text = "여정의 시작";
                shell.Title.text = "히르바";
                shell.Sub.text = "히르바  →  " + stageName;
                shell.BoxKicker.text = "현재 위치  /  마을";
                shell.BoxTitle.text = "히르바";
                shell.BoxBody.text = "마을 출구에서 F 키를 누르면\n황야로 출발합니다.";
                shell.BoxFooter.text = $"NEXT  STAGE {stage + 1:00} {stageName}";
            }
            else
            {
                var kind = g.Progress.KindAt(here);
                bool cleared = g.Progress.Cleared(here);
                shell.Kicker.text = $"현재 스테이지  /  STAGE {stage + 1:00}";
                shell.Title.text = stageName;
                shell.Sub.text = $"ROOM {here + 1:00}  ·  {RoomName(g, layout, here)}";
                shell.BoxKicker.text = "현재 룸  /  " + (kind == ExplorationRoomKind.Combat && !cleared ? "전투" : "비전투");
                shell.BoxTitle.text = RoomName(g, layout, here);
                shell.BoxBody.text = g.Progress.SegmentComplete ? "여섯 방을 모두 완료했습니다.\n출구에서 다음 스테이지로 이동하세요."
                    : kind == ExplorationRoomKind.Combat && !cleared ? "이 방의 적을 모두 처치하면\n연결된 문이 열립니다."
                    : kind == ExplorationRoomKind.Sigil && !cleared ? "제단에서 F 키를 누르면\n성흔을 획득하거나 강화할 수 있습니다."
                    : kind == ExplorationRoomKind.DevilCards && !cleared ? "진열대에서 F 키를 누르면\n인장을 고를 수 있습니다."
                    : "방의 문에서 F 키를 누르면\n연결된 다음 룸으로 이동합니다.";
                // Up to two connected rooms that still need clearing.
                string next = "";
                int listed = 0;
                for (int i = 0; i < 6 && listed < 2; i++)
                {
                    if (i == here || g.Progress.Cleared(i) || !Linked(layout, here, i)) continue;
                    next += (listed++ > 0 ? "   ·   " : "") + $"{i + 1:00} {RoomName(g, layout, i)}";
                }
                shell.BoxFooter.text = listed > 0 ? "NEXT  " + next : "연결된 방을 모두 완료했습니다";
            }

            shell.CounterLabelA.text = "방 완료";
            shell.CounterValueA.text = $"{g.MapsCleared} / 6";
            shell.CounterLabelB.text = "방문";
            shell.CounterValueB.text = $"{g.Progress.VisitedMaps} / 6";
            shell.ShowRun(g);

            // At a stage exit the kept "next stage" button takes the place of the return button.
            shell.Action.gameObject.SetActive(!g.RouteTravel);
            shell.SetAction(town ? "마을로 돌아가기" : "현재 룸으로 돌아가기", true);
            shell.Hint.text = g.RouteTravel ? (g.Room == 4 && !town ? "모든 봉인을 해제했습니다." : "다음 목적지: " + Dungeon.RoomNames[Mathf.Min(g.NextRoom, Dungeon.RoomNames.Length - 1)])
                : "현재 룸과 연결된 방을 확인하세요.";
        }
    }
}
