using System;
using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    [Serializable]
    public sealed class StigmaCombinationLevel
    {
        [TextArea] public string BonusDescription;
        [TextArea] public string SkillDescription;
        public StigmaEffectDefinition[] Bonuses = Array.Empty<StigmaEffectDefinition>();
        public StigmaParameter[] SkillParameters = Array.Empty<StigmaParameter>();
        public bool HasUnspecifiedDesignValues;
    }

    [CreateAssetMenu(menuName = "Western Lemegeton/Stigma/Combination")]
    public sealed class StigmaCombinationSO : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public StigmaDefinitionSO First;
        public StigmaDefinitionSO Second;
        public string SkillName;
        public string SkillModuleId;
        [Min(1)] public int ChargeCost = 2;
        public StigmaCombinationLevel[] Levels = new StigmaCombinationLevel[3];
        public int ChargeCapacity => checked(ChargeCost * 2);
        public bool Matches(StigmaDefinitionSO a, StigmaDefinitionSO b) =>
            a && b && ((First == a && Second == b) || (First == b && Second == a));
        public StigmaCombinationLevel Level(int level) =>
            Levels != null && level >= 1 && level <= Levels.Length ? Levels[level - 1] : null;

        // Higher tiers inherit unspecified skill parameters in both combat and UI.
        public float SkillParameter(int level, string key, float fallback = 0)
        {
            for (int tier = level; tier >= 1; tier--)
            {
                var data = Level(tier);
                if (data == null) continue;
                foreach (var parameter in data.SkillParameters)
                    if (parameter.Key == key) return parameter.Value;
            }
            return fallback;
        }
    }
}
