using System.Collections.Generic;
using UnityEngine;
namespace WesternLemegeton.Passives
{
    [CreateAssetMenu(fileName = "PassiveDatabase", menuName = "Game/Passive/Passive Database")]
    public class PassiveDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<PassiveSO> passives = new();
        [SerializeField] private PassiveEffectRegistrySO effectRegistry;

        private Dictionary<int, PassiveSO> byId;

        public IReadOnlyList<PassiveSO> Passives => passives;
        public PassiveEffectRegistrySO EffectRegistry => effectRegistry;

        public bool TryGetById(int passiveID, out PassiveSO passive)
        {
            EnsureCache();
            return byId.TryGetValue(passiveID, out passive);
        }

        public PassiveSO GetById(int passiveID)
        {
            TryGetById(passiveID, out PassiveSO passive);
            return passive;
        }

        private void EnsureCache()
        {
            if (byId != null)
            {
                return;
            }

            byId = new Dictionary<int, PassiveSO>();

            foreach (PassiveSO passive in passives)
            {
                if (passive == null)
                {
                    continue;
                }

                if (!byId.ContainsKey(passive.PassiveID))
                {
                    byId.Add(passive.PassiveID, passive);
                }
            }
        }

        private void OnEnable()
        {
            byId = null;
        }

#if UNITY_EDITOR
        public void SetImportedPassives(List<PassiveSO> importedPassives)
        {
            passives = importedPassives ?? new List<PassiveSO>();
            byId = null;
        }

        public void SetEffectRegistry(PassiveEffectRegistrySO registry)
        {
            effectRegistry = registry;
        }
#endif
    }
}
