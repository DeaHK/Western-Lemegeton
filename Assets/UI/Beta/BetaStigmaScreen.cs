using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WesternLemegeton.Stigmas;

namespace WesternLemegeton
{
    // Coordinates are pixels in References/Stigma/StigmaManagement.png (1920 x 1080).
    internal sealed class BetaStigmaScreen
    {
        static readonly Color Ink = new Color32(53, 41, 42, 255);
        static readonly Color Cream = new Color32(245, 224, 178, 255);
        static readonly Color Muted = new Color32(204, 175, 148, 255);
        static readonly Color Gold = new Color32(247, 191, 77, 255);
        static readonly Color FadedInk = new Color32(113, 85, 63, 255);
        static readonly string[] Families = { "ros", "ardor", "humus" };
        static readonly string[] Latin = { "rōs", "ardor", "Humus" };
        static readonly string[] Names = { "세브", "코빈" };
        static readonly Color[] Accent = { new Color32(135,202,206,255), new Color32(245,149,98,255), new Color32(155,191,127,255) };
        static readonly Color[] LineColor = { new Color32(20,122,130,255), new Color32(178,76,51,255), new Color32(81,122,69,255) };
        static readonly Vector2[] Centers = {
            new Vector2(478,352), new Vector2(298,570), new Vector2(478,788),
            new Vector2(830,352), new Vector2(1010,570), new Vector2(830,788)
        };
        static readonly int[,] Edges = { {0,1},{1,2},{3,4},{4,5},{0,5},{3,2},{0,3},{1,4},{2,5} };

        readonly StigmaRoomView host;
        readonly RectTransform frame, root;
        readonly Font font;
        readonly Sprite[] symbols = new Sprite[3], discs = new Sprite[3], numerals = new Sprite[3];
        readonly Sprite inactive, dotted, link;
        readonly Sprite[] tabNormal=new Sprite[3], tabSelected=new Sprite[3];
        readonly Image[] goldThreads=new Image[3];
        readonly Image vaporPlaque;
        readonly Text nextValue;
        static readonly System.Text.RegularExpressions.Regex FirstStat = new System.Text.RegularExpressions.Regex(
            @"^(.+?)\s*([+\-]?\d+(?:\.\d+)?(?:%|초|회|개|m)?)\s*(?:\.|,|$)");
        readonly Image[] nodes = new Image[6], marks = new Image[6], numbers = new Image[6], rings = new Image[6], lines = new Image[9];
        readonly Text[] statuses = new Text[6], loadouts = new Text[2];
        readonly Image[] tabs = new Image[3];
        readonly Text[] tabNotes = new Text[3];
        readonly Text money, signature, pairing, linkCount, selection, title, latin, tier, description, transition, nextEffect,
            comboLevel, comboName, comboPair, comboDescription, cost, confirmText;
        readonly Button confirm;
        readonly List<GameObject> popupRows = new List<GameObject>();
        readonly RectTransform popup, popupContent, popupViewport;
        readonly Text popupTitle;
        readonly Image popupPanel, popupClose, popupRule;
        StigmaManager manager;
        StigmaDefinitionSO preview;
        bool offers, upgrade;
        int inspectTier;

