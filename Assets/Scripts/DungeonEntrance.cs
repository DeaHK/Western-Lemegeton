using System;
using UnityEngine;
using WesternLemegeton.Passives;
using WesternLemegeton.Stigmas;
using DropRarity = WesternLemegeton.Passives.PassiveRarity;

namespace WesternLemegeton
{
    public partial class Dungeon
    {
        [Serializable] public struct SealWeight
        {
            public DropRarity Grade;
            [Min(0)] public float Weight;
            public SealWeight(DropRarity grade, float weight) { Grade = grade; Weight = weight; }
        }

        [Header("몬스터 인장 드롭")]
        [SerializeField, Range(0, 100)] private float monsterSealPercent = 8;
        [Tooltip("후보가 있는 등급끼리의 상대 가중치. 0은 제외됩니다.")]
        [SerializeField] private SealWeight[] sealGradeWeights = {
            new SealWeight(DropRarity.Common, 100), new SealWeight(DropRarity.Uncommon, 0),
            new SealWeight(DropRarity.Rare, 0), new SealWeight(DropRarity.Legendary, 0)
        };

        enum EntranceChoice { None, Raven, Seal }
        EntranceChoice entranceChoice;
        public bool EntryChoiceActive => entranceChoice != EntranceChoice.None;
        public bool ChoosingFirstStigma => entranceChoice == EntranceChoice.Raven;
        static readonly string[] FirstStigmas = { "ardor", "ros", "humus" };
        public StigmaDefinitionSO FirstStigma(int index) => Stigmas.Database.Find(FirstStigmas[index]);

        GameObject house, practiceProps, houseMarker;
        RoomAuthoring practiceRoom;
        Enemy target;
        Vector2 houseDoor, practiceDoor;
        readonly Vector2[] gunPoints = new Vector2[3];
        static readonly Vector2 HouseCenter = new Vector2(-350, -350);
        static readonly string[] GunSprites = { "Gun_Frontier", "Gun_Heavy", "Gun_Light" };
        readonly Sprite[] gunIcons = new Sprite[3];
        public Sprite GunIcon(Player.Firearm gun)
        {
            int index = (int)gun;
            if (!gunIcons[index]) gunIcons[index] = Resources.Load<Sprite>("HirvaInterior/" + GunSprites[index]);
            return gunIcons[index];
        }
        public bool InsideHouse => house;
        public bool InPractice => practiceRoom;
        public Vector2 PracticeExit => practiceDoor;

        void RollMonsterSeal(Enemy enemy)
        {
            if (enemy.AuxiliaryTarget || InPractice || IsTown || monsterSealPercent <= 0 ||
                (monsterSealPercent < 100 && UnityEngine.Random.value * 100 >= monsterSealPercent)) return;
            // Choose the grade first, then an item within it. Adding more items does not dilute other grades.
            float total = 0;
            PassiveSO chosen = null;
            foreach (var row in sealGradeWeights)
            {
                if (!(row.Weight > 0)) continue;
                int count = 0;
                PassiveSO candidate = null;
                foreach (var item in PassiveOffers.Seals)
                    if (item && item.Rarity == row.Grade &&
                        UnityEngine.Random.Range(0, ++count) == 0) candidate = item;
                if (!candidate) continue;
                total += row.Weight;
                if (!chosen || UnityEngine.Random.value * total < row.Weight) chosen = candidate;
            }
            if (chosen) Passives.AcquirePassive(chosen, PassiveAcquisitionSource.MonsterDrop);
        }

        static SpriteRenderer EntryArt(Transform root, string key, Vector2 pos, Vector2 size, int order, bool floor = false)
        {
            var sprite = Resources.Load<Sprite>("HirvaInterior/" + key);
            var sr = Ink.Shape(key, root, pos, Vector2.one, Color.white, order);
            sr.sprite = sprite;
            sr.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
            sr.GetComponent<PaperCard>().Flat = floor;
            return sr;
        }

        void PrepareHouseEntrance()
        {
            if (houseMarker) return;
            // Wnn3 is the residence in the lower middle row of the authored Hirva map.
            foreach (var footprint in Scene.Hirva.GetComponentsInChildren<WorldFootprint>(true))
            {
                var node = footprint.transform;
                while (node && node != Scene.Hirva.transform && node.name != "Wnn3") node = node.parent;
                if (!node || node.name != "Wnn3") continue;
                Rect r = footprint.Bounds;
                houseDoor = Rules.OutsideCover(new Vector2(r.center.x, r.yMin - .65f));
                houseMarker = Ink.Shape("House entrance", null, houseDoor, new Vector2(1.4f,.45f), Ink.Cyan, -20, true).gameObject;
                return;
            }
        }

