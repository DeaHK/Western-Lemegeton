using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WesternLemegeton.Passives;
using RuntimePassiveRarity = WesternLemegeton.Passives.PassiveRarity;

namespace WesternLemegeton
{
    [System.Serializable]
    public class UIReference
    {
        public string Key;
        public GameObject Target;
    }

    [DefaultExecutionOrder(500)]
    public sealed class GameUIView : MonoBehaviour
    {
        #region Serialized References / UI Reference Definitions

        // Panels / state routing
        public GameObject TitlePanel, HudPanel, RoutePanel, SigilPanel, CardsPanel, PausePanel, SettingsPanel, ResultsPanel, CinematicPanel, ToastPanel;

        // Shared artwork / fonts / colors
        public VisualCatalog Art;
        public Font FontOverride;
        public string[] SystemFonts = { "Malgun Gothic", "Arial" };
        public Color ActiveColor = new Color(1, .68f, .27f), UnlockedColor = new Color(.35f, .9f, .94f), InactiveColor = new Color(.42f, .38f, .42f);
        public Color[] RarityColors = { new Color(.88f, .88f, .91f), new Color(.28f, .65f, 1), new Color(.77f, .43f, 1) };

        // Serialized key -> target contract
        public List<UIReference> References = new List<UIReference>();

        // Passive slots / shared tooltip
        public RectTransform PassiveContent;
        public PassiveSlotView PassivePrefab;
        public Text PassiveTooltip;

        #endregion

        #region Runtime Cache / Dictionary

        readonly Dictionary<string, GameObject> refs = new Dictionary<string, GameObject>();
        readonly List<PassiveSlotView> slots = new List<PassiveSlotView>();
        readonly List<PassiveInstance> passiveBuffer = new List<PassiveInstance>();
        readonly List<int> slotIds = new List<int>();

        // Existing three stigma definitions (Fire / Nature / Butterfly)
        readonly string[] buildIcons = { "FireIcon", "NatureIcon", "Raven" }, buildNames = { "잿불", "격노", "검은 날개" };
        readonly string[] effects = { "공격에 화상을 더해 지속 피해를 줍니다. 각인이 늘수록 화상 피해가 강해집니다.", "무기 태그 후 일시적으로 공격력이 증가합니다. 각인이 늘수록 강화량이 커집니다.", "까마귀 명령의 공격 시간을 연장하고 연계 공격을 강화합니다." };

        #endregion

        #region Initialization / Awake

        private void Awake()
        {
            foreach (var r in References)
                if (r.Target) refs[r.Key] = r.Target;
            var font = FontOverride ? FontOverride : Font.CreateDynamicFontFromOSFont(SystemFonts, 18);
            foreach (var t in GetComponentsInChildren<Text>(true))
                if (FontOverride || !t.font || t.font.name == "LegacyRuntime") t.font = font;
        }

        #endregion

        #region LateUpdate Orchestration

        private void LateUpdate()
        {
            var g = Dungeon.I;
            if (!g || !g.Hero) return;
            var h = g.GetComponent<GameHUD>();
            var p = g.Hero;
            bool cinematic = g.State == RunState.Cinematic;

            UpdatePanelVisibility(g, h, cinematic);
            UpdateLocationAndSupplies(g);
            UpdateCoreHud(g, p);
            UpdatePlayerHealthAndAmmo(p);
            UpdatePlayerSkillSlots(p);
            UpdateRavenSkillSlots(g);
            UpdateStigmaHud(g, h);
            // These updates remain unconditional even when their panels are inactive.
            Prompt(g);
            Map(g);
            Sigil(g, h);
            Passives(g, h);
            UpdateResults(g);
            UpdateBossPrototype(g);
            UpdateRavenPrompt(g);
            if (cinematic)
            {
                UpdateCinematicOverlay(g);
            }
        }

        #endregion

        #region Global Panel Visibility / Title / Pause / Settings

