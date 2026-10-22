using System.Collections.Generic;
using UnityEngine;
using WesternLemegeton.Passives;
using WesternLemegeton.Stigmas;

namespace WesternLemegeton
{
    public sealed class GameHUD : MonoBehaviour
    {
        public GameUIView View;
        public bool SettingsOpen { get; private set; }
        public bool StigmaManagementOpen { get; private set; }
        public bool StigmaDebugOpen { get; set; }
        public PassiveAcquireResult? Toast { get; private set; }
        public float ToastOpacity => !Toast.HasValue ? 0 : Mathf.Clamp01(Mathf.Min((2.4f - toastTime) / .15f, toastTime / .3f));
        readonly Queue<PassiveAcquireResult> notices = new Queue<PassiveAcquireResult>();
        StigmaManager observedStigmas;
        PassiveManager observedPassives;
        float toastTime;
        RunState treeReturn;
        public int PendingPassiveNotices => notices.Count + (Toast.HasValue ? 1 : 0);

        // Retry after Dungeon.Awake; component Awake order is not a binding contract.
        void Connect()
        {
            var g = Dungeon.I;
            if (!g) return;
            if (!observedStigmas && g.Stigmas)
            {
                observedStigmas = g.Stigmas;
                observedStigmas.Changed += StigmaChanged;
            }
            if (!observedPassives && g.Passives)
            {
                observedPassives = g.Passives;
                observedPassives.PassiveAcquired += Acquired;
                observedPassives.PassivesReset += ResetPassives;
            }
        }

        void OnEnable() { Connect(); }
        void OnDisable()
        {
            if (observedStigmas)
            {
                observedStigmas.Changed -= StigmaChanged;
            }
            if (observedPassives)
            {
                observedPassives.PassiveAcquired -= Acquired;
                observedPassives.PassivesReset -= ResetPassives;
            }
            observedStigmas = null;
            observedPassives = null;
        }
        void StigmaChanged(StigmaChange change)
        {
            if (change.Kind == StigmaChangeKind.Reset) StigmaManagementOpen = false;
        }
        void Acquired(PassiveAcquireResult result) { if (result.Success) notices.Enqueue(result); }
        void ResetPassives() { notices.Clear(); Toast = null; toastTime = 0; }

        void Update()
        {
            Connect();
            var g = Dungeon.I;
            if (!g) return;
            float dt = Time.unscaledDeltaTime;
            if (Toast.HasValue && g.State != RunState.Route)
            {
                toastTime -= dt;
                if (toastTime <= 0) Toast = null;
            }
            if (!Toast.HasValue && notices.Count > 0) { Toast = notices.Dequeue(); toastTime = 2.4f; }
            if (StigmaManagementOpen && g.State != RunState.Paused) StigmaManagementOpen = false;
        }

        public void OpenSettings() { SettingsOpen = true; }
        public void CloseSettings() { SettingsOpen = false; }
        public void OpenStigmaManagement() { var g = Dungeon.I; if (!g.Running) return; StigmaManagementOpen = true; treeReturn = g.State; g.BeforePause = g.State; g.State = RunState.Paused; }
        public void CloseStigmaManagement() { if (!StigmaManagementOpen) return; StigmaManagementOpen = false; Dungeon.I.State = treeReturn; }
    }
}
