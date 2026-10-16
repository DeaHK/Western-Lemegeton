// Western Lemegeton - RoomCatalog.cs
// 역할: 역할·지역별 룸 프리팹 결정
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    [Serializable]
    public class RoomCatalogEntry
    {
        public string PrefabId;
        public RoomRole Role;
        public string Region = "Wilderness";
        public GameObject Prefab;
        [Min(0f)] public float Weight = 1f;
    }

    [CreateAssetMenu(fileName = "RoomCatalog", menuName = "Western Lemegeton/Room Catalog")]
    public class RoomCatalog : ScriptableObject
    {
        public List<RoomCatalogEntry> Entries = new List<RoomCatalogEntry>();

        public RoomCatalogEntry Find(string prefabId)
        {
            return Entries.Find(e => e != null && e.PrefabId == prefabId);
        }

        public void AssignPrefabs(StageLayout layout, string region = "Wilderness")
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            // Separate deterministic RNG stream: catalog changes never change the layout graph.
            var rng = new System.Random(unchecked(layout.Seed * 397 ^ 0x4A7F23));
            var usedIds = new HashSet<string>();
            foreach (RoomNode room in layout.Rooms)
            {
                var candidates = new List<RoomCatalogEntry>();
                foreach (RoomCatalogEntry entry in Entries)
                {
                    if (entry == null || entry.Role != room.Role || entry.Region != region || usedIds.Contains(entry.PrefabId)) continue;
                    if (string.IsNullOrWhiteSpace(entry.PrefabId) || entry.Prefab == null || entry.Weight <= 0f) continue;
                    candidates.Add(entry);
                }
                if (candidates.Count == 0)
                    throw new InvalidOperationException($"RoomCatalog: not enough unique prefabs for role {room.Role} in region {region} (room {room.Index}).");
                candidates.Sort((a, b) => string.CompareOrdinal(a.PrefabId, b.PrefabId));
                double total = 0;
                foreach (var candidate in candidates) total += candidate.Weight;
                double roll = rng.NextDouble() * total;
                RoomCatalogEntry chosen = candidates[candidates.Count - 1];
                foreach (var candidate in candidates)
                {
                    roll -= candidate.Weight;
                    if (roll < 0) { chosen = candidate; break; }
                }
                room.PrefabId = chosen.PrefabId;
                usedIds.Add(chosen.PrefabId);
            }
        }
    }
}
