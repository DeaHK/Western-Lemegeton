// Western Lemegeton - ProceduralRoomGenerator.cs
// 역할: Room 0 Entry, Room 1 Combat 고정 / 나머지 4룸 절차적 생성
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    // Six rooms. Room 0=(0,0) Entry; Room 1=(1,0) Combat. Rooms 2..5 are generated.
    public static class ProceduralRoomGenerator
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public static StageLayout Generate(int seed, StageRoleConfig config, bool allowLoops = true)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var roles = BuildRemainingRoles(config);
            var rng = new System.Random(seed);
            var layout = new StageLayout { Seed = seed, StageType = config.Type, Template = TemplateId.T0, EntryIndex = 0 };
            layout.Rooms.Add(new RoomNode { Index = 0, GridPosition = Vector2Int.zero, Role = RoomRole.Entry });
            layout.Rooms.Add(new RoomNode { Index = 1, GridPosition = Vector2Int.right, Role = RoomRole.Combat });
            Connect(layout, 0, 1);

            // Grow a connected tree. Entry never receives additional links.
            for (int index = 2; index < 6; index++)
            {
                var candidates = new List<Placement>();
                for (int parent = 1; parent < layout.Rooms.Count; parent++)
                {
                    foreach (Vector2Int direction in Directions)
                    {
                        Vector2Int target = layout.Rooms[parent].GridPosition + direction;
                        if (target == Vector2Int.zero || Occupied(layout, target)) continue;
                        candidates.Add(new Placement { Parent = parent, Position = target });
                    }
                }
                if (candidates.Count == 0) throw new InvalidOperationException("No valid procedural room placement.");
                Placement chosen = candidates[rng.Next(candidates.Count)];
                layout.Rooms.Add(new RoomNode { Index = index, GridPosition = chosen.Position });
                Connect(layout, chosen.Parent, index);
            }

            // Optional extra grid-adjacent edges create cycles; never bypass the fixed first room.
            if (allowLoops)
            {
                for (int a = 1; a < 6; a++)
                for (int b = a + 1; b < 6; b++)
                {
                    if (layout.Rooms[a].Neighbors.Contains(b)) continue;
                    if (Manhattan(layout.Rooms[a].GridPosition, layout.Rooms[b].GridPosition) != 1) continue;
                    if (rng.NextDouble() < 0.35) Connect(layout, a, b);
                }
            }

            foreach (RoomNode room in layout.Rooms) room.Depth = -1;
            var queue = new Queue<int>();
            layout.Rooms[0].Depth = 0;
            queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int next in layout.Rooms[current].Neighbors)
                {
                    if (layout.Rooms[next].Depth != -1) continue;
                    layout.Rooms[next].Depth = layout.Rooms[current].Depth + 1;
                    queue.Enqueue(next);
                }
            }
            int maxDepth = 0;
            foreach (RoomNode room in layout.Rooms) maxDepth = Math.Max(maxDepth, room.Depth);
            var exits = layout.Rooms.FindAll(room => room.Index >= 2 && room.Depth == maxDepth);
            if (exits.Count == 0) throw new InvalidOperationException("No valid exit room.");
            layout.ExitIndex = exits[rng.Next(exits.Count)].Index;

            for (int i = roles.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                RoomRole temp = roles[i]; roles[i] = roles[j]; roles[j] = temp;
            }
            for (int i = 2; i < 6; i++) layout.Rooms[i].Role = roles[i - 2];
            foreach (RoomNode room in layout.Rooms) room.Neighbors.Sort();
            return layout;
        }

        private static List<RoomRole> BuildRemainingRoles(StageRoleConfig config)
        {
            var roles = new List<RoomRole>();
            if (config.Counts == null) throw new InvalidOperationException("StageRoleConfig Counts is missing.");
            foreach (RoleCount count in config.Counts)
            {
                if (count == null || count.Count < 0) throw new InvalidOperationException("Invalid role count.");
                for (int i = 0; i < count.Count; i++) roles.Add(count.Role);
            }
            if (roles.Count != 6) throw new InvalidOperationException("StageRoleConfig must contain exactly six roles.");
            if (roles.FindAll(r => r == RoomRole.Entry).Count != 1) throw new InvalidOperationException("Exactly one Entry is required.");
            if (!roles.Remove(RoomRole.Entry) || !roles.Remove(RoomRole.Combat))
                throw new InvalidOperationException("Procedural mode requires at least one Combat for fixed Room 1.");
            return roles;
        }

        private static bool Occupied(StageLayout layout, Vector2Int position)
        {
            foreach (RoomNode room in layout.Rooms) if (room.GridPosition == position) return true;
            return false;
        }
        private static int Manhattan(Vector2Int a, Vector2Int b) { return Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y); }
        private static void Connect(StageLayout layout, int a, int b)
        {
            if (!layout.Rooms[a].Neighbors.Contains(b)) layout.Rooms[a].Neighbors.Add(b);
            if (!layout.Rooms[b].Neighbors.Contains(a)) layout.Rooms[b].Neighbors.Add(a);
        }
        private struct Placement { public int Parent; public Vector2Int Position; }
    }
}
