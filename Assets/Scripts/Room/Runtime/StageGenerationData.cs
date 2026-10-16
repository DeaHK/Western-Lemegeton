// Western Lemegeton - StageGenerationData.cs
// 역할: 공통 열거형과 룸/스테이지 데이터 및 ScriptableObject 정의
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    public enum RoomRole { Entry, Combat, Sigil, Shop, StartChoice, Boss, Event, CardMerchant }
    public enum StageType { Basic, Sigil, Shop, Boss, Event, Card }
    public enum TemplateId { T0, T1, T2, T3 }

    [Serializable]
    public class RoomNode
    {
        public int Index;
        public Vector2Int GridPosition;
        public RoomRole Role;
        public int Depth;
        public List<int> Neighbors = new List<int>();
        public string PrefabId = ""; // Stable catalog identifier; no instantiated object here.
    }

    [Serializable]
    public class StageLayout
    {
        public StageType StageType;
        public TemplateId Template;
        public int Seed;
        public int EntryIndex;
        public int ExitIndex;
        public List<RoomNode> Rooms = new List<RoomNode>();
    }

    [Serializable]
    public class RoleCount
    {
        public RoomRole Role;
        [Min(0)] public int Count;
    }

    [CreateAssetMenu(fileName = "StageRoleConfig", menuName = "Western Lemegeton/Stage Role Config")]
    public class StageRoleConfig : ScriptableObject
    {
        public StageType Type = StageType.Basic;
        public List<RoleCount> Counts = new List<RoleCount>();
    }
}
