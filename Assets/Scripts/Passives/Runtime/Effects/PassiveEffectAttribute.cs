using System;
namespace WesternLemegeton.Passives
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PassiveEffectAttribute : Attribute
    {
        public string Key { get; }

        public PassiveEffectAttribute(string key)
        {
            Key = key;
        }
    }
}
