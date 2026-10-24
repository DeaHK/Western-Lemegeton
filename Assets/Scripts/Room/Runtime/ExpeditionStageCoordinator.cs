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
        [SerializeField] private int[] layerSizes = { 1, 2, 3, 1, 1 };
        [SerializeField] private StageType[] middleTypes = { StageType.Basic };
        [Header("One 6-room config per StageType used in the run")]
        [SerializeField] private List<StageTypeConfigEntry> stageConfigs = new List<StageTypeConfigEntry>();

        public RunMapLayout CurrentMap { get; private set; }
        public RunExpeditionProgress Progress { get; private set; } = new RunExpeditionProgress();
        public StageLayout CurrentStageLayout => stageProvider != null ? stageProvider.CurrentLayout : null;
        public StageLayoutProvider StageProvider => stageProvider;
        public string LastError { get; private set; }
        public bool IsBusy { get; private set; }
        public event Action StateChanged;
        public event Action<StageLayout> StagePrepared;

        /// <summary>새 런을 생성하고 첫 Basic 스테이지의 6개 방 데이터를 준비한다.</summary>
        public bool StartNewRun(int seed)
        {
            if (IsBusy) return false;
            LastError = null;
            if (stageProvider == null) return Fail("Assign StageLayoutProvider.");
            IsBusy = true;
            try
            {
                RunMapLayout map = RunMapGenerator.Generate(seed, layerSizes, middleTypes);
                // 시작 전에 필요한 StageType 설정이 모두 있는지 확인한다.
                foreach (RunMapNode node in map.Nodes)
                    if (GetConfig(node.Type) == null)
                        throw new InvalidOperationException($"Missing StageRoleConfig for {node.Type}.");
                var progress = new RunExpeditionProgress();
                progress.Start(map);
                if (!Prepare(map, progress.CurrentNode, out StageLayout layout)) return false;
                CurrentMap = map;
                Progress = progress;
                stageProvider.CommitPreparedStage(layout);
                PublishPrepared(layout);
                return true;
            }
            catch (Exception ex) { return Fail(ex.Message); }
            finally { IsBusy = false; }
        }

        /// <summary>현재 선택된 런 노드의 타입과 고유 Seed로 6개 방을 생성한다.</summary>
        public bool GenerateCurrentStage()
        {
            if (IsBusy) return false;
            IsBusy = true;
            LastError = null;
            try
            {
                if (!Prepare(CurrentMap, Progress.CurrentNode, out StageLayout layout)) return false;
                stageProvider.CommitPreparedStage(layout);
                PublishPrepared(layout);
                return true;
            }
            finally { IsBusy = false; }
        }

        public bool CompleteCurrentStage()
        {
            if (IsBusy || !Progress.CompleteCurrentStage()) return false;
            IsBusy = true;
            try { PublishStateChanged(); return true; }
            finally { IsBusy = false; }
        }

        /// <summary>현재 스테이지를 완료한 뒤에만 연결된 다음 노드로 이동할 수 있다.</summary>
        public bool TryAdvanceTo(int nextNodeId)
        {
            if (IsBusy) return false;
            LastError = null;
            if (Progress.RunCompleted || Progress.CurrentNode == null || !Progress.CurrentStageCompleted ||
                !Progress.CurrentNode.NextIds.Contains(nextNodeId)) return Fail("Complete the current stage and select a connected next node.");
            IsBusy = true;
            try
            {
                RunMapNode next = CurrentMap.Find(nextNodeId);
                if (!Prepare(CurrentMap, next, out StageLayout layout)) return false;
                if (!Progress.TrySelectNextNode(nextNodeId)) return Fail("The selected route is no longer available.");
                stageProvider.CommitPreparedStage(layout);
                PublishPrepared(layout);
                return true;
            }
            finally { IsBusy = false; }
        }

        public static int GetStageSeed(int mapSeed, int nodeId) => unchecked((mapSeed * 397) ^ (nodeId * 7919) ^ 0x47C2A1);

        private bool Prepare(RunMapLayout map, RunMapNode node, out StageLayout layout)
        {
            layout = null;
            if (stageProvider == null || map == null || node == null) return Fail("Run, node or StageLayoutProvider is missing.");
            StageRoleConfig config = GetConfig(node.Type);
            if (config == null) return Fail($"Missing StageRoleConfig for {node.Type}.");
            if (!stageProvider.TryPrepareStage(config, GetStageSeed(map.Seed, node.Id), out layout))
                return Fail(stageProvider.LastError);
            return true;
        }

        private bool Fail(string message) { LastError = message; return false; }

        private void PublishPrepared(StageLayout layout)
        {
            // All observers see matching map/node/layout. Listener errors cannot undo a committed stage.
            stageProvider.NotifyLayoutGenerated();
            if (StagePrepared != null)
                foreach (Action<StageLayout> listener in StagePrepared.GetInvocationList())
                    try { listener(layout); } catch (Exception ex) { Debug.LogException(ex, this); }
            PublishStateChanged();
        }

        private void PublishStateChanged()
        {
            if (StateChanged == null) return;
            foreach (Action listener in StateChanged.GetInvocationList())
                try { listener(); } catch (Exception ex) { Debug.LogException(ex, this); }
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
