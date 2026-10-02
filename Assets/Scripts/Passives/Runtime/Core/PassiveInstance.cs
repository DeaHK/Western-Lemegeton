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

        // Runtime effects should read Value from the instance rather than Data.Value.
        // For now it returns the base Excel value. When the upgrade table is added,
        // upgrade scaling can be resolved here without changing every effect script.
        public float Value => data != null ? data.Value : 0f;
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
            level = Math.Max(1, initialLevel);
            stack = Math.Max(1, initialStack);
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
