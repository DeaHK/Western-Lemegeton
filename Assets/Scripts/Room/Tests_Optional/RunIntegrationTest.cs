// Western Lemegeton - RunIntegrationTest.cs
// 역할: 개발 전용 런 Seed 반복 테스트
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using UnityEngine;

namespace WesternLemegeton
{
    /// <summary>개발 중에만 사용하는 순수 RunMap Seed 회귀 테스트. 릴리스에서는 제거 가능.</summary>
    public class RunIntegrationTest : MonoBehaviour
    {
        [SerializeField] private int startSeed = 12345;
        [SerializeField, Min(1)] private int testCount = 400;
        [SerializeField] private int[] layerSizes = { 1, 2, 3, 1, 1 };
        [SerializeField] private StageType[] middleTypes = { StageType.Basic, StageType.Sigil, StageType.Shop };

        [ContextMenu("Validate 400 Run Seeds")]
        private void ValidateSeeds()
        {
            int passed = 0;
            for (int i = 0; i < testCount; i++)
            {
                int seed = unchecked(startSeed + i);
                try
                {
                    RunMapLayout a = RunMapGenerator.Generate(seed, layerSizes, middleTypes);
                    RunMapLayout b = RunMapGenerator.Generate(seed, layerSizes, middleTypes);
                    if (Fingerprint(a) != Fingerprint(b)) throw new Exception("Run map seed mismatch.");
                    var progress = new RunExpeditionProgress();
                    progress.Start(a);
                    int guard = layerSizes.Length + 1;
                    while (!progress.RunCompleted && guard-- > 0)
                    {
                        if (!progress.CompleteCurrentStage()) throw new Exception("Cannot complete stage.");
                        var options = progress.GetAvailableNextNodes();
                        if (options.Count == 0) break;
                        if (!progress.TrySelectNextNode(options[0].Id)) throw new Exception("Cannot advance.");
                    }
                    if (!progress.RunCompleted || progress.CompletedNodeIds.Count != layerSizes.Length)
                        throw new Exception("Run did not finish at the boss.");
                    passed++;
                }
                catch (Exception ex) { Debug.LogError($"Run Seed {seed} failed: {ex.Message}", this); }
            }
            Debug.Log($"Run integration validation: {passed}/{testCount} passed", this);
        }

        private static string Fingerprint(RunMapLayout map)
        {
            var text = new System.Text.StringBuilder();
            foreach (RunMapNode node in map.Nodes)
                text.Append(node.Id).Append(':').Append(node.Layer).Append(':').Append(node.Type)
                    .Append(':').Append(string.Join(",", node.NextIds)).Append(';');
            return text.ToString();
        }
    }
}
