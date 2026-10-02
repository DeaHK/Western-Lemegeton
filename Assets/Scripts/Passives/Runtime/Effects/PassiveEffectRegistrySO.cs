using System;
using System.Collections.Generic;
using UnityEngine;
namespace WesternLemegeton.Passives
{
    [CreateAssetMenu(
        fileName = "PassiveEffectRegistry",
        menuName = "Game/Passive/Effect Registry"
    )]
    public class PassiveEffectRegistrySO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string key;
            [SerializeField] private PassiveEffectScript prototype;

            public string Key => key;
            public PassiveEffectScript Prototype => prototype;

#if UNITY_EDITOR
            public Entry(string importedKey, PassiveEffectScript importedPrototype)
            {
                key = importedKey;
                prototype = importedPrototype;
            }
#endif
        }

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, PassiveEffectScript> byKey;

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryCreate(string key, out PassiveEffectScript effect)
        {
            effect = null;

            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            EnsureCache();

            if (!byKey.TryGetValue(key, out PassiveEffectScript prototype) ||
                prototype == null)
            {
                return false;
            }

            effect = Instantiate(prototype);
            effect.name = prototype.name + " (Runtime)";
            effect.hideFlags = HideFlags.DontSave;
            return true;
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            EnsureCache();
            return byKey.ContainsKey(key);
        }

        private void EnsureCache()
        {
            if (byKey != null)
            {
                return;
            }

            byKey = new Dictionary<string, PassiveEffectScript>(StringComparer.Ordinal);

            foreach (Entry entry in entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.Key) ||
                    entry.Prototype == null)
                {
                    continue;
                }

                if (!byKey.ContainsKey(entry.Key))
                {
                    byKey.Add(entry.Key, entry.Prototype);
                }
            }
        }

        private void OnEnable()
        {
            byKey = null;
        }

#if UNITY_EDITOR
        public void SetGeneratedEntries(List<Entry> generatedEntries)
        {
            entries = generatedEntries ?? new List<Entry>();
            byKey = null;
        }
#endif
    }
}
