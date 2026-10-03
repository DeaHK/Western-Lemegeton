using System;
using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton.Passives
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PassiveManager))]
    public sealed class PassiveOfferService : MonoBehaviour
    {
        readonly List<PassiveSO> offers = new();
        IReadOnlyList<PassiveSO> offerView;
        PassiveManager manager;
        bool hasContext;
        int seed, contextA, contextB, requestedCount;
        PassiveAcquisitionSource source;

        public IReadOnlyList<PassiveSO> CurrentOffers => offerView ??= offers.AsReadOnly();
        public bool HasOffer => offers.Count > 0;
        public int OfferCount => offers.Count;

        void Awake() => manager = GetComponent<PassiveManager>();

        public bool PrepareOffer(PassiveAcquisitionSource source, int runSeed,
            int contextA, int contextB, int count = 3)
        {
            if (hasContext && seed == runSeed && this.contextA == contextA &&
                this.contextB == contextB && this.source == source && requestedCount == count)
                return HasOffer; // Claimed contexts cannot be rerolled either.

            if (!manager || !manager.Database || count <= 0 ||
                !Enum.IsDefined(typeof(PassiveAcquisitionSource), source))
            {
                Debug.LogError("Passive offer requires an initialized manager/database, valid source and positive count.", this);
                return false;
            }

            var candidates = new List<PassiveSO>();
            var ids = new HashSet<int>();
            foreach (var passive in manager.Database.Passives)
            {
                if (!passive || passive.PassiveID <= 0 || passive.Category != PassiveCategory.Flat ||
                    !passive.HasValue || passive.ValueType == PassiveValueType.None ||
                    string.IsNullOrWhiteSpace(passive.StatType) ||
                    !WesternPassiveStatKeys.IsInitialRuntimeSupported(passive.StatType)) continue;
                if (!ids.Add(passive.PassiveID))
                {
                    Debug.LogError($"Duplicate PassiveID {passive.PassiveID} in offer database.", this);
                    return false;
                }
                candidates.Add(passive);
            }
            if (candidates.Count < count)
            {
                Debug.LogError($"Passive offer needs {count} supported definitions; found {candidates.Count}.", this);
                return false;
            }
            candidates.Sort((a, b) => a.PassiveID.CompareTo(b.PassiveID));
            var random = new System.Random(CombineSeed(runSeed, contextA, contextB, source, count));
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var swap = candidates[i]; candidates[i] = candidates[j]; candidates[j] = swap;
            }
            offers.Clear();
            for (int i = 0; i < count; i++) offers.Add(candidates[i]);
            seed = runSeed; this.contextA = contextA; this.contextB = contextB;
            this.source = source; requestedCount = count; hasContext = true;
            return true;
        }

        static int CombineSeed(int seed, int contextA, int contextB,
            PassiveAcquisitionSource source, int count)
        {
            unchecked
            {
                seed = seed * 31 + contextA;
                seed = seed * 31 + contextB;
                seed = seed * 31 + (int)source;
                return seed * 31 + count;
            }
        }

        public PassiveSO GetOffer(int index) => index >= 0 && index < offers.Count ? offers[index] : null;

        public bool TryClaim(int index, out PassiveAcquireResult result)
        {
            var passive = GetOffer(index);
            if (!manager || !passive)
            {
                result = new PassiveAcquireResult(PassiveAcquireKind.Failed, null, source, 0, 0,
                    "Passive offer is missing or the choice index is invalid.");
                return false;
            }
            result = manager.AcquirePassiveById(passive.PassiveID, source);
            if (!result.Success) return false;
            offers.Clear();
            return true;
        }

        public void ResetRun()
        {
            offers.Clear();
            hasContext = false;
        }
    }
}
