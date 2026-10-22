// Western Lemegeton - RoomSpawnData.cs
// 역할: 다른 담당자에게 전달하는 룸 스폰 데이터
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    public enum RoomDirection { North, East, South, West }

    [Serializable]
    public class RoomConnectionData
    {
        public int NeighborIndex;
        public RoomDirection Direction;
    }

    // Read-only handoff snapshot for the room-instantiation owner.
    // RoomIndex is generated runtime identity, NOT RoomAuthoring.RoomIndex on the prefab asset.
    public class RoomSpawnData
    {
        public int RoomIndex;
        public string PrefabId;
        public GameObject Prefab;
        public RoomRole Role;
        public Vector2Int GridPosition;
        public int Depth;
        public bool IsEntry;
        public bool IsExit;
        public List<RoomConnectionData> Connections = new List<RoomConnectionData>();
    }
}
