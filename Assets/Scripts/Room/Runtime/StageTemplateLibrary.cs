// Western Lemegeton - StageTemplateLibrary.cs
// 역할: 기존 T0~T3 임시 좌표·연결 템플릿
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using UnityEngine;

namespace WesternLemegeton
{
    public sealed class StageTemplateDefinition
    {
        public readonly TemplateId Id;
        public readonly Vector2Int[] Positions;
        public readonly Vector2Int[] Edges;
        public StageTemplateDefinition(TemplateId id, Vector2Int[] positions, Vector2Int[] edges)
        {
            Id = id; Positions = positions; Edges = edges;
        }
    }

    public static class StageTemplateLibrary
    {
        // TEMPORARY graph layouts. Replace with the approved 11.5 Excel coordinates/links later.
        // Each edge is an undirected pair of node indices; all layouts contain six nodes.
        public static StageTemplateDefinition Get(TemplateId id)
        {
            switch (id)
            {
                case TemplateId.T0:
                    return new StageTemplateDefinition(id,
                        new[] { P(0, 0), P(1, 0), P(2, 0), P(3, 0), P(2, 1), P(3, 1) },
                        new[] { P(0, 1), P(1, 2), P(2, 3), P(2, 4), P(4, 5) });
                case TemplateId.T1:
                    return new StageTemplateDefinition(id,
                        new[] { P(0, 0), P(1, 0), P(2, 0), P(2, 1), P(2, 2), P(3, 2) },
                        new[] { P(0, 1), P(1, 2), P(2, 3), P(3, 4), P(4, 5) });
                case TemplateId.T2:
                    return new StageTemplateDefinition(id,
                        new[] { P(0, 0), P(1, 0), P(2, 0), P(1, 1), P(1, -1), P(2, -1) },
                        new[] { P(0, 1), P(1, 2), P(1, 3), P(1, 4), P(4, 5) });
                case TemplateId.T3:
                    return new StageTemplateDefinition(id,
                        new[] { P(0, 0), P(1, 0), P(2, 0), P(2, 1), P(1, 1), P(3, 1) },
                        new[] { P(0, 1), P(1, 2), P(2, 3), P(3, 4), P(4, 1), P(3, 5) });
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }
        private static Vector2Int P(int x, int y) { return new Vector2Int(x, y); }
    }
}
