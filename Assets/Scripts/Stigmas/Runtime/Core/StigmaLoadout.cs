using System;
using System.Collections.Generic;

namespace WesternLemegeton.Stigmas
{
    public enum StigmaOwner { Player, Raven }
    public enum StigmaChangeKind { Acquired, Equipped, LevelChanged, Reset }

    public readonly struct StigmaEquipment
    {
        public readonly StigmaDefinitionSO Definition;
        public readonly int Level;
        public bool IsEquipped => Definition;
        public StigmaEquipment(StigmaDefinitionSO definition, int level) { Definition = definition; Level = level; }
    }

    public readonly struct StigmaChange
    {
        public readonly StigmaChangeKind Kind;
        public readonly StigmaOwner Owner;
        public readonly StigmaEquipment Before, After;
        public StigmaChange(StigmaChangeKind kind, StigmaOwner owner, StigmaEquipment before, StigmaEquipment after)
        { Kind = kind; Owner = owner; Before = before; After = after; }
    }

    // Pure run state. No currency, attacks, timers, UI or service-room policy.
    public sealed class StigmaLoadout
    {
        readonly StigmaDatabaseSO database;
        readonly List<StigmaDefinitionSO>[] acquired = {
            new List<StigmaDefinitionSO>(), new List<StigmaDefinitionSO>()
        };
        readonly IReadOnlyList<StigmaDefinitionSO>[] acquiredViews;
        readonly Dictionary<string, int>[] levels = {
            new Dictionary<string, int>(StringComparer.Ordinal), new Dictionary<string, int>(StringComparer.Ordinal)
        };
        readonly StigmaEquipment[] equipped = new StigmaEquipment[2];
        public event Action<StigmaChange> Changed;
        public IReadOnlyList<StigmaDefinitionSO> GetAcquired(StigmaOwner owner) => acquiredViews[Index(owner)];
        public int TotalAcquiredCount => acquired[0].Count + acquired[1].Count;
        public StigmaDatabaseSO Database => database;
        public bool BothEquipped => equipped[0].IsEquipped && equipped[1].IsEquipped;
        public int EffectiveLevel => BothEquipped ? Math.Min(equipped[0].Level, equipped[1].Level) : 0;
        public StigmaCombinationSO Combination => database.FindCombination(equipped[0].Definition, equipped[1].Definition);
        public int ChargeCapacity => Combination ? Combination.ChargeCapacity : 0;

        public StigmaLoadout(StigmaDatabaseSO database)
        {
            if (!database) throw new ArgumentNullException(nameof(database));
            if (!database.Validate(out string error)) throw new ArgumentException(error, nameof(database));
            this.database = database;
            acquiredViews = new[] { acquired[0].AsReadOnly(), acquired[1].AsReadOnly() };
        }

        public StigmaEquipment Get(StigmaOwner owner) => equipped[Index(owner)];
        public int GetLevel(StigmaOwner owner, string id) =>
            id != null && levels[Index(owner)].TryGetValue(id, out int level) ? level : 0;
        public int EffectLevel(StigmaOwner owner)
        {
            var item = Get(owner);
            return item.IsEquipped ? item.Definition.UseOwnerLevel ? item.Level : EffectiveLevel : 0;
        }
        public bool Owns(StigmaOwner owner, string id) => acquired[Index(owner)].Exists(d => d.Id == id);
        public bool IsSuperseded(StigmaOwner owner, StigmaDefinitionSO definition) =>
            definition && acquired[Index(owner)].Exists(d => d.IsUpgradeOf(definition));
        public bool IsRoomCandidate(StigmaOwner owner, StigmaDefinitionSO definition) =>
            definition && definition.SupportsOwner(owner) && definition.InitialChoice && !Owns(owner, definition.Id) && !IsSuperseded(owner, definition) &&
            (!definition.BaseDefinition || Owns(owner, definition.BaseDefinition.Id));

        public bool CanEquip(StigmaOwner owner, string id, out string error)
        {
            var definition = acquired[Index(owner)].Find(d => d.Id == id);
            if (!definition) { error = "선택한 대상이 먼저 성흔을 획득해야 합니다."; return false; }
            if (!definition.SupportsOwner(owner)) { error = "이 대상이 장착할 수 없는 성흔입니다."; return false; }
            if (IsSuperseded(owner, definition)) { error = "강화 성흔 보유 중에는 하위 성흔을 장착할 수 없습니다."; return false; }
            if (equipped[Index(owner)].Definition == definition) { error = "이미 장착 중입니다."; return false; }
            error = null; return true;
        }

        public bool TryAcquire(StigmaOwner owner, string id, out string error)
        {
            int index = Index(owner);
            var definition = database.Find(id);
            if (!definition) { error = "등록되지 않은 성흔입니다."; return false; }
            if (!definition.SupportsOwner(owner)) { error = "이 대상이 획득할 수 없는 성흔입니다."; return false; }
            if (Owns(owner, id)) { error = "선택한 대상이 이미 획득한 성흔입니다."; return false; }
            if (IsSuperseded(owner, definition)) { error = "강화 성흔 보유 중에는 하위 성흔을 획득할 수 없습니다."; return false; }
            acquired[index].Add(definition);
            levels[index].Add(id, 1);
            var before = equipped[index];
            bool replaceBase = before.IsEquipped && definition.IsUpgradeOf(before.Definition);
            if (replaceBase) equipped[index] = new StigmaEquipment(definition, GetLevel(owner, id));
            error = null;
            Changed?.Invoke(new StigmaChange(StigmaChangeKind.Acquired, owner, default, new StigmaEquipment(definition, GetLevel(owner, id))));
            if (replaceBase) Changed?.Invoke(new StigmaChange(StigmaChangeKind.Equipped, owner, before, equipped[index]));
            return true;
        }

        // Call only after an acquisition/room transaction has passed its own gates.
        // The debug adapter deliberately bypasses those gameplay gates, not this model's invariants.
        public bool TryEquip(StigmaOwner owner, string id, out string error)
        {
            int index = Index(owner);
            if (!CanEquip(owner, id, out error)) return false;
            var definition = acquired[index].Find(d => d.Id == id);
            var before = equipped[index];
            equipped[index] = new StigmaEquipment(definition, GetLevel(owner, id));
            error = null;
            Changed?.Invoke(new StigmaChange(StigmaChangeKind.Equipped, owner, before, equipped[index]));
            return true;
        }

        public bool TrySetLevel(StigmaOwner owner, int level, out string error)
        {
            int index = Index(owner);
            var before = equipped[index];
            if (!before.IsEquipped || level < 1 || level > StigmaRulesSO.MaximumLevel)
            { error = "장착한 성흔의 단계는 1~3이어야 합니다."; return false; }
            error = null;
            if (before.Level == level) return true;
            levels[index][before.Definition.Id] = level;
            equipped[index] = new StigmaEquipment(before.Definition, level);
            Changed?.Invoke(new StigmaChange(StigmaChangeKind.LevelChanged, owner, before, equipped[index]));
            return true;
        }

        public void ResetRun()
        {
            foreach (var collection in acquired) collection.Clear();
            foreach (var collection in levels) collection.Clear();
            Array.Clear(equipped, 0, equipped.Length);
            Changed?.Invoke(new StigmaChange(StigmaChangeKind.Reset, default, default, default));
        }

        static int Index(StigmaOwner owner)
        {
            int index = (int)owner;
            if (index < 0 || index > 1) throw new ArgumentOutOfRangeException(nameof(owner));
            return index;
        }
    }
}