        public BetaStigmaScreen(StigmaRoomView host, UnityAction openInventory)
        {
            this.host = host;
            frame = (RectTransform)host.transform;
            var legacy = new GameObject("Legacy controls",typeof(RectTransform)).GetComponent<RectTransform>();
            legacy.SetParent(frame,false);
            legacy.anchorMin=Vector2.zero; legacy.anchorMax=Vector2.one; legacy.offsetMin=legacy.offsetMax=Vector2.zero;
            for (int i=frame.childCount-2; i>=0; i--) frame.GetChild(i).SetParent(legacy,false);
            legacy.gameObject.SetActive(false);
            frame.anchorMin=Vector2.zero; frame.anchorMax=Vector2.one; frame.offsetMin=frame.offsetMax=Vector2.zero;
            frame.GetComponent<Image>().color = new Color32(37,30,34,255);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Pretendard", "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" },24);
            root = Box(frame,"Stigma management",0,0,1920,1080);
            root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);
            Picture(root,"Original chrome",0,0,1920,1080,Art("Stigma/ManagementChrome")).raycastTarget=true;
            inactive=Art("Stigma/InactiveNode"); dotted=Art("Stigma/DottedLine"); link=Art("Stigma/LinkLine");
            discs[0]=Art("Stigma/RosNode"); discs[1]=Art("Stigma/ArdorNode"); discs[2]=inactive;
            symbols[0]=Art("Stigma/RosMark"); symbols[1]=Art("Stigma/ArdorMark"); symbols[2]=Art("Journey/SproutIcon");
            var artNames=new[] { "Ros","Ardor","Humus" };
            for(int i=0;i<3;i++)
            {
                tabNormal[i]=Art("Stigma/"+artNames[i]+"Tab");
                tabSelected[i]=Art("Stigma/"+artNames[i]+"TabSelected");
            }
            for (int i=0;i<3;i++) numerals[i]=Art("Stigma/Tier"+(i+1));
            Hit(root,"Open seals",1332,35,272,78,openInventory);
            Hit(root,"Help",1756,191,44,44,ShowHelp);
            Hit(root,"Close",1812,191,44,44,() => host.CloseButton.onClick.Invoke());
            Hit(root,"Close footer",1654,1000,178,40,() => host.CloseButton.onClick.Invoke());
            Label(root,"Balance caption",1688,43,190,24,17,Muted).text="보유 구리동전";
            money=Label(root,"Balance",1688,65,186,39,28,Gold,true);
            signature=Label(root,"Combination family",74,191,230,26,17,Cream,true);
            pairing=Label(root,"Combination",74,214,244,36,22,Cream,true);
            vaporPlaque=Picture(root,"Evaporation nameplate",56,188,264,68,Art("Stigma/VaporPlaque"));
            linkCount=Label(root,"Active links",78,903,320,38,22,Cream,true);

            for (int i=0;i<lines.Length;i++)
            {
                Vector2 a=Centers[Edges[i,0]], b=Centers[Edges[i,1]], delta=b-a;
                var line=Picture(root,"Connection "+i,a.x,a.y,delta.magnitude,5,null);
                line.rectTransform.pivot=new Vector2(0,.5f);
                line.rectTransform.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                lines[i]=line;
                if(i>=6)
                {
                    var stripe=Picture(line.transform,"Gold thread",0,2.5f,delta.magnitude,2,null);
                    stripe.color=Gold; goldThreads[i-6]=stripe;
                }
            }
            var selectedRing=Art("Stigma/SelectionRing");
            for (int i=0;i<6;i++)
            {
                int index=i; var p=Centers[i]; int owner=i/3;
                if (i%3==0)
                {
                    Label(root,Names[owner],p.x-110,191,230,37,26,Ink,true,TextAnchor.MiddleCenter).text=Names[owner];
                    loadouts[owner]=Label(root,"Equipment "+owner,p.x-150,228,305,35,21,Ink,true,TextAnchor.MiddleCenter);
                    Hit(root,"Select "+Names[owner],p.x-100,191,200,70,()=>SelectNode(index));
                }
                nodes[i]=Picture(root,"Node "+i,p.x-79,p.y-79,158,158,inactive);
                marks[i]=Picture(root,"Element "+i,p.x-21,p.y-41,42,45,null);
                numbers[i]=Picture(root,"Tier "+i,p.x-22,p.y+12,44,30,numerals[i%3]);
                rings[i]=Picture(root,"Selection "+i,p.x-79,p.y-79,158,158,selectedRing);
                statuses[i]=Label(root,"State "+i,p.x-145,p.y+83,290,30,18,Ink,true,TextAnchor.MiddleCenter);
                Hit(root,"Inspect node "+i,p.x-79,p.y-79,158,158,()=>SelectNode(index));
            }
            for(int i=0;i<3;i++)
            {
                int family=i, x=1332+i*180;
                tabs[i]=Picture(root,"Tab "+i,x,260,172,65,tabNormal[i]);
                tabNotes[i]=Label(root,"Tab equipment "+i,x+50,297,119,22,14,Muted);
                Hit(root,"Choose "+Families[i],x,260,172,65,()=>SelectFamily(family));
            }
            selection=Label(root,"Selected owner",1332,345,532,34,19,Gold,true);
            title=Label(root,"Selected name",1332,375,354,64,48,Cream,true);
            latin=Label(root,"Latin",1690,385,174,48,35,Accent[0],false,TextAnchor.MiddleRight);
            tier=Label(root,"Level",1333,436,532,38,22,Accent[0],true);
            description=Label(root,"Effect",1332,481,532,68,21,Muted,false,TextAnchor.UpperLeft);
            Label(root,"Upgrade label",1350,568,188,33,18,Muted,true).text="성흔 강화";
            transition=Label(root,"Upgrade tiers",1540,565,306,40,24,Cream,true,TextAnchor.MiddleRight);
            nextEffect=Label(root,"Next effect",1350,607,292,43,21,Cream,true);
            nextValue=Label(root,"Effect difference",1644,607,202,43,26,Gold,true,TextAnchor.MiddleRight);
            comboLevel=Label(root,"Link level",1352,686,495,29,17,Gold,true);
            comboName=Label(root,"Link name",1352,714,496,43,30,Cream,true);
            comboPair=Label(root,"Equipped pair",1352,759,496,29,20,Accent[0],true);
            comboDescription=Label(root,"Link effect",1352,786,497,31,18,Muted);
            cost=Label(root,"Price",1510,834,354,36,22,Gold,true,TextAnchor.MiddleRight);
            confirmText=Label(root,"Action",1332,893,322,64,26,Ink,true,TextAnchor.MiddleCenter);
            confirm=Hit(root,"Confirm",1332,893,322,64,Confirm);
            Hit(root,"Change stigma",1670,893,194,64,ShowChoices);

