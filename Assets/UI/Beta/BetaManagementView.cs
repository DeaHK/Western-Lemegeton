using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WesternLemegeton.Passives;
using WesternLemegeton.Stigmas;
using SealRarity = WesternLemegeton.Passives.PassiveRarity;

namespace WesternLemegeton
{
    // The beta panels read the existing loadout and inventory. Purchases still go through StigmaRoomView.
    internal sealed class BetaManagementView
    {
        readonly StigmaRoomView host;
        readonly GameUIView gameUI;
        readonly Font font;
        readonly Image inventory;
        public readonly BetaStigmaScreen Stigma;
        readonly List<PassiveInstance> items = new List<PassiveInstance>();
        readonly Button[] cards = new Button[12];
        readonly Image[] icons = new Image[12];
        readonly Text[] captions = new Text[12];
        readonly Text details, pageInfo;
        readonly Button previous, next;
        bool descending = true;
        int page, selected = -1, rarity = -1;
        public bool InventoryOpen => inventory.gameObject.activeSelf && inventory.gameObject.activeInHierarchy;

        public BetaManagementView(StigmaRoomView host)
        {
            this.host = host;
            gameUI = host.GetComponentInParent<GameUIView>();
            font = host.DetailName.font;
            Stigma = new BetaStigmaScreen(host, OpenInventory);
            inventory = Panel(host.transform, "Seal collection", 0, 0, 1280, 720, null);
            inventory.color = new Color(.14f,.12f,.13f);
            inventory.raycastTarget = true;
            Label(inventory.transform, 24,18,220,58,30).text = "인장 관리";
            Panel(inventory.transform, "Collection", 24,104,810,576,"Stigma/DetailPanel");
            Panel(inventory.transform, "Detail", 851,104,405,576,"Journey/JourneyPanel");
            string[] filters = { "전체", "일반", "희귀", "레어", "전설" };
            for (int i = 0; i < filters.Length; i++)
            {
                int value = i-1;
                Control(inventory.transform, filters[i], 43+i*103,130,95,40,filters[i], () => { rarity=value; page=0; selected=-1; RefreshInventory(); });
            }
            Control(inventory.transform,"Sort",590,130,211,40,"등급순 전환",() => { descending=!descending; page=0; RefreshInventory(); });
            for (int i=0; i<cards.Length; i++)
            {
                int index=i;
                var card=Control(inventory.transform,"Seal "+i,42+i%4*193,192+i/4*134,182,124,"",() => { selected=page*12+index; RefreshInventory(); });
                cards[i]=card;
                icons[i]=Panel(card.transform,"Icon",61,8,60,60,null);
                icons[i].preserveAspect=true;
                captions[i]=Label(card.transform,4,70,174,48,15);
            }
            details=Label(inventory.transform,872,148,364,438,20);
            details.alignment=TextAnchor.UpperLeft;
            previous=Control(inventory.transform,"Previous",48,615,75,40,"이전",() => { page--; RefreshInventory(); });
            next=Control(inventory.transform,"Next",712,615,75,40,"다음",() => { page++; RefreshInventory(); });
            pageInfo=Label(inventory.transform,132,614,572,42,17);
            Control(inventory.transform,"Back",885,610,335,45,"성흔 관리로 돌아가기",CloseInventory);
            CloseInventory();
        }

        public void CloseInventory() => inventory.gameObject.SetActive(false);
        void OpenInventory()
        {
            inventory.gameObject.SetActive(true);
            inventory.transform.SetAsLastSibling();
            page=0; selected=-1; RefreshInventory();
        }

        void RefreshInventory()
        {
            Dungeon.I.Passives.CopyActivePassives(items);
            if (rarity>=0) items.RemoveAll(p => (int)p.Rarity!=rarity);
            items.Sort((a,b) => {
                int byRarity = descending ? b.Rarity.CompareTo(a.Rarity) : a.Rarity.CompareTo(b.Rarity);
                return byRarity!=0 ? byRarity : a.PassiveID.CompareTo(b.PassiveID);
            });
            int pages=Mathf.Max(1,(items.Count+11)/12);
            page=Mathf.Clamp(page,0,pages-1);
            if (selected < page*12 || selected >= Mathf.Min(items.Count,(page+1)*12)) selected=page*12;
            for (int i=0;i<cards.Length;i++)
            {
                int index=page*12+i;
                cards[i].gameObject.SetActive(index<items.Count);
                if(index>=items.Count) continue;
                var item=items[index];
                icons[i].sprite=gameUI.PassiveIcon(item.Data); icons[i].enabled=icons[i].sprite;
                captions[i].text=item.Data.NameStringKey+"\n"+Rarity(item)+" · "+item.Stack+"중첩";
                cards[i].image.color=index==selected ? new Color(1,.8f,.48f) : Color.white;
            }
            pageInfo.text=$"획득 인장 {items.Count}종   ·   {page+1} / {pages}";
            previous.interactable=page>0; next.interactable=page+1<pages;
            details.text=items.Count==0 ? "표시할 인장이 없습니다." :
                $"인장 상세\n\n{items[selected].Data.NameStringKey}\n{Rarity(items[selected])} · {items[selected].Stack}중첩\n\n{items[selected].Data.DescriptionStringKey}\n\n현재 효과\n{GameUIView.AppliedEffect(items[selected])}\n\n획득 시 자동 적용";
        }

        static string Rarity(PassiveInstance item) => item.Rarity switch {
            SealRarity.Common => "일반", SealRarity.Uncommon => "희귀", SealRarity.Rare => "레어", _ => "전설" };
        static Sprite Art(string name) => Resources.Load<Sprite>("WesternBeta/"+name);
        static RectTransform Box(Transform parent,string name,int x,int y,int w,int h)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false); rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.up;
            rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(w,h);
            return rect;
        }
        static Image Panel(Transform parent,string name,int x,int y,int w,int h,string art)
        {
            var image=Box(parent,name,x,y,w,h).gameObject.AddComponent<Image>();
            if(art!=null) image.sprite=Art(art);
            image.raycastTarget=false;
            return image;
        }
        Text Label(Transform parent,int x,int y,int w,int h,int size)
        {
            var text=Box(parent,"Label",x,y,w,h).gameObject.AddComponent<Text>();
            text.font=font; text.fontSize=size; text.alignment=TextAnchor.MiddleCenter; text.raycastTarget=false;
            return text;
        }
        Button Control(Transform parent,string name,int x,int y,int w,int h,string title,UnityEngine.Events.UnityAction action)
        {
            var image=Panel(parent,name,x,y,w,h,"Common/CancelButton");
            image.raycastTarget=true;
            var button=image.gameObject.AddComponent<Button>();
            button.targetGraphic=image; button.onClick.AddListener(action);
            if(title.Length>0) Label(image.transform,1,0,w-2,h,16).text=title;
            return button;
        }
    }
}
