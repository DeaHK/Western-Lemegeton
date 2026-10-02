using System;
using System.Collections.Generic;
using UnityEngine;
namespace WesternLemegeton.Passives
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PassiveRuntimeContext))]
    public sealed class PassiveManager : MonoBehaviour
    {
        private sealed class ActivePassive
        {
            public PassiveInstance Instance;
            public PassiveEffectScript Effect;
            public StatModifierContainer FlatTarget;
        }

        [Header("Database")]
        [SerializeField] private PassiveDatabaseSO database;

        [Tooltip("Optional override. If empty, Database.EffectRegistry is used.")]
        [SerializeField] private PassiveEffectRegistrySO effectRegistryOverride;

        [Header("Test / Starting Passives")]
        [SerializeField] private List<PassiveSO> startingPassives = new();

        [Header("Debug")]
        [SerializeField] private bool logChanges;

        private readonly Dictionary<int, ActivePassive> activePassives = new();

        private PassiveRuntimeContext runtimeContext;
        private PassiveEffectContext effectContext;

        public PassiveDatabaseSO Database => database;
        public PassiveRuntimeContext RuntimeContext => runtimeContext;

        public event Action<PassiveInstance> PassiveAdded;
        public event Action<PassiveInstance> PassiveRemoved;
        public event Action<PassiveInstance> PassiveChanged;

        public IEnumerable<PassiveInstance> ActivePassives
        {
            get
            {
                foreach (ActivePassive active in activePassives.Values)
                {
                    yield return active.Instance;
                }
            }
        }

        private PassiveEffectRegistrySO EffectRegistry =>
            effectRegistryOverride != null
                ? effectRegistryOverride
                : database != null
                    ? database.EffectRegistry
                    : null;

        private void Awake()
        {
            runtimeContext = GetComponent<PassiveRuntimeContext>();
            effectContext = new PassiveEffectContext(this, runtimeContext);
            runtimeContext.RegisterService<PassiveManager>(this);
        }

        private void Start()
        {
            foreach (PassiveSO passive in startingPassives)
            {
                AddPassive(passive);
            }
        }

        private void OnDestroy()
        {
            List<int> ids = new(activePassives.Keys);

            foreach (int id in ids)
            {
                RemovePassive(id);
            }

            if (runtimeContext != null)
            {
                runtimeContext.UnregisterService<PassiveManager>(this);
            }
        }

        // The component survives rooms/stages; only its instances reset with a run.
        public void SetDatabase(PassiveDatabaseSO value)
        {
            if (activePassives.Count != 0)
                throw new InvalidOperationException("Reset passives before changing database.");
            database = value;
        }

        public void ResetRun()
        {
            foreach (int id in new List<int>(activePassives.Keys)) RemovePassive(id);
        }

        public bool AddPassiveById(int passiveID, string source = null)
        {
            if (database == null)
            {
                Debug.LogError($"{name}: PassiveDatabase is not assigned.");
                return false;
            }

            if (!database.TryGetById(passiveID, out PassiveSO passive))
            {
                Debug.LogError($"{name}: PassiveID {passiveID} was not found.");
                return false;
            }

            return AddPassive(passive, source);
        }

        public bool AddPassive(PassiveSO passive, string source = null)
        {
            if (passive == null)
            {
                return false;
            }

            if (activePassives.ContainsKey(passive.PassiveID))
            {
                if (logChanges)
                {
                    Debug.Log(
                        $"{name}: Passive {passive.PassiveID} is already active. " +
                        "Use UpgradePassive/SetPassiveLevel when the upgrade system is used."
                    );
                }

                return false;
            }

            ActivePassive active = new()
            {
                Instance = new PassiveInstance(passive, source: source)
            };

            activePassives.Add(passive.PassiveID, active);

            if (!TryApply(active))
            {
                TryRemoveAppliedEffect(active);
                activePassives.Remove(passive.PassiveID);
                CleanupEffect(active);
                return false;
            }

            PassiveAdded?.Invoke(active.Instance);

            if (logChanges)
            {
                Debug.Log(
                    $"{name}: Passive added - {passive.PassiveID} / " +
                    $"{passive.NameStringKey}"
                );
            }

            return true;
        }

        public bool RemovePassive(int passiveID)
        {
            if (!activePassives.TryGetValue(passiveID, out ActivePassive active))
            {
                return false;
            }

            TryRemoveAppliedEffect(active);
            activePassives.Remove(passiveID);
            PassiveRemoved?.Invoke(active.Instance);
            CleanupEffect(active);

            if (logChanges)
            {
                Debug.Log($"{name}: Passive removed - {passiveID}");
            }

            return true;
        }

        public bool UpgradePassive(int passiveID, int amount = 1)
        {
            if (amount <= 0 ||
                !activePassives.TryGetValue(passiveID, out ActivePassive active))
            {
                return false;
            }

            return Reconfigure(
                active,
                active.Instance.Level + amount,
                active.Instance.Stack
            );
        }

        public bool SetPassiveLevel(int passiveID, int level)
        {
            if (!activePassives.TryGetValue(passiveID, out ActivePassive active))
            {
                return false;
            }

            return Reconfigure(active, Mathf.Max(1, level), active.Instance.Stack);
        }

        public bool AddPassiveStack(int passiveID, int amount = 1)
        {
            if (amount == 0 ||
                !activePassives.TryGetValue(passiveID, out ActivePassive active))
            {
                return false;
            }

            return Reconfigure(
                active,
                active.Instance.Level,
                Mathf.Max(1, active.Instance.Stack + amount)
            );
        }

        public bool SetPassiveStack(int passiveID, int stack)
        {
            if (!activePassives.TryGetValue(passiveID, out ActivePassive active))
            {
                return false;
            }

            return Reconfigure(active, active.Instance.Level, Mathf.Max(1, stack));
        }

        public bool RefreshPassive(int passiveID)
        {
            if (!activePassives.TryGetValue(passiveID, out ActivePassive active))
            {
                return false;
            }

            return Reconfigure(active, active.Instance.Level, active.Instance.Stack);
        }

        public bool HasPassive(int passiveID)
        {
            return activePassives.ContainsKey(passiveID);
        }

        public bool TryGetPassive(int passiveID, out PassiveInstance passive)
        {
            if (activePassives.TryGetValue(passiveID, out ActivePassive active))
            {
                passive = active.Instance;
                return true;
            }

            passive = null;
            return false;
        }

        private bool Reconfigure(ActivePassive active, int newLevel, int newStack)
        {
            int oldLevel = active.Instance.Level;
            int oldStack = active.Instance.Stack;

            TryRemoveAppliedEffect(active);
            CleanupEffect(active);

            active.Instance.SetLevel(newLevel);
            active.Instance.SetStack(newStack);

            if (TryApply(active))
            {
                PassiveChanged?.Invoke(active.Instance);
                return true;
            }

            TryRemoveAppliedEffect(active);
            CleanupEffect(active);
            active.Instance.SetLevel(oldLevel);
            active.Instance.SetStack(oldStack);

            if (!TryApply(active))
            {
                Debug.LogError(
                    $"{name}: Failed to restore PassiveID {active.Instance.PassiveID} " +
                    "after reconfiguration failed."
                );
            }

            return false;
        }

        private bool TryApply(ActivePassive active)
        {
            PassiveInstance passive = active.Instance;

            if (passive == null || passive.Data == null)
            {
                return false;
            }

            switch (passive.Category)
            {
                case PassiveCategory.Flat:
                    return TryApplyFlat(active);

                case PassiveCategory.Ability:
                case PassiveCategory.Shift:
                    return TryApplyScripted(active);

                default:
                    Debug.LogError(
                        $"{name}: Unsupported passive category for " +
                        $"PassiveID {passive.PassiveID}."
                    );
                    return false;
            }
        }

        private bool TryApplyFlat(ActivePassive active)
        {
            PassiveInstance passive = active.Instance;
            StatModifierContainer target = runtimeContext.DefaultFlatStatTarget;

            if (target == null)
            {
                Debug.LogError(
                    $"{name}: PassiveID {passive.PassiveID} is Flat, but " +
                    "PassiveRuntimeContext.DefaultFlatStatTarget is not assigned."
                );
                return false;
            }

            if (string.IsNullOrWhiteSpace(passive.StatType) ||
                !passive.Data.HasValue ||
                passive.ValueType == PassiveValueType.None)
            {
                Debug.LogError(
                    $"{name}: Flat PassiveID {passive.PassiveID} has invalid " +
                    "StatType / Value / ValueType data."
                );
                return false;
            }

            bool applied = target.SetModifier(
                passive.ModifierSourceKey,
                passive.StatType,
                passive.Value,
                passive.ValueType
            );

            if (applied)
            {
                active.FlatTarget = target;
            }

            return applied;
        }

        private bool TryApplyScripted(ActivePassive active)
        {
            PassiveInstance passive = active.Instance;

            if (string.IsNullOrWhiteSpace(passive.ScriptName))
            {
                Debug.LogError(
                    $"{name}: PassiveID {passive.PassiveID} requires ScriptName."
                );
                return false;
            }

            if (EffectRegistry == null)
            {
                Debug.LogError(
                    $"{name}: PassiveEffectRegistry is not assigned. " +
                    "Reimport the passive Excel or rebuild the registry."
                );
                return false;
            }

            if (!EffectRegistry.TryCreate(passive.ScriptName, out PassiveEffectScript effect))
            {
                Debug.LogError(
                    $"{name}: Passive effect '{passive.ScriptName}' is not registered."
                );
                return false;
            }

            active.Effect = effect;

            try
            {
                effect.Apply(effectContext, passive);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        private void TryRemoveAppliedEffect(ActivePassive active)
        {
            PassiveInstance passive = active.Instance;

            if (passive == null)
            {
                return;
            }

            switch (passive.Category)
            {
                case PassiveCategory.Flat:
                    if (active.FlatTarget != null)
                    {
                        active.FlatTarget.RemoveModifier(passive.ModifierSourceKey);
                        active.FlatTarget = null;
                    }
                    break;

                case PassiveCategory.Ability:
                case PassiveCategory.Shift:
                    if (active.Effect != null)
                    {
                        try
                        {
                            active.Effect.Remove(effectContext, passive);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception, this);
                        }
                    }
                    break;
            }
        }

        private static void CleanupEffect(ActivePassive active)
        {
            if (active?.Effect == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(active.Effect);
            active.Effect = null;
        }
    }
}
