using UnityEngine;
using UnityEngine.UI;

namespace WesternLemegeton.Stigmas.Editor
{
    // Builds only the stigma management panel; preserves the existing Canvas and HUD.
    public static class StigmaUIAuthoring
    {
        static readonly Color Ink = new Color(.92f, .86f, .73f), Muted = new Color(.72f, .65f, .7f);
        static readonly Color Background = new Color(.055f, .04f, .065f, .99f), Panel = new Color(.12f, .08f, .14f);
        static readonly Color Border = new Color(.3f, .24f, .3f), Gold = new Color(.84f, .71f, .47f);

        public static StigmaRoomView Build(GameUIView host)
        {
            if (host.StigmaView) { EnsureServiceControls(host.StigmaView, host); return host.StigmaView; }
            var root = Rect(host.transform, "Stigma_Management", new Rect(0, 0, 1280, 720));
            Image(root, Background, true);
            var view = root.gameObject.AddComponent<StigmaRoomView>();
            host.StigmaView = view;
            view.Art = host.Art;
            Font font = host.FontOverride ? host.FontOverride : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Label(root, "Title", new Rect(40, 24, 1000, 45), "성흔 관리 · SIGIL MANAGEMENT", font, 27, Ink);
            view.PlayerButton = Button(root, "Player_Equipment", new Rect(40, 88, 590, 76), "플레이어\n미장착", font, out view.PlayerText);
            view.RavenButton = Button(root, "Raven_Equipment", new Rect(650, 88, 590, 76), "까마귀\n미장착", font, out view.RavenText);
            view.CollectionText = Label(root, "Collection", new Rect(40, 182, 460, 30), "획득한 성흔 0종", font, 16, Muted);
            view.PreviousButton = Button(root, "Previous_Page", new Rect(510, 180, 48, 30), "<", font, out _);
            view.NextButton = Button(root, "Next_Page", new Rect(570, 180, 48, 30), ">", font, out _);
            Vector2 center = new Vector2(330, 406);
            const float radius = 166;
            for (int i = 0; i < 64; i++)
            {
                float a = i * Mathf.PI / 32, b = (i + 1) * Mathf.PI / 32;
                Vector2 from = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Vector2 to = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                var line = Rect(root, "Ring_" + i, new Rect(from.x, from.y, Vector2.Distance(from, to), 1.5f));
                line.pivot = new Vector2(0, .5f);
                line.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                Image(line, Border);
            }
            view.Slots = new StigmaSlotView[6];
            for (int i = 0; i < 6; i++)
            {
                float a = (-120 + i * 60) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                var border = Rect(root, "Acquired_Slot_" + i, new Rect(point.x - 37, point.y - 37, 74, 74));
                var frame = Image(border, Border, true);
                var fill = Rect(border, "Fill", new Rect(2, 2, 70, 70)); Image(fill, Panel);
                var image = Image(Rect(border, "Icon", new Rect(12, 12, 50, 50)), Color.white); image.preserveAspect = true;
                var button = border.gameObject.AddComponent<Button>(); button.targetGraphic = frame;
                view.Slots[i] = new StigmaSlotView {
                    Button = button, Frame = frame, Icon = image,
                    Name = Label(root, "Slot_Name_" + i, new Rect(point.x - 80, point.y + 43, 160, 27), "빈 칸", font, 16, Ink, TextAnchor.MiddleCenter),
                    Equipped = Label(root, "Slot_Owner_" + i, new Rect(point.x - 80, point.y + 70, 160, 22), "", font, 13, Gold, TextAnchor.MiddleCenter)
                };
            }
            Label(root, "Combination_Caption", new Rect(255, 359, 150, 24), "현재 조합", font, 13, Muted, TextAnchor.MiddleCenter);
            view.CurrentCombination = Label(root, "Combination", new Rect(240, 387, 180, 36), "성흔 미완성", font, 23, Gold, TextAnchor.MiddleCenter);
            view.CurrentSkill = Label(root, "Link_Skill", new Rect(235, 428, 190, 64), "", font, 14, Muted, TextAnchor.UpperCenter);

            var detail = Rect(root, "Selected_Stigma", new Rect(670, 198, 570, 424)); Image(detail, Panel);
            view.DetailIcon = Image(Rect(detail, "Icon", new Rect(24, 24, 72, 72)), Color.white); view.DetailIcon.preserveAspect = true;
            view.DetailName = Label(detail, "Name", new Rect(118, 24, 426, 40), "획득한 성흔 없음", font, 27, Ink);
            view.DetailLevel = Label(detail, "Level", new Rect(118, 69, 426, 60), "", font, 16, Muted);
            view.DetailEffect = Label(detail, "Effect", new Rect(24, 143, 522, 94), "", font, 17, Ink);
            view.DetailPreview = Label(detail, "Preview", new Rect(24, 247, 522, 88), "", font, 16, Muted);
            view.EquipButton = Button(detail, "Equip", new Rect(24, 352, 522, 48), "장착 정보 보기", font, out view.ActionText);
            view.StatusText = Label(root, "Phase_Status", new Rect(40, 650, 850, 42), "", font, 14, Muted);
            view.CloseButton = Button(root, "Close", new Rect(950, 646, 290, 48), "돌아가기 · ESC", font, out _);
            root.gameObject.SetActive(false);
            EnsureServiceControls(view, host);
            return view;
        }

