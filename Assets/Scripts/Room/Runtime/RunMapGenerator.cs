// Western Lemegeton - RunMapGenerator.cs
// 역할: 런 분기 후보 및 연결 생성
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;

namespace WesternLemegeton
{
    /// <summary>런 전체의 단계별 선택지를 만드는 순수 데이터 생성기. UI와 씬을 다루지 않는다.</summary>
    public static class RunMapGenerator
    {
        public static RunMapLayout Generate(int seed, int[] layerSizes, StageType[] middleTypes)
        {
            if (layerSizes == null || layerSizes.Length < 2 || layerSizes[0] != 1 || layerSizes[layerSizes.Length - 1] != 1)
                throw new ArgumentException("First and last layers must each have one node.");
            if (middleTypes == null || middleTypes.Length == 0) throw new ArgumentException("Middle types required.");
            foreach (int size in layerSizes) if (size < 1) throw new ArgumentException("Layer size must be positive.");
            for (int i = 0; i < layerSizes.Length - 1; i++)
                if (layerSizes[i + 1] > 2 * layerSizes[i])
                    throw new ArgumentException("A layer has too many targets for two outgoing links per source.");

            var random = new Random(seed);
            var map = new RunMapLayout { Seed = seed };
            var layers = new List<List<RunMapNode>>();
            int id = 0;
            for (int layer = 0; layer < layerSizes.Length; layer++)
            {
                var nodes = new List<RunMapNode>();
                for (int slot = 0; slot < layerSizes[layer]; slot++)
                {
                    StageType type = layer == 0 ? StageType.Basic :
                        layer == layerSizes.Length - 1 ? StageType.Boss : middleTypes[random.Next(middleTypes.Length)];
                    var node = new RunMapNode { Id = id++, Layer = layer, Slot = slot, Type = type };
                    nodes.Add(node);
                    map.Nodes.Add(node);
                }
                layers.Add(nodes);
            }

            // 각 목적지에 먼저 유입 연결을 1개씩 배정한다. 출발 노드당 최대 2개를 보장한다.
            // 기존 V6의 임의 배정 후 보완 방식은 일부 Seed에서 용량 부족이 발생할 수 있었다.
            for (int layer = 0; layer < layers.Count - 1; layer++)
            {
                List<RunMapNode> from = layers[layer];
                List<RunMapNode> to = layers[layer + 1];
                var shuffledTargets = new List<RunMapNode>(to);
                Shuffle(shuffledTargets, random);
                var shuffledSources = new List<RunMapNode>(from);
                Shuffle(shuffledSources, random);
                for (int target = 0; target < shuffledTargets.Count; target++)
                    shuffledSources[target % shuffledSources.Count].NextIds.Add(shuffledTargets[target].Id);

                // 아직 출구가 없는 출발 노드에는 목적지 하나를 연결한다.
                foreach (RunMapNode source in shuffledSources)
                    if (source.NextIds.Count == 0) source.NextIds.Add(to[random.Next(to.Count)].Id);
                foreach (RunMapNode source in from) source.NextIds.Sort();
            }
            RunMapValidator.Validate(map, layerSizes);
            return map;
        }

        private static void Shuffle<T>(List<T> values, Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T item = values[i]; values[i] = values[j]; values[j] = item;
            }
        }
    }
}
