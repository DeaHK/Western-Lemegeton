using System;
using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    public enum StigmaOwnerRestriction { Any, PlayerOnly, RavenOnly }
    // A new kind is an asset, not a new enum value in every combat actor.
    [Serializable]
    public sealed class StigmaParameter
    {
        public string Key;
        public float Value;
    }

    [Serializable]
    public sealed class StigmaEffectDefinition
    {
        public string ModuleId;
        public StigmaParameter[] Parameters = Array.Empty<StigmaParameter>();
    }

    [Serializable]
    public sealed class StigmaLevelDefinition
    {
        [TextArea] public string Description;
        public StigmaEffectDefinition[] Effects = Array.Empty<StigmaEffectDefinition>();
    }

    [CreateAssetMenu(menuName = "Western Lemegeton/Stigma/Definition")]
    public sealed class StigmaDefinitionSO : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public Sprite Icon;
        public string FallbackIconKey;
        [Tooltip("Eligible for room cards. Enhanced definitions additionally require the same owner's base stigma.")]
        public bool InitialChoice;
        [Tooltip("Upgrade lineage. Owning an upgrade prevents equipping its ancestors; effects are explicitly authored.")]
        public StigmaDefinitionSO BaseDefinition;
        public bool UseBaseCombination;
        public StigmaOwnerRestriction OwnerRestriction;
        [Tooltip("Use the equipped owner's level for individual effects; combination level still uses the lower owner level.")]
        public bool UseOwnerLevel;
        public StigmaEffectDefinition[] CommonEffects = Array.Empty<StigmaEffectDefinition>();
        public StigmaLevelDefinition[] Levels = new StigmaLevelDefinition[3];

        public bool IsEnhanced => BaseDefinition;
        public bool SupportsOwner(StigmaOwner owner) =>
            (owner == StigmaOwner.Player || owner == StigmaOwner.Raven) &&
            (OwnerRestriction == StigmaOwnerRestriction.Any ||
             (OwnerRestriction == StigmaOwnerRestriction.PlayerOnly && owner == StigmaOwner.Player) ||
             (OwnerRestriction == StigmaOwnerRestriction.RavenOnly && owner == StigmaOwner.Raven));
        public StigmaLevelDefinition Level(int level) =>
            Levels != null && level >= 1 && level <= Levels.Length ? Levels[level - 1] : null;

        public bool IsUpgradeOf(StigmaDefinitionSO definition)
        {
            for (var parent = BaseDefinition; parent; parent = parent.BaseDefinition)
                if (parent == definition) return true;
            return false;
        }
        public StigmaEffectDefinition Effect(int level, string module)
        {
            var data = Level(level);
            if (data != null) foreach (var effect in data.Effects) if (effect.ModuleId == module) return effect;
            foreach (var effect in CommonEffects) if (effect.ModuleId == module) return effect;
            return null;
        }
    }
}
