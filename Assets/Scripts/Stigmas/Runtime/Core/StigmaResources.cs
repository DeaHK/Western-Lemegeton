using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    // Run currency is independent of equipment and survives room travel.
    public sealed class RunWallet
    {
        public int Copper { get; private set; }
        public void Add(int amount) { if (amount > 0) Copper = (int)Math.Min(int.MaxValue, (long)Copper + amount); }
        public bool Spend(int amount)
        {
            if (amount < 0 || Copper < amount) return false;
            Copper -= amount; return true;
        }
        public void Reset() => Copper = 0;
    }

    // Other effects may register their own sources; removing stigma never removes those sources.
    public sealed class BonusHealth
    {
        readonly Dictionary<string, float> caps = new Dictionary<string, float>();
        readonly Dictionary<string, float> remaining = new Dictionary<string, float>();
        public float Current { get { float total = 0; foreach (float value in remaining.Values) total += value; return total; } }
        public float Capacity { get { float max = 0; foreach (var v in caps.Values) max = Mathf.Max(max, v); return max; } }
        public void SetSource(string source, float cap) { caps[source] = Mathf.Max(0, cap); ClampToCap(); }
        public void RemoveSource(string source) { caps.Remove(source); remaining.Remove(source); ClampToCap(); }
        public void Grant(string source, float amount)
        {
            if (!caps.ContainsKey(source)) return;
            remaining.TryGetValue(source, out float old);
            remaining[source] = old + Mathf.Clamp(amount, 0, Mathf.Max(0, Capacity - Current));
        }
        public float Absorb(float damage)
        {
            float pending = Mathf.Max(0, damage);
            foreach (var source in new List<string>(remaining.Keys))
            {
                float used = Mathf.Min(remaining[source], pending);
                remaining[source] -= used; pending -= used;
                if (pending <= 0) break;
            }
            return pending;
        }
        void ClampToCap() { if (Current > Capacity) Absorb(Current - Capacity); }
        public void Clear() { caps.Clear(); remaining.Clear(); }
    }

    // Owned by each Enemy; never stored globally in the equipment manager.
    public sealed class StigmaEnemyState
    {
        public int Marks { get; private set; }
        public int ChargeGrants { get; private set; }
        readonly float[] markSlow = new float[2];
        readonly bool[] markVulnerable = new bool[2];
        float timedSlow, slowRemaining, vulnerableRemaining;
        public float Slow(float maximum) => Mathf.Min(maximum, Mathf.Max(timedSlow, Mathf.Max(markSlow[0], markSlow[1])));
        public bool Vulnerable => vulnerableRemaining > 0 || markVulnerable[0] || markVulnerable[1];
        public void Mark(StigmaOwner owner, float slow, bool vulnerable)
        {
            int index = (int)owner; Marks |= 1 << index;
            markSlow[index] = slow; markVulnerable[index] = vulnerable;
        }
        public void ConvertMarks() { ClearMarks(); ChargeGrants++; }
        void ClearMarks() { Marks = 0; Array.Clear(markSlow, 0, 2); Array.Clear(markVulnerable, 0, 2); }
        public void Debuff(float slow, float seconds, bool vulnerable)
        {
            timedSlow = Mathf.Max(timedSlow, slow); slowRemaining = Mathf.Max(slowRemaining, seconds);
            if (vulnerable) vulnerableRemaining = Mathf.Max(vulnerableRemaining, seconds);
        }
        public void Tick(float dt)
        {
            slowRemaining -= dt; vulnerableRemaining -= dt;
            if (slowRemaining <= 0) timedSlow = 0;
        }
        public void ClearEffects() { ClearMarks(); timedSlow = slowRemaining = vulnerableRemaining = 0; }
    }
}
