// Western Lemegeton - StageLayoutValidator.cs
// 역할: 6룸 좌표, 연결, 깊이, 역할 조건 검사
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WesternLemegeton
{
    // Validation only: does not instantiate rooms or modify production systems.
    public static class StageLayoutValidator
    {
        public static void Validate(StageLayout layout, StageRoleConfig config, bool procedural = false)
        {
            if (layout == null || layout.Rooms == null || layout.Rooms.Count != 6) throw new InvalidOperationException("Layout must contain exactly 6 rooms.");
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (layout.EntryIndex < 0 || layout.EntryIndex >= 6 || layout.ExitIndex < 0 || layout.ExitIndex >= 6 || layout.ExitIndex == layout.EntryIndex)
                throw new InvalidOperationException("Invalid entry or exit index.");

            var positions = new HashSet<Vector2Int>();
            var edges = new HashSet<string>();
            for (int i = 0; i < 6; i++)
            {
                RoomNode room = layout.Rooms[i];
                if (room == null || room.Index != i) throw new InvalidOperationException($"Invalid room index at {i}.");
                if (!positions.Add(room.GridPosition)) throw new InvalidOperationException($"Duplicate grid position at room {i}.");
                if (room.Neighbors == null || room.Neighbors.Count == 0) throw new InvalidOperationException($"Isolated room {i}.");
                var seen = new HashSet<int>();
                foreach (int neighbor in room.Neighbors)
                {
                    if (neighbor < 0 || neighbor >= 6 || neighbor == i || !seen.Add(neighbor)) throw new InvalidOperationException($"Invalid or duplicate neighbor in room {i}.");
                    RoomNode other = layout.Rooms[neighbor];
                    if (other == null || other.Neighbors == null || !other.Neighbors.Contains(i)) throw new InvalidOperationException($"Asymmetric connection {i}-{neighbor}.");
                    Vector2Int delta = room.GridPosition - other.GridPosition;
                    if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1) throw new InvalidOperationException($"Non-adjacent connection {i}-{neighbor}.");
                    edges.Add(Math.Min(i, neighbor) + ":" + Math.Max(i, neighbor));
                }
            }

            int[] distances = new int[6];
            for (int i = 0; i < 6; i++) distances[i] = -1;
            var queue = new Queue<int>();
            distances[layout.EntryIndex] = 0;
            queue.Enqueue(layout.EntryIndex);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighbor in layout.Rooms[current].Neighbors)
                {
                    if (distances[neighbor] >= 0) continue;
                    distances[neighbor] = distances[current] + 1;
                    queue.Enqueue(neighbor);
                }
            }
            int maxDepth = 0;
            for (int i = 0; i < 6; i++)
            {
                if (distances[i] < 0 || layout.Rooms[i].Depth != distances[i]) throw new InvalidOperationException($"Unreachable room or incorrect depth: {i}.");
                maxDepth = Math.Max(maxDepth, distances[i]);
            }
            if (layout.Rooms[layout.ExitIndex].Depth != maxDepth) throw new InvalidOperationException("Exit is not at maximum depth.");
            if (layout.Rooms[layout.EntryIndex].Role != RoomRole.Entry) throw new InvalidOperationException("Entry room has incorrect role.");

            var expected = new Dictionary<RoomRole, int>();
            foreach (RoleCount count in config.Counts)
            {
                if (count == null || count.Count < 0) throw new InvalidOperationException("Invalid role configuration.");
                if (!expected.ContainsKey(count.Role)) expected[count.Role] = 0;
                expected[count.Role] += count.Count;
            }
            var actual = new Dictionary<RoomRole, int>();
            foreach (RoomNode room in layout.Rooms)
            {
                if (!actual.ContainsKey(room.Role)) actual[room.Role] = 0;
                actual[room.Role]++;
            }
            foreach (var pair in expected)
                if (!actual.TryGetValue(pair.Key, out int count) || count != pair.Value) throw new InvalidOperationException($"Incorrect role count: {pair.Key}.");
            if (actual.Count != expected.Count) throw new InvalidOperationException("Unexpected role found.");

            if (procedural)
            {
                if (layout.EntryIndex != 0 || layout.Rooms[0].GridPosition != Vector2Int.zero || layout.Rooms[0].Neighbors.Count != 1 || layout.Rooms[0].Neighbors[0] != 1)
                    throw new InvalidOperationException("Procedural entry must be fixed and connect only to Room 1.");
                if (layout.Rooms[1].Role != RoomRole.Combat || layout.Rooms[1].GridPosition != Vector2Int.right || !layout.Rooms[1].Neighbors.Contains(0))
                    throw new InvalidOperationException("Procedural Room 1 must be fixed Combat at (1,0).");
                return; // Template-specific checks do not apply to procedural layouts.
            }
            if (layout.Template == TemplateId.T1 && edges.Count != 5) throw new InvalidOperationException("T1 must be a 6-node chain (5 edges).");
            if (layout.Template == TemplateId.T1)
            {
                int endpoints = 0;
                foreach (RoomNode room in layout.Rooms)
                {
                    if (room.Neighbors.Count > 2) throw new InvalidOperationException("T1 cannot branch.");
                    if (room.Neighbors.Count == 1) endpoints++;
                }
                if (endpoints != 2) throw new InvalidOperationException("T1 must have two endpoints.");
            }
            if (layout.Template == TemplateId.T2)
            {
                bool hasHub = false;
                foreach (RoomNode room in layout.Rooms) if (room.Neighbors.Count >= 3) hasHub = true;
                if (!hasHub) throw new InvalidOperationException("T2 requires a hub with at least 3 links.");
            }
            if (layout.Template == TemplateId.T3 && edges.Count < 6) throw new InvalidOperationException("T3 requires a cycle.");
        }

        public static string Fingerprint(StageLayout layout)
        {
            var result = new StringBuilder();
            result.Append(layout.StageType).Append('|').Append(layout.Template).Append('|').Append(layout.Seed).Append('|').Append(layout.EntryIndex).Append('|').Append(layout.ExitIndex);
            foreach (RoomNode room in layout.Rooms)
            {
                var links = new List<int>(room.Neighbors);
                links.Sort();
                result.Append(';').Append(room.Index).Append(',').Append(room.GridPosition.x).Append(',').Append(room.GridPosition.y)
                    .Append(',').Append(room.Role).Append(',').Append(room.Depth).Append(',').Append(room.PrefabId).Append(',').Append(string.Join("-", links));
            }
            return result.ToString();
        }
    }
}
