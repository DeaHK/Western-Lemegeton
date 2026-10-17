using System.Collections.Generic;
using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    public sealed class StigmaCardOffer
    {
        public StigmaOwner Owner { get; }
        public StigmaDefinitionSO Definition { get; }
        public StigmaCardOffer(StigmaOwner owner, StigmaDefinitionSO definition) { Owner = owner; Definition = definition; }
    }

    public sealed class StigmaRoomService
    {
        sealed class RoomOffer
        {
            public readonly List<StigmaCardOffer> Cards = new List<StigmaCardOffer>();
            public IReadOnlyList<StigmaCardOffer> View;
            public bool Claimed;
        }
        readonly StigmaManager manager;
        readonly Dictionary<int, RoomOffer> roomOffers = new Dictionary<int, RoomOffer>();
        bool committing;
        public StigmaRoomService(StigmaManager manager) { this.manager = manager; }
        public bool IsOpen
        {
            get
            {
                var g = Dungeon.I;
                if (!g || g.Stigmas != manager || g.State != RunState.SigilChoice || g.IsTown || g.CurrentKind != ExplorationRoomKind.Sigil) return false;
                foreach (var e in g.Enemies) if (e && !e.Dead) return false;
                return true;
            }
        }
        RoomOffer CurrentOffer()
        {
            if (!IsOpen) return null;
            var g = Dungeon.I;
            int key = g.Room * ExpeditionProgress.MapCount + g.MapIndex;
            if (roomOffers.TryGetValue(key, out var stored)) return stored;
            var pool = new List<StigmaCardOffer>();
            foreach (var d in manager.Database.Definitions)
                if (d.InitialChoice)
                    foreach (StigmaOwner owner in System.Enum.GetValues(typeof(StigmaOwner)))
                        if (manager.Loadout.IsRoomCandidate(owner, d)) pool.Add(new StigmaCardOffer(owner, d));
            var random = new System.Random(unchecked(g.Seed ^ ((g.Room + 1) * 73856093) ^ ((g.MapIndex + 1) * 19349663)));
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                var card = pool[i]; pool[i] = pool[other]; pool[other] = card;
            }
            stored = new RoomOffer();
            stored.Cards.AddRange(pool.GetRange(0, Mathf.Min(3, pool.Count)));
            stored.View = stored.Cards.AsReadOnly();
            roomOffers.Add(key, stored);
            return stored;
        }
        public IReadOnlyList<StigmaCardOffer> GetOffers() => CurrentOffer()?.View ?? System.Array.Empty<StigmaCardOffer>();
        public bool HasClaimedCard => CurrentOffer()?.Claimed ?? false;
        public void ResetRun() => roomOffers.Clear();

        public bool CanClaimCard(int index, out string error)
        {
            error = null;
            if (!IsOpen || committing) { error = "성흔방 제단에서만 획득할 수 있습니다."; return false; }
            var offer = CurrentOffer();
            if (offer.Claimed) { error = "이 방의 성흔 카드는 이미 획득했습니다."; return false; }
            if (index < 0 || index >= offer.Cards.Count) { error = "카드를 선택하세요."; return false; }
            var card = offer.Cards[index];
            if (!manager.Loadout.IsRoomCandidate(card.Owner, card.Definition)) { error = "이미 보유했거나 강화 성흔 획득 조건이 바뀐 카드입니다."; return false; }
            return true;
        }
        public bool ClaimCard(int index, out string error)
        {
            if (!CanClaimCard(index, out error)) return false;
            var offer = CurrentOffer();
            var card = offer.Cards[index];
            committing = true;
            offer.Claimed = true;
            try
            {
                if (!manager.TryAcquire(card.Owner, card.Definition.Id, out error)) { offer.Claimed = false; return false; }
                // The loadout already replaces an equipped ancestor on upgrade acquisition.
                // Otherwise only the first acquisition auto-equips; unrelated cards are stored.
                if (!manager.Loadout.Get(card.Owner).IsEquipped) manager.TryEquip(card.Owner, card.Definition.Id, out _);
                Dungeon.I.Progress.CompleteService();
                return true;
            }
            finally { committing = false; }
        }
        public bool Quote(StigmaOwner owner, string id, bool upgrade, out int cost, out string error)
        {
            cost = 0; error = null;
            if (owner != StigmaOwner.Player && owner != StigmaOwner.Raven) { error = "대상을 선택하세요."; return false; }
            if (!IsOpen || committing) { error = "전투가 끝난 성흔방 제단에서만 이용할 수 있습니다."; return false; }
            var equipment = manager.Loadout.Get(owner);
            if (upgrade)
            {
                if (!equipment.IsEquipped) { error = "먼저 성흔을 장착하세요."; return false; }
                if (equipment.Level >= StigmaRulesSO.MaximumLevel) { error = "최대 단계입니다."; return false; }
                cost = manager.Database.Rules.UpgradeCosts[equipment.Level - 1];
            }
            else
            {
                var definition = manager.Database.Find(id);
                if (!definition || !manager.Loadout.Owns(owner, id)) { error = "카드로 먼저 획득한 성흔만 장착할 수 있습니다."; return false; }
                if (!manager.Loadout.CanEquip(owner, id, out error)) return false;
                cost = equipment.IsEquipped ? manager.Database.Rules.ReplacementCosts[equipment.Level - 1] : 0;
            }
            return true;
        }
        public bool Purchase(StigmaOwner owner, string id, bool upgrade, out string error)
        {
            if (!Quote(owner, id, upgrade, out int cost, out error)) return false;
            var g = Dungeon.I;
            if (!g.Wallet.Spend(cost)) { error = "구리동전이 부족합니다."; return false; }
            committing = true;
            try
            {
                if (upgrade) manager.TrySetLevel(owner, manager.Loadout.Get(owner).Level + 1, out error);
                else manager.TryEquip(owner, id, out error);
                if (error != null) { g.Wallet.Add(cost); return false; }
                g.Progress.CompleteService();
                return true;
            }
            finally { committing = false; }
        }
    }
}