        private void UpdatePanelVisibility(Dungeon g, GameHUD h, bool cinematic)
        {
            Panel(TitlePanel, g.State == RunState.Title);
            Panel(HudPanel, g.State != RunState.Title && !cinematic);
            Panel(RoutePanel, g.State == RunState.Route);
            Panel(SigilPanel, g.State == RunState.SigilChoice || h.BuildTreeOpen);
            Panel(CardsPanel, g.State == RunState.CardChoice);
            Panel(PausePanel, g.State == RunState.Paused && !h.BuildTreeOpen);
            Panel(SettingsPanel, h.SettingsOpen);
            Panel(ResultsPanel, g.State == RunState.Dead || g.State == RunState.Victory);
            Panel(CinematicPanel, cinematic);
        }

        #endregion

        #region Core HUD

        private void UpdateCoreHud(Dungeon g, Player p)
        {
            Show("Wave", !g.IsTown && g.CurrentKind == ExplorationRoomKind.Combat);
            Text("WaveText", $"WAVE {g.WaveIndex+1} / {g.WavesInMap} · 남은 적 {g.Enemies.Count}");
            Show("WaveBreak", g.State == RunState.WaveBreak);
            Text("WaveBreakText", $"다음 WAVE {g.WaveIndex+1}\n{Mathf.CeilToInt(g.WaveCountdown)}초 후 적이 접근합니다");
            Text("CombatStatus", $"회피 {(p.DashCd>0?p.DashCd.ToString("F1")+"s":"●")}  COMBO {p.HitCombo}  {(p.Fury>0?"태그 강화":"")}");
        }

        #endregion

        #region Player Health / Ammo / Weapon

        private void UpdatePlayerHealthAndAmmo(Player p)
        {
            Fill("HP", p.Hp / 100);
            Text("HPText", $"{Mathf.CeilToInt(p.Hp)} / 100");
            Fill("Ammo", p.Ammo / 6f);
            Text("AmmoText", p.Reload > 0 ? $"장전 {p.Reload:F1}s · {p.Ammo}/6" : $"리볼버 {p.Ammo}/6");
        }

        #endregion

        #region Skill / Cooldown UI

        private void UpdatePlayerSkillSlots(Player p)
        {
            Skill(0, p.Weapon == 0 ? "SlashIcon" : "ShotIcon", p.TagCd, p.Weapon == 0 ? "단검" : "리볼버");
            Skill(1, "HunterDash", p.DashCd, "회피");
            Skill(2, p.Weapon == 0 ? "SlashIcon" : "ShotIcon", p.Skill2Cd, p.Weapon == 0 ? "베어 가르기" : "원 샷");
            Skill(3, p.Weapon == 0 ? "SlamIcon" : "BarrageIcon", p.Skill3Cd, p.Weapon == 0 ? "내려찍기" : "난사");
        }

        private void Skill(int i, string icon, float cooldown, string label)
        {
            Image("SkillIcon" + i, icon);
            Text("SkillLabel" + i, label);
            Show("SkillCooldown" + i, cooldown > 0);
            Text("SkillCooldownText" + i, cooldown.ToString("F1"));
        }

        #endregion

        #region Raven Existing UI

        private void UpdateRavenSkillSlots(Dungeon g)
        {
            Skill(4, "Raven", g.Crow.Cooldown, g.Crow.Active > 0 ? $"공격 {g.Crow.Active:F1}s" : "까마귀 명령");
            Skill(5, "RavenPortrait", g.Crow.LinkCooldown, g.Crow.LinkWindow > 0 ? $"연계 {g.Crow.LinkWindow:F1}s" : "연계 대기");
        }

        private void UpdateRavenPrompt(Dungeon g)
        {
            Show("CrowPrompt", g.Crow.LinkWindow > 0 || g.Crow.ComboFlash > 0);
            Text("CrowPromptText", g.Crow.LinkWindow > 0 ? $"R · 연계 {g.Crow.LinkWindow:F1}s" : "까마귀 콤보 연계!");
        }

        #endregion

        #region Passive Inventory / Slot / Tooltip / Toast / Card Choices

