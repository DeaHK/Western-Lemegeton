using System;
using System.Collections.Generic;
using UnityEngine;
namespace WesternLemegeton.Passives
{
    [DisallowMultipleComponent]
    public sealed class PassiveRuntimeContext : MonoBehaviour
    {
        [Header("Default Flat Target")]
        [Tooltip(
            "Current Excel Flat rows are applied here. This can be the player's " +
            "StatModifierContainer today, while Ability/Shift effects may target " +
            "enemies, summons, stage systems, or anything else through services."
        )]
        [SerializeField] private StatModifierContainer defaultFlatStatTarget;

        private readonly Dictionary<Type, object> services = new();

        public StatModifierContainer DefaultFlatStatTarget => defaultFlatStatTarget;

        public void SetDefaultFlatStatTarget(StatModifierContainer target)
        {
            defaultFlatStatTarget = target;
        }

        public void RegisterService<T>(T service) where T : class
        {
            if (service == null)
            {
                return;
            }

            services[typeof(T)] = service;
        }

        public void UnregisterService<T>(T service) where T : class
        {
            Type type = typeof(T);

            if (!services.TryGetValue(type, out object current))
            {
                return;
            }

            if (ReferenceEquals(current, service))
            {
                services.Remove(type);
            }
        }

        public bool TryGetService<T>(out T service) where T : class
        {
            Type type = typeof(T);

            if (!services.TryGetValue(type, out object raw))
            {
                service = null;
                return false;
            }

            if (raw is UnityEngine.Object unityObject && unityObject == null)
            {
                services.Remove(type);
                service = null;
                return false;
            }

            service = raw as T;
            return service != null;
        }

        public T GetService<T>() where T : class
        {
            TryGetService(out T service);
            return service;
        }
    }
}
