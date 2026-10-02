using System;
using System.Collections.Generic;
using UnityEngine;
namespace WesternLemegeton.Passives
{
    [DisallowMultipleComponent]
    public sealed class StatModifierContainer : MonoBehaviour
    {
        private sealed class ModifierRecord
        {
            public string StatType;
            public float Value;
            public PassiveValueType ValueType;
        }

        private sealed class ModifierBucket
        {
            public float Raw;
            public float Percent;
        }

        private readonly Dictionary<string, ModifierRecord> modifiersBySource =
            new(StringComparer.Ordinal);

        private readonly Dictionary<string, ModifierBucket> bucketsByStat =
            new(StringComparer.Ordinal);

        public event Action<string> StatChanged;

        public bool SetModifier(
            string sourceKey,
            string statType,
            float value,
            PassiveValueType valueType)
        {
            if (string.IsNullOrWhiteSpace(sourceKey) ||
                string.IsNullOrWhiteSpace(statType) ||
                valueType == PassiveValueType.None)
            {
                return false;
            }

            string oldStatType = null;

            if (modifiersBySource.TryGetValue(sourceKey, out ModifierRecord oldRecord))
            {
                oldStatType = oldRecord.StatType;
                RemoveFromBucket(oldRecord);
                modifiersBySource.Remove(sourceKey);
            }

            ModifierRecord record = new()
            {
                StatType = statType,
                Value = value,
                ValueType = valueType
            };

            modifiersBySource.Add(sourceKey, record);
            AddToBucket(record);

            if (!string.IsNullOrWhiteSpace(oldStatType) &&
                !string.Equals(oldStatType, statType, StringComparison.Ordinal))
            {
                StatChanged?.Invoke(oldStatType);
            }

            StatChanged?.Invoke(statType);
            return true;
        }

        public bool RemoveModifier(string sourceKey)
        {
            if (string.IsNullOrWhiteSpace(sourceKey) ||
                !modifiersBySource.TryGetValue(sourceKey, out ModifierRecord record))
            {
                return false;
            }

            modifiersBySource.Remove(sourceKey);
            RemoveFromBucket(record);
            StatChanged?.Invoke(record.StatType);
            return true;
        }

        public float Evaluate(string statType, float baseValue)
        {
            if (string.IsNullOrWhiteSpace(statType) ||
                !bucketsByStat.TryGetValue(statType, out ModifierBucket bucket))
            {
                return baseValue;
            }

            return (baseValue + bucket.Raw) * (1f + bucket.Percent / 100f);
        }

        public int EvaluateInt(string statType, int baseValue)
        {
            return Mathf.RoundToInt(Evaluate(statType, baseValue));
        }

        public float GetRawBonus(string statType)
        {
            return TryGetBucket(statType, out ModifierBucket bucket)
                ? bucket.Raw
                : 0f;
        }

        public float GetPercentBonus(string statType)
        {
            return TryGetBucket(statType, out ModifierBucket bucket)
                ? bucket.Percent
                : 0f;
        }

        private void AddToBucket(ModifierRecord record)
        {
            if (!bucketsByStat.TryGetValue(record.StatType, out ModifierBucket bucket))
            {
                bucket = new ModifierBucket();
                bucketsByStat.Add(record.StatType, bucket);
            }

            switch (record.ValueType)
            {
                case PassiveValueType.Raw:
                    bucket.Raw += record.Value;
                    break;

                case PassiveValueType.Percent:
                    bucket.Percent += record.Value;
                    break;
            }
        }

        private void RemoveFromBucket(ModifierRecord record)
        {
            if (!bucketsByStat.TryGetValue(record.StatType, out ModifierBucket bucket))
            {
                return;
            }

            switch (record.ValueType)
            {
                case PassiveValueType.Raw:
                    bucket.Raw -= record.Value;
                    break;

                case PassiveValueType.Percent:
                    bucket.Percent -= record.Value;
                    break;
            }

            if (Mathf.Approximately(bucket.Raw, 0f) &&
                Mathf.Approximately(bucket.Percent, 0f))
            {
                bucketsByStat.Remove(record.StatType);
            }
        }

        private bool TryGetBucket(string statType, out ModifierBucket bucket)
        {
            if (string.IsNullOrWhiteSpace(statType))
            {
                bucket = null;
                return false;
            }

            return bucketsByStat.TryGetValue(statType, out bucket);
        }
    }
}