        private void Passives(Dungeon g, GameHUD h)
        {
            g.Passives.CopyActivePassives(passiveBuffer);
            Text("PassiveCount", $"획득 패시브 {g.Passives.Count} · 성흔과 별도");
            Show("PassiveEmpty", passiveBuffer.Count == 0);
            bool rebuild = slots.Count != passiveBuffer.Count || slotIds.Count != passiveBuffer.Count;
            if (!rebuild)
                for (int i = 0; i < passiveBuffer.Count; i++)
                    if (!slots[i] || slotIds[i] != passiveBuffer[i].PassiveID) { rebuild = true; break; }
            if (rebuild)
            {
                foreach (var slot in slots)
                    if (slot) Destroy(slot.gameObject);
                slots.Clear();
                slotIds.Clear();
                if (PassiveTooltip) PassiveTooltip.gameObject.SetActive(false);
            }
            while (slots.Count < passiveBuffer.Count && PassivePrefab && PassiveContent)
            {
                var item = passiveBuffer[slots.Count];
                var slot = Instantiate(PassivePrefab, PassiveContent);
                slot.name = "Passive:" + item.PassiveID;
                slot.Tooltip = PassiveTooltip;
                slots.Add(slot);
                slotIds.Add(item.PassiveID);
            }
            for (int i = 0; i < slots.Count; i++)
            {
                var item = passiveBuffer[i];
                var slot = slots[i];
                if (slot.Icon) slot.Icon.sprite = PassiveIcon(item.Data);
                if (slot.Border) slot.Border.color = PassiveRarityColor(item.Rarity);
                string description = item.Data.NameStringKey + "\n" + Rarity(item.Rarity) + " · " + Source(item.InitialSource) +
                    $" · STACK {item.Stack} · LEVEL {item.Level}\n" + item.Data.DescriptionStringKey;
                if (slot.Description != description && PassiveTooltip && PassiveTooltip.gameObject.activeSelf && PassiveTooltip.text == slot.Description)
                    PassiveTooltip.text = description;
                slot.Description = description;
            }
            UpdatePassiveToast(g, h);
        }

        private void UpdatePassiveToast(Dungeon g, GameHUD h)
        {
            bool show = h.Toast.HasValue && h.Toast.Value.Instance != null && h.Toast.Value.Instance.Data && g.State != RunState.Route && g.State != RunState.Cinematic && !h.SettingsOpen;
            Panel(ToastPanel, show);
            if (show)
            {
                var toast = h.Toast.Value;
                var item = toast.Instance;
                SetPassiveImage("ToastIcon", PassiveIcon(item.Data));
                Text("ToastName", item.Data.NameStringKey + (toast.WasStacked ? $" · STACK {toast.PreviousStack} → {toast.NewStack}" : " 획득"));
                Text("ToastDetail", Rarity(item.Rarity) + " · " + Source(toast.Source));
                Color("ToastBorder", PassiveRarityColor(item.Rarity));
                var group = ToastPanel ? ToastPanel.GetComponent<CanvasGroup>() : null;
                if (group) group.alpha = h.ToastOpacity;
            }
        }

        // Existing card-choice rendering stays at the end of Sigil's update.
        private void UpdateCardChoices(Dungeon g)
        {
            for (int n = 0; n < 3; n++)
            {
                var offer = g.PassiveOffers.GetOffer(n);
                if (!offer)
                {
                    Color("CardBorder" + n, InactiveColor);
                    Text("CardRarity" + n, ""); Text("CardName" + n, ""); Text("CardEffect" + n, "");
                    SetPassiveImage("CardIcon" + n, null);
                    continue;
                }
                Color("CardBorder" + n, PassiveRarityColor(offer.Rarity));
                Text("CardRarity" + n, Rarity(offer.Rarity) + " · 악마카드");
                bool hasName = Element<Text>("CardName" + n);
                Text("CardName" + n, offer.NameStringKey);
                string detail = (hasName ? "" : offer.NameStringKey + "\n") + offer.DescriptionStringKey +
                    "\n효과: " + offer.StatType + " " + SignedValue(offer.Value, offer.ValueType == PassiveValueType.Percent);
                if (g.Passives.TryGetPassive(offer.PassiveID, out var owned))
                    detail += $"\n보유 STACK {owned.Stack} → {(long)owned.Stack + 1}";
                Text("CardEffect" + n, detail);
                SetPassiveImage("CardIcon" + n, PassiveIcon(offer));
            }
        }

