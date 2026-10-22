// Western Lemegeton - ExpeditionStageCoordinator.cs
// 역할: 런 노드와 6룸 생성기를 연결하는 상위 API
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    [Serializable]
    public class StageTypeConfigEntry
    {
        public StageType Type;
        public StageRoleConfig Config;
    }

    /// <summary>
    /// RunMap -> RunExpeditionProgress -> StageLayoutProvider를 잇는 UI 비의존 파사드.
    /// 룸 Instantiate, 문 연결, 씬 이동은 다른 담당자의 시스템에서 수행한다.
    /// </summary>
    public class ExpeditionStageCoordinator : MonoBehaviour
    {
        [Header("Existing provider (V8 replacement)")]
        [SerializeField] private StageLayoutProvider stageProvider;
        [Header("Run map generation")]
        [SerializeField] private int runSeed = 12345;
        [SerializeField] private int[] layerSizes = { 1, 2, 3, 2, 1 };
        [SerializeField] private StageType[] middleTypes = { StageType.Basic, StageType.Sigil, StageType.Shop };
        [Header("One 6-room config per StageType used in the run")]
        [SerializeField] private List<StageTypeConfigEntry> stageConfigs = new List<StageTypeConfigEntry>();

        public RunMapLayout CurrentMap { get; private set; }
        public RunExpeditionProgress Progress { get; private set; } = new RunExpeditionProgress();
        public StageLayout CurrentStageLayout => stageProvider != null ? stageProvider.CurrentLayout : null;

        /// <summary>새 런을 생성하고 첫 Basic 스테이지의 6개 방 데이터를 준비한다.</summary>
        public bool StartNewRun(int seed)
        {
            if (stageProvider == null) { Debug.LogError("Assign StageLayoutProvider.", this); return false; }
            try
            {
                RunMapLayout map = RunMapGenerator.Generate(seed, layerSizes, middleTypes);
                // 시작 전에 필요한 StageType 설정이 모두 있는지 확인한다.
                foreach (RunMapNode node in map.Nodes)
                    if (GetConfig(node.Type) == null)
                        throw new InvalidOperationException($"Missing StageRoleConfig for {node.Type}.");
                CurrentMap = map;
                Progress.Start(map);
                return GenerateCurrentStage();
            }
            catch (Exception ex) { Debug.LogError($"StartNewRun failed: {ex.Message}", this); return false; }
        }

        /// <summary>현재 선택된 런 노드의 타입과 고유 Seed로 6개 방을 생성한다.</summary>
        public bool GenerateCurrentStage()
        {
            if (stageProvider == null || Progress.CurrentNode == null) return false;
            StageRoleConfig config = GetConfig(Progress.CurrentNode.Type);
            if (config == null) { Debug.LogError("Stage config missing.", this); return false; }
            // 다른 노드라도 재현 가능하도록 런 Seed와 노드 ID를 섞는다.
            int stageSeed = unchecked((CurrentMap.Seed * 397) ^ (Progress.CurrentNodeId * 7919) ^ 0x47C2A1);
            return stageProvider.TryGenerateStage(config, stageSeed);
        }

        public bool CompleteCurrentStage() => Progress.CompleteCurrentStage();

        /// <summary>현재 스테이지를 완료한 뒤에만 연결된 다음 노드로 이동할 수 있다.</summary>
        public bool TryAdvanceTo(int nextNodeId)
        {
            if (Progress.CurrentNode == null || !Progress.CurrentStageCompleted) return false;
            if (!Progress.CurrentNode.NextIds.Contains(nextNodeId)) return false;
            // 선택 전에 설정 유효성을 확인하여 진행 상태만 바뀌는 문제를 줄인다.
            RunMapNode next = CurrentMap.Find(nextNodeId);
            if (next == null || GetConfig(next.Type) == null) return false;
            if (!Progress.TrySelectNextNode(nextNodeId)) return false;
            return GenerateCurrentStage();
        }

        public List<RunMapNode> GetAvailableNextNodes() => Progress.GetAvailableNextNodes();

        private StageRoleConfig GetConfig(StageType type)
        {
            foreach (StageTypeConfigEntry item in stageConfigs)
                if (item != null && item.Type == type && item.Config != null && item.Config.Type == type) return item.Config;
            return null;
        }

        [ContextMenu("Test: Start Run And Print")]
        private void TestStart()
        {
            if (!StartNewRun(runSeed)) return;
            PrintStatus();
        }

        [ContextMenu("Test: Complete And Advance First Option")]
        private void TestAdvance()
        {
            if (Progress.CurrentNode == null) { Debug.LogWarning("Start run first.", this); return; }
            if (!Progress.CurrentStageCompleted) CompleteCurrentStage();
            List<RunMapNode> options = GetAvailableNextNodes();
            if (options.Count == 0) { PrintStatus(); return; }
            if (TryAdvanceTo(options[0].Id)) PrintStatus();
        }

        [ContextMenu("Test: Auto Complete Run")]
        private void TestAutoRun()
        {
            if (!StartNewRun(runSeed)) return;
            int guard = CurrentMap.Nodes.Count + 1;
            while (!Progress.RunCompleted && guard-- > 0)
            {
                if (!CompleteCurrentStage()) break;
                List<RunMapNode> options = GetAvailableNextNodes();
                if (options.Count == 0) break;
                if (!TryAdvanceTo(options[0].Id)) break;
            }
            PrintStatus();
        }

        private void PrintStatus()
        {
            Debug.Log($"Expedition: Seed={CurrentMap.Seed} Node={Progress.CurrentNodeId} Type={Progress.CurrentNode.Type} " +
                $"Rooms={(CurrentStageLayout == null ? 0 : CurrentStageLayout.Rooms.Count)} " +
                $"Completed={Progress.CompletedNodeIds.Count} RunCompleted={Progress.RunCompleted}", this);
        }
    }
}
