using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using WesternLemegeton.Passives;
using WesternLemegeton.Stigmas;
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
        public GameObject TitlePanel, HudPanel, RoutePanel, CardsPanel, PausePanel, SettingsPanel, ResultsPanel, CinematicPanel, ToastPanel;
        public StigmaRoomView StigmaView;

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
        PassiveSlotView selectedSlot;
        Text houseSign;
        readonly List<int> slotIds = new List<int>();
        readonly RectTransform[] miniNodeRects = new RectTransform[6];
        readonly RectTransform[] miniLabelRects = new RectTransform[6];
        readonly Text[] miniLabels = new Text[6];
        readonly Image[] miniNodeFills = new Image[6];
        readonly Vector2[] miniNodeBasePositions = new Vector2[6];
        readonly Vector2[] miniNodeBaseSizes = new Vector2[6];
        readonly Vector2[] miniLabelBaseSizes = new Vector2[6];
        readonly string[] miniLabelBaseText = new string[6];
        readonly int[] miniLabelBaseFontSizes = new int[6];
        readonly Color[] miniFillBaseColors = new Color[6];
        readonly Image[] miniLinks = new Image[5];
        readonly Vector2[] miniLinkBasePositions = new Vector2[5];
        readonly Vector2[] miniLinkBaseSizes = new Vector2[5];
        readonly Quaternion[] miniLinkBaseRotations = new Quaternion[5];
        readonly List<Image> extraMiniLinks = new List<Image>();
        readonly Vector2[] generatedMiniPoints = new Vector2[6];
        RectTransform miniMapRect;
        Color miniLinkColor;
        bool generatedMiniMapActive;
        readonly Image[] rounds = new Image[6];
        BetaRoomMap roomMap;
        readonly Sprite[] weaponGlyphs = new Sprite[4], stigmaGlyphs = new Sprite[3];
        static readonly string[] combatExtras = { "ComboPlate", "CombatStatus", "SwapFrame", "SwapIcon", "SwapKeycap", "SwapKey", "SwapReady", "SwapLabel" };
        int generatedMiniEdgeCount;
        public bool GeneratedMinimapActive => generatedMiniMapActive;
        public int GeneratedMinimapEdgeCount => generatedMiniEdgeCount;
        public Vector2 GetGeneratedMinimapPoint(int roomIndex) => roomIndex >= 0 && roomIndex < generatedMiniPoints.Length ? generatedMiniPoints[roomIndex] : Vector2.zero;

        #endregion

        #region Initialization / Awake

        private void Awake()
        {
            foreach (var r in References)
                if (r.Target) refs[r.Key] = r.Target;
            CacheMinimap();
            for (int i = 0; i < rounds.Length; i++)
                rounds[i] = HudPanel.transform.Find("Round" + i)?.GetComponent<Image>();
            BuildWeaponSkillSlots();
            string[] weapons = { "Dagger", "Revolver", "Shotgun", "Rifle" };
            for (int i=0; i<weapons.Length; i++) weaponGlyphs[i]=Resources.Load<Sprite>("WesternBeta/Hud/Glyphs/"+weapons[i]);
            string[] elements = { "WaterIcon", "FlameIcon", "SproutIcon" };
            for (int i=0; i<elements.Length; i++) stigmaGlyphs[i]=Resources.Load<Sprite>("WesternBeta/Journey/"+elements[i]);
            PlaceBossBar();
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
            if (g.ManagedSession && (g.State == RunState.Title || (GameManager.I && GameManager.I.IsLoading)))
            {
                // 타이틀·로딩 중에는 콘텐츠가 없으므로 HUD 갱신을 건너뛰고, 남아 있을 수 있는 알림만 숨깁니다.
                if (ToastPanel) ToastPanel.SetActive(false);
                return;
            }
            UpdateLocationAndSupplies(g);
            UpdateCoreHud(g, p);
            UpdatePlayerHealthAndAmmo(p);
            UpdatePlayerSkillSlots(p);
            UpdateRavenSkillSlots(g);
            UpdateStigmaHud(g);
            // These updates remain unconditional even when their panels are inactive.
            Prompt(g);
            Map(g);
            UpdateCardChoices(g);
            Passives(g, h);
            UpdateResults(g);
            UpdateBossHealth(g);
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
            if (StigmaView) Panel(StigmaView.gameObject, h.StigmaManagementOpen || g.State == RunState.SigilChoice);
            Panel(CardsPanel, g.State == RunState.CardChoice);
            Panel(PausePanel, g.State == RunState.Paused && !h.StigmaManagementOpen);
            Panel(SettingsPanel, h.SettingsOpen);
            Panel(ResultsPanel, g.State == RunState.Dead || g.State == RunState.Victory);
            Panel(CinematicPanel, cinematic);
        }

        #endregion

        #region Core HUD

        private void UpdateCoreHud(Dungeon g, Player p)
        {
            Show("Minimap", true);
            Show("Map_Button", true);
            Show("Supply_Panel", true);
            Show("Supplies", true);
            Show("Wave", !g.InBossTrial && !g.InPractice && !g.IsTown && g.CurrentKind == ExplorationRoomKind.Combat);
            Text("WaveText", $"WAVE {g.WaveIndex+1} / {g.WavesInMap} · 남은 적 {g.Enemies.Count}");
            Show("WaveBreak", g.State == RunState.WaveBreak);
            Text("WaveBreakText", $"다음 WAVE {g.WaveIndex+1}\n{Mathf.CeilToInt(g.WaveCountdown)}초 후 적이 접근합니다");
            bool combat = !g.IsTown && !g.InsideHouse;
            Text("CombatStatus", $"COMBO       {p.HitCombo}");
            foreach (var key in combatExtras) Show(key,combat);
            // The small "E ... R ..." strip above the weapon panel is replaced by the two slots left of dodge.
            Show("WeaponSkillsPlate",false);
            for (int i=2; i<6; i++)
            {
                Show("SkillFrame"+i,combat);
                Show("Keycap"+i,combat);
                Show("SkillIcon"+i,combat);
                Show("SkillKey"+i,combat);
                Show("SkillLabel"+i,combat);
            }
            Text("SwapReady",p.TagCd>0 ? $"{p.TagCd:F1}초" : "준비");
        }

        #endregion

        #region Player Health / Ammo / Weapon

        private void UpdatePlayerHealthAndAmmo(Player p)
        {
            Fill("HP", p.Hp / p.MaxHealth);
            string bonus = p.BonusHp.Current>0 ? $"   +{p.BonusHp.Current:0.#}" : "";
            Text("HPText", $"{Mathf.CeilToInt(p.Hp)} / {p.MaxHealth:0}{bonus}");
            bool gun=p.Weapon==1;
            for(int i=0;i<rounds.Length;i++)
            {
                Show("RoundSlot"+i,gun);
                if(rounds[i]) rounds[i].enabled=gun && i<p.Ammo;
            }
            Text("AmmoText",gun ? $"{p.Ammo} <size=13>/ 6</size>" : "");
            Text("ReloadStatus",gun && p.Reload>0 ? $"재장전 {p.ReloadRemaining:F1}초" : "");
        }

        #endregion

        #region Skill / Cooldown UI

        private void UpdatePlayerSkillSlots(Player p)
        {
            SetPassiveImage("SkillIcon0",weaponGlyphs[p.Weapon==0 ? 0 : (int)p.Gun+1]);
            Text("SkillKey0","LMB");
            Text("SkillLabel0",p.WeaponLabel);
            Cooldown(1,p.DashCd);
            Text("SkillLabel1","회피");
            bool blade=p.Weapon==0;
            SlotArt(2,blade ? "SlashIcon" : "ShotIcon",42);
            Text("SkillLabel2",blade ? "베어 가르기" : "원 샷");
            Cooldown(2,p.Skill2Cd);
            SlotArt(3,blade ? "SlamIcon" : "BarrageIcon",42);
            Text("SkillLabel3",blade ? "내려찍기" : "난사");
            Cooldown(3,p.Skill3Cd);
            // The primary attack has no cooldown. Left active, its old shade showed as a dark box
            // with "0" between the dodge and weapon panels.
            Show("SkillCooldown0",false);
        }

        // E and R weapon skills get their own slots to the left of the dodge slot.
        // They are copies of the dodge slot, so the frame, key cap, label and cooldown bar match it exactly.
        // To move them, change Pitch (distance between slot left edges, in canvas units).
        void BuildWeaponSkillSlots()
        {
            const float Pitch=101;   // slot width 88 + the 13 gap the right-hand slots use
            string[] parts={ "SkillFrame", "Keycap", "SkillKey", "SkillIcon", "SkillLabel", "SkillCooldown" };
            for (int slot=2; slot<=3; slot++)
            {
                float shift=-Pitch*(4-slot);   // E sits two places left of dodge, R one place
                foreach (string part in parts)
                {
                    if (TryRef(part+slot,out var old)) old.SetActive(false);   // the earlier authored piece, if any
                    if (!TryRef(part+"1",out var source)) continue;
                    var copy=Instantiate(source,source.transform.parent);
                    copy.name=part+slot;
                    copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex()+1);
                    ((RectTransform)copy.transform).anchoredPosition+=Vector2.right*shift;
                    refs[part+slot]=copy;
                    if (part=="SkillCooldown" && copy.GetComponentInChildren<Text>(true) is Text seconds)
                        refs["SkillCooldownText"+slot]=seconds.gameObject;
                    if (part=="SkillLabel" && copy.GetComponent<Text>() is Text label)
                    {
                        // Skill names are longer than "회피"; let them shrink a little rather than clip.
                        label.resizeTextForBestFit=true;
                        label.resizeTextMaxSize=label.fontSize;
                        label.resizeTextMinSize=9;
                    }
                }
                Text("SkillKey"+slot,slot==2 ? "E" : "R");
            }
        }

        // Shows one of the game's own skill pictures in a slot instead of the placeholder glyph.
        // The picture stays centred where the glyph was. Character art has wide empty margins,
        // so it is given a larger box than the square skill icons.
        void SlotArt(int slot,string icon,float size)
        {
            var image=Element<Image>("SkillIcon"+slot);
            var sprite=Art ? Art.Get(icon) : null;
            if (!image || !sprite) return;
            var rect=image.rectTransform;
            Vector2 change=Vector2.one*size-rect.sizeDelta;
            rect.anchoredPosition+=new Vector2((rect.pivot.x-.5f)*change.x,(rect.pivot.y-.5f)*change.y);
            rect.sizeDelta=Vector2.one*size;
            image.sprite=sprite;
            image.color=UnityEngine.Color.white;
            image.preserveAspect=true;
            image.enabled=true;
        }

        void Cooldown(int index,float seconds)
        {
            Show("SkillCooldown"+index,seconds>0 && (index==1 || !Dungeon.I.IsTown));
            if(seconds>0) Text("SkillCooldownText"+index,$"{seconds:F1}초");
        }

        private void UpdateRavenSkillSlots(Dungeon g)
        {
            SlotArt(4,"Raven",58);
            SlotArt(5,"RavenPortrait",52);
            Text("SkillLabel4","소환 · 코빈");
            Text("SkillLabel5","연계 스킬");
            Text("ActionStatus4",g.Crow.Active>0 ? $"소환 · {g.Crow.Active:F1}초" : "준비");
            Text("ActionStatus5",g.Crow.LinkWindow>0 ? $"연계 · {g.Crow.LinkWindow:F1}초" : "대기");
            Show("ActionStatus4",!g.IsTown && g.Crow.Cooldown<=0);
            Show("ActionStatus5",!g.IsTown && g.Crow.LinkCooldown<=0);
            Cooldown(4,g.Crow.Cooldown);
            Cooldown(5,g.Crow.LinkCooldown);
        }

        private void UpdateRavenPrompt(Dungeon g)
        {
            Show("CrowPrompt", g.Crow.LinkWindow > 0 || g.Crow.ComboFlash > 0);
            Text("CrowPromptText", g.Crow.LinkWindow > 0 ? $"F · 연계 {g.Crow.LinkWindow:F1}s" : "까마귀 콤보 연계!");
        }

        #endregion

        #region Passive Inventory / Slot / Tooltip / Toast / Card Choices

        private void Passives(Dungeon g, GameHUD h)
        {
            g.Passives.CopyActivePassives(passiveBuffer);
            long acquired = 0;
            foreach (var item in passiveBuffer) if (g.PassiveOffers.Seals.Contains(item.Data)) acquired += item.Stack;
            Text("PassiveCount", $"획득 인장 {acquired} · 성흔과 별도");
            Show("PassiveEmpty", false);
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
                slot.Selected = SelectPassive;
                slots.Add(slot);
                slotIds.Add(item.PassiveID);
            }
            for (int i = 0; i < slots.Count; i++)
            {
                var item = passiveBuffer[i];
                var slot = slots[i];
                if (slot.Icon) slot.Icon.sprite = PassiveIcon(item.Data);
                if (slot.Border) slot.Border.color = PassiveRarityColor(item.Rarity);
                slot.SetStack(item.Stack);
                slot.Description = AppliedEffect(item);
                if (selectedSlot == slot && PassiveTooltip) PassiveTooltip.text = slot.Description;
            }
            UpdatePassiveToast(g, h);
        }

        void SelectPassive(PassiveSlotView slot)
        {
            selectedSlot = selectedSlot == slot ? null : slot;
            PassiveTooltip.text = selectedSlot ? selectedSlot.Description : "";
            PassiveTooltip.gameObject.SetActive(selectedSlot);
        }

        internal static string AppliedEffect(PassiveInstance item)
        {
            string value = item.Value.ToString("+0.##;-0.##;0");
            string amount = value + (item.ValueType == PassiveValueType.Percent ? "%" : "");
            return item.StatType switch
            {
                "ATK" => "공격력 " + amount,
                "HP" => "최대 체력 " + amount,
                "Move" => "이동 속도 " + amount,
                "CrowATK" => "까마귀 공격력 " + amount,
                "CrowRange" => "까마귀 탐색 반경 " + amount,
                "GunReload" => "장전 속도 " + amount,
                "CritDamage" => "치명타 배율 " + value,
                "CritRate" => $"치명타 확률 +{item.Value * 100:0.##}%p",
                "CrowDuration" => $"까마귀 소환 +{item.Value:0.##}초 · 처치 공속 버프 +{item.Value / 15:0.##}초",
                _ => item.StatType + " " + amount
            };
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

        // Card choices refresh independently of the stigma management panel.
        private void UpdateCardChoices(Dungeon g)
        {
            if (g.State != RunState.CardChoice) return;
            bool raven = g.ChoosingFirstStigma;
            bool entrance = g.EntryChoiceActive;
            Text("Card_Title", raven ? "까마귀의 첫 성흔" : entrance ? "던전 입구 · 인장 3택 1" : "악마카드 · 하나의 계약을 선택하세요");
            Text("Card_Subtitle", raven ? "첫 성흔 하나를 무료로 획득합니다." : entrance ? "이번 런에 적용할 첫 인장을 선택하세요." : "획득한 카드는 이번 런 동안 유지됩니다.");
            Text("Cards_CloseText", entrance ? "선택 없이 출발 · ESC" : "나중에 선택 · ESC");
            for (int n = 0; n < 3; n++)
            {
                Text("Card_Choose_" + n + "Text", raven ? "이 성흔 선택" : entrance ? "이 인장 선택" : "이 카드 선택");
                if (raven)
                {
                    var definition = g.FirstStigma(n);
                    Color("CardBorder" + n, ActiveColor);
                    Text("CardRarity" + n, "까마귀 · 첫 획득 무료");
                    Text("CardName" + n, definition.DisplayName);
                    Text("CardEffect" + n, definition.Level(1)?.Description);
                    // Stigma choices show the beta medallion in the card's icon slot.
                    if (!BetaUi.StigmaBadge(Element<Image>("CardIcon" + n), definition))
                        SetPassiveImage("CardIcon" + n, definition.Icon ? definition.Icon : Art.Get(definition.FallbackIconKey));
                    continue;
                }
                BetaUi.StigmaBadge(Element<Image>("CardIcon" + n), null);   // seal cards use plain icons
                var offer = g.PassiveOffers.GetOffer(n);
                if (!offer)
                {
                    Color("CardBorder" + n, InactiveColor);
                    Text("CardRarity" + n, ""); Text("CardName" + n, ""); Text("CardEffect" + n, "");
                    SetPassiveImage("CardIcon" + n, null);
                    continue;
                }
                Color("CardBorder" + n, PassiveRarityColor(offer.Rarity));
                Text("CardRarity" + n, Rarity(offer.Rarity) + (entrance ? " · 인장" : " · 악마카드"));
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

        #region Stigma HUD

        private void UpdateStigmaHud(Dungeon g)
        {
            var state = g.Stigmas.Loadout;
            for (int i = 0; i < 2; i++)
            {
                var equipment = state.Get((StigmaOwner)i);
                string name=i==0 ? "세브" : "코빈";
                string item=equipment.IsEquipped ? equipment.Definition.DisplayName+" "+equipment.Level+"단계" : "미보유";
                Text("Stack"+i,name+"\n"+item);
                Text("RouteStack"+i,name+" · "+item);
                var def=equipment.Definition;
                while(def && def.BaseDefinition) def=def.BaseDefinition;
                int kind=def ? def.Id=="humus" ? 2 : def.Id=="ardor" ? 1 : 0 : 1-i;
                SetPassiveImage("StigmaIcon"+i,stigmaGlyphs[kind]);
                Color("StigmaIcon"+i,equipment.IsEquipped ? UnityEngine.Color.white : new Color(.70f,.60f,.49f,.72f));
            }
            int charges = g.Stigmas.Combat?.Charges ?? 0;
            string chargeText = $"G · {charges}/{state.ChargeCapacity}";
            Text("Stack2",g.IsTown ? "" : state.Combination ? chargeText : "성흔 · 양쪽 장착 필요");
            Text("RouteStack2",chargeText);
            Color("StigmaBorder2", state.Combination && charges >= state.Combination.ChargeCost ? ActiveColor : InactiveColor);
        }

        #endregion

        #region Dungeon Map / Route / Room Nodes

        private void UpdateLocationAndSupplies(Dungeon g)
        {
            string location = g.InBossTrial ? g.TrialLocation : g.IsTown ? "히르바" : g.InPractice ? "붉은 모래길" : g.GetRoomName(g.MapIndex);
            string kind = g.InPractice ? "무기 선택실" : g.InsideHouse ? "사냥꾼의 집" : g.IsTown ? "마을" : g.InBossTrial ? "시련" : $"방 {g.MapIndex+1:00}";
            Text("Location",location);
            Text("LocationKind",kind);
            Text("PromptCaption",kind);
            Text("Notice", g.UsesGeneratedStage && !g.IsTown && g.Progress.SegmentComplete
                ? "방 6/6 완료 · M으로 지도를 열고 다음 스테이지를 선택하세요" : g.Notice);
            UpdateHouseSign(g);
            Text("ObjectiveTitle",g.IsTown ? "마을 안내" : "진척도");
            Text("Supplies",g.IsTown ? "구리동전\n보급품" : "완료한 방\n구리동전");
            Text("ObjectiveValues",g.IsTown ? $"{g.Wallet.Copper}\n{g.Supplies}" : $"{g.MapsCleared} / 6\n{g.Wallet.Copper}");
        }

        void UpdateHouseSign(Dungeon g)
        {
            if (!g.ShowHouseSign)
            {
                if (houseSign) houseSign.gameObject.SetActive(false);
                return;
            }
            if (!houseSign)
            {
                houseSign = Instantiate(Element<Text>("Notice"), HudPanel.transform);
                houseSign.name = "Hunter House Sign";
                houseSign.text = "사냥꾼의 집";
                houseSign.color = Ink.Gold;
                houseSign.alignment = TextAnchor.MiddleCenter;
                houseSign.raycastTarget = false;
                houseSign.rectTransform.sizeDelta = new Vector2(135,26);
                houseSign.rectTransform.pivot = new Vector2(.5f,0);
                houseSign.gameObject.AddComponent<Outline>().effectColor = UnityEngine.Color.black;
            }
            houseSign.gameObject.SetActive(true);
            var screen = g.Cam.WorldToViewportPoint(PaperWorld.Point(g.HouseDoor, .3f));
            houseSign.rectTransform.anchorMin = houseSign.rectTransform.anchorMax = new Vector2(screen.x,screen.y);
            houseSign.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void Prompt(Dungeon g)
        {
            string message = "";
            if (g.Running)
            {
                if (g.InBossTrial || g.NearTrialGate) message = g.TrialPrompt;
                else if (g.InPractice) message = g.PracticePrompt();
                else if (g.IsTown)
                {
                    if (g.HousePrompt != null) message = g.HousePrompt;
                    else if (g.InsideHouse) message = "";
                    else if (g.NpcDialogue && g.NpcDialogue.NearbyNpc) message = $"F · {g.NpcDialogue.NearbyNpcName}와 대화";
                    else if (Vector2.Distance(g.Pos, RoomAuthoring.Point(g.Scene.Hirva.TownExit)) < 3) message = "F · 황야로 출발";
                }
                else
                {
                    var door = g.NearbyDoor();
                    // 분기 표식은 방 번호가 아니라 다음 스테이지 노드를 가리킵니다.
                    if (door && door.RunMapDestination >= 0)
                        message = $"F · 다음 스테이지 ({door.RunMapDestinationType}) 이동";
                    else if (door) message = door.StageExit ? (g.Progress.SegmentComplete ? "F · 스테이지 출구 / 다음 여정 선택" : $"출구 봉인 · 미완료 방 {6-g.MapsCleared}개") : (g.Progress.CanLeave ? $"F · {door.Destination+1:00} {g.GetRoomName(door.Destination)} 이동" : "문 봉인 · 이 방의 모든 적을 처치하세요");
                    else if (g.CurrentKind != ExplorationRoomKind.Combat && g.CurrentKind != ExplorationRoomKind.Entry && Vector2.Distance(g.Pos, RoomAuthoring.Point(g.CurrentRoom.Layout.Authored.ServicePoint)) < 2.4f) message = g.CurrentKind == ExplorationRoomKind.Sigil ? "F · 성흔 획득 / 교체 / 강화" : g.Progress.MapComplete ? "이 방의 보상을 선택했습니다" : "F · 악마카드 진열대 / 카드 선택";
                }
            }
            Show("Prompt", message != "");
            Text("PromptText", message);
            if (message.Length > 0 && Element<RectTransform>("Prompt") is RectTransform banner)
                banner.anchoredPosition = new Vector2(banner.anchoredPosition.x,
                    g.InBossTrial || g.IsBossWave ? -125f : -19f);
        }

        private void Map(Dungeon g)
        {
            // The room map (M key) is drawn by BetaRoomMap. It is built the first time the panel opens.
            if (RoutePanel.activeSelf) (roomMap ??= new BetaRoomMap(this)).Refresh(g);
            bool field=!g.IsTown && !g.InPractice && !g.InBossTrial && !g.InsideHouse;
            if (miniMapRect) Panel(miniMapRect.gameObject,field);
            if (field && g.UsesGeneratedStage) UpdateGeneratedMinimap(g.Progress.GeneratedLayout);
            else RestoreFixedMinimap();
            for (int i = 0; i < 6; i++)
            {
                bool current = !g.IsTown && g.MapIndex == i, done = !g.IsTown && g.Progress.Cleared(i), visited = !g.IsTown && g.Progress.Visited(i);
                var color = current ? UnlockedColor : done ? ActiveColor : visited ? new Color(.97f, .89f, .72f) : InactiveColor;
                Color("MiniNode" + i, color);
                if (g.UsesGeneratedStage && miniNodeFills[i]) miniNodeFills[i].color = GeneratedRoleColor(g.Progress.GeneratedLayout.Rooms[i].Role);
            }
            // The "next stage" button is the authored one, restyled and placed by BetaRoomMap.
            Show("RouteTravel", g.RouteTravel);
            Text("RouteTravelText", g.IsTown ? "황야로 출발" : g.Room == 4 ? "탐험 완료" : "다음 스테이지로 이동");
        }

        private void CacheMinimap()
        {
            if (TryRef("Minimap", out var root))
            {
                miniMapRect=new GameObject("RoomLayout",typeof(RectTransform)).GetComponent<RectTransform>();
                miniMapRect.SetParent(root.transform,false);
                miniMapRect.anchorMin=miniMapRect.anchorMax=miniMapRect.pivot=Vector2.up;
                miniMapRect.anchoredPosition=new Vector2(14,-33);
                miniMapRect.sizeDelta=new Vector2(190,145);
                miniMapRect.localScale=Vector3.one*.74f;
                for(int i=0;i<5;i++)
                    if(TryRef("MiniLink"+i,out var line)) line.transform.SetParent(miniMapRect,false);
                for(int i=0;i<6;i++)
                    if(TryRef("MiniNode"+i,out var room)) room.transform.SetParent(miniMapRect,false);
            }
            for (int i = 0; i < 6; i++)
            {
                if (TryRef("MiniNode" + i, out var node))
                {
                    miniNodeRects[i] = node.GetComponent<RectTransform>();
                    if (miniNodeRects[i]) { miniNodeBasePositions[i] = miniNodeRects[i].anchoredPosition; miniNodeBaseSizes[i] = miniNodeRects[i].sizeDelta; }
                }
                if (TryRef("MiniNode" + i + "_Fill", out var fill))
                {
                    miniNodeFills[i] = fill.GetComponent<Image>();
                    if (miniNodeFills[i]) miniFillBaseColors[i] = miniNodeFills[i].color;
                }
                if (TryRef("MiniLabel" + i, out var label))
                {
                    miniLabels[i] = label.GetComponent<Text>();
                    miniLabelRects[i] = label.GetComponent<RectTransform>();
                    if (miniLabels[i]) { miniLabelBaseText[i] = miniLabels[i].text; miniLabelBaseFontSizes[i] = miniLabels[i].fontSize; }
                    if (miniLabelRects[i]) miniLabelBaseSizes[i] = miniLabelRects[i].sizeDelta;
                }
            }
            for (int i = 0; i < miniLinks.Length; i++)
            {
                if (!TryRef("MiniLink" + i, out var link)) continue;
                miniLinks[i] = link.GetComponent<Image>();
                if (!miniLinks[i]) continue;
                var rect = miniLinks[i].rectTransform;
                miniLinkBasePositions[i] = rect.anchoredPosition;
                miniLinkBaseSizes[i] = rect.sizeDelta;
                miniLinkBaseRotations[i] = rect.localRotation;
                miniLinkColor = miniLinks[i].color;
            }
        }

        private void UpdateGeneratedMinimap(StageLayout layout)
        {
            if (!miniMapRect || layout == null || layout.Rooms == null || layout.Rooms.Count != 6) return;
            generatedMiniMapActive = true;
            int minX = layout.Rooms[0].GridPosition.x, maxX = minX;
            int minY = layout.Rooms[0].GridPosition.y, maxY = minY;
            for (int i = 1; i < 6; i++)
            {
                var grid = layout.Rooms[i].GridPosition;
                minX = Mathf.Min(minX, grid.x); maxX = Mathf.Max(maxX, grid.x);
                minY = Mathf.Min(minY, grid.y); maxY = Mathf.Max(maxY, grid.y);
            }

            const float nodeWidth = 30f, nodeHeight = 20f;
            float spanX = maxX - minX, spanY = maxY - minY;
            float scaleX = spanX > 0 ? Mathf.Max(1f, miniMapRect.rect.width - nodeWidth) / spanX : float.PositiveInfinity;
            float scaleY = spanY > 0 ? Mathf.Max(1f, miniMapRect.rect.height - nodeHeight) / spanY : float.PositiveInfinity;
            float scale = Mathf.Min(scaleX, scaleY);
            if (float.IsInfinity(scale) || float.IsNaN(scale)) scale = 1f;
            float centerX = (minX + maxX) * .5f, centerY = (minY + maxY) * .5f;
            Vector2 mapCenter = new Vector2(miniMapRect.rect.width * .5f, miniMapRect.rect.height * .5f);

            for (int i = 0; i < 6; i++)
            {
                RoomNode room = layout.Rooms[i];
                Vector2 point = mapCenter + new Vector2((room.GridPosition.x - centerX) * scale, -(room.GridPosition.y - centerY) * scale);
                generatedMiniPoints[i] = point;
                if (miniNodeRects[i])
                {
                    miniNodeRects[i].sizeDelta = new Vector2(nodeWidth, nodeHeight);
                    miniNodeRects[i].anchoredPosition = new Vector2(point.x - nodeWidth * .5f, -(point.y - nodeHeight * .5f));
                }
                if (miniLabels[i]) miniLabels[i].text = (i + 1).ToString("00");
                if (miniNodeFills[i]) miniNodeFills[i].color = GeneratedRoleColor(room.Role);
            }

            int edgeCount = 0;
            for (int i = 0; i < 6; i++)
                foreach (int neighbor in layout.Rooms[i].Neighbors)
                    if (neighbor > i)
                    {
                        EnsureMiniLink(edgeCount);
                        SetMiniLink(edgeCount, generatedMiniPoints[i], generatedMiniPoints[neighbor]);
                        edgeCount++;
                    }
            for (int i = edgeCount; i < miniLinks.Length + extraMiniLinks.Count; i++)
            {
                Image link = GetMiniLink(i);
                if (link) link.gameObject.SetActive(false);
            }
            generatedMiniEdgeCount = edgeCount;
        }

        private void EnsureMiniLink(int index)
        {
            if (index < miniLinks.Length || index - miniLinks.Length < extraMiniLinks.Count) return;
            if (miniLinks[0] == null) return;
            var clone = Instantiate(miniLinks[0], miniMapRect, false);
            clone.name = "GeneratedMiniLink" + extraMiniLinks.Count;
            clone.raycastTarget = false;
            clone.transform.SetSiblingIndex(miniNodeRects[0] ? miniNodeRects[0].GetSiblingIndex() : miniMapRect.childCount - 1);
            extraMiniLinks.Add(clone);
        }

        private Image GetMiniLink(int index) => index < miniLinks.Length ? miniLinks[index] : extraMiniLinks[index - miniLinks.Length];

        private void SetMiniLink(int index, Vector2 a, Vector2 b)
        {
            Image image = GetMiniLink(index);
            if (!image) return;
            var rect = image.rectTransform;
            Vector2 delta = b - a;
            rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = new Vector2(a.x, -a.y);
            rect.sizeDelta = new Vector2(delta.magnitude, 2f);
            rect.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            image.color = miniLinkColor;
            image.gameObject.SetActive(true);
        }

        private void RestoreFixedMinimap()
        {
            if (!generatedMiniMapActive) return;
            generatedMiniMapActive = false;
            for (int i = 0; i < 6; i++)
            {
                if (miniNodeRects[i])
                {
                    miniNodeRects[i].anchoredPosition = miniNodeBasePositions[i];
                    miniNodeRects[i].sizeDelta = miniNodeBaseSizes[i];
                }
                if (miniLabels[i]) miniLabels[i].text = miniLabelBaseText[i];
                if (miniLabelRects[i]) miniLabelRects[i].sizeDelta = miniLabelBaseSizes[i];
                if (miniLabels[i] && miniLabelBaseFontSizes[i] > 0) miniLabels[i].fontSize = miniLabelBaseFontSizes[i];
                if (miniNodeFills[i]) miniNodeFills[i].color = miniFillBaseColors[i];
            }
            for (int i = 0; i < miniLinks.Length; i++)
            {
                if (!miniLinks[i]) continue;
                var rect = miniLinks[i].rectTransform;
                rect.anchoredPosition = miniLinkBasePositions[i];
                rect.sizeDelta = miniLinkBaseSizes[i];
                rect.localRotation = miniLinkBaseRotations[i];
                miniLinks[i].gameObject.SetActive(true);
            }
            foreach (var link in extraMiniLinks) if (link) link.gameObject.SetActive(false);
            generatedMiniEdgeCount = 0;
        }

        private bool TryRef(string key, out GameObject target) => refs.TryGetValue(key, out target) && target;

        private static Color GeneratedRoleColor(RoomRole role)
        {
            switch (role)
            {
                case RoomRole.Entry: return new Color(.25f, .53f, .58f);
                case RoomRole.Sigil: return new Color(.48f, .30f, .75f);
                case RoomRole.Shop: return new Color(.76f, .47f, .19f);
                case RoomRole.CardMerchant: return new Color(.25f, .43f, .72f);
                case RoomRole.Boss: return new Color(.75f, .22f, .19f);
                case RoomRole.Event: return new Color(.30f, .61f, .38f);
                default: return new Color(.38f, .36f, .37f);
            }
        }

        #endregion

        #region Boss health

        private void PlaceBossBar()
        {
            var bar = Element<RectTransform>("Boss");
            if (!bar) return;
            bar.anchorMin = new Vector2(.26f,.91f);
            bar.anchorMax = new Vector2(.74f,.91f);
            bar.pivot = Vector2.one * .5f;
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0,26);
            var track = Element<Image>("BossHP_Background");
            var health = Element<Image>("BossHP");
            FitBossLayer(track.rectTransform, Vector2.zero, Vector2.one);
            FitBossLayer(health.rectTransform, new Vector2(.014f,.23f), new Vector2(.985f,.78f));
            track.sprite = Resources.Load<Sprite>("WesternBeta/Hud/BossHealthTrack");
            track.color = UnityEngine.Color.white;
            health.sprite = Resources.Load<Sprite>("WesternBeta/Hud/BossHealthFill");
            health.color = UnityEngine.Color.white;
            health.type = UnityEngine.UI.Image.Type.Filled;
            health.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            health.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            var bezel = new GameObject("Boss emblem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            bezel.transform.SetParent(bar,false);
            bezel.raycastTarget = false;
            bezel.sprite = Resources.Load<Sprite>("WesternBeta/Hud/BossIcon");
            FitBossLayer(bezel.rectTransform, Vector2.up * .5f, Vector2.up * .5f);
            bezel.rectTransform.sizeDelta = Vector2.one * 50;
            var name = Element<Text>("BossHPText");
            FitBossLayer(name.rectTransform, Vector2.up, Vector2.one);
            name.rectTransform.pivot = new Vector2(.5f,0);
            name.rectTransform.sizeDelta = new Vector2(0,33);
            name.rectTransform.anchoredPosition = Vector2.down * 4;
            name.alignment = TextAnchor.MiddleCenter;
            name.resizeTextForBestFit = false;
            name.fontSize = 25;
            name.fontStyle = FontStyle.Bold;
            name.color = new Color(1,.97f,.86f);
            var outline = name.GetComponent<Outline>() ?? name.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.09f,.052f,.034f,1);
            outline.effectDistance = new Vector2(1.8f,-1.8f);
            name.transform.SetAsLastSibling();
        }

        static void FitBossLayer(RectTransform layer, Vector2 min, Vector2 max)
        {
            layer.anchorMin = min;
            layer.anchorMax = max;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
        }
        private void UpdateBossHealth(Dungeon g)
        {
            Enemy boss = g.InBossTrial ? g.TrialBoss : g.IsBossWave && g.Enemies.Count > 0 ? g.Enemies[0] : null;
            Show("Boss", boss);
            if (!boss) return;
            Fill("BossHP", boss.Hp / Mathf.Max(1, boss.MaxHp));
            string name = g.InBossTrial ? g.TrialBossName : "봉인의 파수꾼";
            Text("BossHPText", $"{name}  ·  {Mathf.CeilToInt(100 * boss.Hp / Mathf.Max(1, boss.MaxHp))}%");
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

        private void Image(string key, Sprite sprite)
        {
            var image = Element<Image>(key);
            if (image) image.sprite = sprite;
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

        internal Sprite PassiveIcon(PassiveSO passive)
        {
            if (!passive) return null;
            if (passive.Icon) return passive.Icon;
            if (!Art) return null;
            if (!string.IsNullOrEmpty(passive.PassiveIconResource)) return Art.Get(passive.PassiveIconResource);
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

        #endregion
    }
}
