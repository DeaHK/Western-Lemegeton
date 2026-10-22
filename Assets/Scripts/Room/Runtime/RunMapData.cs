// Western Lemegeton - RunMapData.cs
// 역할: 런 분기 노드와 전체 맵 데이터
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;

namespace WesternLemegeton
{
    /// <summary>런 맵의 한 스테이지 후보. NextIds는 다음 레이어의 노드 ID다.</summary>
    [Serializable]
    public class RunMapNode
    {
        public int Id;
        public int Layer;
        public int Slot;
        public StageType Type;
        public List<int> NextIds = new List<int>();
    }

    /// <summary>런 전체 분기 데이터. 게임 오브젝트를 생성하지 않는다.</summary>
    [Serializable]
    public class RunMapLayout
    {
        public int Seed;
        public List<RunMapNode> Nodes = new List<RunMapNode>();

        public RunMapNode Find(int id) => Nodes.Find(n => n.Id == id);
        public List<RunMapNode> Options(int id)
        {
            var result = new List<RunMapNode>();
            RunMapNode node = Find(id);
            if (node == null) return result;
            foreach (int next in node.NextIds)
            {
                RunMapNode candidate = Find(next);
                if (candidate != null) result.Add(candidate);
            }
            return result;
        }
    }
}