        #endregion

        #region Stigma / Sigil UI

        private void UpdateStigmaHud(Dungeon g, GameHUD h)
        {
            for (int i = 0; i < 3; i++)
            {
                var type = (SeongheunType)i;
                int stack = g.Build.Stack(type);
                bool active = g.Build.IsActive(type);
                float pulse = h.StackPulse(type);
                Text("Stack" + i, "각인 " + stack);
                Text("RouteStack" + i, stack + " 각인");
                Color("StigmaBorder" + i, pulse > 0 ? UnityEngine.Color.white : active ? ActiveColor : InactiveColor);
                Color("StigmaIcon" + i, active ? UnityEngine.Color.white : new Color(.5f, .5f, .55f));
                var icon = Object("StigmaPulse" + i);
                if (icon) icon.transform.localScale = Vector3.one * (1 + (pulse > 0 ? Mathf.Sin((1 - pulse / .25f) * Mathf.PI) * .22f : 0));
            }
        }

        private void Sigil(Dungeon g, GameHUD h)
        {
            int i = h.SelectedSigil;
            var type = (SeongheunType)i;
            int stack = g.Build.Stack(type);
            bool claim = g.State == RunState.SigilChoice;
            Image("SigilCenter", buildIcons[i]);
            Image("SigilDetailIcon", buildIcons[i]);
            Text("SigilName", buildNames[i]);
            Text("SigilStack", $"각인 {stack} · {(g.Build.IsActive(type)?"활성":"대기")}");
            Text("SigilEffect", effects[i]);
            Text("SigilThreshold", $"첫 활성화: {g.Build.Threshold(type)} 각인\n강화 노드: {g.Build.Threshold(type)+2} 각인");
            Show("SigilClaim", claim);
            Text("SigilClaimText", buildNames[i] + $" 각인 +1 ({stack} → {stack+1})");
            for (int n = 0; n < 6; n++)
            {
                var t = (SeongheunType)(n / 2);
                int needed = g.Build.Threshold(t) + (n % 2) * 2;
                Color("SigilNode" + n, g.Build.Stack(t) >= needed ? UnlockedColor : InactiveColor);
                Text("SigilNodeText" + n, buildNames[n / 2] + " · " + needed + " 각인");
            }
            UpdateCardChoices(g);
        }

        #endregion

        #region Dungeon Map / Route / Room Nodes

        private void UpdateLocationAndSupplies(Dungeon g)
        {
            Text("Location", g.IsTown ? "HIRVA / 히르바" : $"스테이지 {g.Room+1:00} · 방 {g.MapIndex+1}/6 / {Dungeon.MapNames[g.MapIndex]}");
            Text("Notice", g.Notice);
            Text("Supplies", $"보급품 {g.Supplies}\n방 완료 {g.MapsCleared} / 6");
        }

        private void Prompt(Dungeon g)
        {
            string message = "";
            if (g.Running)
            {
                if (g.IsTown)
                {
                    if (Vector2.Distance(g.Pos, RoomAuthoring.Point(g.Scene.Hirva.TownExit)) < 3) message = "E · 황야로 출발";
                }
                else
                {
                    var door = g.NearbyDoor();
                    if (door) message = door.StageExit ? (g.Progress.SegmentComplete ? "E · 스테이지 출구 / 다음 여정 선택" : $"출구 봉인 · 미완료 방 {6-g.MapsCleared}개") : (g.Progress.CanLeave ? $"E · {Dungeon.MapNames[door.Destination]} 이동" : "문 봉인 · 이 방의 모든 적을 처치하세요");
                    else if (g.CurrentKind != ExplorationRoomKind.Combat && Vector2.Distance(g.Pos, RoomAuthoring.Point(g.CurrentRoom.Layout.Authored.ServicePoint)) < 2.4f) message = g.Progress.MapComplete ? "이 방의 보상을 선택했습니다" : g.CurrentKind == ExplorationRoomKind.Sigil ? "E · 성흔 제단 / 각인 강화" : "E · 악마카드 진열대 / 카드 선택";
                }
            }
            Show("Prompt", message != "");
            Text("PromptText", message);
        }

