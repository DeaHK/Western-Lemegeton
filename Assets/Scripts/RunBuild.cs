using System;
using UnityEngine;

namespace WesternLemegeton
{
    public enum SeongheunType { Fire, Nature, Butterfly }

    // Stigma runtime state for one run. Passive state belongs to PassiveManager.
    public class RunBuild : MonoBehaviour
    {
        #region Stigma Runtime State

        readonly int[] stacks = new int[3];
        readonly int[] thresholds = { 1, 1, 1 };

        #endregion

        #region Events

        public event Action<SeongheunType, int, int> StackChanged;
        public event Action Reset;

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
            // Thresholds persist; notify only after stigma stacks are cleared.
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
