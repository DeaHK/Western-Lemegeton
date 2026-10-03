using System.Collections.Generic;
using UnityEngine;
using WesternLemegeton.Passives;

namespace WesternLemegeton
{
    public sealed class GameHUD : MonoBehaviour
    {
        public GameUIView View;
        public bool SettingsOpen { get; private set; }
        public bool BuildTreeOpen { get; private set; }
        public int SelectedSigil { get; private set; }
        public PassiveAcquireResult? Toast { get; private set; }
        public float ToastOpacity => !Toast.HasValue ? 0 : Mathf.Clamp01(Mathf.Min((2.4f - toastTime) / .15f, toastTime / .3f));
        readonly float[] pulses = new float[3];
        readonly Queue<PassiveAcquireResult> notices = new Queue<PassiveAcquireResult>();
        RunBuild observed;
        PassiveManager observedPassives;
        float toastTime;
        RunState treeReturn;
        public float StackPulse(SeongheunType type) => Mathf.Max(0, pulses[(int)type] - Time.unscaledTime);
        public int PendingPassiveNotices => notices.Count + (Toast.HasValue ? 1 : 0);

        // Retry after Dungeon.Awake; component Awake order is not a binding contract.
        void Connect()
        {
            var g = Dungeon.I;
            if (!g) return;
            if (!observed && g.Build)
            {
                observed = g.Build;
                observed.StackChanged += StackChanged;
                observed.Reset += ResetBuild;
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
            if (observed)
            {
                observed.StackChanged -= StackChanged;
                observed.Reset -= ResetBuild;
            }
            if (observedPassives)
            {
                observedPassives.PassiveAcquired -= Acquired;
                observedPassives.PassivesReset -= ResetPassives;
            }
            observed = null;
            observedPassives = null;
        }
        void StackChanged(SeongheunType t, int before, int after)
        {
            if (after > before) pulses[(int)t] = Time.unscaledTime + .25f;
        }
        void Acquired(PassiveAcquireResult result) { if (result.Success) notices.Enqueue(result); }
        void ResetBuild() { System.Array.Clear(pulses, 0, 3); BuildTreeOpen = false; }
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
            if (BuildTreeOpen && g.State != RunState.Paused) BuildTreeOpen = false;
        }

        public void OpenSettings() { SettingsOpen = true; }
        public void CloseSettings() { SettingsOpen = false; }
        public void SelectSigil(int index) { SelectedSigil = Mathf.Clamp(index, 0, 2); }
        public void OpenBuildTree() { var g = Dungeon.I; if (!g.Running) return; BuildTreeOpen = true; treeReturn = g.State; g.BeforePause = g.State; g.State = RunState.Paused; }
        public void CloseBuildTree() { if (!BuildTreeOpen) return; BuildTreeOpen = false; Dungeon.I.State = treeReturn; }
    }
}
