using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    // Component stays serializable in release builds; all input and mutation UI is compiled out.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class StigmaDebugMenu : MonoBehaviour
    {
        public static bool Available
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        StigmaManager manager;
        GameHUD hud;
        StigmaOwner owner;
        int definitionIndex;
        Vector2 scroll;
        string message = "획득 → 장착 → 단계 변경 순서로 확인합니다.";
        void Awake() { manager = GetComponent<StigmaManager>(); hud = GetComponent<GameHUD>(); }
        void Update()
        {
            if (!Dungeon.I || Dungeon.I.State == RunState.Cinematic) return;
            if (manager && hud && Input.GetKeyDown(manager.Database.Rules.DebugKey))
                hud.StigmaDebugOpen = !hud.StigmaDebugOpen;
        }
        void OnDisable() { if (hud) hud.StigmaDebugOpen = false; }
        void OnGUI()
        {
            if (!manager || !hud || !hud.StigmaDebugOpen) return;
            float scale = Mathf.Max(.5f, Mathf.Min(Screen.width / 520f, Screen.height / 680f));
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(12, 12, 490, 650), GUI.skin.window);
            GUILayout.Label("STIGMA DEBUG · F9 닫기 · 게임 진행 정지");
            owner = (StigmaOwner)GUILayout.Toolbar((int)owner, new[] { "플레이어", "까마귀" });
            scroll = GUILayout.BeginScrollView(scroll);
            var definitions = manager.Database.Definitions.FindAll(d => d.SupportsOwner(owner));
            definitionIndex = Mathf.Clamp(definitionIndex, 0, Mathf.Max(0, definitions.Count - 1));
            for (int i = 0; i < definitions.Count; i++)
            {
                int storedLevel = manager.Loadout.GetLevel(owner, definitions[i].Id);
                string levelLabel = storedLevel > 0 ? " · Lv." + storedLevel : " · 미보유";
                if (GUILayout.Toggle(i == definitionIndex, definitions[i].DisplayName + levelLabel + " [" + definitions[i].Id + "]", GUI.skin.button)) definitionIndex = i;
            }
            GUILayout.EndScrollView();
            GUI.enabled = definitions.Count > 0;
            string ownerName = owner == StigmaOwner.Player ? "플레이어" : "까마귀";
            if (GUILayout.Button(ownerName + "에게 선택한 성흔 지급 (장착과 별개)"))
                message = manager.TryAcquire(owner, definitions[definitionIndex].Id, out string error) ? ownerName + " 획득 완료" : error;
            if (GUILayout.Button("선택한 성흔 즉시 지급·장착 (하위/룸 조건 우회)"))
            {
                string id = definitions[definitionIndex].Id;
                bool available = manager.Loadout.Owns(owner, id) || manager.TryAcquire(owner, id, out _);
                if (available && manager.Loadout.Get(owner).Definition == definitions[definitionIndex]) message = "장착 완료";
                else message = manager.TryEquip(owner, id, out string error) ? "장착 및 효과 갱신 완료" : error;
            }
            GUI.enabled = true;
            GUILayout.BeginHorizontal();
            for (int level = 1; level <= 3; level++)
                if (GUILayout.Button("Lv." + level)) message = manager.TrySetLevel(owner, level, out string error) ? "단계 변경 완료" : error;
            GUILayout.EndHorizontal();
            var state = manager.Loadout;
            GUILayout.Label($"플레이어 {state.GetAcquired(StigmaOwner.Player).Count}종 / 까마귀 {state.GetAcquired(StigmaOwner.Raven).Count}종");
            GUILayout.Label($"양쪽 장착 {state.BothEquipped} / 조합 Lv.{state.EffectiveLevel} / 플레이어 고유 Lv.{state.EffectLevel(StigmaOwner.Player)}");
            GUILayout.Label("조합: " + (state.Combination ? state.Combination.DisplayName : "없음/미등록"));
            if (manager.Combat != null)
            {
                var combat = manager.Combat;
                GUILayout.Label($"표식 {combat.Charges}/{state.ChargeCapacity} · 가상체력 {combat.Shield.Current:0.#}/{combat.Shield.Capacity:0.#}");
                GUILayout.Label($"구리동전 {Dungeon.I.Wallet.Copper} · 사역마 {combat.Skills.FamiliarCount} · 장판 {combat.Skills.GroundCount}");
                GUILayout.Label($"공속 배율 P {Dungeon.I.PassiveStats.Evaluate(StigmaStats.PlayerSpeed, 1):0.##} / R {Dungeon.I.PassiveStats.Evaluate(StigmaStats.RavenSpeed, 1):0.##}");
                var shot = StigmaWeaponEffects.Resolve(state, Dungeon.I.Hero.Ammo, 4);
                GUILayout.Label($"탄 {Dungeon.I.Hero.Ammo}/6 · 원 샷 ×{shot.DamageMultiplier:0.##} / 거리 {shot.Range:0.#} / 쿨감 {(1-shot.CooldownMultiplier)*100:0.##}%");
                GUILayout.Label($"재장전 기본 {Dungeon.I.Hero.BaseReloadSeconds:0.##}초 · 남은 {Dungeon.I.Hero.ReloadRemaining:0.##}초 · 속도 ×{Dungeon.I.Hero.ReloadSpeed:0.##}");
                GUILayout.BeginHorizontal();
                for (int ammo = 0; ammo <= 6; ammo++)
                    if (GUILayout.Button("탄 " + ammo)) Dungeon.I.Hero.SetAmmoForDebug(ammo);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("표식 최대")) combat.SetCharges(state.ChargeCapacity);
                if (GUILayout.Button("표식 0")) combat.SetCharges(0);
                if (GUILayout.Button("동전 +2000")) Dungeon.I.Wallet.Add(2000);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(message);
            if (GUILayout.Button("신규 성흔 상태만 초기화")) { manager.ResetRun(); message = "신규 성흔 상태 초기화 완료"; }
            GUI.enabled = hud.StigmaManagementOpen || (Dungeon.I && !hud.SettingsOpen &&
                (Dungeon.I.State == RunState.Town || Dungeon.I.State == RunState.Combat || Dungeon.I.State == RunState.Exit || Dungeon.I.State == RunState.WaveBreak));
            if (GUILayout.Button("1번 성흔 UI 열기")) { hud.StigmaDebugOpen = false; if (!hud.StigmaManagementOpen) hud.OpenStigmaManagement(); }
            GUI.enabled = true;
            if (GUILayout.Button("닫기")) hud.StigmaDebugOpen = false;
            GUILayout.EndArea();
            GUI.matrix = previous;
        }
#endif
    }
}
