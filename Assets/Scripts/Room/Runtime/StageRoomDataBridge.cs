// Western Lemegeton - StageRoomDataBridge.cs
// 역할: 룸 데이터와 출입구 방향 변환
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    // Pure data adapter: never instantiates rooms or changes authored prefabs.
    public class StageRoomDataBridge : MonoBehaviour
    {
        [SerializeField] private StageLayoutProvider provider;
        public StageLayoutProvider Provider => provider;

        public bool TryGetRoomSpawnData(int roomIndex, out RoomSpawnData data)
        {
            data = null;
            if (provider == null || !provider.HasLayout) return false;
            RoomNode room = provider.GetRoom(roomIndex);
            if (room == null) return false;
            GameObject prefab = provider.GetRoomPrefab(roomIndex);
            if (prefab == null) return false;

            var result = new RoomSpawnData
            {
                RoomIndex = room.Index,
                PrefabId = room.PrefabId,
                Prefab = prefab,
                Role = room.Role,
                GridPosition = room.GridPosition,
                Depth = room.Depth,
                IsEntry = room.Index == provider.CurrentLayout.EntryIndex,
                IsExit = room.Index == provider.CurrentLayout.ExitIndex
            };

            foreach (int neighborIndex in room.Neighbors)
            {
                RoomNode neighbor = provider.GetRoom(neighborIndex);
                if (neighbor == null) { Debug.LogError($"Missing neighbor {neighborIndex} for room {room.Index}.", this); return false; }
                Vector2Int delta = neighbor.GridPosition - room.GridPosition;
                if (!TryDirection(delta, out RoomDirection direction))
                {
                    Debug.LogError($"Room {room.Index} -> {neighbor.Index}: invalid non-adjacent grid delta {delta}.", this);
                    return false;
                }
                result.Connections.Add(new RoomConnectionData { NeighborIndex = neighborIndex, Direction = direction });
            }
            data = result;
            return true;
        }

        public bool TryGetAllRoomSpawnData(out List<RoomSpawnData> rooms)
        {
            rooms = new List<RoomSpawnData>();
            if (provider == null || !provider.HasLayout) return false;
            foreach (RoomNode node in provider.CurrentLayout.Rooms)
            {
                if (!TryGetRoomSpawnData(node.Index, out RoomSpawnData data)) { rooms.Clear(); return false; }
                rooms.Add(data);
            }
            return true;
        }

        private static bool TryDirection(Vector2Int delta, out RoomDirection direction)
        {
            direction = RoomDirection.North;
            if (delta == Vector2Int.up) { direction = RoomDirection.North; return true; }
            if (delta == Vector2Int.right) { direction = RoomDirection.East; return true; }
            if (delta == Vector2Int.down) { direction = RoomDirection.South; return true; }
            if (delta == Vector2Int.left) { direction = RoomDirection.West; return true; }
            return false;
        }

        [ContextMenu("Test Print Room Handoff Data")]
        private void TestPrintRoomHandoffData()
        {
            if (provider == null) { Debug.LogError("Assign StageLayoutProvider.", this); return; }
            if (!provider.TryGenerateStage() || !TryGetAllRoomSpawnData(out List<RoomSpawnData> rooms))
            {
                Debug.LogError("StageRoomDataBridge: generation/handoff failed.", this);
                return;
            }
            foreach (RoomSpawnData room in rooms)
            {
                var links = new List<string>();
                foreach (RoomConnectionData link in room.Connections) links.Add($"{link.Direction}->{link.NeighborIndex}");
                Debug.Log($"Room {room.RoomIndex} / {room.Role} / {room.PrefabId} / Depth {room.Depth} / Grid {room.GridPosition} / Entry {room.IsEntry} / Exit {room.IsExit} / Doors [{string.Join(", ", links)}]", this);
            }
            Debug.Log($"StageRoomDataBridge: {rooms.Count} room handoff records ready (no Instantiate).", this);
        }
    }
}