        private void Map(Dungeon g)
        {
            for (int i = 0; i < 6; i++)
            {
                bool current = !g.IsTown && g.MapIndex == i, done = !g.IsTown && g.Progress.Cleared(i), visited = !g.IsTown && g.Progress.Visited(i);
                var color = current ? UnlockedColor : done ? ActiveColor : visited ? new Color(.97f, .89f, .72f) : InactiveColor;
                Color("MiniNode" + i, color);
                Color("RouteNode" + i, color);
                string kind = RoomGraph.Kind(i) == ExplorationRoomKind.Sigil ? "성흔 강화" : RoomGraph.Kind(i) == ExplorationRoomKind.DevilCards ? "악마카드 선택" : "전투";
                Text("RouteNodeText" + i, $"{i+1:00} {Dungeon.MapNames[i]}\n{kind} · {(current?"현재 위치":done?"완료":visited?"미완료":"미방문")}");
            }
            Show("RouteTravel", g.RouteTravel);
            Text("RouteStage", g.IsTown ? "여정의 시작" : $"STAGE {g.Room+1:00}");
            Text("RouteName", g.IsTown ? "히르바 → 붉은 모래길" : Dungeon.RoomNames[g.Room]);
            Text("RouteProgress", $"방 완료 {g.MapsCleared}/6 · 방문 {g.Progress.VisitedMaps}/6");
            Fill("RouteHP", g.Hero.Hp / 100);
            Text("RouteHPText", $"HP {Mathf.CeilToInt(g.Hero.Hp)} / 100");
            Text("RouteBuild", $"악마카드 {g.Passives.Count}장\n공격 {PassiveBonus(g, WesternPassiveStatKeys.PlayerAttack)} · 이동 {PassiveBonus(g, WesternPassiveStatKeys.MoveSpeed)}\n까마귀 공격 {PassiveBonus(g, WesternPassiveStatKeys.RavenAttack)}");
            Text("RouteTravelText", g.IsTown ? "황야로 출발" : g.Room == 4 ? "탐험 완료" : "다음 스테이지로 이동");
            Text("RouteHint", g.RouteTravel ? g.Room == 4 && !g.IsTown ? "모든 봉인을 해제했습니다." : "다음: " + Dungeon.RoomNames[g.NextRoom] : "여섯 방을 완료한 뒤 출구 문에서 다음 스테이지로 이동할 수 있습니다.");
        }

        #endregion

        #region Existing Boss Prototype UI

        // Existing dormant/prototype display path; no boss functionality is added.
        private void UpdateBossPrototype(Dungeon g)
        {
            Show("Boss", g.IsBossWave && g.Enemies.Count > 0);
            if (g.IsBossWave && g.Enemies.Count > 0 && g.Enemies[0]) Fill("BossHP", g.Enemies[0].Hp / g.Enemies[0].MaxHp);
        }

        #endregion

        #region Results / Dead / Victory

        private void UpdateResults(Dungeon g)
        {
            Text("ResultsTitle", g.State == RunState.Victory ? "봉인이 무너졌다" : "다시, 잿빛 황야로");
            Text("ResultsDetail", $"도달 {g.Room+1} / 5 · 처치 {g.Kills} · {(int)g.RunTime}초");
        }

        #endregion

        #region Cinematic Overlay

        private void UpdateCinematicOverlay(Dungeon g)
        {
            var c = g.Cinematics;
            var top = Element<CanvasGroup>("CinemaBars");
            if (top) top.alpha = c.Letterbox;
            var title = Element<CanvasGroup>("CinemaTitle");
            if (title) title.alpha = c.TitleOpacity;
            var shade = Element<CanvasGroup>("CinemaShade");
            if (shade) shade.alpha = c.Shade;
            Text("CinemaHeading", c.Heading);
            Text("CinemaCaption", c.Caption);
            Text("CinemaHint", c.IsPaused ? "P 계속 · SPACE 건너뛰기" : "SPACE / ESC 건너뛰기 · P 일시정지");
        }