        static void EnsureServiceControls(StigmaRoomView view, GameUIView host)
        {
            Font font = host.FontOverride ? host.FontOverride : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!view.OfferButton)
                view.OfferButton = Button(view.transform, "Service_Offer", new Rect(670, 165, 570, 30), "제단 선택지 · 다음 후보", font, out view.OfferText);
            var equipRect = view.EquipButton.GetComponent<RectTransform>();
            equipRect.sizeDelta = new Vector2(250, 48);
            view.ActionText.rectTransform.sizeDelta = new Vector2(222, 36);
            if (!view.UpgradeButton)
                view.UpgradeButton = Button(equipRect.parent, "Upgrade_Equipped", new Rect(292, 352, 254, 48), "장착 성흔 강화", font, out view.UpgradeText);
            view.ActionText.fontSize = view.UpgradeText.fontSize = 16;
            view.OfferText.fontSize = 15;
            view.OfferButton.gameObject.SetActive(false);
            view.UpgradeButton.gameObject.SetActive(false);
            EnsureDetailScroll(view);
            EnsureOfferCards(view, font);
        }

        static void EnsureOfferCards(StigmaRoomView view, Font font)
        {
            if (view.OffersPanel) return;
            var panel = Rect(view.transform, "Stigma_Card_Offers", new Rect(40, 210, 600, 424));
            Image(panel, Background, true);
            view.OffersPanel = panel.gameObject;
            view.OffersTitle = Label(panel, "Title", new Rect(10, 8, 580, 30), "3장 중 1장 선택 · 카드를 눌러 효과 확인", font, 16, Ink);
            view.OfferCards = new StigmaOfferCardView[3];
            for (int i = 0; i < view.OfferCards.Length; i++)
            {
                var card = Rect(panel, "Card_" + i, new Rect(10 + i * 198, 48, 184, 346));
                var frame = Image(card, Border, true);
                Image(Rect(card, "Fill", new Rect(2, 2, 180, 342)), Panel);
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = frame;
                var icon = Image(Rect(card, "Icon", new Rect(60, 48, 64, 64)), Color.white); icon.preserveAspect = true;
                view.OfferCards[i] = new StigmaOfferCardView {
                    Button = button, Frame = frame, Icon = icon,
                    Owner = Label(card, "Owner", new Rect(12, 12, 160, 24), "플레이어용", font, 14, Gold, TextAnchor.MiddleCenter),
                    Name = Label(card, "Name", new Rect(12, 122, 160, 30), "성흔", font, 20, Ink, TextAnchor.MiddleCenter),
                    Effect = Label(card, "Effect", new Rect(12, 162, 160, 150), "", font, 14, Muted)
                };
                Label(card, "Select_Hint", new Rect(12, 318, 160, 20), "선택 · 효과 보기", font, 13, Ink, TextAnchor.MiddleCenter);
            }
            Label(panel, "Hint", new Rect(10, 400, 580, 22), "이미 가진 성흔은 제외 · 이 방에서 총 1장 획득", font, 13, Muted);
            panel.gameObject.SetActive(false);
        }

        static void EnsureDetailScroll(StigmaRoomView view)
        {
            var detail = view.DetailIcon.transform.parent;
            if (!view.DetailScroll)
            {
                var viewport = Rect(detail, "Effect_Details_Scroll", new Rect(24, 112, 522, 216));
                Image(viewport, Color.clear, true);
                viewport.gameObject.AddComponent<RectMask2D>();
                view.DetailScroll = viewport.gameObject.AddComponent<ScrollRect>();
                view.DetailScroll.viewport = viewport;
                view.DetailEffect.transform.SetParent(viewport, false);
                view.DetailScroll.content = view.DetailEffect.rectTransform;
            }
            view.DetailScroll.horizontal = false;
            view.DetailScroll.vertical = true;
            view.DetailScroll.movementType = ScrollRect.MovementType.Clamped;
            view.DetailScroll.scrollSensitivity = 25;
            SetBox(view.DetailIcon.rectTransform, new Rect(24, 18, 64, 64));
            SetBox(view.DetailName.rectTransform, new Rect(108, 18, 438, 36));
            SetBox(view.DetailLevel.rectTransform, new Rect(108, 57, 438, 48));
            view.DetailLevel.fontSize = 15;
            SetBox(view.DetailEffect.rectTransform, new Rect(0, 0, 510, 216));
            view.DetailEffect.fontSize = 16;
            view.DetailEffect.supportRichText = true;
            view.DetailEffect.verticalOverflow = VerticalWrapMode.Overflow;
            SetBox(view.DetailPreview.rectTransform, new Rect(24, 330, 522, 20));
            view.DetailPreview.fontSize = 12;
        }

        static void SetBox(RectTransform rt, Rect box)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(box.x, -box.y);
            rt.sizeDelta = box.size;
        }

        static RectTransform Rect(Transform parent, string name, Rect box)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(box.x, -box.y); rt.sizeDelta = box.size;
            return rt;
        }
        static Image Image(RectTransform rt, Color color, bool raycast = false)
        { var image = rt.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image; }
        static Text Label(Transform parent, string name, Rect box, string text, Font font, int size, Color color, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var label = Rect(parent, name, box).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.color = color; label.text = text; label.alignment = alignment;
            label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        static Button Button(Transform parent, string name, Rect box, string text, Font font, out Text label)
        {
            var rt = Rect(parent, name, box); var image = Image(rt, Panel, true);
            var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            label = Label(rt, "Label", new Rect(14, 6, box.width - 28, box.height - 12), text, font, 18, Ink, TextAnchor.MiddleLeft);
            return button;
        }
    }
}
