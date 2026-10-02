using UnityEngine;
namespace WesternLemegeton.Passives
{
    public abstract class PassiveEffectScript : ScriptableObject
    {
        public abstract void Apply(
            PassiveEffectContext context,
            PassiveInstance passive
        );

        public abstract void Remove(
            PassiveEffectContext context,
            PassiveInstance passive
        );
    }
}
