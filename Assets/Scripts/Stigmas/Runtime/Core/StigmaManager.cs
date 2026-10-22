using System;
using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    [DisallowMultipleComponent]
    public sealed class StigmaManager : MonoBehaviour
    {
        [SerializeField] StigmaDatabaseSO database;
        StigmaLoadout loadout;
        public StigmaDatabaseSO Database => database;
        public StigmaLoadout Loadout { get { EnsureInitialized(); return loadout; } }
        public event Action<StigmaChange> Changed;
        StigmaCombat combat;
        StigmaRoomService room;
        public StigmaCombat Combat => combat;
        public StigmaRoomService Room => room ?? (room = new StigmaRoomService(this));
        public bool CombatEffectsConnected => combat != null;
        void Start() { if (Dungeon.I && Dungeon.I.Stigmas == this) combat = new StigmaCombat(this, Dungeon.I); }
        void Update()
        {
            if (combat == null || !Dungeon.I || !Dungeon.I.Running) return;
            combat.Tick(UnityEngine.Time.deltaTime);
            if (UnityEngine.Input.GetKeyDown(Database.Rules.LinkSkillKey))
            {
                var target = PaperWorld.Mouse(Dungeon.I.Cam, UnityEngine.Input.mousePosition);
                if (!combat.Skills.TryCast(target, out string error)) Dungeon.I.Tell(error);
            }
        }

        void Awake() => EnsureInitialized();
        void EnsureInitialized()
        {
            if (loadout != null) return;
            loadout = new StigmaLoadout(database);
            loadout.Changed += ForwardChange;
        }
        void ForwardChange(StigmaChange change)
        {
            if (change.Kind == StigmaChangeKind.Reset) room?.ResetRun();
            combat?.Changed(change); Changed?.Invoke(change);
        }
        void OnDestroy() { if (loadout != null) loadout.Changed -= ForwardChange; }
        public bool TryAcquire(StigmaOwner owner, string id, out string error) => Loadout.TryAcquire(owner, id, out error);
        public bool TryEquip(StigmaOwner owner, string id, out string error) => Loadout.TryEquip(owner, id, out error);
        public bool TrySetLevel(StigmaOwner owner, int level, out string error) => Loadout.TrySetLevel(owner, level, out error);
        public void ResetRun() => Loadout.ResetRun();

        public void Configure(StigmaDatabaseSO value)
        {
            if (!value) throw new ArgumentException("Stigma database is missing or unloaded.");
            if (!value.Validate(out string error)) throw new ArgumentException("Invalid stigma database: " + error);
            if (loadout != null && loadout.TotalAcquiredCount > 0) throw new InvalidOperationException("Cannot replace an active stigma database.");
            if (loadout != null) loadout.Changed -= ForwardChange;
            database = value;
            loadout = null;
            EnsureInitialized();
        }
    }
}
