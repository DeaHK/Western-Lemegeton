using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WesternLemegeton
{
    /// <summary>RunMap selection -> real coordinator -> six-room handoff. Does not mutate Dungeon.</summary>
    public sealed class RunStageSelectionController : MonoBehaviour
    {
        [Serializable] public sealed class StageReadyEvent : UnityEvent<int> { }
        [SerializeField] private ExpeditionStageCoordinator coordinator;
        [SerializeField] private StageRoomDataBridge roomBridge;
        [SerializeField] private RunMapView view;
        [SerializeField] private int initialSeed = 12345;
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private bool showDeveloperControls;
        [SerializeField] private StageReadyEvent onStageReady = new StageReadyEvent();
        public ExpeditionStageCoordinator Coordinator => coordinator;
        public RunMapView View => view;
        public StageLayout PreparedLayout { get; private set; }
        public IReadOnlyList<RoomSpawnData> PreparedRooms => preparedRooms;
        public bool HasPreparedStage => PreparedLayout != null && coordinator && PreparedLayout == coordinator.CurrentStageLayout;
        public string LastError { get; private set; }
        public StageReadyEvent OnStageReady => onStageReady;
        public event Action<bool> MapVisibilityChanged;
        public bool EntryInProgress { get; private set; }
        private string entryStatus;
        public void SetEntryProgress(bool busyEntering, string message) { EntryInProgress = busyEntering; entryStatus = message; SyncState(); }
        public void RequestStageEntry() { if (!EntryInProgress && HasPreparedStage) onStageReady.Invoke(coordinator.Progress.CurrentNodeId); }
        private IReadOnlyList<RoomSpawnData> preparedRooms = Array.Empty<RoomSpawnData>();
        private InputField seedInput;
        private bool busy;
        private ExpeditionStageCoordinator observedCoordinator;
        private RunMapView observedView;

        public void Configure(ExpeditionStageCoordinator source, StageRoomDataBridge bridge, RunMapView mapView, bool visibleAtStart)
        {
            Disconnect();
            coordinator = source; roomBridge = bridge; view = mapView; showOnStart = visibleAtStart;
            if (Application.isPlaying && isActiveAndEnabled) Connect();
        }
        private void OnEnable() { if (Application.isPlaying) Connect(); }
        private void OnDisable() { Disconnect(); }
        private void Start()
        {
            if (!view) return;
            if (showDeveloperControls) BuildControls();
            if (showOnStart) ShowMap(); else HideMap(); SyncState();
        }
        private void Connect()
        {
            Disconnect();
            if (!coordinator || !view) return;
            view.Coordinator = coordinator;
            observedView = view; observedCoordinator = coordinator;
            observedView.OnNodeSelected.AddListener(SelectNode);
            observedCoordinator.StagePrepared += StagePrepared;
            observedCoordinator.StateChanged += SyncState;
            if (coordinator.CurrentStageLayout != null && coordinator.CurrentMap != null) StagePrepared(coordinator.CurrentStageLayout);
        }
        private void Disconnect()
        {
            if (observedView) observedView.OnNodeSelected.RemoveListener(SelectNode);
            if (observedCoordinator)
            {
                observedCoordinator.StagePrepared -= StagePrepared;
                observedCoordinator.StateChanged -= SyncState;
            }
            observedView = null; observedCoordinator = null;
        }
        private bool ValidateBindings()
        {
            if (!coordinator || !view || !roomBridge || roomBridge.Provider != coordinator.StageProvider)
                return Fail("Assign Coordinator, View and Bridge using the same StageLayoutProvider.");
            return true;
        }
        public void StartConfiguredRun()
        {
            int seed = initialSeed;
            if (seedInput && !int.TryParse(seedInput.text, out seed)) { Fail("Seed must be a 32-bit integer."); return; }
            StartNewRun(seed);
        }
        public bool StartNewRun(int seed)
        {
            if (busy || EntryInProgress || !ValidateBindings()) return false;
            busy = true; LastError = null; entryStatus = null;
            try
            {
                if (!coordinator.StartNewRun(seed)) return Fail(coordinator.LastError);
                return HasPreparedStage;
            }
            finally { busy = false; }
        }
        /// <summary>Starts a fresh generated run from the existing field exit interaction.</summary>
        public bool StartNewRunFromField()
        {
            int seed = unchecked(Environment.TickCount ^ GetInstanceID());
            return StartNewRun(seed);
        }
        public void SelectNode(int id) { TrySelectNode(id); }
        public bool TrySelectNode(int id)
        {
            if (busy || EntryInProgress || !ValidateBindings()) return false;
            busy = true; LastError = null; entryStatus = null;
            try
            {
                if (!coordinator.TryAdvanceTo(id)) return Fail(coordinator.LastError);
                return HasPreparedStage;
            }
            finally { busy = false; }
        }
        private void StagePrepared(StageLayout layout)
        {
            if (layout == PreparedLayout) { SyncState(); return; }
            if (!ValidateBindings()) return;
            if (!roomBridge.TryGetAllRoomSpawnData(out List<RoomSpawnData> rooms) || rooms.Count != 6)
            {
                PreparedLayout = null; preparedRooms = Array.Empty<RoomSpawnData>();
                Fail("Six-room handoff unavailable. Check Bridge."); return;
            }
            PreparedLayout = layout; preparedRooms = rooms.AsReadOnly(); LastError = null;
            SyncState();
            Debug.Log($"Stage data ready: Node={coordinator.Progress.CurrentNodeId} Type={layout.StageType} Seed={layout.Seed} Rooms={rooms.Count} Entry={layout.EntryIndex} Exit={layout.ExitIndex}", this);
            onStageReady.Invoke(coordinator.Progress.CurrentNodeId);
        }
        private void SyncState()
        {
            if (!view) return;
            string message;
            if (!string.IsNullOrEmpty(LastError)) message = "Preparation failed: " + LastError;
            else if (!string.IsNullOrEmpty(entryStatus)) message = entryStatus;
            else if (!HasPreparedStage) message = "마을 출구 근처에서 F를 눌러 원정을 시작하세요.";
            else if (coordinator.Progress.RunCompleted) message = "RUN COMPLETE — all selected stages completed.";
            else if (coordinator.Progress.CurrentStageCompleted) message = "STAGE CLEARED  •  출구방의 분기 표식으로 이동해 F를 누르세요. 지도 노드는 경로 안내용입니다.";
            else message = $"DATA READY  •  Node {coordinator.Progress.CurrentNodeId} / {PreparedLayout.StageType}  •  Seed {PreparedLayout.Seed}  •  {preparedRooms.Count} rooms  •  Entry {PreparedLayout.EntryIndex} / Exit {PreparedLayout.ExitIndex}  •  경로 선택은 출구방 분기 표식에서 F";
            view.StatusOverride = message; view.Refresh();
        }
        private bool Fail(string message) { LastError = string.IsNullOrEmpty(message) ? "Operation unavailable." : message; SyncState(); return false; }
        public void ShowMap() { if (view) { view.gameObject.SetActive(true); view.Refresh(); MapVisibilityChanged?.Invoke(true); } }
        public void HideMap() { if (view) { view.gameObject.SetActive(false); MapVisibilityChanged?.Invoke(false); } }
        public void SetFieldRouteSelection(bool enabled)
        {
            if (!view) return;
            view.SelectionEnabled = !enabled;
            view.Refresh();
        }

        private void BuildControls()
        {
            var row = new GameObject("StageSelectionControls", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(view.transform, false); row.anchorMin = row.anchorMax = new Vector2(.5f, 1);
            row.anchoredPosition = new Vector2(0, -70); row.sizeDelta = new Vector2(800, 30);
            var input = Control("Seed", row, -250, 200);
            var image = input.gameObject.AddComponent<Image>(); image.color = new Color(.13f, .17f, .21f);
            var text = Label(input, initialSeed.ToString());
            seedInput = input.gameObject.AddComponent<InputField>();
            seedInput.targetGraphic = image; seedInput.textComponent = text;
            seedInput.contentType = InputField.ContentType.IntegerNumber;
            seedInput.characterLimit = 11; seedInput.text = initialSeed.ToString();
            AddButton(row, "Start run", -55, 150, StartConfiguredRun);
            AddButton(row, "Enter stage", 120, 150, RequestStageEntry);
            AddButton(row, "Close map", 285, 130, HideMap);
            var launcher = AddButton(transform, "Open RunMap", 0, 150, ShowMap);
            launcher.anchorMin = launcher.anchorMax = new Vector2(1, 1);
            launcher.anchoredPosition = new Vector2(-90, -22);
        }
        private static RectTransform Control(string name, Transform parent, float x, float width)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchoredPosition = new Vector2(x, 0); rect.sizeDelta = new Vector2(width, 30); return rect;
        }
        private static Text Label(RectTransform parent, string value)
        {
            var text = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(6, 0); text.rectTransform.offsetMax = new Vector2(-6, 0);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter; text.text = value; text.raycastTarget = false; return text;
        }
        private static RectTransform AddButton(Transform parent, string title, float x, float width, UnityAction action)
        {
            var rect = Control(title, parent, x, width);
            rect.gameObject.AddComponent<Image>().color = new Color(.18f, .24f, .3f);
            Label(rect, title); rect.gameObject.AddComponent<Button>().onClick.AddListener(action); return rect;
        }
    }
}
