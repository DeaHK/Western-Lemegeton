// Western Lemegeton - RunMapValidator.cs
// 역할: 런 연결·도달 가능성 검사
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;

namespace WesternLemegeton
{
    /// <summary>노드 개수, 연결 범위, 시작 노드로부터의 도달 가능성을 검사한다.</summary>
    public static class RunMapValidator
    {
        public static void Validate(RunMapLayout map, int[] sizes)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            int expected = 0;
            foreach (int size in sizes) expected += size;
            if (map.Nodes.Count != expected) throw new Exception("Node count mismatch.");
            var ids = new HashSet<int>();
            foreach (RunMapNode node in map.Nodes)
            {
                if (!ids.Add(node.Id)) throw new Exception("Duplicate node ID.");
                if (node.Layer < 0 || node.Layer >= sizes.Length) throw new Exception("Invalid layer.");
                if (node.NextIds.Count > 2 || (node.Layer < sizes.Length - 1 && node.NextIds.Count < 1))
                    throw new Exception("Invalid outgoing link count.");
                if (node.Layer == sizes.Length - 1 && node.NextIds.Count != 0)
                    throw new Exception("Boss layer must have no outgoing links.");
                var unique = new HashSet<int>();
                foreach (int nextId in node.NextIds)
                {
                    if (!unique.Add(nextId)) throw new Exception("Duplicate link.");
                    RunMapNode target = map.Find(nextId);
                    if (target == null || target.Layer != node.Layer + 1) throw new Exception("Invalid link target.");
                }
            }
            var reachable = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(map.Nodes[0].Id);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (!reachable.Add(current)) continue;
                foreach (RunMapNode next in map.Options(current)) queue.Enqueue(next.Id);
            }
            if (reachable.Count != map.Nodes.Count) throw new Exception("Some nodes are unreachable.");
            foreach (RunMapNode node in map.Nodes)
            {
                if (node.Layer == 0 && node.Type != StageType.Basic) throw new Exception("Start must be Basic.");
                if (node.Layer == sizes.Length - 1 && node.Type != StageType.Boss) throw new Exception("End must be Boss.");
            }
        }
    }
}
