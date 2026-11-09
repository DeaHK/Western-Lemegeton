using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WesternLemegeton
{
    /// <summary>Owns generated instances; adapts ready stage data to the existing Dungeon.</summary>
    public sealed class StageDungeonBuilder : MonoBehaviour
    {
        [SerializeField] private RunStageSelectionController controller;
        [SerializeField] private Dungeon dungeon;
        [SerializeField] private Vector2 gridSpacing = new Vector2(60, 44);
        public bool IsBuilding { get; private set; }
        public string LastError { get; private set; }
        public StageLayout ActiveLayout { get; private set; }
        public RoomAuthoring[] ActiveRooms { get; private set; }
        public GameObject ActiveRoot { get; private set; }
        public int InstalledNodeId { get; private set; } = -1;
        public RunStageSelectionController Controller => controller;
        private GameObject pendingRoot;
        private RunMapLayout activeRunMap;
        private bool mapVisible;

        public void Configure(RunStageSelectionController source, Dungeon target) { controller = source; dungeon = target; }
        private void OnEnable()
        {
            if (!Application.isPlaying || !controller || !dungeon) return;
            controller.OnStageReady.AddListener(EnterPreparedStage);
            controller.MapVisibilityChanged += MapVisibility;
            dungeon.GeneratedMapRequested += controller.ShowMap;
            dungeon.GeneratedMapCloseRequested += controller.HideMap;
            dungeon.GeneratedRunStartRequested += StartRunFromField;
            dungeon.GeneratedStageReleased += ReleaseActive;
            dungeon.GeneratedStageCompletionRequested += CompleteCurrentStageAtExit;
            dungeon.GeneratedRouteSelectionRequested += SelectFieldRoute;
            controller.SetFieldRouteSelection(true);
            mapVisible = controller.View && controller.View.gameObject.activeInHierarchy;
            UpdateBlock();
            if (controller.HasPreparedStage) EnterPreparedStage(controller.Coordinator.Progress.CurrentNodeId);
        }
        private void OnDisable()
        {
            if (controller)
            {
                controller.OnStageReady.RemoveListener(EnterPreparedStage);
                controller.MapVisibilityChanged -= MapVisibility;
            }
            if (dungeon)
            {
                if (controller) dungeon.GeneratedMapRequested -= controller.ShowMap;
                if (controller) dungeon.GeneratedMapCloseRequested -= controller.HideMap;
                dungeon.GeneratedRunStartRequested -= StartRunFromField;
                dungeon.GeneratedStageReleased -= ReleaseActive;
                dungeon.GeneratedStageCompletionRequested -= CompleteCurrentStageAtExit;
                dungeon.GeneratedRouteSelectionRequested -= SelectFieldRoute;
                dungeon.RunMapInputBlocked = false;
            }
            StopAllCoroutines();
            DiscardPending();
            IsBuilding = false;
            if (controller) controller.SetEntryProgress(false, null);
        }
        private void OnDestroy() { ReleaseActive(); }
        private void MapVisibility(bool visible) { mapVisible = visible; UpdateBlock(); }
        private void UpdateBlock() { if (dungeon) dungeon.RunMapInputBlocked = mapVisible || IsBuilding; }

        public void EnterPreparedStage(int nodeId)
        {
            if (!controller || !dungeon || !controller.HasPreparedStage) return;
            if (ActiveLayout == controller.PreparedLayout && ActiveRoot)
            { controller.HideMap(); return; }
            StopAllCoroutines(); DiscardPending();
            StartCoroutine(Build(nodeId));
        }

        private IEnumerator Build(int nodeId)
        {
            IsBuilding = true; LastError = null; UpdateBlock();
            controller.SetEntryProgress(true, "Preparing rooms...");
            var layout = controller.PreparedLayout;
            var map = controller.Coordinator.CurrentMap;
            var node = map?.Find(nodeId);
            string error = null;
            try { Validate(layout, controller.PreparedRooms, node); }
            catch (Exception ex) { error = ex.Message; }
            if (error != null) { Fail(error); yield break; }

            pendingRoot = new GameObject($"Generated Stage {nodeId} Seed {layout.Seed}");
            SceneManager.MoveGameObjectToScene(pendingRoot, dungeon.gameObject.scene);
            pendingRoot.SetActive(false);
            var authored = new RoomAuthoring[6];
            foreach (var data in controller.PreparedRooms)
            {
                try { authored[data.RoomIndex] = Spawn(data, pendingRoot.transform); }
                catch (Exception ex) { error = ex.Message; }
                if (error != null) { Fail(error); yield break; }
                controller.SetEntryProgress(true, $"Preparing rooms {data.RoomIndex + 1}/6...");
                yield return null;
            }
            if (controller.PreparedLayout != layout || controller.Coordinator.Progress.CurrentNodeId != nodeId)
            { Fail("Prepared stage changed while rooms were being built. Retry entry."); yield break; }
            var previousRoot = ActiveRoot;
            var root = pendingRoot;
            try
            {
                ValidateDoors(layout, authored);
                root.SetActive(true);
                dungeon.EnterGeneratedStage(layout, authored, map.Seed, node.Layer, activeRunMap != map);
                ActiveRoot = root; ActiveLayout = layout; ActiveRooms = authored;
                activeRunMap = map; InstalledNodeId = nodeId;
                pendingRoot = null;
            }
            catch (Exception ex) { error = ex.Message; }
            if (error != null) { Fail(error); yield break; }
            if (previousRoot && previousRoot != root) { previousRoot.SetActive(false); Destroy(previousRoot); }
            IsBuilding = false;
            controller.SetEntryProgress(false, $"ENTERED  •  Node {nodeId} / {layout.StageType}  •  {authored.Length} rooms");
            controller.HideMap(); UpdateBlock();
            Debug.Log($"Generated Dungeon entered: Node={nodeId} Seed={layout.Seed} Rooms={authored.Length} Entry={layout.EntryIndex} Player={dungeon.Pos}", this);
        }

        private void Validate(StageLayout layout, IReadOnlyList<RoomSpawnData> rooms, RunMapNode node)
        {
            if (layout == null || node == null || rooms.Count != 6 || gridSpacing.x < 32 || gridSpacing.y < 20)
                throw new InvalidOperationException("Six rooms, a valid run node and sufficient grid spacing required.");
            if (!dungeon.Scene || !dungeon.Hero || !dungeon.Crow || !dungeon.Cinematics)
                throw new InvalidOperationException("Dungeon scene bindings are not ready.");
            var ids = new HashSet<int>();
            foreach (var data in rooms)
            {
                if (data.RoomIndex < 0 || data.RoomIndex >= 6 || !ids.Add(data.RoomIndex) || !data.Prefab)
                    throw new InvalidOperationException("Invalid room identity or prefab.");
                var authoring = data.Prefab.GetComponent<RoomAuthoring>();
                if (!authoring || !authoring.InitialSpawn || authoring.Doors == null || authoring.Doors.Length == 0 || !authoring.Doors[0])
                    throw new InvalidOperationException($"{data.Prefab.name}: root RoomAuthoring, InitialSpawn and door template required.");
            }
        }

        private RoomAuthoring Spawn(RoomSpawnData data, Transform parent)
        {
            var instance = Instantiate(data.Prefab, parent, false);
            instance.name = $"Room {data.RoomIndex:00} {data.Role} [{data.PrefabId}]";
            instance.transform.localPosition = new Vector3(data.GridPosition.x * gridSpacing.x, 0, data.GridPosition.y * gridSpacing.y);
            instance.transform.localRotation = Quaternion.identity;
            instance.SetActive(false);
            var room = instance.GetComponent<RoomAuthoring>();
            room.RoomIndex = data.RoomIndex; room.Town = false;
            ApplyRoleSpecificObjects(room, data.Role);
            var template = room.Doors[0];
            foreach (var old in room.GetComponentsInChildren<RoomDoor>(true)) old.gameObject.SetActive(false);
            var doors = new List<RoomDoor>();
            foreach (var link in data.Connections)
            {
                Vector2 direction = Direction(link.Direction);
                Vector2 local = new Vector2(direction.x * 13.4f, direction.y * 7.25f);
                doors.Add(CreateDoor(room, template, local, direction, link.NeighborIndex, false));
            }
            // Keep the field exit on an unused side. Putting it at the south side unconditionally
            // caused it to overlap a real south connection door in some generated layouts.
            if (data.IsExit)
            {
                Vector2 exitPosition;
                Vector2 exitDirection;
                if (TryChooseUnusedExitSide(data.Connections, out RoomDirection exitSide))
                {
                    exitDirection = Direction(exitSide);
                    exitPosition = exitDirection * (exitSide == RoomDirection.East || exitSide == RoomDirection.West ? 11f : 5.5f);
                }
                else
                {
                    // All four sides are occupied; put the exit well inside the room rather
                    // than duplicating one of those boundary entrances.
                    exitPosition = Vector2.zero;
                    exitDirection = Vector2.down;
                }
                doors.Add(CreateDoor(room, template, exitPosition, exitDirection, -1, true));
            }
            room.Doors = doors.ToArray();
            Vector2 spawn = RoomAuthoring.Point(room.InitialSpawn) - room.Center;
            room.RuntimeClearAreas.Add(new Rect(spawn - Vector2.one * 1.5f, Vector2.one * 3));
            return room;
        }

        private RoomDoor CreateDoor(RoomAuthoring room, RoomDoor template, Vector2 local, Vector2 outward, int destination, bool exit)
        {
            var door = Instantiate(template, room.transform, false);
            door.name = exit ? "Generated Stage Exit" : $"Generated Door to {destination}";
            door.transform.localPosition = new Vector3(local.x, 0, local.y);
            // Room entrances are camera-facing sprite props. Rotate their positions in the
            // room graph, but preserve the authored visual rotation so side/top exits do not
            // turn the sprite plane edge-on or make the gate look tilted.
            door.Owner = room; door.Destination = destination; door.StageExit = exit;
            var arrival = new GameObject("Generated Arrival").transform;
            arrival.SetParent(room.transform, false);
            Vector2 pad = local - outward * 2;
            arrival.localPosition = new Vector3(pad.x, 0, pad.y); door.Arrival = arrival;
            room.RuntimeClearAreas.Add(Rect.MinMaxRect(Mathf.Min(local.x, pad.x) - 1.8f, Mathf.Min(local.y, pad.y) - 1.8f,
                Mathf.Max(local.x, pad.x) + 1.8f, Mathf.Max(local.y, pad.y) + 1.8f));
            door.gameObject.SetActive(true);
            return door;
        }

        private static void ApplyRoleSpecificObjects(RoomAuthoring room, RoomRole role)
        {
            bool showSigil = role == RoomRole.Sigil;
            bool showCards = role == RoomRole.Shop || role == RoomRole.CardMerchant;
            foreach (Transform child in room.GetComponentsInChildren<Transform>(true))
            {
                string objectName = child.name.Trim();
                if (string.Equals(objectName, "Altar sigil", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objectName, "Stigma altar ground", StringComparison.OrdinalIgnoreCase))
                    child.gameObject.SetActive(showSigil);
                else if (string.Equals(objectName, "Devil card stand", StringComparison.OrdinalIgnoreCase))
                    child.gameObject.SetActive(showCards);
            }
        }

        private static Vector2 Direction(RoomDirection direction)
        {
            switch (direction) { case RoomDirection.North: return Vector2.up; case RoomDirection.East: return Vector2.right;
                case RoomDirection.South: return Vector2.down; default: return Vector2.left; }
        }

        private static bool TryChooseUnusedExitSide(IReadOnlyList<RoomConnectionData> connections, out RoomDirection side)
        {
            // Preserve the old south-facing placement when that side is free, then use another
            // free side. An exit always has a distinct physical doorway from graph connections.
            RoomDirection[] preference = { RoomDirection.South, RoomDirection.North, RoomDirection.East, RoomDirection.West };
            foreach (RoomDirection candidate in preference)
            {
                bool occupied = false;
                foreach (RoomConnectionData connection in connections)
                    if (connection.Direction == candidate) { occupied = true; break; }
                if (occupied) continue;
                side = candidate;
                return true;
            }
            side = RoomDirection.South;
            return false;
        }
        private static void ValidateDoors(StageLayout layout, RoomAuthoring[] rooms)
        {
            foreach (var node in layout.Rooms)
                foreach (int neighbor in node.Neighbors)
                {
                    var door = Array.Find(rooms[node.Index].Doors, d => !d.StageExit && d.Destination == neighbor);
                    var reverse = Array.Find(rooms[neighbor].Doors, d => !d.StageExit && d.Destination == node.Index);
                    if (!door || !reverse || !door.Arrival || !reverse.Arrival) throw new InvalidOperationException("Reciprocal door/arrival missing.");
                }
        }
        private void Fail(string error)
        {
            DiscardPending(); LastError = error; IsBuilding = false;
            controller.SetEntryProgress(false, "Entry failed: " + error + "  Choose Enter stage to retry.");
            controller.ShowMap(); UpdateBlock(); Debug.LogError("Generated Dungeon entry failed: " + error, this);
        }
        private void DiscardPending() { if (pendingRoot) { pendingRoot.SetActive(false); Destroy(pendingRoot); } pendingRoot = null; }
        private void ReleaseActive()
        {
            if (ActiveRoot) { ActiveRoot.SetActive(false); Destroy(ActiveRoot); }
            ActiveRoot = null; ActiveRooms = null; ActiveLayout = null; activeRunMap = null; InstalledNodeId = -1;
        }

        private bool CompleteCurrentStageAtExit()
        {
            if (!controller || !dungeon || !ActiveRoot || IsBuilding || InstalledNodeId < 0) return false;
            var coordinator = controller.Coordinator;
            var progress = coordinator ? coordinator.Progress : null;
            if (progress == null || progress.CurrentNodeId != InstalledNodeId || ActiveLayout != dungeon.Progress.GeneratedLayout)
                return false;

            // The run progress object owns the idempotency guard. Repeated calls cannot
            // append the same node twice or re-trigger the terminal result.
            if (!progress.CurrentStageCompleted && !coordinator.CompleteCurrentStage()) return false;
            if (progress.RunCompleted)
            {
                controller.SetEntryProgress(false, "RUN COMPLETE  •  Final Boss defeated.");
                dungeon.RunMapInputBlocked = false;
                dungeon.State = RunState.Victory;
                Debug.Log($"Run completed at Boss node {InstalledNodeId}; completed={progress.CompletedNodeIds.Count}.", this);
                return true;
            }

            controller.SetEntryProgress(false, $"STAGE CLEAR  •  Node {InstalledNodeId}. Choose an available route.");
            SpawnFieldRouteChoices();
            controller.HideMap();
            Debug.Log($"Stage completed at exit: Node={InstalledNodeId}; completed={progress.CompletedNodeIds.Count}.", this);
            return true;
        }

        private void SpawnFieldRouteChoices()
        {
            if (!controller || ActiveLayout == null || ActiveRooms == null || ActiveLayout.ExitIndex < 0 || ActiveLayout.ExitIndex >= ActiveRooms.Length) return;
            var room = ActiveRooms[ActiveLayout.ExitIndex];
            var exit = Array.Find(room.Doors, door => door && door.StageExit);
            if (!exit) return;
            var options = controller.Coordinator.GetAvailableNextNodes();
            if (options.Count == 0) return;
            exit.gameObject.SetActive(false);
            var template = exit;
            const float spacing = 8f;
            Vector3 spreadAxis = Mathf.Abs(template.transform.localPosition.x) > Mathf.Abs(template.transform.localPosition.z)
                ? Vector3.forward : Vector3.right;
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var choice = Instantiate(template, room.transform, false);
                choice.gameObject.SetActive(true);
                choice.name = $"Field Route to Node {option.Id:00} ({option.Type})";
                choice.StageExit = false;
                choice.Destination = -1;
                choice.RunMapDestination = option.Id;
                choice.RunMapDestinationType = option.Type;
                choice.RunMapRouteIndex = i;
                choice.RefreshRunMapMarkerColor();
                var local = template.transform.localPosition + spreadAxis * ((i - (options.Count - 1) * .5f) * spacing);
                choice.transform.localPosition = local;
                if (choice.Arrival) choice.Arrival.gameObject.SetActive(false);
                room.Doors = Append(room.Doors, choice);
                // Dungeon resolves F/proximity interactions from its bound ExplorationRoom,
                // which snapshots RoomAuthoring.Doors at stage entry. Keep both indexes in sync.
                if (dungeon.CurrentRoom != null && !dungeon.CurrentRoom.Doors.Contains(choice))
                    dungeon.CurrentRoom.Doors.Add(choice);
            }
            controller.SetEntryProgress(false, "STAGE CLEAR  •  가까운 분기 표식으로 이동해 F를 누르세요. M: 지도");
        }

        private bool SelectFieldRoute(int nodeId)
        {
            if (!controller || !ActiveRoot || !dungeon || !dungeon.Progress.SegmentComplete || IsBuilding) return false;
            bool selected = controller.TrySelectNode(nodeId);
            if (!selected) controller.SetEntryProgress(false, "분기 선택 실패 · 지도에서 경로 상태를 확인하세요.");
            return selected;
        }

        private static RoomDoor[] Append(RoomDoor[] doors, RoomDoor added)
        {
            var result = new RoomDoor[doors.Length + 1];
            Array.Copy(doors, result, doors.Length);
            result[result.Length - 1] = added;
            return result;
        }

        private bool StartRunFromField()
        {
            if (!controller || IsBuilding) return false;
            bool started = controller.StartNewRunFromField();
            if (!started) controller.ShowMap();
            return started;
        }
    }
}
