using System;
namespace WesternLemegeton.Passives
{
    [Serializable]
    public sealed class PassiveInstance
    {
        private readonly PassiveSO data;
        private int level;
        private int stack;

        public PassiveSO Data => data;
        public int PassiveID => data != null ? data.PassiveID : 0;
        public int Level => level;
        public int Stack => stack;
        public string Source { get; }
        public PassiveAcquisitionSource InitialSource { get; }
        public PassiveAcquisitionSource LastSource { get; private set; }
        public PassiveRarity Rarity => data != null ? data.Rarity : PassiveRarity.Common;

        // Stack counts acquisitions of the same passive. Level belongs to a separate
        // upgrade system: current Value scales only with Stack; Level scaling is not implemented.
        public float BaseValue => data != null ? data.Value : 0f;
        public float Value => BaseValue * Stack;

        public PassiveValueType ValueType =>
            data != null ? data.ValueType : PassiveValueType.None;

        public string StatType => data != null ? data.StatType : string.Empty;
        public string ScriptName => data != null ? data.ScriptName : string.Empty;
        public PassiveCategory Category =>
            data != null ? data.Category : PassiveCategory.Flat;

        internal string ModifierSourceKey => $"Passive:{PassiveID}";

        internal PassiveInstance(PassiveSO passive, int initialLevel = 1, int initialStack = 1, string source = null)
        {
            data = passive;
            Source = source;
            InitialSource = ParseAcquisitionSource(source);
            LastSource = InitialSource;
            level = Math.Max(1, initialLevel);
            stack = Math.Max(1, initialStack);
        }

        internal PassiveInstance(PassiveSO passive, PassiveAcquisitionSource source,
            int initialLevel = 1, int initialStack = 1)
            : this(passive, initialLevel, initialStack, source.ToString())
        {
        }

        internal void RecordAcquisition(PassiveAcquisitionSource source)
        {
            LastSource = source;
        }

        private static PassiveAcquisitionSource ParseAcquisitionSource(string source)
        {
            return Enum.TryParse(source, true, out PassiveAcquisitionSource parsed) &&
                Enum.IsDefined(typeof(PassiveAcquisitionSource), parsed)
                ? parsed : PassiveAcquisitionSource.Unknown;
        }

        internal void SetLevel(int newLevel)
        {
            level = Math.Max(1, newLevel);
        }

        internal void SetStack(int newStack)
        {
            stack = Math.Max(1, newStack);
        }
    }
}
