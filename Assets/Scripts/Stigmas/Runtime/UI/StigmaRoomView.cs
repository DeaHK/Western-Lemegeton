using System;
using UnityEngine;
using UnityEngine.UI;

namespace WesternLemegeton.Stigmas
{
    [Serializable]
    public sealed class StigmaSlotView
    {
        public Button Button;
        public Image Frame, Icon;
        public Text Name, Equipped;
    }

    [Serializable]
    public sealed class StigmaOfferCardView
    {
        public Button Button;
        public Image Frame, Icon;
        public Text Owner, Name, Effect;
    }

    // Authored UGUI view. The selected owner's acquisitions populate the six visible slots.
    public sealed class StigmaRoomView : MonoBehaviour
    {
        public VisualCatalog Art;
        public Button PlayerButton, RavenButton, EquipButton, PreviousButton, NextButton, CloseButton;
        public Button OfferButton, UpgradeButton;
        public Text OfferText, UpgradeText;
        public Text PlayerText, RavenText, CollectionText, CurrentCombination, CurrentSkill;
        public Text DetailName, DetailLevel, DetailEffect, DetailPreview, ActionText, StatusText;
        public Image DetailIcon;
        public ScrollRect DetailScroll;
        public GameObject OffersPanel;
        public Text OffersTitle;
        public StigmaOfferCardView[] OfferCards = new StigmaOfferCardView[3];
        public StigmaSlotView[] Slots = new StigmaSlotView[StigmaRulesSO.VisibleSlots];
        public Color SelectedColor = new Color(.84f, .71f, .47f), IdleColor = new Color(.3f, .24f, .3f);
        StigmaManager manager;
        StigmaOwner owner;
        string selectedId;
        int page;
        bool wired;
        string transactionMessage;
        bool showOffers;
        int selectedOffer = -1;
        public StigmaOwner SelectedOwner => owner;
        public string SelectedId => selectedId;
        public int Page => page;

        void OnEnable()
        {
            Wire();
            if (Dungeon.I && Dungeon.I.Stigmas) Bind(Dungeon.I.Stigmas);
        }
        void OnDisable() { if (manager) manager.Changed -= OnChanged; }
        void Wire()
        {
            if (wired) return;
            wired = true;
            PlayerButton.onClick.AddListener(() => SelectOwner(StigmaOwner.Player));
            RavenButton.onClick.AddListener(() => SelectOwner(StigmaOwner.Raven));
            PreviousButton.onClick.AddListener(() => SetPage(page - 1));
            NextButton.onClick.AddListener(() => SetPage(page + 1));
            EquipButton.onClick.AddListener(PurchaseSelected);
            if (OfferButton) OfferButton.onClick.AddListener(ToggleOffers);
            if (UpgradeButton) UpgradeButton.onClick.AddListener(UpgradeEquipped);
            CloseButton.onClick.AddListener(() => {
                if (!Dungeon.I) return;
                if (Dungeon.I.State == RunState.SigilChoice) Dungeon.I.CloseService();
                else Dungeon.I.GetComponent<GameHUD>().CloseStigmaManagement();
            });
            for (int i = 0; i < Slots.Length; i++)
            {
                int index = i;
                Slots[i].Button.onClick.AddListener(() => SelectSlot(index));
            }
            for (int i = 0; i < OfferCards.Length; i++)
            {
                int index = i;
                if (OfferCards[i]?.Button) OfferCards[i].Button.onClick.AddListener(() => SelectOffer(index));
            }
        }
        public void Bind(StigmaManager value)
        {
            Wire();
            if (manager) manager.Changed -= OnChanged;
            manager = value;
            if (manager) manager.Changed += OnChanged;
            showOffers = manager && manager.Room.IsOpen && !manager.Room.HasClaimedCard && manager.Room.GetOffers().Count > 0;
            selectedOffer = -1;
            Refresh();
        }
        void OnChanged(StigmaChange change)
        {
            if (change.Kind == StigmaChangeKind.Reset) { selectedId = null; page = 0; owner = StigmaOwner.Player; selectedOffer = -1; }
            Refresh();
        }
        public void SelectOwner(StigmaOwner value)
        {
            if (value != StigmaOwner.Player && value != StigmaOwner.Raven) return;
            if (owner != value) { selectedId = null; page = 0; }
            if (showOffers) selectedId = null;
            showOffers = false;
            owner = value;
            transactionMessage = null;
            Refresh();
        }
        public void SetPage(int value)
        {
            if (!manager) return;
            page = Mathf.Clamp(value, 0, Math.Max(0, (manager.Loadout.GetAcquired(owner).Count - 1) / StigmaRulesSO.VisibleSlots));
            selectedId = null;
            Refresh();
        }
        public void SelectSlot(int index)
        {
            if (!manager || index < 0 || index >= StigmaRulesSO.VisibleSlots) return;
            int absolute = page * StigmaRulesSO.VisibleSlots + index;
            var acquired = manager.Loadout.GetAcquired(owner);
            if (absolute >= acquired.Count) return;
            selectedId = acquired[absolute].Id;
            transactionMessage = null;
            Refresh();
        }