        public Vector2 HouseDoor => houseDoor;
        public bool ShowHouseSign => houseMarker && IsTown && !InsideHouse && State == RunState.Town;

        public string HousePrompt => !IsTown ? null : InsideHouse
            ? (Vector2.Distance(Pos, HouseCenter + new Vector2(0,-3.15f)) < 1.8f ? "F · 집 밖으로" : null)
            : houseMarker && Vector2.Distance(Pos, houseDoor) < 1.8f ? "F · 사냥꾼의 집에 들어가기" : null;

        void EnterHouse()
        {
            Scene.Hirva.gameObject.SetActive(false);
            houseMarker.SetActive(false);
            house = new GameObject("Hunter House Interior");
            var root = house.transform;
            root.position = HouseCenter;
            Ink.Shape("House backdrop",root,Vector2.zero,Vector2.one*100,new Color(.035f,.025f,.035f),-2000);
            Rules.RoomOrigin = HouseCenter;
            Obstacles = SolidObstacles = Array.Empty<Rect>();
            for (int x = -1; x <= 1; x++)
                for (int y = 0; y < 2; y++)
                    EntryArt(root,"Wood_Floor",new Vector2(x*4,y*4-2),Vector2.one*4.02f,-1500,true);
            for (int i=-2; i<=2; i++)
                EntryArt(root,"Wood_Wall",new Vector2(i*2.4f,4.15f),new Vector2(2.45f,2.8f),-110);
            EntryArt(root,"Bed",new Vector2(-4.3f,2.1f),new Vector2(2.5f,1.65f),-40);
            EntryArt(root,"Table",new Vector2(-1.2f,2),new Vector2(2.1f,1.45f),-40);
            EntryArt(root,"Bookshelf",new Vector2(4.4f,2),new Vector2(1.65f,2.25f),-40);
            EntryArt(root,"Chair",new Vector2(.65f,.55f),new Vector2(.85f,1.05f),-10);
            EntryArt(root,"Stove",new Vector2(-4.5f,-.2f),new Vector2(1,1.5f),5);
            EntryArt(root,"Rug",new Vector2(0,-.2f),new Vector2(4,2.2f),-1000,true);
            EntryArt(root,"Rug",new Vector2(0,-3.15f),new Vector2(1.6f,.7f),-700,true);
            PlaceAt(HouseCenter + new Vector2(0,-2.5f));
            Crow.transform.position = HouseCenter + new Vector2(2.7f,1.15f);
        }

        void PlaceAt(Vector2 point)
        {
            Hero.transform.position = point;
            Hero.SafeEntry();
            Crow.ResetAt(point);
            ResetCamera();
        }

        void BeginPractice()
        {
            var authored = Scene.Rooms[0];
            practiceRoom = Instantiate(authored, new Vector3(-400,0,-400), Quaternion.identity);
            practiceRoom.name = "Weapon Selection Room";
            practiceRoom.gameObject.SetActive(true);
            foreach (var room in Rooms) room.Layout.Root.gameObject.SetActive(false);
            ApplyFootprints(practiceRoom.Capture(), practiceRoom.Center);
            practiceProps = new GameObject("Weapon Racks and Target");
            var center = practiceRoom.Center;
            for (int i=0; i<gunPoints.Length; i++)
            {
                gunPoints[i] = Rules.OutsideCover(center + new Vector2((i-1)*3,0));
                var icon = GunIcon((Player.Firearm)i);
                float width = 1.35f + .35f * i;
                EntryArt(practiceProps.transform,GunSprites[i],gunPoints[i],
                    new Vector2(width,width * icon.rect.height / icon.rect.width),2);
            }
            Vector2 targetPos = Rules.OutsideCover(center + new Vector2(0,3.4f));
            var visual = EntryArt(practiceProps.transform,"Training_Scarecrow",targetPos,new Vector2(1.5f,1.9f),-25);
            target = visual.gameObject.AddComponent<Enemy>();
            target.AuxiliaryTarget = true;
            target.MaxHp = target.Hp = 1;
            var anchor = new GameObject("Practice hit volume").transform;
            anchor.SetParent(practiceProps.transform);
            anchor.position = PaperWorld.Point(targetPos,.9f);
            var body = CombatBody.For(target);
            body.BodyAnchor = anchor;
            body.BodySize = new Vector3(1.5f,1.9f,.7f);
            target.Damaged += PracticeHit;
            Enemies.Add(target);
            // Use the authored doorway so its visual and interaction point agree.
            RoomDoor exit = null;
            foreach (var door in practiceRoom.Doors)
                if (door && (!exit || door.Position.x > exit.Position.x)) exit = door;
            foreach (var door in practiceRoom.Doors)
                if (door && door != exit) door.gameObject.SetActive(false);
            practiceDoor = Rules.OutsideCover(exit ? exit.Position : center + new Vector2(11,0));
            if (exit)
            {
                exit.enabled = false;
                if (exit.Marker) exit.Marker.color = Ink.Cyan;
            }
            Ink.Shape("Practice exit",practiceProps.transform,practiceDoor,new Vector2(2,.65f),Ink.Cyan,-2,true);
            State = RunState.Exit;
            BuildTrainingStations(practiceProps.transform, center);
            PlaceAt(Rules.OutsideCover(center + new Vector2(0,-2.5f)));
            Tell("무기 테스트실 · 총 앞에서 T · 표적 시험 후 청록색 출구에서 F");
        }

