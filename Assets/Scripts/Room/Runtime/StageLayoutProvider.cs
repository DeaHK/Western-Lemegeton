// Western Lemegeton - StageLayoutProvider.cs
// 역할: 생성 모드 및 Seed 제어, 캐시, 스테이지 재생성
// 주의: 기존 게임의 ExpeditionProgress.cs / RoomAuthoring 및 실제 Instantiate 로직은 수정하지 않음.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    public enum RoomGenerationMode { TemplatesT0ToT3, ProceduralFixedFirstTwo }

    // Data only. Does not instantiate rooms or modify RoomAuthoring / Dungeon.
    public class StageLayoutProvider : MonoBehaviour
    {
        [Header("Generation inputs")]
        [SerializeField] private StageRoleConfig roleConfig;
        [SerializeField] private RoomCatalog roomCatalog;
        [SerializeField] private string region = "Wilderness";
        [SerializeField] private int seed = 12345;
        [SerializeField] private bool useRandomTemplate = true;
        [SerializeField] private TemplateId fixedTemplate = TemplateId.T0;
        [Header("V7 procedural rooms")]
        [SerializeField] private RoomGenerationMode generationMode = RoomGenerationMode.ProceduralFixedFirstTwo;
        [SerializeField] private bool allowLoops = true;

        public StageLayout CurrentLayout { get; private set; }
        public bool HasLayout => CurrentLayout != null;
        public string LastError { get; private set; }
        public event Action<StageLayout> OnLayoutGenerated;

        /// <summary>Inspector 설정으로 최초 한 번 생성한다. 이미 있으면 캐시를 재사용한다.</summary>
        public bool TryGenerateStage() => TryGenerateStageInternal(roleConfig, seed, false);

        /// <summary>런 노드 전환용: 설정과 Seed를 명시하면 항상 새 StageLayout을 생성한다.</summary>
        public bool TryGenerateStage(StageRoleConfig overrideConfig, int overrideSeed)
        {
            return TryGenerateStageInternal(overrideConfig, overrideSeed, true);
        }

        private bool TryGenerateStageInternal(StageRoleConfig config, int requestedSeed, bool forceRegenerate)
        {
            if (!forceRegenerate && HasLayout) return true;
            if (!TryPrepareStage(config, requestedSeed, out StageLayout generated)) return false;
            CommitPreparedStage(generated);
            NotifyLayoutGenerated();
            return true;
        }

        // Generation is transactional: preparation never changes the active layout or emits events.
        internal bool TryPrepareStage(StageRoleConfig config, int requestedSeed, out StageLayout layout)
        {
            layout = null;
            LastError = null;
            if (config == null || roomCatalog == null)
            {
                LastError = "Assign Role Config and Room Catalog.";
                return false;
            }
            try
            {
                StageLayout generated = generationMode == RoomGenerationMode.ProceduralFixedFirstTwo
                    ? ProceduralRoomGenerator.Generate(requestedSeed, config, allowLoops)
                    : StageGenerator.Generate(requestedSeed, config, useRandomTemplate ? (TemplateId?)null : fixedTemplate);
                StageLayoutValidator.Validate(generated, config, generationMode == RoomGenerationMode.ProceduralFixedFirstTwo);
                roomCatalog.AssignPrefabs(generated, region);
                layout = generated;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        internal void CommitPreparedStage(StageLayout layout)
        {
            CurrentLayout = layout;
            seed = layout.Seed;
        }

        internal void NotifyLayoutGenerated()
        {
            if (OnLayoutGenerated == null) return;
            foreach (Action<StageLayout> listener in OnLayoutGenerated.GetInvocationList())
                try { listener(CurrentLayout); }
                catch (Exception ex) { Debug.LogException(ex, this); }
        }

        public RoomNode GetRoom(int roomIndex)
        {
            return HasLayout ? CurrentLayout.Rooms.Find(room => room.Index == roomIndex) : null;
        }
        public List<RoomNode> GetConnectedRooms(int roomIndex)
        {
            var connected = new List<RoomNode>();
            RoomNode room = GetRoom(roomIndex);
            if (room == null) return connected;
            foreach (int index in room.Neighbors)
            {
                RoomNode neighbor = GetRoom(index);
                if (neighbor != null) connected.Add(neighbor);
            }
            return connected;
        }
        public GameObject GetRoomPrefab(int roomIndex)
        {
            RoomNode room = GetRoom(roomIndex);
            if (room == null || roomCatalog == null) return null;
            RoomCatalogEntry entry = roomCatalog.Find(room.PrefabId);
            return entry != null ? entry.Prefab : null;
        }
        public int GetDepth(int roomIndex)
        {
            RoomNode room = GetRoom(roomIndex);
            return room != null ? room.Depth : -1;
        }
        public void ResetForNextStage(int nextSeed)
        {
            CurrentLayout = null;
            seed = nextSeed;
        }
        [ContextMenu("Reset Cached Layout (Keep Seed)")]
        private void ResetCachedLayout() { CurrentLayout = null; Debug.Log("Stage layout cache cleared.", this); }
        [ContextMenu("Regenerate With Next Seed And Print")]
        private void RegenerateNextSeed() { ResetForNextStage(unchecked(seed + 1)); TestGenerateAndPrint(); }
        [ContextMenu("Test Generate And Print")]
        private void TestGenerateAndPrint()
        {
            if (!TryGenerateStage()) return;
            Debug.Log($"StageLayoutProvider: Mode {generationMode} / {CurrentLayout.StageType} / Seed {CurrentLayout.Seed} / Exit {CurrentLayout.ExitIndex}", this);
            foreach (RoomNode room in CurrentLayout.Rooms)
                Debug.Log($"Room {room.Index} / {room.Role} / {room.PrefabId} / Grid {room.GridPosition} / Depth {room.Depth} / Neighbors [{string.Join(",", room.Neighbors)}]", this);
        }
    }
}