            popup=Box(root,"Stigma dialog",0,0,1920,1080);
            var shade=Picture(popup,"Dismiss",0,0,1920,1080,null);
            shade.color=new Color(0,0,0,.62f); ButtonFor(shade,()=>ClosePopup());
            // Drawn from flat blocks so it can be any height: outline, dark fill, gold top bar.
            popupPanel=Picture(popup,"Dialog",555,205,810,652,null); popupPanel.color=new Color32(110,86,74,255); popupPanel.raycastTarget=true;
            var fill=Picture(popupPanel.transform,"Fill",0,0,10,10,null); fill.color=new Color32(52,42,44,255);
            fill.rectTransform.anchorMin=Vector2.zero; fill.rectTransform.anchorMax=Vector2.one;
            fill.rectTransform.offsetMin=new Vector2(2,2); fill.rectTransform.offsetMax=new Vector2(-2,-2);
            var bar=Picture(popupPanel.transform,"Gold bar",0,0,10,7,null); bar.color=Gold;
            bar.rectTransform.anchorMin=new Vector2(0,1); bar.rectTransform.anchorMax=Vector2.one;
            bar.rectTransform.offsetMin=new Vector2(0,-7); bar.rectTransform.offsetMax=Vector2.zero;
            popupTitle=Label(popup,"Dialog title",600,235,620,54,32,Cream,true);
            popupClose=Picture(popup,"Close dialog",1280,238,44,44,Art("Stigma/CloseButton")); ButtonFor(popupClose,()=>ClosePopup());
            popupRule=Picture(popup,"Dialog rule",600,296,720,2,null); popupRule.color=new Color32(110,86,74,255);
            popupViewport=Box(popup,"Options viewport",599,313,720,503);
            popupViewport.gameObject.AddComponent<RectMask2D>();
            popupViewport.gameObject.AddComponent<Image>().color=Color.clear;
            var scroll=popupViewport.gameObject.AddComponent<ScrollRect>();
            popupContent=Box(popupViewport,"Options",0,0,720,503);
            scroll.viewport=popupViewport; scroll.content=popupContent; scroll.horizontal=false;
            scroll.movementType=ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=35;
            popup.gameObject.SetActive(false);
            Fit();
        }

