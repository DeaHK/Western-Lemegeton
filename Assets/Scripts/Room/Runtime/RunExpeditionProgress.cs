// Western Lemegeton - RunExpeditionProgress.cs
// 역할: 현재 런 노드, 스테이지 완료 및 다음 선택 상태
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;

namespace WesternLemegeton
{
    /// <summary>UI와 무관한 런 진행 상태. 시작 노드는 자동 선택되며 완료 시 다음 후보가 열린다.</summary>
    public sealed class RunExpeditionProgress
    {
        public RunMapLayout Map { get; private set; }
        public int CurrentNodeId { get; private set; } = -1;
        public bool CurrentStageCompleted { get; private set; }
        public bool RunCompleted { get; private set; }
        public List<int> CompletedNodeIds { get; } = new List<int>();

        public void Start(RunMapLayout map)
        {
            if (map == null || map.Nodes == null || map.Nodes.Count == 0) throw new ArgumentException("Run map is empty.");
            Map = map;
            CurrentNodeId = map.Nodes[0].Id;
            CurrentStageCompleted = false;
            RunCompleted = false;
            CompletedNodeIds.Clear();
        }

        public RunMapNode CurrentNode => Map == null ? null : Map.Find(CurrentNodeId);

        /// <summary>현재 스테이지 완료를 기록한다. 보스 노드 완료 시 런이 종료된다.</summary>
        public bool CompleteCurrentStage()
        {
            if (CurrentNode == null || CurrentStageCompleted || RunCompleted) return false;
            CurrentStageCompleted = true;
            CompletedNodeIds.Add(CurrentNodeId);
            if (CurrentNode.NextIds.Count == 0) RunCompleted = true;
            return true;
        }

        /// <summary>현재 스테이지를 완료한 후 연결된 다음 후보만 선택할 수 있다.</summary>
        public bool TrySelectNextNode(int nextId)
        {
            if (!CurrentStageCompleted || RunCompleted || CurrentNode == null) return false;
            if (!CurrentNode.NextIds.Contains(nextId)) return false;
            CurrentNodeId = nextId;
            CurrentStageCompleted = false;
            return true;
        }

        public List<RunMapNode> GetAvailableNextNodes()
        {
            if (Map == null || !CurrentStageCompleted || RunCompleted) return new List<RunMapNode>();
            return Map.Options(CurrentNodeId);
        }
    }
}