        void PurchaseSelected()
        {
            if (!manager || selectedId == null) return;
            if (showOffers)
            {
                if (manager.Room.ClaimCard(selectedOffer, out string error))
                {
                    showOffers = false;
                    transactionMessage = OwnerName(owner) + " 성흔 획득 완료 · 이번 방의 카드 선택 완료";
                    var acquired = manager.Loadout.GetAcquired(owner);
                    for (int i = 0; i < acquired.Count; i++) if (acquired[i].Id == selectedId) page = i / StigmaRulesSO.VisibleSlots;
                }
                else transactionMessage = error;
            }
            else transactionMessage = manager.Room.Purchase(owner, selectedId, false, out string error) ? "성흔 장착 완료" : error;
            Refresh();
        }
        void UpgradeEquipped()
        {
            if (!manager) return;
            if (showOffers || manager.Loadout.Get(owner).Definition?.Id != selectedId)
            { transactionMessage = "선택한 성흔을 먼저 장착하세요."; Refresh(); return; }
            transactionMessage = manager.Room.Purchase(owner, null, true, out string error) ? "장착 성흔 강화 완료" : error;
            Refresh();
        }
        void ToggleOffers()
        {
            if (!manager || !manager.Room.IsOpen) return;
            showOffers = !showOffers && !manager.Room.HasClaimedCard && manager.Room.GetOffers().Count > 0;
            selectedOffer = -1; selectedId = null;
            transactionMessage = null; Refresh();
        }
        public void SelectOffer(int index)
        {
            if (!manager || !manager.Room.IsOpen || manager.Room.HasClaimedCard) return;
            var cards = manager.Room.GetOffers();
            if (index < 0 || index >= cards.Count) return;
            showOffers = true; selectedOffer = index;
            owner = cards[index].Owner; selectedId = cards[index].Definition.Id;
            page = 0; transactionMessage = null; Refresh();
        }
        void LateUpdate() { if (manager) Refresh(); }

