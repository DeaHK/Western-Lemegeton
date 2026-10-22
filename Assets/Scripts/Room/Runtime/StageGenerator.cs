// Western Lemegeton - StageGenerator.cs
// 역할: 고정 템플릿 모드의 6룸 생성
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    public static class StageGenerator
    {
        // This prototype does NOT instantiate prefabs or change existing RoomGraph / Dungeon.
        public static StageLayout Generate(int seed, StageRoleConfig config, TemplateId? forcedTemplate = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var rng = new System.Random(seed);
            TemplateId template = forcedTemplate ?? (TemplateId)rng.Next(0, 4);
            var source = StageTemplateLibrary.Get(template);
            var result = new StageLayout { Seed = seed, StageType = config.Type, Template = template, EntryIndex = 0 };
            if (source.Positions.Length != 6) throw new InvalidOperationException("Template must have exactly six nodes.");
            for (int i = 0; i < 6; i++) result.Rooms.Add(new RoomNode { Index = i, GridPosition = source.Positions[i] });

            foreach (var edge in source.Edges)
            {
                int a = edge.x, b = edge.y;
                if (a < 0 || a >= 6 || b < 0 || b >= 6 || a == b) throw new InvalidOperationException("Invalid template edge.");
                if (result.Rooms[a].Neighbors.Contains(b)) throw new InvalidOperationException("Duplicate edge.");
                result.Rooms[a].Neighbors.Add(b);
                result.Rooms[b].Neighbors.Add(a);
                Vector2Int d = source.Positions[b] - source.Positions[a];
                if (Mathf.Abs(d.x) + Mathf.Abs(d.y) != 1) throw new InvalidOperationException("Connected nodes must be grid-adjacent.");
            }

            var queue = new Queue<int>();
            foreach (var room in result.Rooms) room.Depth = -1;
            result.Rooms[0].Depth = 0;
            queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int next in result.Rooms[current].Neighbors)
                {
                    if (result.Rooms[next].Depth >= 0) continue;
                    result.Rooms[next].Depth = result.Rooms[current].Depth + 1;
                    queue.Enqueue(next);
                }
            }
            foreach (var room in result.Rooms)
                if (room.Depth < 0) throw new InvalidOperationException("Disconnected template node: " + room.Index);

            // Pick an exit among the farthest reachable rooms; ties are deterministic for the seed.
            int maxDepth = 0;
            foreach (var room in result.Rooms) maxDepth = Math.Max(maxDepth, room.Depth);
            var exits = result.Rooms.FindAll(r => r.Depth == maxDepth && r.Index != 0);
            result.ExitIndex = exits[rng.Next(exits.Count)].Index;

            var roles = new List<RoomRole>();
            foreach (var item in config.Counts)
            {
                if (item == null || item.Count < 0) throw new InvalidOperationException("Invalid role count.");
                for (int i = 0; i < item.Count; i++) roles.Add(item.Role);
            }
            if (roles.Count != 6) throw new InvalidOperationException("StageRoleConfig must contain exactly six room roles.");
            if (roles.FindAll(r => r == RoomRole.Entry).Count != 1) throw new InvalidOperationException("Exactly one Entry role is required.");
            roles.Remove(RoomRole.Entry);
            result.Rooms[0].Role = RoomRole.Entry;
            for (int i = roles.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                RoomRole temp = roles[i]; roles[i] = roles[j]; roles[j] = temp;
            }
            for (int i = 1; i < 6; i++) result.Rooms[i].Role = roles[i - 1];
            return result;
        }
    }
}