        public void Fit()
        {
            var size=frame.rect.size;
            root.localScale=Vector3.one*Mathf.Min(size.x/1920f,size.y/1080f);
        }

        static int Family(StigmaDefinitionSO d)
        {
            if (!d) return -1;
            while (d.BaseDefinition) d=d.BaseDefinition;
            return System.Array.IndexOf(Families,d.Id);
        }

        public void Refresh(StigmaManager value,bool selectingOffer,string message)
        {
            manager=value; offers=selectingOffer;
            var state=manager.Loadout;
            var owner=host.SelectedOwner;
            var equipped=state.Get(owner);
            var selected=preview ? preview : manager.Database.Find(host.SelectedId);
            int family=Family(selected);
            var combo=state.Combination;
            int activeLinks=combo ? state.EffectiveLevel : 0;
            money.text=(Dungeon.I ? Dungeon.I.Wallet.Copper : 0).ToString();
            signature.text=combo ? (combo.Id=="vapor" ? "EVAPORATION" : combo.Id.Replace('_',' ').ToUpperInvariant()) : "STIGMA";
            pairing.text=combo ? $"{combo.First.DisplayName} · {combo.Second.DisplayName}  <color=#F5BD52>{combo.DisplayName}</color>" : "성흔 연계 대기";
            vaporPlaque.enabled=combo && combo.Id=="vapor";
            linkCount.text=$"연계 활성  {activeLinks} / 3";
            var a=state.Get(StigmaOwner.Player); var b=state.Get(StigmaOwner.Raven);
            for (int i=0;i<6;i++)
            {
                int column=i/3, level=i%3+1;
                var eq=column==0 ? a : b;
                int kind=Family(eq.Definition);
                bool lit=eq.IsEquipped && level<=eq.Level;
                bool focused=column==(int)owner && level==(inspectTier>0 ? inspectTier : Mathf.Max(1,eq.Level));
                if(level==1)
                {
                    loadouts[column].text=eq.IsEquipped ? $"{eq.Definition.DisplayName}  ·  {eq.Level}단계" : "미장착";
                    loadouts[column].color=kind>=0 ? LineColor[kind] : FadedInk;
                }
                nodes[i].sprite=lit && kind>=0 ? discs[kind] : inactive;
                nodes[i].color=lit && kind==2 ? new Color(.62f,.86f,.52f) : Color.white;
                marks[i].sprite=symbols[kind>=0 ? kind : (column==0 ? 0 : 1)];
                marks[i].color=lit ? (kind==0 ? new Color32(100,157,147,255) : Cream) : new Color(.50f,.39f,.24f,.32f);
                numbers[i].color=lit ? Cream : new Color(.34f,.27f,.2f,.48f);
                rings[i].enabled=focused;
                statuses[i].text=focused ? (lit && level==eq.Level && level<3 && manager.Room.IsOpen ? "선택됨 · 강화 가능" : "선택됨") :
                    lit ? level+"단계 활성" : "미활성 · 강화 대기";
                statuses[i].color=focused && kind>=0 ? LineColor[kind] : lit ? Ink : FadedInk;
            }
            for(int i=0;i<lines.Length;i++)
            {
                int from=Edges[i,0],to=Edges[i,1];
                var first=from<3 ? a : b; var second=to<3 ? a : b;
                bool lit=first.Level>=from%3+1 && second.Level>=to%3+1 && (from/3==to/3 || combo);
                int k=Family(first.Definition);
                var line=lines[i];
                line.sprite=lit ? (i>=6 ? link : null) : dotted;
                line.type=lit ? Image.Type.Simple : Image.Type.Tiled;
                if(i>=6) goldThreads[i-6].enabled=lit;
                line.color=!lit ? new Color(.44f,.33f,.26f,.42f) : i>=6 ? Color.white : k>=0 ? LineColor[k] : FadedInk;
                line.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,lit ? (i>=6 ? 7 : 5) : 2);
            }
            for(int i=0;i<3;i++)
            {
                tabs[i].sprite=family==i ? tabSelected[i] : tabNormal[i];
                string holder=Family(a.Definition)==i ? " · 세브 장착 중" : Family(b.Definition)==i ? " · 코빈 장착 중" : "";
                tabNotes[i].text=Latin[i]+holder;
            }
            int ownLevel=selected ? state.GetLevel(owner,selected.Id) : 0;
            int levelShown=inspectTier>0 && equipped.Definition==selected ? inspectTier : Mathf.Max(1,ownLevel);
            bool actual=selected && selected==equipped.Definition && !offers;
            int nextTier=Mathf.Min(3,Mathf.Max(1,ownLevel)+1);
            Color accent=family>=0 ? Accent[family] : Cream;
            selection.text="선택한 성흔  /  "+Names[(int)owner];
            title.text=selected ? selected.DisplayName : "미장착";
            latin.text=selected ? (family>=0 && !selected.IsEnhanced ? Latin[family] : selected.Id) : "";
            latin.color=tier.color=accent;
            tier.text=selected ? levelShown+"단계" : "";
            description.text=selected ? selected.Level(levelShown)?.Description : "성흔 변경에서 보유 성흔과 획득 후보를 확인하세요.";
            transition.text=ownLevel>=3 ? "3단계 · 최대" : $"{Mathf.Max(1,ownLevel)}단계  →  {nextTier}단계";
            string effect=selected ? selected.Level(nextTier)?.Description : "성흔 선택 대기";
            var beforeStat=FirstStat.Match(selected ? selected.Level(Mathf.Max(1,ownLevel)).Description : "");
            var afterStat=FirstStat.Match(effect ?? "");
            bool comparable=beforeStat.Success && afterStat.Success && beforeStat.Groups[1].Value.Trim()==afterStat.Groups[1].Value.Trim();
            nextEffect.rectTransform.sizeDelta=new Vector2(comparable ? 292 : 496,43);
            nextEffect.text=comparable ? beforeStat.Groups[1].Value.Trim() : effect;
            nextValue.text=comparable ? beforeStat.Groups[2].Value+"  →  "+afterStat.Groups[2].Value : "";
            comboLevel.text="연계 스킬  ·  "+(combo ? activeLinks+"단계 활성" : "미활성");
            comboName.text=combo ? combo.DisplayName : "연계 대기";
            comboPair.text=(a.IsEquipped ? a.Definition.DisplayName+" "+Roman(a.Level) : "세브 미장착")+" × "+
                (b.IsEquipped ? b.Definition.DisplayName+" "+Roman(b.Level) : "코빈 미장착");
            comboDescription.text=combo ? combo.SkillName+" · "+combo.Level(activeLinks)?.BonusDescription : "세브와 코빈 양쪽에 성흔을 장착하세요.";
            upgrade=actual;
            bool claim=offers && !preview;
            int price=0;
            if (!claim && selected)
            {
                if (actual && ownLevel>0 && ownLevel<3) price=manager.Database.Rules.UpgradeCosts[ownLevel-1];
                else if (!actual && equipped.IsEquipped) price=manager.Database.Rules.ReplacementCosts[equipped.Level-1];
            }
            cost.text=claim ? "무료" : actual && ownLevel>=3 ? "—" : $"구리동전 {price} / {money.text}";
            confirm.interactable=!preview && (actual ? host.UpgradeButton.interactable : host.EquipButton.interactable);
            confirmText.text=claim ? "성흔 획득" : actual ? (ownLevel>=3 ? "최대 단계" : "강화  ·  "+nextTier+"단계") : "성흔 장착";
            confirmText.color=confirm.interactable ? Ink : FadedInk;
            if (!string.IsNullOrEmpty(message)) description.text=message+"\n"+description.text;
        }

        static string Roman(int level) => level==3 ? "III" : level==2 ? "II" : "I";

        void SelectNode(int index)
        {
            preview=null; inspectTier=index%3+1;
            host.SelectEquippedOwner((StigmaOwner)(index/3));
        }

        void SelectFamily(int index)
        {
            if (!manager) return;
            var owner=host.SelectedOwner;
            var state=manager.Loadout;
            var candidates=manager.Room.GetOffers();
            if (!manager.Room.HasClaimedCard)
                for(int i=0;i<candidates.Count;i++)
                    if(Family(candidates[i].Definition)==index && (offers || !state.Get(owner).IsEquipped))
                    { ChooseOffer(i); return; }
            for(int pass=0;pass<2;pass++)
            {
                var target=pass==0 ? owner : (owner==StigmaOwner.Player ? StigmaOwner.Raven : StigmaOwner.Player);
                var list=state.GetAcquired(target);
                for(int i=list.Count-1;i>=0;i--)
                    if(Family(list[i])==index && !state.IsSuperseded(target,list[i])) { ChooseOwned(target,i); return; }
            }
            if (!manager.Room.HasClaimedCard)
                for(int i=0;i<candidates.Count;i++) if(Family(candidates[i].Definition)==index) { ChooseOffer(i); return; }
            inspectTier=0; preview=manager.Database.Find(Families[index]); host.Refresh();
        }

        void ChooseOwned(StigmaOwner owner,int index)
        {
            ResetSelection(); host.SelectOwner(owner);
            host.SetPage(index/StigmaRulesSO.VisibleSlots); host.SelectSlot(index%StigmaRulesSO.VisibleSlots);
        }
        void ChooseOffer(int index) { ResetSelection(); host.SelectOffer(index); }
        public void Confirm()
        {
            if (popup.gameObject.activeSelf || !confirm.interactable) return;
            inspectTier=0;
            if (upgrade) host.UpgradeButton.onClick.Invoke(); else host.EquipButton.onClick.Invoke();
        }
        public void ResetSelection() { inspectTier=0; preview=null; ClosePopup(); }
        public bool ClosePopup()
        {
            if (!popup.gameObject.activeSelf) return false;
            popup.gameObject.SetActive(false); return true;
        }

        void BeginPopup(string heading)
        {
            foreach(var obj in popupRows) { obj.SetActive(false); Object.Destroy(obj); }
            popupRows.Clear();
            popupTitle.text=heading; popupContent.anchoredPosition=Vector2.zero;
            popup.gameObject.SetActive(true); popup.SetAsLastSibling();
        }
        void Option(string caption,UnityAction choose)
        {
            int y=popupRows.Count*67;
            var row=Picture(popupContent,"Option",0,y,706,58,Art("Common/CancelButton"));
            ButtonFor(row,choose); popupRows.Add(row.gameObject);
            Label(row.transform,"Choice",20,0,670,58,22,Cream,true).text=caption;
        }
        void ShowChoices()
        {
            if(!manager) return;
            BeginPopup("성흔 변경");
            var candidates=manager.Room.GetOffers();
            if(!manager.Room.HasClaimedCard)
                for(int i=0;i<candidates.Count;i++)
                {
                    int index=i; var card=candidates[i];
                    Option($"{Names[(int)card.Owner]}  ·  {card.Definition.DisplayName}     획득 후보",()=>ChooseOffer(index));
                }
            for(int column=0;column<2;column++)
            {
                var owner=(StigmaOwner)column; var list=manager.Loadout.GetAcquired(owner);
                for(int i=0;i<list.Count;i++)
                {
                    int index=i;
                    Option($"{Names[column]}  ·  {list[i].DisplayName}     {manager.Loadout.GetLevel(owner,list[i].Id)}단계",()=>ChooseOwned(owner,index));
                }
            }
            if(popupRows.Count==0) Option("보유한 성흔이 없습니다.",()=>ClosePopup());
            LayoutPopup(popupRows.Count*67-9);
        }

        // The dialog is only as tall as what it shows (up to the scroll limit) and stays centred.
        void LayoutPopup(float content)
        {
            float view=Mathf.Min(503,content), height=112+view+44, top=Mathf.Round((1080-height)*.5f);
            popupPanel.rectTransform.anchoredPosition=new Vector2(555,-top);
            popupPanel.rectTransform.sizeDelta=new Vector2(810,height);
            popupTitle.rectTransform.anchoredPosition=new Vector2(600,-(top+30));
            popupClose.rectTransform.anchoredPosition=new Vector2(1280,-(top+35));
            popupRule.rectTransform.anchoredPosition=new Vector2(600,-(top+94));
            popupViewport.anchoredPosition=new Vector2(599,-(top+112));
            popupViewport.sizeDelta=new Vector2(720,view);
            popupContent.sizeDelta=new Vector2(720,Mathf.Max(view,content));
            popupContent.anchoredPosition=Vector2.zero;
        }

        // One dark inset box with a gold edge, like the link skill box on the main screen.
        float HelpSection(float y,string heading,string body)
        {
            var box=Picture(popupContent,"Help section",0,y,706,100,Art("Journey/DescriptionPanel"));
            var edge=Picture(popupContent,"Help edge",0,y,4,100,null); edge.color=Gold;
            Label(popupContent,"Help heading",28,y+14,650,30,20,Gold,true).text=heading;
            var text=Label(popupContent,"Help body",28,y+50,650,40,20,Muted,false,TextAnchor.UpperLeft);
            text.resizeTextForBestFit=false; text.lineSpacing=1.2f; text.text=body;
            float lines=Mathf.Ceil(text.preferredHeight);
            text.rectTransform.sizeDelta=new Vector2(650,lines);
            float height=50+lines+18;
            box.rectTransform.sizeDelta=new Vector2(706,height);
            edge.rectTransform.sizeDelta=new Vector2(4,height);
            return y+height+14;
        }
        void ShowHelp()
        {
            BeginPopup("성흔 안내");
            string linkKey=manager ? manager.Database.Rules.LinkSkillKey.ToString() : "G";
            float y=0;
            y=HelpSection(y,"단계 보기","왼쪽 판에서 세브 또는 코빈의 단계 원을 누르면 그 단계의 효과가 오른쪽에 표시됩니다.");
            y=HelpSection(y,"성흔 변경","로스 · 아르도르 · 우무스 탭이나 [성흔 변경] 버튼으로 제단의 획득 후보와 보유 성흔을 고릅니다. 장착과 교체는 성흔 제단에서만 할 수 있습니다.");
            y=HelpSection(y,"강화","장착 중인 성흔은 성흔 제단에서 구리동전을 내고 3단계까지 강화합니다. 강화 버튼 대신 E 키를 눌러도 됩니다.");
            y=HelpSection(y,"연계 스킬",$"세브와 코빈이 모두 성흔을 장착하면 조합에 맞는 연계 스킬이 열립니다. 연계 단계는 두 성흔 중 낮은 단계를 따르며, 전투 중 {linkKey} 키로 사용합니다.");
            // Rows of an earlier dialog are already switched off and queued for removal; track only the new ones.
            foreach(Transform child in popupContent) if(child.gameObject.activeSelf) popupRows.Add(child.gameObject);
            LayoutPopup(y-14);
        }

        static Sprite Art(string name) => Resources.Load<Sprite>("WesternBeta/"+name);
        static RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=Vector2.up;
            r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
        }
        static Image Picture(Transform parent,string name,float x,float y,float w,float h,Sprite sprite)
        {
            var image=Box(parent,name,x,y,w,h).gameObject.AddComponent<Image>();
            image.sprite=sprite; image.raycastTarget=false; return image;
        }
        Text Label(Transform parent,string name,float x,float y,float w,float h,int size,Color color,bool bold=false,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var text=Box(parent,name,x,y,w,h).gameObject.AddComponent<Text>();
            text.font=font; text.fontSize=size; text.fontStyle=bold ? FontStyle.Bold : FontStyle.Normal;
            text.color=color; text.alignment=align; text.raycastTarget=false;
            text.resizeTextForBestFit=true; text.resizeTextMaxSize=size; text.resizeTextMinSize=Mathf.Max(13,size*2/3);
            return text;
        }
        static Button ButtonFor(Image image,UnityAction action)
        {
            image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();
            button.targetGraphic=image; button.transition=Selectable.Transition.None;
            button.onClick.AddListener(action); return button;
        }
        static Button Hit(Transform parent,string name,float x,float y,float w,float h,UnityAction action)
        {
            var hit=Picture(parent,name,x,y,w,h,null); hit.color=Color.clear;
            return ButtonFor(hit,action);
        }
    }
}