        public void Refresh()
        {
            if (!manager) return;
            var state = manager.Loadout;
            bool service = manager.Room.IsOpen;
            var offers = manager.Room.GetOffers();
            bool claimed = manager.Room.HasClaimedCard;
            showOffers = showOffers && service && !claimed && offers.Count > 0;
            if (showOffers)
            {
                selectedOffer = Mathf.Clamp(selectedOffer, 0, offers.Count - 1);
                owner = offers[selectedOffer].Owner;
                selectedId = offers[selectedOffer].Definition.Id;
            }
            var acquired = state.GetAcquired(owner);
            var player = state.Get(StigmaOwner.Player);
            var raven = state.Get(StigmaOwner.Raven);
            var mine = state.Get(owner);
            var other = state.Get(owner == StigmaOwner.Player ? StigmaOwner.Raven : StigmaOwner.Player);
            page = Mathf.Clamp(page, 0, Math.Max(0, (acquired.Count - 1) / StigmaRulesSO.VisibleSlots));
            int offset = page * StigmaRulesSO.VisibleSlots;
            if (!showOffers && (selectedId == null || !state.Owns(owner, selectedId)))
            {
                selectedId = offset < acquired.Count ? acquired[offset].Id : null;
                for (int i = offset; i < Math.Min(offset + StigmaRulesSO.VisibleSlots, acquired.Count); i++)
                    if (mine.Definition == acquired[i]) { selectedId = mine.Definition.Id; break; }
            }
            var selected = selectedId == null ? null : manager.Database.Find(selectedId);
            PlayerText.text = "플레이어" + (owner == StigmaOwner.Player ? " · 선택 중" : "") + "\n" + EquipmentText(player);
            RavenText.text = "까마귀" + (owner == StigmaOwner.Raven ? " · 선택 중" : "") + "\n" + EquipmentText(raven);
            PlayerButton.image.color = owner == StigmaOwner.Player ? new Color(.25f,.18f,.24f) : new Color(.12f,.08f,.14f);
            RavenButton.image.color = owner == StigmaOwner.Raven ? new Color(.25f,.18f,.24f) : new Color(.12f,.08f,.14f);
            int pages = Math.Max(1, (acquired.Count + 5) / 6);
            CollectionText.text = showOffers ? "성흔 획득 · 플레이어용 / 까마귀용 혼합" : $"{OwnerName(owner)}의 성흔 {acquired.Count}종 · {page + 1}/{pages}";
            PreviousButton.gameObject.SetActive(!showOffers && pages > 1);
            NextButton.gameObject.SetActive(!showOffers && pages > 1);
            PreviousButton.interactable = page > 0;
            NextButton.interactable = page + 1 < pages;
            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                slot.Button.gameObject.SetActive(!showOffers);
                slot.Name.gameObject.SetActive(!showOffers);
                slot.Equipped.gameObject.SetActive(!showOffers);
                var d = offset + i < acquired.Count ? acquired[offset + i] : null;
                slot.Button.interactable = d;
                slot.Frame.color = d && d == selected ? SelectedColor : IdleColor;
                slot.Name.text = d ? d.DisplayName + (d.IsEnhanced ? " ✦" : "") + " · Lv." + state.GetLevel(owner, d.Id) : "빈 칸";
                SetIcon(slot.Icon, d);
                slot.Equipped.text = d && state.IsSuperseded(owner, d) ? "강화 보유 · 장착 불가" : d && mine.Definition == d ? "장착 중" : "";
            }
            var current = state.Combination;
            CurrentCombination.gameObject.SetActive(!showOffers);
            CurrentSkill.gameObject.SetActive(!showOffers);
            CurrentCombination.text = current ? current.DisplayName : state.BothEquipped ? "조합 미등록" : "성흔 미완성";
            CurrentSkill.text = current ? current.SkillName + $"\n{manager.Database.Rules.LinkSkillKey} · 표식 {manager.Combat?.Charges ?? 0}/{current.ChargeCapacity}" : "두 캐릭터의 장착 상태";
            if (OffersPanel) OffersPanel.SetActive(showOffers);
            if (OffersTitle) OffersTitle.text = $"{offers.Count}장 중 1장 선택 · 카드를 눌러 효과 확인";
            for (int i = 0; i < OfferCards.Length; i++)
            {
                var card = OfferCards[i];
                if (card?.Button == null) continue;
                bool visible = showOffers && i < offers.Count;
                card.Button.gameObject.SetActive(visible);
                if (!visible) continue;
                var offer = offers[i];
                card.Frame.color = i == selectedOffer ? SelectedColor : IdleColor;
                card.Owner.text = OwnerName(offer.Owner) + "용";
                card.Owner.color = offer.Owner == StigmaOwner.Player ? SelectedColor : new Color(.4f,.85f,1);
                card.Name.text = offer.Definition.DisplayName + (offer.Definition.IsEnhanced ? " ✦" : "");
                const int ownLevel = 1;
                card.Effect.text = $"획득 단계 Lv.{ownLevel}\n" + offer.Definition.Level(ownLevel).Description +
                    (offer.Definition.IsEnhanced ? "\n" + offer.Definition.BaseDefinition.DisplayName + "의 강화 성흔" : "");
                card.Button.interactable = state.IsRoomCandidate(offer.Owner, offer.Definition);
                SetIcon(card.Icon, offer.Definition);
            }
            SetIcon(DetailIcon, selected);
            if (!selected)
            {
                DetailName.text = OwnerName(owner) + "의 성흔 없음";
                DetailLevel.text = "";
                SetDetailText(OwnerName(owner) + "가 획득한 성흔이 빈 칸에 하나씩 추가됩니다.");
                EquipButton.interactable = false;
            }
            else
            {
                int level = Math.Max(1, state.GetLevel(owner, selected.Id));
                int effective = other.IsEquipped ? Math.Min(level, other.Level) : 0;
                int ownEffect = selected.UseOwnerLevel ? level : effective;
                DetailName.text = selected.DisplayName;
                bool equipped = mine.Definition == selected;
                DetailLevel.text = $"{OwnerName(owner)} · {(showOffers ? "획득 후보" : equipped ? "장착" : "장착 시")} Lv.{level}\n" +
                    (effective > 0 ? selected.UseOwnerLevel ? $"고유 Lv.{ownEffect} · 조합/G Lv.{effective}" : $"{(equipped ? "현재" : "장착 시")} 효과 Lv.{effective} · 양쪽 중 낮은 단계" : "상대 미장착 · 효과 비활성") +
                    (selected.IsEnhanced ? " · 강화 성흔" : "");
                var preview = manager.Database.FindCombination(selected, other.Definition);
                SetDetailText(StigmaDescriptions.Detail(selected, level, mine, other, preview, manager.Database.Rules, OwnerName(owner)));
            }
            int copper = Dungeon.I ? Dungeon.I.Wallet.Copper : 0;
            bool canEquip = manager.Room.Quote(owner, selectedId, false, out int cost, out string equipError);
            EquipButton.interactable = showOffers ? manager.Room.CanClaimCard(selectedOffer, out _) : canEquip && copper >= cost;
            ActionText.text = showOffers ? OwnerName(owner) + "용 카드 획득" : state.IsSuperseded(owner, selected) ? "강화 보유 · 하위 장착 불가" : !service ? "성흔방에서 장착 가능" : canEquip ? (cost == 0 ? "장착 / 교체 · 무료" : $"교체 · {cost} 동전") : mine.IsEquipped ? "장착 중" : "획득한 성흔 없음";
            if (OfferButton) OfferButton.gameObject.SetActive(service);
            if (OfferButton) OfferButton.interactable = showOffers || (!claimed && offers.Count > 0);
            if (OfferText) OfferText.text = showOffers ? "보유 성흔 관리 · 장착 / 레벨 강화" : claimed ? "이번 방의 성흔 카드 획득 완료" : offers.Count > 0 ? $"성흔 카드 선택 · {offers.Count}장 중 1장" : "새로 획득할 성흔이 없습니다";
            bool canUpgrade = manager.Room.Quote(owner, null, true, out int upgradeCost, out string upgradeError);
            bool selectedEquipped = selected && mine.Definition == selected;
            if (UpgradeButton) { UpgradeButton.gameObject.SetActive(service && !showOffers); UpgradeButton.interactable = selectedEquipped && canUpgrade && copper >= upgradeCost; }
            if (UpgradeText) UpgradeText.text = !selectedEquipped ? "선택 성흔 장착 후 강화" : canUpgrade ? $"강화 Lv.{mine.Level + 1} · {upgradeCost}" : mine.Level == 3 ? "최대 단계" : "장착 후 강화";
            StatusText.text = $"구리동전 {copper} · 가상체력 {manager.Combat?.Shield.Current ?? 0:0.#}\n" +
                (transactionMessage ?? (showOffers ? "카드 1장을 선택하고 획득 버튼으로 확정하세요. 첫 성흔·장착 중인 하위의 강화는 자동 장착됩니다." : service ? "보유 성흔은 무료로 교체할 수 있습니다. 레벨 강화는 현재 장착 성흔에 적용됩니다." : "장착 정보 조회 · 교체와 강화는 성흔방에서 가능합니다."));
        }

        void SetDetailText(string text)
        {
            if (DetailEffect.text != text)
            {
                DetailEffect.text = text;
                if (DetailScroll)
                {
                    DetailEffect.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                        Mathf.Max(DetailScroll.viewport.rect.height, DetailEffect.preferredHeight + 8));
                    DetailScroll.StopMovement();
                    DetailEffect.rectTransform.anchoredPosition = Vector2.zero;
                }
            }
            DetailPreview.text = DetailScroll && DetailEffect.rectTransform.rect.height > DetailScroll.viewport.rect.height + 1
                ? "휠 / 드래그로 아래의 고유 효과·다음 단계 확인 ↓" : "";
        }

        void SetIcon(Image image, StigmaDefinitionSO data)
        {
            image.sprite = data ? data.Icon ? data.Icon : Art ? Art.Get(data.FallbackIconKey) : null : null;
            image.enabled = image.sprite;
        }
        static string EquipmentText(StigmaEquipment e) => e.IsEquipped ? e.Definition.DisplayName + " · Lv." + e.Level : "미장착";
        static string OwnerName(StigmaOwner value) => value == StigmaOwner.Player ? "플레이어" : "까마귀";
    }
}