        #endregion

        #region Utility / Element Lookup / Formatting

        public GameObject Object(string key) => refs.TryGetValue(key, out var value) ? value : null;

        public T Element<T>(string key) where T : Component
        {
            var o = Object(key);
            return o ? o.GetComponent<T>() : null;
        }

        private void Text(string key, string text)
        {
            var t = Element<Text>(key);
            if (t) t.text = text;
        }

        private void Show(string key, bool show)
        {
            var o = Object(key);
            if (o && o.activeSelf != show) o.SetActive(show);
        }

        private static void Panel(GameObject o, bool on)
        {
            if (o && o.activeSelf != on) o.SetActive(on);
        }

        private void Image(string key, string icon)
        {
            var image = Element<Image>(key);
            if (image) image.sprite = Art.Get(icon);
        }

        private void Color(string key, Color color)
        {
            var graphic = Element<Graphic>(key);
            if (graphic) graphic.color = color;
        }

        private void Fill(string key, float value)
        {
            var image = Element<Image>(key);
            if (image) image.fillAmount = Mathf.Clamp01(value);
        }

        private Sprite PassiveIcon(PassiveSO passive)
        {
            if (!passive) return null;
            if (passive.Icon) return passive.Icon;
            if (!Art) return null;
            string key = passive.StatType == WesternPassiveStatKeys.PlayerAttack ? "BulletIcon" :
                passive.StatType == WesternPassiveStatKeys.MoveSpeed ? "WaterIcon" :
                passive.StatType == WesternPassiveStatKeys.RavenAttack ? "RavenPortrait" : null;
            return key == null ? null : Art.Get(key);
        }

        private void SetPassiveImage(string key, Sprite sprite)
        {
            var image = Element<Image>(key);
            if (image) { image.sprite = sprite; image.enabled = sprite != null; }
        }

        private Color PassiveRarityColor(RuntimePassiveRarity rarity)
        {
            int index = (int)rarity;
            if (index < 0 || index > 3) return InactiveColor;
            if (RarityColors != null && index < RarityColors.Length) return RarityColors[index];
            return rarity == RuntimePassiveRarity.Legendary ? new Color(1f, .72f, .18f) : InactiveColor;
        }

        private static string Rarity(RuntimePassiveRarity rarity) => rarity switch
        {
            RuntimePassiveRarity.Common => "일반", RuntimePassiveRarity.Uncommon => "희귀",
            RuntimePassiveRarity.Rare => "레어", RuntimePassiveRarity.Legendary => "전설", _ => "등급 미상"
        };

        private static string Source(PassiveAcquisitionSource source) => source switch
        {
            PassiveAcquisitionSource.EntranceChoice => "던전 입구", PassiveAcquisitionSource.ShopPurchase => "상점 구매",
            PassiveAcquisitionSource.MonsterDrop => "몬스터 드롭", PassiveAcquisitionSource.BossReward => "보스 보상",
            PassiveAcquisitionSource.EventReward => "이벤트 보상", PassiveAcquisitionSource.Debug => "디버그 지급", _ => "출처 미상"
        };

        private static string SignedValue(float value, bool percent = false) =>
            (value >= 0 ? "+" : "") + value.ToString("0.##") + (percent ? "%" : "");

        private static string PassiveBonus(Dungeon g, string statType)
        {
            float raw = g.PassiveStats.GetRawBonus(statType), percent = g.PassiveStats.GetPercentBonus(statType);
            if (raw == 0 && percent == 0) return "+0";
            if (raw == 0) return SignedValue(percent, true);
            if (percent == 0) return SignedValue(raw);
            return SignedValue(raw) + " / " + SignedValue(percent, true);
        }

        #endregion
    }
}
