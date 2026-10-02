using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton
{
    #region Enums / Passive Data Types

    public enum SeongheunType { Fire, Nature, Butterfly }
    public enum PassiveRarity { Common, Uncommon, Rare }
    public enum PassiveSource { MonsterDrop, ShopPurchase, EventReward }

    public sealed class PassiveDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Icon { get; }

        public PassiveDefinition(string id, string name, string icon)
        {
            Id = id;
            Name = name;
            Icon = icon;
        }
    }

    public sealed class OwnedPassive
    {
        public PassiveDefinition Definition { get; }
        public PassiveRarity Rarity { get; }
        public PassiveSource Source { get; }

        public OwnedPassive(PassiveDefinition definition, PassiveRarity rarity, PassiveSource source)
        {
            Definition = definition;
            Rarity = rarity;
            Source = source;
        }
    }

    #endregion

    // One run's build data. Acquisition policy and item effects belong to the caller.
    public class RunBuild : MonoBehaviour
    {
        #region Stigma Runtime State

        readonly int[] stacks = new int[3];
        readonly int[] thresholds = { 1, 1, 1 };

        #endregion

        #region Passive Definition Catalog

        readonly Dictionary<string, PassiveDefinition> catalog = new Dictionary<string, PassiveDefinition>();

        #endregion

        #region Passive Runtime Inventory

        readonly List<OwnedPassive> inventory = new List<OwnedPassive>();
        public IReadOnlyList<OwnedPassive> Passives => inventory.AsReadOnly();

        #endregion

        #region Events

        public event Action<SeongheunType, int, int> StackChanged;
        public event Action<OwnedPassive> PassiveAcquired;
        public event Action Reset;

        #endregion

        #region Initialization / Catalog Registration

        private void Awake()
        {
            RegisterDefaultPassives();
        }

        private void RegisterDefaultPassives()
        {
            RegisterPassive(new PassiveDefinition("spent_bullet", "악마카드 · 탄환의 계약", "BulletIcon"));
            RegisterPassive(new PassiveDefinition("blue_charm", "악마카드 · 방랑자의 계약", "WaterIcon"));
            RegisterPassive(new PassiveDefinition("raven_seal", "악마카드 · 검은 날개의 계약", "RavenPortrait"));
        }

        #endregion

        #region Passive Lookup / Validation

        public void RegisterPassive(PassiveDefinition definition)
        {
            ValidatePassiveDefinition(definition);
            catalog[definition.Id] = definition;
        }

        private static void ValidatePassiveDefinition(PassiveDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.Name) || !CombatArt.Texture(definition.Icon)) throw new ArgumentException("Passive requires an id, name and valid Combat icon");
        }

        #endregion

        #region Passive Acquisition

        public bool AcquirePassive(string passiveId, PassiveRarity rarity, PassiveSource source)
        {
            // Preserve the original lookup/validation short-circuit order.
            if (passiveId == null || !catalog.TryGetValue(passiveId, out var definition) || !Enum.IsDefined(typeof(PassiveRarity), rarity) || !Enum.IsDefined(typeof(PassiveSource), source)) return false;
            var item = new OwnedPassive(definition, rarity, source);
            RecordPassiveAcquisition(item);
            return true;
        }

        private void RecordPassiveAcquisition(OwnedPassive item)
        {
            inventory.Add(item);
            PassiveAcquired?.Invoke(item);
        }

        #endregion

        #region Passive Bonus Calculation

        private float Bonus(string id, float baseValue)
        {
            float value = 0;
            foreach (var item in inventory)
                if (item.Definition.Id == id) value += baseValue * (1 + .5f * (int)item.Rarity);
            return value;
        }

        public float PlayerDamageBonus => Bonus("spent_bullet", .08f);
        public float MoveSpeedBonus => Bonus("blue_charm", .08f);
        public float RavenDamageBonus => Bonus("raven_seal", .12f);

        #endregion

        #region Stigma Query

        public int Stack(SeongheunType type) => stacks[Index(type)];
        public int Threshold(SeongheunType type) => thresholds[Index(type)];
        public bool IsActive(SeongheunType type) => Stack(type) >= Threshold(type);
        public int EffectLevel(SeongheunType type) => IsActive(type) ? Stack(type) : 0;

        #endregion

        #region Stigma Mutation

        public void SetThreshold(SeongheunType type, int threshold)
        {
            if (threshold < 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            int i = Index(type);
            thresholds[i] = threshold;
            // Threshold changes notify with the same stack as both old and new values.
            StackChanged?.Invoke(type, stacks[i], stacks[i]);
        }

        public void ApplySeongheunStack(SeongheunType type, int increment)
        {
            int i = Index(type);
            long next = (long)stacks[i] + increment;
            SetStack(type, (int)Math.Max(0, Math.Min(int.MaxValue, next)));
        }

        public void SetStack(SeongheunType type, int value)
        {
            int i = Index(type), old = stacks[i];
            stacks[i] = Math.Max(0, value);
            if (old != stacks[i]) StackChanged?.Invoke(type, old, stacks[i]);
        }

        #endregion

        #region Run Reset

        public void ResetRun()
        {
            Array.Clear(stacks, 0, 3);
            inventory.Clear();
            // Catalog and thresholds persist; notify only after runtime state is cleared.
            Reset?.Invoke();
        }

        #endregion

        #region Utility / Validation

        private static int Index(SeongheunType type)
        {
            int n = (int)type;
            if (n < 0 || n >= 3) throw new ArgumentOutOfRangeException(nameof(type));
            return n;
        }

        #endregion
    }
}
