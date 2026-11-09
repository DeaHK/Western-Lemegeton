using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WesternLemegeton
{
    /// <summary>Explicit development check. Room clears are simulated; this is not a combat victory test.</summary>
    public sealed class GeneratedDungeonSmoke : MonoBehaviour
    {
        public StageDungeonBuilder Builder;
        private IEnumerator Start()
        {
            var checks = Checks();
            bool failed = false;
            while (true)
            {
                bool more = false;
                try { more = checks.MoveNext(); }
                catch (Exception ex) { failed = true; Debug.LogError("Generated Dungeon checks FAILED: " + ex, this); }
                if (failed || !more) break;
                yield return checks.Current;
            }
            if (!failed) Debug.Log("Generated Dungeon checks PASSED: town-exit field start, automatic stage entry, no default Start/Enter button bar, prefab instances, grid/XZ/XY placement, reciprocal doors, Entry, all-edge traversal, combat locks, exactly-once stage completion via exit, five-stage run completion via Boss exit, failed-build retry, stage cleanup, legacy return. Room clears were simulated.", this);
            if (Dungeon.I) Dungeon.I.RunMapInputBlocked = false;
            Destroy(this);
        }

        private IEnumerator Checks()
        {
            var flow = Builder.Controller;
            var d = Dungeon.I;
            var progress = d.Progress;
            var sceneRooms = (RoomAuthoring[])d.Scene.Rooms.Clone();
            d.EnterTown();
            flow.HideMap();
            d.Hero.transform.position = RoomAuthoring.Point(d.Scene.Hirva.TownExit);
            d.RunMapInputBlocked = false;
            bool fieldStarted = d.TryTravel();
            Check(fieldStarted && Builder.IsBuilding,
                $"Town exit field interaction starts the generated run (travel={fieldStarted}, building={Builder.IsBuilding}, state={d.State}, blocked={d.RunMapInputBlocked}, prepared={flow.HasPreparedStage}, error={flow.LastError ?? Builder.LastError ?? "none"})");
            float deadline = Time.realtimeSinceStartup + 120;
            while (Builder.IsBuilding && Time.realtimeSinceStartup < deadline) yield return null;
            Check(!Builder.IsBuilding && Builder.ActiveRoot && Builder.LastError == null, "First build installed");
            Check(d.Progress == progress && d.UsesGeneratedStage && d.MapIndex == flow.PreparedLayout.EntryIndex, "Reuse progress and enter Entry");
            yield return null;
            var gameView = UnityEngine.Object.FindAnyObjectByType<GameUIView>();
            Check(gameView && gameView.GeneratedMinimapActive, "HUD minimap switches to the generated room layout");
            int expectedMiniEdges = 0;
            foreach (var room in d.Progress.GeneratedLayout.Rooms) expectedMiniEdges += room.Neighbors.Count;
            expectedMiniEdges /= 2;
            Check(gameView.GeneratedMinimapEdgeCount == expectedMiniEdges, "HUD minimap draws each generated connection once");
            foreach (var room in d.Progress.GeneratedLayout.Rooms)
                foreach (int neighbor in room.Neighbors)
                    if (neighbor > room.Index)
                    {
                        Vector2 mapDelta = gameView.GetGeneratedMinimapPoint(neighbor) - gameView.GetGeneratedMinimapPoint(room.Index);
                        Vector2Int gridDelta = d.Progress.GeneratedLayout.Rooms[neighbor].GridPosition - room.GridPosition;
                        Check(gridDelta.x != 0 ? Mathf.Sign(mapDelta.x) == Mathf.Sign(gridDelta.x) && Mathf.Abs(mapDelta.y) < .1f :
                            Mathf.Sign(mapDelta.y) == -Mathf.Sign(gridDelta.y) && Mathf.Abs(mapDelta.x) < .1f,
                            "HUD minimap connections follow generated grid positions");
                    }
            Check(d.Progress.MapComplete && d.Enemies.Count == 0 && d.CurrentKind == ExplorationRoomKind.Entry, "Entry safe and clear");
            Check(Vector2.Distance(d.Pos, RoomAuthoring.Point(d.CurrentRoom.Layout.Authored.InitialSpawn)) < .1f, "Player at authored Entry spawn in logical XY");
            Check(Vector2.Distance((Vector2)d.Crow.transform.position, d.Pos) < 2, "Raven follows entry");
            Check(flow.View.transform.Find("StageSelectionControls") == null, "Default RunMap hides the old Start/Enter button bar");
            for (int i = 0; i < 6; i++)
            {
                var data = flow.PreparedRooms[i]; var room = Builder.ActiveRooms[data.RoomIndex];
                Check(room != data.Prefab.GetComponent<RoomAuthoring>() && room != sceneRooms[i], "Runtime prefab instance");
                Check(room.RoomIndex == data.RoomIndex, "Runtime room identity overrides prefab identity");
                foreach (var child in room.GetComponentsInChildren<Transform>(true))
                {
                    if (string.Equals(child.name, "Altar sigil", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(child.name, "Stigma altar ground", StringComparison.OrdinalIgnoreCase))
                        Check(child.gameObject.activeSelf == (data.Role == RoomRole.Sigil), "Sigil scenery only appears in Sigil rooms");
                    if (string.Equals(child.name, "Devil card stand", StringComparison.OrdinalIgnoreCase))
                        Check(child.gameObject.activeSelf == (data.Role == RoomRole.Shop || data.Role == RoomRole.CardMerchant), "Card stands only appear in card rooms");
                }
                var point = room.transform.position;
                Check(Mathf.Approximately(point.x, data.GridPosition.x * 60) && Mathf.Approximately(point.z, data.GridPosition.y * 44) && Mathf.Approximately(point.y, 0), "Grid to XZ");
                Check(room.Doors.Length == data.Connections.Count + (data.IsExit ? 1 : 0), "Only generated connections and exit");
                foreach (var link in data.Connections)
                {
                    var door = Array.Find(room.Doors, item => !item.StageExit && item.Destination == link.NeighborIndex);
                    var reverse = Array.Find(Builder.ActiveRooms[link.NeighborIndex].Doors, item => !item.StageExit && item.Destination == data.RoomIndex);
                    Check(door && reverse && door.Owner == room && door.Arrival && reverse.Arrival, "Door owners and reverse arrivals");
                    var offset = door.LocalPosition;
                    Check(link.Direction == RoomDirection.North ? offset.y > 0 && Mathf.Abs(offset.x) < .1f :
                        link.Direction == RoomDirection.South ? offset.y < 0 && Mathf.Abs(offset.x) < .1f :
                        link.Direction == RoomDirection.East ? offset.x > 0 && Mathf.Abs(offset.y) < .1f : offset.x < 0 && Mathf.Abs(offset.y) < .1f, "Direction placement");
                }
                Check(sceneRooms[i] == d.Scene.Rooms[i], "Authored scene room reference retained");
                if (data.IsExit)
                {
                    var roomExitMarker = Array.Find(room.Doors, door => door && door.StageExit);
                    Check(roomExitMarker, "Exit room has one field-exit marker");
                    foreach (var graphDoor in room.Doors)
                        if (graphDoor && !graphDoor.StageExit)
                            Check(Vector2.Distance(roomExitMarker.LocalPosition, graphDoor.LocalPosition) >= 6f,
                                "Field-exit marker is separated from every room connection door");
                }
            }
            flow.ShowMap(); Check(d.RunMapInputBlocked && !d.Running, "Map blocks gameplay input");
            flow.HideMap(); Check(!d.RunMapInputBlocked, "Closing map resumes input");

            // Traverse every generated edge forward and backward, simulating clears only in this fixture.
            var walk = new List<int>();
            BuildWalk(d.Progress.GeneratedLayout, d.MapIndex, new HashSet<string>(), walk);
            foreach (int target in walk)
            {
                ClearForTesting(d);
                var door = d.CurrentRoom.Doors.Find(item => !item.StageExit && item.Destination == target);
                Check(door, "Door exists for graph edge");
                int previous = d.MapIndex;
                d.Hero.transform.position = door.Position;
                d.RunMapInputBlocked = false;
                Check(d.TryTravel() && d.MapIndex == target, "Field proximity interaction follows generated adjacency");
                var reverse = d.CurrentRoom.Doors.Find(item => !item.StageExit && item.Destination == previous);
                Check(Vector2.Distance(d.Pos, RoomAuthoring.Point(reverse.Arrival)) < .1f, "Arrival pad placement");
                int active = 0; foreach (var room in Builder.ActiveRooms) if (room.gameObject.activeSelf) active++;
                Check(active == 1, "Only current room active");
                if (!d.Progress.MapComplete && d.CurrentKind == ExplorationRoomKind.Combat)
                {
                    Check(d.Enemies.Count > 0, "Existing wave spawn reused");
                    d.Hero.transform.position = reverse.Position;
                    Check(!d.TryTravel(), "Combat exit locked until clear");
                }
                d.RunMapInputBlocked = true;
                yield return null;
            }
            ClearForTesting(d);
            Check(d.Progress.SegmentComplete && d.Progress.VisitedMaps == 6, "All generated rooms reachable and tracked");
            var oldRoot = Builder.ActiveRoot;
            d.Hero.Hp = 57;
            foreach (int target in FindPath(d.Progress.GeneratedLayout, d.MapIndex, d.Progress.GeneratedLayout.ExitIndex))
            {
                var door = d.CurrentRoom.Doors.Find(item => !item.StageExit && item.Destination == target);
                Check(door, "Path to the generated exit room exists");
                d.Hero.transform.position = door.Position; d.RunMapInputBlocked = false;
                Check(d.TryTravel(), "Reach the exit room through the field interaction");
                d.RunMapInputBlocked = true;
                yield return null;
            }
            var stageExit = d.CurrentRoom.Doors.Find(item => item.StageExit);
            d.Hero.transform.position = stageExit.Position;
            d.RunMapInputBlocked = false;
            Check(d.TryTravel(), "Physical stage exit completes through the Dungeon adapter");
            Check(flow.Coordinator.Progress.CurrentStageCompleted && flow.Coordinator.Progress.CompletedNodeIds.Count == 1 && !flow.View.gameObject.activeInHierarchy, "Stage clear records once and returns to field route choices");
            var routeChoices = d.CurrentRoom.Doors.FindAll(item => item && item.RunMapDestination >= 0 && item.gameObject.activeInHierarchy);
            Check(routeChoices.Count == flow.Coordinator.GetAvailableNextNodes().Count && routeChoices.Count > 0, "Available next nodes become physical field markers");
            CheckRouteChoiceReadability(routeChoices);
            Check(!flow.View.IsNodeInteractable(flow.Coordinator.GetAvailableNextNodes()[0].Id), "RunMap nodes are informational; route selection uses field markers");
            int next = routeChoices[0].RunMapDestination;
            d.Hero.transform.position = routeChoices[0].Position;
            Check(d.TryTravel(), "F/proximity interaction selects the physical route marker");
            deadline = Time.realtimeSinceStartup + 120;
            while (Builder.IsBuilding && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Check(Builder.ActiveRoot && !oldRoot && Builder.InstalledNodeId == next, "Old generated stage destroyed after replacement");
            Check(d.Hero.Hp == 57 && d.Progress == progress, "Stage transition retains player health and progress object");

            oldRoot = Builder.ActiveRoot;
            var oldLayout = d.Progress.GeneratedLayout;
            var spacing = typeof(StageDungeonBuilder).GetField("gridSpacing", BindingFlags.Instance | BindingFlags.NonPublic);
            var originalSpacing = spacing.GetValue(Builder);
            spacing.SetValue(Builder, new Vector2(1, 1));
            flow.StartNewRun(12345);
            Check(!Builder.IsBuilding && Builder.LastError != null && Builder.ActiveRoot == oldRoot && d.Progress.GeneratedLayout == oldLayout, "Invalid build preserves existing playable stage");
            spacing.SetValue(Builder, originalSpacing);
            flow.RequestStageEntry();
            deadline = Time.realtimeSinceStartup + 120;
            while (Builder.IsBuilding && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Check(Builder.ActiveRoot && !oldRoot && Builder.LastError == null && d.Hero.Hp == d.Hero.MaxHealth, "Retry installs new run and resets player");
            yield return CompleteRunAtExits(flow, d);
            oldRoot = Builder.ActiveRoot;
            d.EnterTown();
            yield return null;
            Check(!d.UsesGeneratedStage && !oldRoot && d.IsTown && d.Progress.KindAt(2) == ExplorationRoomKind.Sigil, "Legacy town and fixed graph restored");
            d.StartRun(); d.Cinematics.Cancel();
            Check(!d.UsesGeneratedStage && d.Rooms.Length == 6 && d.CurrentRoom.Layout.Authored == sceneRooms[0], "Legacy authored dungeon still starts");
            d.EnterTown();
        }
        private IEnumerator CompleteRunAtExits(RunStageSelectionController flow, Dungeon d)
        {
            int transitions = 0;
            while (!flow.Coordinator.Progress.RunCompleted && transitions < 5)
            {
                var layout = d.Progress.GeneratedLayout;
                Check(layout != null && layout.StageType == flow.Coordinator.Progress.CurrentNode.Type, "Dungeon and RunMap stage are synchronized");
                if (layout.StageType == StageType.Boss)
                    Check(layout.Rooms[layout.ExitIndex].Role == RoomRole.Boss, "Boss-stage exit room uses the Boss role");
                var walk = new List<int>();
                BuildWalk(layout, d.MapIndex, new HashSet<string>(), walk);
                foreach (int target in walk)
                {
                    ClearForTesting(d);
                    var door = d.CurrentRoom.Doors.Find(item => !item.StageExit && item.Destination == target);
                    Check(door, "Connected door exists while completing run");
                    d.Hero.transform.position = door.Position; d.RunMapInputBlocked = false;
                    Check(d.TryTravel(), "Field proximity interaction reaches each room");
                    d.RunMapInputBlocked = true;
                    if (d.CurrentKind == ExplorationRoomKind.Combat && d.Progress.GeneratedLayout.Rooms[d.MapIndex].Role == RoomRole.Boss)
                    {
                        while (d.Progress.Wave < d.Progress.WaveCount - 1) d.Progress.CompleteWave();
                        Check(d.IsBossWave, "Final generated Boss wave is selected");
                    }
                    yield return null;
                }
                ClearForTesting(d);
                Check(d.Progress.SegmentComplete, "All six generated rooms are cleared");
                foreach (int target in FindPath(layout, d.MapIndex, layout.ExitIndex))
                {
                    var door = d.CurrentRoom.Doors.Find(item => !item.StageExit && item.Destination == target);
                    Check(door, "Path to the final exit room exists");
                    d.Hero.transform.position = door.Position; d.RunMapInputBlocked = false;
                    Check(d.TryTravel(), "Reach the stage exit room through the field interaction");
                    d.RunMapInputBlocked = true;
                    yield return null;
                }
                var exit = d.CurrentRoom.Doors.Find(item => item.StageExit);
                d.Hero.transform.position = exit.Position; d.RunMapInputBlocked = false;
                Check(d.TryTravel(), "Stage exit field interaction calls CompleteCurrentStage and routes onward");
                transitions++;
                if (flow.Coordinator.Progress.RunCompleted)
                {
                    Check(flow.Coordinator.Progress.CompletedNodeIds.Count == 5 && d.State == RunState.Victory && !flow.View.gameObject.activeInHierarchy,
                        "Boss exit completes the five-node run and shows the victory result");
                    break;
                }
                Check(flow.Coordinator.Progress.CurrentStageCompleted && flow.Coordinator.Progress.CompletedNodeIds.Count == transitions && !flow.View.gameObject.activeInHierarchy,
                    "Nonterminal exit records one completion and presents field route choices");
                var choices = d.CurrentRoom.Doors.FindAll(item => item && item.RunMapDestination >= 0 && item.gameObject.activeInHierarchy);
                Check(choices.Count == flow.Coordinator.GetAvailableNextNodes().Count && choices.Count > 0, "Every available next node has a field marker");
                CheckRouteChoiceReadability(choices);
                int next = choices[0].RunMapDestination;
                d.Hero.transform.position = choices[0].Position;
                Check(d.TryTravel(), "Select a connected next stage through field interaction");
                float deadline = Time.realtimeSinceStartup + 120;
                while (Builder.IsBuilding && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                Check(!Builder.IsBuilding && Builder.ActiveRoot && Builder.InstalledNodeId == next, "Next generated stage is installed");
            }
            Check(flow.Coordinator.Progress.RunCompleted && transitions == 5, "Run terminates only at final Boss stage exit");
        }
        private static void ClearForTesting(Dungeon d)
        {
            if (d.CurrentKind == ExplorationRoomKind.Combat)
            { int guard = 10; while (!d.Progress.MapComplete && guard-- > 0) d.Progress.CompleteWave(); }
            else d.Progress.CompleteService();
            typeof(Dungeon).GetMethod("DestroyTrackedEnemies", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, null);
            d.Cinematics.Cancel(); d.State = RunState.Exit;
        }
        private static void CheckRouteChoiceReadability(List<RoomDoor> choices)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                Check(choices[i].Marker, "Every field route marker has a visible color indicator");
                Check(choices[i].Marker.color == choices[i].GetExpectedRunMapMarkerColor(), "Field route marker color matches its distinct choice");
                for (int j = i + 1; j < choices.Count; j++)
                {
                    Check(Vector2.Distance(choices[i].LocalPosition, choices[j].LocalPosition) >= 7.9f,
                        "Field route choices have clear interaction spacing");
                    Check(choices[i].Marker.color != choices[j].Marker.color, "Sibling route choices have distinct colors");
                }
            }
        }
        private static void BuildWalk(StageLayout layout, int room, HashSet<string> edges, List<int> walk)
        {
            foreach (int neighbor in layout.Rooms[room].Neighbors)
            {
                string key = Math.Min(room, neighbor) + ":" + Math.Max(room, neighbor);
                if (!edges.Add(key)) continue;
                walk.Add(neighbor); BuildWalk(layout, neighbor, edges, walk); walk.Add(room);
            }
        }
        private static List<int> FindPath(StageLayout layout, int start, int goal)
        {
            var previous = new int[layout.Rooms.Count];
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;
            var queue = new Queue<int>(); previous[start] = start; queue.Enqueue(start);
            while (queue.Count > 0 && previous[goal] < 0)
            {
                int current = queue.Dequeue();
                foreach (int next in layout.Rooms[current].Neighbors)
                    if (previous[next] < 0) { previous[next] = current; queue.Enqueue(next); }
            }
            if (previous[goal] < 0) throw new InvalidOperationException("Exit room is unreachable.");
            var path = new List<int>();
            for (int room = goal; room != start; room = previous[room]) path.Add(room);
            path.Reverse(); return path;
        }
        private static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
    }
}
