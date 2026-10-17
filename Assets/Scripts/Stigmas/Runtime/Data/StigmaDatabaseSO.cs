using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    [CreateAssetMenu(menuName = "Western Lemegeton/Stigma/Database")]
    public sealed class StigmaDatabaseSO : ScriptableObject
    {
        public StigmaRulesSO Rules;
        public List<StigmaDefinitionSO> Definitions = new List<StigmaDefinitionSO>();
        public List<StigmaCombinationSO> Combinations = new List<StigmaCombinationSO>();

        public StigmaDefinitionSO Find(string id) => Definitions.Find(d => d && d.Id == id);
        public StigmaCombinationSO FindCombination(StigmaDefinitionSO a, StigmaDefinitionSO b)
        {
            if (!a || !b) return null;
            var exact = Combinations.Find(c => c && c.Matches(a, b));
            if (exact) return exact;
            while (a.UseBaseCombination && a.BaseDefinition) a = a.BaseDefinition;
            while (b.UseBaseCombination && b.BaseDefinition) b = b.BaseDefinition;
            return Combinations.Find(c => c && c.Matches(a, b));
        }

        public bool Validate(out string error)
        {
            error = null;
            if (!Rules) return Fail("Stigma rules are missing.", out error);
            if (Rules.MaxChargeGrantsPerEnemy < 0 || Rules.MaximumSlowFraction < 0 || Rules.MaximumSlowFraction > 1 ||
                float.IsNaN(Rules.MaximumSlowFraction)) return Fail("Invalid stigma rules.", out error);
            if (!ValidCosts(Rules.ReplacementCosts, 3) || !ValidCosts(Rules.UpgradeCosts, 2))
                return Fail("Stigma costs must contain nonnegative entries for every level.", out error);
            foreach (var values in new[] { Rules.FloodRadius, Rules.FloodSeconds, Rules.FloodDashRefund,
                Rules.BloomStunReduction, Rules.KillChainSpeedStep, Rules.MireRadius, Rules.InfernoRadius })
                if (values == null || values.Length != 3 || !Array.TrueForAll(values, FiniteNonnegative))
                    return Fail("Combat tuning requires three finite nonnegative values per tier array.", out error);
            if (Rules.EnemyCopper < 0 || Rules.BossCopper < 0 || Rules.KillChainMaxStacks < 1)
                return Fail("Invalid currency or kill-chain tuning.", out error);
            foreach (float value in new[] { Rules.KnockbackDistance, Rules.BaseHitStunSeconds, Rules.BloomKillRefill,
                Rules.KillChainSeconds, Rules.RushLevel3Seconds, Rules.RushLevel3PenaltySeconds, Rules.FamiliarInterval,
                Rules.FamiliarRange, Rules.FamiliarExplosionRadius, Rules.FamiliarExplosionPercent, Rules.InfernoTargetRange,
                Rules.GroundTickSeconds, Rules.GroundAttackPercent, Rules.LinkBaseAttack, Rules.MireForwardDistance })
                if (!FiniteNonnegative(value)) return Fail("Invalid combat tuning value.", out error);
            if (Definitions == null || Combinations == null) return Fail("Stigma lists cannot be null.", out error);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in Definitions)
            {
                if (!d || string.IsNullOrWhiteSpace(d.Id) || !ids.Add(d.Id) || string.IsNullOrWhiteSpace(d.DisplayName))
                    return Fail("Missing or duplicate stigma definition ID/name.", out error);
                if (d.Levels == null || d.Levels.Length != 3) return Fail(d.Id + ": exactly three levels required.", out error);
                if (!ValidEffects(d.CommonEffects) || (d.UseBaseCombination && !d.BaseDefinition))
                    return Fail(d.Id + ": invalid common effects or combination lineage.", out error);
                if (!Enum.IsDefined(typeof(StigmaOwnerRestriction), d.OwnerRestriction))
                    return Fail(d.Id + ": invalid owner restriction.", out error);
                foreach (var level in d.Levels)
                    if (level == null || !ValidEffects(level.Effects)) return Fail(d.Id + ": invalid level/effect parameters.", out error);
            }
            foreach (var d in Definitions)
            {
                var seen = new HashSet<StigmaDefinitionSO> { d };
                for (var parent = d.BaseDefinition; parent; parent = parent.BaseDefinition)
                    if (!Definitions.Contains(parent) || !seen.Add(parent)) return Fail(d.Id + ": invalid/cyclic base definition.", out error);
            }
            var pairKeys = new HashSet<string>(StringComparer.Ordinal);
            ids.Clear();
            foreach (var c in Combinations)
            {
                if (!c || string.IsNullOrWhiteSpace(c.Id) || !ids.Add(c.Id) || !Definitions.Contains(c.First) ||
                    !Definitions.Contains(c.Second) || !c.First || !c.Second || c.ChargeCost < 1 || c.ChargeCost > int.MaxValue / 2)
                    return Fail("Invalid stigma combination ID, members or cost.", out error);
                // Definition IDs can contain separators; length prefix avoids collisions.
                string a = c.First.Id, b = c.Second.Id;
                if (string.CompareOrdinal(a, b) > 0) { string swap = a; a = b; b = swap; }
                if (!pairKeys.Add(a.Length + ":" + a + b)) return Fail("Duplicate unordered stigma combination.", out error);
                if (string.IsNullOrWhiteSpace(c.SkillModuleId) || c.Levels == null || c.Levels.Length != 3)
                    return Fail(c.Id + ": missing skill/levels.", out error);
                foreach (var level in c.Levels)
                    if (level == null || !ValidEffects(level.Bonuses) || !ValidParameters(level.SkillParameters))
                        return Fail(c.Id + ": invalid combination parameters.", out error);
            }
            return true;
        }

        static bool ValidCosts(int[] values, int count) => values != null && values.Length == count && Array.TrueForAll(values, x => x >= 0);
        static bool FiniteNonnegative(float value) => value >= 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        static bool ValidEffects(StigmaEffectDefinition[] effects)
        {
            if (effects == null) return false;
            foreach (var effect in effects)
                if (effect == null || string.IsNullOrWhiteSpace(effect.ModuleId) || !ValidParameters(effect.Parameters)) return false;
            return true;
        }
        static bool ValidParameters(StigmaParameter[] parameters)
        {
            if (parameters == null) return false;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in parameters)
                if (p == null || string.IsNullOrWhiteSpace(p.Key) || !keys.Add(p.Key) || float.IsNaN(p.Value) || float.IsInfinity(p.Value)) return false;
            return true;
        }
        static bool Fail(string value, out string error) { error = value; return false; }
    }
}