        void PracticeHit(Enemy dummy)
        {
            dummy.Hp = dummy.MaxHp;
            Ink.Ring(dummy.transform.position,1,Ink.Gold);
            Tell("표적 명중 · " + Hero.WeaponLabel);
        }

        void HandlePracticeWeaponInput()
        {
            if (!InPractice || !Input.GetKeyDown(KeyCode.T)) return;
            int gun = NearbyGun();
            if (gun >= 0) Hero.SelectPracticeGun((Player.Firearm)gun);
        }

        int NearbyGun()
        {
            for (int i=0; i<gunPoints.Length; i++) if (Vector2.Distance(Pos,gunPoints[i]) < 1.35f) return i;
            return -1;
        }

        public string PracticePrompt()
        {
            var station = NearbyTrainingStation();
            if (station != null) return StationPrompt(station);
            if (Vector2.Distance(Pos,practiceDoor) < 2f) return "F · 무기 테스트실 나가기";
            int gun = NearbyGun();
            return gun < 0 ? "총 앞에서 T · 표적 시험 · 청록색 출구" : "T · " + Player.GunNames[gun] + " 착용 / 해제";
        }

        bool TryEntryInteraction()
        {
            if (HousePrompt != null)
            {
                if (InsideHouse)
                {
                    Destroy(house); house = null;
                    BuildTown();
                    houseMarker.SetActive(true);
                    PlaceAt(houseDoor);
                }
                else EnterHouse();
                return true;
            }
            if (!InPractice) return false;
            if (Vector2.Distance(Pos,practiceDoor) < 2f)
            {
                entranceChoice = EntranceChoice.Raven;
                State = RunState.CardChoice;
            }
            else return false;
            return true;
        }

        bool FinishEntryChoice(int index)
        {
            if (!EntryChoiceActive || State != RunState.CardChoice) return false;
            if (ChoosingFirstStigma)
            {
                if (index >= 0)
                {
                    var id = FirstStigmas[index];
                    if (!Stigmas.TryAcquire(StigmaOwner.Raven,id,out string error) ||
                        !Stigmas.TryEquip(StigmaOwner.Raven,id,out error)) { Tell(error); return true; }
                }
                ClearPractice();
                Hero.ResetAfterPractice();
                Crow.ResetForRun();
                ClearTransient();
                ActivateRoomLayout();
                PlaceActorsAtRoomEntry(-1);
                if (PassiveOffers.PrepareOffer(PassiveAcquisitionSource.EntranceChoice,Seed,Room,MapIndex,3))
                { entranceChoice = EntranceChoice.Seal; return true; }
            }
            else if (index >= 0 && !PassiveOffers.TryClaim(index,out var result))
            { Tell(result.Error); return true; }
            PassiveOffers.DismissOffers();
            entranceChoice = EntranceChoice.None;
            BeginRoomContent(-1);
            return true;
        }

        void ClearPractice()
        {
            if (practiceRoom) trainingStations.Clear();
            if (target) { target.Damaged -= PracticeHit; Enemies.Remove(target); target = null; }
            if (practiceProps) { practiceProps.SetActive(false); Destroy(practiceProps); practiceProps = null; }
            if (practiceRoom) { practiceRoom.gameObject.SetActive(false); Destroy(practiceRoom.gameObject); practiceRoom = null; }
        }

        void ResetEntrance()
        {
            ClearBossTrial();
            entranceChoice = EntranceChoice.None;
            ClearPractice();
            if (house) { Destroy(house); house = null; }
            if (houseMarker) { Destroy(houseMarker); houseMarker = null; }
        }
    }
}
