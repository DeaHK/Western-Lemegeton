using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WesternLemegeton.Passives;
using WesternLemegeton.Stigmas;

namespace WesternLemegeton
{
    // Small builders shared by the journey screens.
    // Every coordinate is a pixel position in the 1920 x 1080 mockups, measured from the top-left corner.
    internal static class BetaUi
    {
        public static readonly Color Ink = new Color32(53,41,42,255);
        public static readonly Color Cream = new Color32(245,224,178,255);
        public static readonly Color Muted = new Color32(204,175,148,255);
        public static readonly Color Gold = new Color32(247,191,77,255);
        public static readonly Color Night = new Color32(37,30,34,255);
        public static readonly Color Umber = new Color32(96,72,54,255);
        static Font body, display;

        // Korean interface font, then a serif for the western style numerals and captions.
        public static Font Body => body ? body : body = Font.CreateDynamicFontFromOSFont(
            new[] { "Pretendard", "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 24);
        public static Font Display => display ? display : display = Font.CreateDynamicFontFromOSFont(
            new[] { "Georgia", "Times New Roman", "Malgun Gothic" }, 24);

        public static Sprite Art(string name) => Resources.Load<Sprite>("WesternBeta/" + name);

        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        public static Image Picture(Transform parent, string name, float x, float y, float w, float h, Sprite sprite)
        {
            var image = Box(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        public static Image Block(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var image = Picture(parent, name, x, y, w, h, null);
            image.color = color;
            return image;
        }

        public static Text Label(Transform parent, string name, float x, float y, float w, float h, int size, Color color,
            bool bold = false, TextAnchor align = TextAnchor.MiddleLeft, bool serif = false)
        {
            var text = Box(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = serif ? Display : Body;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            // Long names shrink a little instead of being cut off.
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = size;
            text.resizeTextMinSize = Mathf.Max(12, size * 2 / 3);
            return text;
        }

        public static Button Press(Image image, UnityAction action)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            if (action != null) button.onClick.AddListener(action);
            return button;
        }

        // An invisible clickable area laid over artwork that is drawn separately.
        public static Button Hit(Transform parent, string name, float x, float y, float w, float h, UnityAction action)
        {
            var area = Picture(parent, name, x, y, w, h, null);
            area.color = Color.clear;
            return Press(area, action);
        }

        // A straight bar from one point to another, used for map connections.
        public static Image Line(Transform parent, string name, Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 delta = to - from;
            var line = Block(parent, name, from.x, from.y, delta.magnitude, thickness, color);
            line.rectTransform.pivot = new Vector2(0, .5f);
            line.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return line;
        }

        public static string Signed(Dungeon g, string stat)
        {
            float raw = g.PassiveStats.GetRawBonus(stat), percent = g.PassiveStats.GetPercentBonus(stat);
            if (raw == 0) return (percent >= 0 ? "+" : "") + percent.ToString("0.##") + (percent == 0 ? "" : "%");
            string flat = (raw >= 0 ? "+" : "") + raw.ToString("0.##");
            return percent == 0 ? flat : flat + " / " + (percent >= 0 ? "+" : "") + percent.ToString("0.##") + "%";
        }

        static readonly string[] StigmaFamilies = { "ros", "ardor", "humus" };
        static readonly string[] StigmaDiscs = { "Stigma/RosNode", "Stigma/ArdorNode", "Stigma/InactiveNode" };
        static readonly string[] StigmaMarks = { "Stigma/RosMark", "Stigma/ArdorMark", "Journey/SproutIcon" };

        // 0 ros, 1 ardor, 2 humus, -1 unknown. An upgraded stigma belongs to the family it grew from.
        public static int StigmaFamily(StigmaDefinitionSO definition)
        {
            if (!definition) return -1;
            while (definition.BaseDefinition) definition = definition.BaseDefinition;
            return System.Array.IndexOf(StigmaFamilies, definition.Id);
        }

        // Draws a stigma as the beta medallion (coloured disc + element mark) inside an existing icon image,
        // so it keeps that image's place and size. Pass null to return the image to ordinary icon use.
        // Returns false when the stigma has no beta art; the caller then falls back to its old icon.
        public static bool StigmaBadge(Image icon, StigmaDefinitionSO definition)
        {
            if (!icon) return false;
            var holder = icon.transform.Find("Stigma mark");
            int family = StigmaFamily(definition);
            if (family < 0)
            {
                if (holder) { holder.gameObject.SetActive(false); icon.color = Color.white; }
                return false;
            }
            Image mark;
            if (holder) mark = holder.GetComponent<Image>();
            else
            {
                mark = new GameObject("Stigma mark", typeof(RectTransform)).AddComponent<Image>();
                mark.rectTransform.SetParent(icon.transform, false);
                // The mark covers the middle 36% of the disc and is centred on it.
                mark.rectTransform.anchorMin = new Vector2(.32f, .32f);
                mark.rectTransform.anchorMax = new Vector2(.68f, .68f);
                mark.rectTransform.offsetMin = mark.rectTransform.offsetMax = Vector2.zero;
                mark.preserveAspect = true;
                mark.raycastTarget = false;
            }
            mark.gameObject.SetActive(true);
            mark.sprite = Art(StigmaMarks[family]);
            mark.color = family == 0 ? new Color32(190,232,226,255) : Cream;
            icon.sprite = Art(StigmaDiscs[family]);
            // There is no humus disc yet, so the blank disc is tinted green (same as the stigma screen).
            icon.color = family == 2 ? new Color(.62f, .86f, .52f) : Color.white;
            icon.enabled = true;
            return true;
        }

        public static string StageName(StageType type) => type switch
        {
            StageType.Basic => "황야", StageType.Boss => "보스", StageType.Sigil => "성흔",
            StageType.Shop => "상점", StageType.Event => "이벤트", _ => "카드"
        };
    }

    // The frame both journey screens share: header, map board, right-hand status panel and footer.
    // The stage select map and the room map only place their own nodes on Graph and fill in the texts.
    internal sealed class BetaJourneyShell
    {
        static readonly string[] Families = { "ardor", "humus", "ros" };
        static readonly string[] FamilyNames = { "아르도르", "우무스", "로스" };
        static readonly string[] FamilyIcons = { "Journey/FlameIcon", "Journey/SproutIcon", "Journey/WaterIcon" };
        static readonly Color[] FamilyColors = { new Color32(197,78,48,255), new Color32(58,140,62,255), new Color32(38,150,160,255) };

        sealed class StigmaCard
        {
            public RectTransform Rect;
            public Image[] Border = new Image[4];
            public Image Icon;
            public Text Name, State;
        }

        readonly RectTransform frame;
        public readonly RectTransform Root, Graph;
        public readonly Text Subtitle, ChapterNumber, ChapterTotal, ChapterName, Currency, PlateKicker, PlateName,
            Kicker, Title, Sub, BoxKicker, BoxTitle, BoxBody, BoxFooter, CounterLabelA, CounterValueA, CounterLabelB, CounterValueB,
            HealthValue, StatsA, StatsB, ActionLabel, LegendTitle, Hint;
        public readonly Image HealthFill, ActionImage;
        public readonly Button Action, Close;
        readonly StigmaCard[] cards = new StigmaCard[3];
        readonly int[] cardOrder = { 0, 1, 2 };
        readonly List<GameObject> legend = new List<GameObject>();

        public BetaJourneyShell(RectTransform frame, UnityAction onAction, UnityAction onClose)
        {
            this.frame = frame;
            var backdrop = new GameObject("Journey backdrop", typeof(RectTransform)).GetComponent<RectTransform>();
            backdrop.SetParent(frame, false);
            backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            var shade = backdrop.gameObject.AddComponent<Image>();
            shade.color = BetaUi.Night;
            shade.raycastTarget = true;

            Root = BetaUi.Box(frame, "Journey", 0, 0, 1920, 1080);
            Root.anchorMin = Root.anchorMax = Root.pivot = new Vector2(.5f, .5f);

            // Header
            BetaUi.Label(Root, "Brand", 36, 18, 420, 30, 20, BetaUi.Gold, true, TextAnchor.MiddleLeft, true).text = "WEST OF THE DAMNED";
            BetaUi.Label(Root, "Screen title", 34, 44, 330, 70, 55, BetaUi.Cream, true).text = "황야의 여정";
            Subtitle = BetaUi.Label(Root, "Subtitle", 377, 78, 940, 34, 24, BetaUi.Muted);
            BetaUi.Picture(Root, "Chapter badge", 1390, 35, 274, 73, BetaUi.Art("Journey/ChapterBadge"));
            BetaUi.Label(Root, "Chapter caption", 1410, 41, 130, 22, 14, BetaUi.Gold, true).text = "CHAPTER";
            ChapterNumber = BetaUi.Label(Root, "Chapter number", 1408, 60, 62, 42, 34, BetaUi.Gold, true, TextAnchor.MiddleLeft, true);
            ChapterTotal = BetaUi.Label(Root, "Chapter total", 1474, 68, 70, 32, 22, BetaUi.Muted, true);
            ChapterName = BetaUi.Label(Root, "Chapter name", 1558, 54, 98, 44, 27, BetaUi.Cream, true);
            BetaUi.Picture(Root, "Currency badge", 1682, 35, 204, 73, BetaUi.Art("Journey/CurrencyBadge"));
            BetaUi.Label(Root, "Currency caption", 1702, 41, 170, 24, 17, BetaUi.Muted).text = "보유 구리동전";
            Currency = BetaUi.Label(Root, "Currency", 1702, 62, 170, 40, 28, BetaUi.Cream, true);
            BetaUi.Block(Root, "Header rule", 34, 136, 1852, 2, new Color32(110,86,74,255));

            // Map board. Graph sits above the board art and below the name plate.
            BetaUi.Picture(Root, "Map board", 28, 164, 1352, 812, BetaUi.Art("Journey/MapBoard"));
            Graph = BetaUi.Box(Root, "Graph", 0, 0, 1920, 1080);
            BetaUi.Picture(Root, "Plate", 56, 192, 270, 88, BetaUi.Art("Journey/TerritoryPlate"));
            PlateKicker = BetaUi.Label(Root, "Plate caption", 75, 199, 236, 30, 20, BetaUi.Gold, true, TextAnchor.MiddleLeft, true);
            PlateName = BetaUi.Label(Root, "Plate name", 74, 227, 238, 44, 30, BetaUi.Cream, true);

            // Status panel
            BetaUi.Picture(Root, "Status panel", 1404, 164, 488, 812, BetaUi.Art("Journey/JourneyPanel")).raycastTarget = true;
            Kicker = BetaUi.Label(Root, "Kicker", 1432, 186, 432, 30, 19, BetaUi.Gold, true);
            Title = BetaUi.Label(Root, "Title", 1430, 216, 434, 60, 44, BetaUi.Cream, true);
            Sub = BetaUi.Label(Root, "Sub", 1432, 278, 432, 34, 24, BetaUi.Muted);
            BetaUi.Picture(Root, "Detail box", 1430, 322, 436, 211, BetaUi.Art("Journey/DescriptionPanel"));
            BoxKicker = BetaUi.Label(Root, "Detail caption", 1452, 338, 392, 30, 19, BetaUi.Gold, true);
            BoxTitle = BetaUi.Label(Root, "Detail title", 1452, 370, 392, 46, 28, BetaUi.Cream, true);
            BoxBody = BetaUi.Label(Root, "Detail body", 1452, 420, 392, 68, 21, BetaUi.Muted, false, TextAnchor.UpperLeft);
            BoxBody.lineSpacing = 1.15f;
            BoxFooter = BetaUi.Label(Root, "Detail footer", 1452, 492, 392, 30, 18, BetaUi.Cream, true);
            CounterLabelA = BetaUi.Label(Root, "Counter A", 1432, 548, 84, 34, 19, BetaUi.Muted);
            CounterValueA = BetaUi.Label(Root, "Counter A value", 1517, 544, 170, 42, 27, BetaUi.Cream, true);
            CounterLabelB = BetaUi.Label(Root, "Counter B", 1706, 548, 66, 34, 19, BetaUi.Muted);
            CounterValueB = BetaUi.Label(Root, "Counter B value", 1774, 544, 92, 42, 27, BetaUi.Cream, true);
            BetaUi.Picture(Root, "Heart", 1433, 599, 24, 24, BetaUi.Art("Journey/HeartIcon"));
            BetaUi.Label(Root, "Health caption", 1469, 594, 120, 34, 20, BetaUi.Cream, true).text = "체력";
            HealthValue = BetaUi.Label(Root, "Health", 1624, 594, 240, 34, 22, BetaUi.Cream, true, TextAnchor.MiddleRight);
            BetaUi.Picture(Root, "Health track", 1432, 632, 432, 24, BetaUi.Art("Journey/HealthTrack"));
            HealthFill = BetaUi.Picture(Root, "Health fill", 1436, 636, 424, 16, BetaUi.Art("Journey/HealthFill"));
            HealthFill.type = Image.Type.Filled;
            HealthFill.fillMethod = Image.FillMethod.Horizontal;
            HealthFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            for (int i = 0; i < 3; i++)
            {
                float x = 1432 + i * 149;
                var card = new StigmaCard();
                card.Rect = BetaUi.Picture(Root, "Stigma card " + i, x, 683, 134, 119, BetaUi.Art("Journey/RosCard")).rectTransform;
                // A coloured frame is drawn only while that stigma is equipped.
                card.Border[0] = BetaUi.Block(card.Rect, "Top", 0, 0, 134, 2, Color.white);
                card.Border[1] = BetaUi.Block(card.Rect, "Bottom", 0, 117, 134, 2, Color.white);
                card.Border[2] = BetaUi.Block(card.Rect, "Left", 0, 0, 2, 119, Color.white);
                card.Border[3] = BetaUi.Block(card.Rect, "Right", 132, 0, 2, 119, Color.white);
                card.Icon = BetaUi.Picture(card.Rect, "Icon", 49, 13, 36, 36, null);
                card.Icon.preserveAspect = true;
                card.Name = BetaUi.Label(card.Rect, "Name", 4, 52, 126, 28, 18, BetaUi.Cream, true, TextAnchor.MiddleCenter);
                card.State = BetaUi.Label(card.Rect, "State", 4, 79, 126, 26, 17, BetaUi.Muted, false, TextAnchor.MiddleCenter);
                cards[i] = card;
            }
            StatsA = BetaUi.Label(Root, "Stats A", 1432, 814, 432, 30, 20, BetaUi.Muted);
            StatsB = BetaUi.Label(Root, "Stats B", 1432, 845, 432, 30, 20, BetaUi.Muted);

            ActionImage = BetaUi.Picture(Root, "Action", 1430, 896, 438, 70, BetaUi.Art("Journey/TravelButton"));
            Action = BetaUi.Press(ActionImage, onAction);
            ActionLabel = BetaUi.Label(ActionImage.transform, "Label", 20, 0, 398, 62, 30, BetaUi.Ink, true, TextAnchor.MiddleCenter);
            BetaUi.Picture(ActionImage.transform, "Arrow", 388, 17, 28, 28, BetaUi.Art("Journey/NextArrow"));

            // Footer
            LegendTitle = BetaUi.Label(Root, "Legend title", 28, 1002, 124, 36, 20, BetaUi.Cream, true);
            Hint = BetaUi.Label(Root, "Hint", 860, 1002, 740, 36, 20, BetaUi.Muted);
            BetaUi.Picture(Root, "Map key", 1633, 1000, 49, 41, BetaUi.Art("Journey/MapKey"));
            BetaUi.Label(Root, "Escape key", 1694, 1003, 76, 36, 20, BetaUi.Muted, true).text = "/ ESC";
            BetaUi.Label(Root, "Close caption", 1806, 1001, 92, 38, 22, BetaUi.Cream, true).text = "×  닫기";
            Close = BetaUi.Hit(Root, "Close", 1622, 994, 280, 52, onClose);
            Fit();
        }

        // The whole screen is laid out at 1920 x 1080 and scaled to whatever the panel really is.
        public void Fit()
        {
            var size = frame.rect.size;
            if (size.x > 0 && size.y > 0) Root.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
        }

        public void SetLegend(string title, params (Color color, string name)[] entries)
        {
            foreach (var item in legend) Object.Destroy(item);
            legend.Clear();
            LegendTitle.text = title;
            float x = 156;
            foreach (var entry in entries)
            {
                var swatch = BetaUi.Block(Root, "Legend swatch", x, 1013, 15, 15, entry.color);
                var label = BetaUi.Label(Root, "Legend label", x + 25, 1002, 320, 36, 20, BetaUi.Muted);
                label.resizeTextForBestFit = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.text = entry.name;
                legend.Add(swatch.gameObject);
                legend.Add(label.gameObject);
                x += 25 + label.preferredWidth + 84;
            }
            // The hint follows the legend, but never crowds the M / ESC keys on the right.
            float hintX = Mathf.Max(x - 20, 880);
            Hint.rectTransform.anchoredPosition = new Vector2(hintX, -1002);
            Hint.rectTransform.sizeDelta = new Vector2(1610 - hintX, 36);
        }

        public void SetAction(string label, bool available)
        {
            ActionLabel.text = label;
            Action.interactable = available;
            ActionImage.color = available ? Color.white : new Color(.62f, .58f, .54f, .85f);
        }

        static int Family(StigmaDefinitionSO definition)
        {
            if (!definition) return -1;
            while (definition.BaseDefinition) definition = definition.BaseDefinition;
            return System.Array.IndexOf(Families, definition.Id);
        }

        // Currency, health, the three stigma cards and the build lines. These read the same run on both screens.
        public void ShowRun(Dungeon g)
        {
            bool run = g && g.Hero;
            Currency.text = run ? g.Wallet.Copper.ToString("N0") : "0";
            float hp = run ? g.Hero.Hp : 0, max = run ? Mathf.Max(1, g.Hero.MaxHealth) : 1;
            HealthValue.text = run ? $"{Mathf.CeilToInt(hp)} / {max:0}" : "- / -";
            HealthFill.fillAmount = Mathf.Clamp01(hp / max);

            // 0 = not owned, 1 = owned but not equipped, 2+ = equipped (higher sorts first).
            var rank = new int[3]; var text = new string[3];
            for (int i = 0; i < 3; i++) text[i] = "미보유";
            if (run && g.Stigmas)
                for (int owner = 0; owner < 2; owner++)
                {
                    var who = (StigmaOwner)owner;
                    foreach (var owned in g.Stigmas.Loadout.GetAcquired(who))
                    {
                        int kind = Family(owned);
                        if (kind >= 0 && rank[kind] == 0) { rank[kind] = 1; text[kind] = "보유 · 미장착"; }
                    }
                    var equipped = g.Stigmas.Loadout.Get(who);
                    int family = Family(equipped.Definition);
                    if (family < 0 || rank[family] >= 2 + equipped.Level) continue;
                    rank[family] = 2 + equipped.Level;
                    text[family] = (owner == 0 ? "세브" : "코빈") + " · " + equipped.Level + "단계";
                }
            // Equipped stigmas come first, as in the mockup. The sort is stable so ties keep their usual order.
            for (int i = 0; i < 3; i++) cardOrder[i] = i;
            for (int i = 1; i < 3; i++)
                for (int j = i; j > 0 && rank[cardOrder[j]] > rank[cardOrder[j-1]]; j--)
                    (cardOrder[j], cardOrder[j-1]) = (cardOrder[j-1], cardOrder[j]);
            for (int slot = 0; slot < 3; slot++)
            {
                int kind = cardOrder[slot];
                var card = cards[slot];
                bool equippedNow = rank[kind] >= 2, ownedNow = rank[kind] >= 1;
                foreach (var edge in card.Border) { edge.enabled = equippedNow; edge.color = FamilyColors[kind]; }
                card.Icon.sprite = BetaUi.Art(FamilyIcons[kind]);
                card.Icon.color = equippedNow ? BetaUi.Cream : ownedNow ? BetaUi.Muted : new Color(.45f, .39f, .37f);
                card.Name.text = FamilyNames[kind];
                card.Name.color = ownedNow ? BetaUi.Cream : new Color(.50f, .44f, .42f);
                card.State.text = text[kind];
                card.State.color = equippedNow ? BetaUi.Cream : ownedNow ? BetaUi.Muted : new Color(.50f, .44f, .42f);
            }

            if (run)
            {
                StatsA.text = $"공격력 {BetaUi.Signed(g, WesternPassiveStatKeys.PlayerAttack)}   ·   최대 체력 {BetaUi.Signed(g, WesternPassiveStatKeys.MaxHealth)}   ·   이동속도 {BetaUi.Signed(g, WesternPassiveStatKeys.MoveSpeed)}";
                StatsB.text = $"인장 {g.Passives.Count}장   ·   까마귀 공격 {BetaUi.Signed(g, WesternPassiveStatKeys.RavenAttack)}";
            }
            else StatsA.text = StatsB.text = "";
        }
    }
}
