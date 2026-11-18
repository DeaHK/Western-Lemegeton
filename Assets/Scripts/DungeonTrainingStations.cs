using System.Collections.Generic;
using UnityEngine;
using WesternLemegeton.Passives;

namespace WesternLemegeton
{
    public partial class Dungeon
    {
        enum StationKind { Seal, Altar, Copper, Protection }

        sealed class TrainingStation
        {
            public StationKind Kind;
            public PassiveSO Seal;
            public Vector2 World;
            public TextMesh Label;
        }

        readonly List<TrainingStation> trainingStations = new List<TrainingStation>();
        const int CopperGrant = 10000;
        public bool InTrainingRoom => InTrialGallery || InPractice;
        public float TrainingHalfWidth => InTrialGallery
            ? Mathf.Max(21, 11 + 3.35f * Mathf.CeilToInt(PassiveOffers.Seals.Count / 3f)) : 15;
        public bool AtTrainingInteraction => AtBossSelector || NearbyTrainingStation() != null;

        void BuildTrainingStations(Transform parent, Vector2 center)
        {
            trainingStations.Clear();
            var root = new GameObject("Training stations").transform;
            root.SetParent(parent, false);
            root.position = center;
            var seals = new List<PassiveSO>(PassiveOffers.Seals);
            seals.Sort((a,b) => a.PassiveID.CompareTo(b.PassiveID));
            for (int i = 0; i < seals.Count; i++)
            {
                // Keep the central lane from the entrance to the combat portal clear.
                Vector2 point = InTrialGallery
                    ? new Vector2(-9 - 3.35f * (i / 3), 4.5f - 3.6f * (i % 3))
                    : new Vector2(-6.0f - 3.35f * (i / 4), 5.8f - 3.5f * (i % 4));
                AddStation(root, center, point, StationKind.Seal, seals[i]);
            }
            Vector2 first = InTrialGallery ? new Vector2(4.1f,3.6f) : new Vector2(3.5f,-4.6f);
            AddStation(root, center, first, StationKind.Altar);
            AddStation(root, center, first + Vector2.right * 3.65f, StationKind.Copper);
            AddStation(root, center, first + Vector2.right * 7.3f, StationKind.Protection);
        }

        void AddStation(Transform root, Vector2 center, Vector2 point, StationKind kind, PassiveSO seal = null)
        {
            Vector2 world = center + point;
            if (InPractice)
            {
                // The weapon room uses an authored obstacle layout. Place the plate
                // on walkable ground using the same clearance as the player's body.
                world = Rules.OutsideCover(practiceRoom.Placement(point), .8f);
                point = world - center;
            }
            var station = new TrainingStation { Kind = kind, Seal = seal, World = world };
            trainingStations.Add(station);
            string name = StationName(station);
            var plate = new GameObject(name).transform;
            plate.SetParent(root, false);
            plate.localPosition = point;
            if (seal)
            {
                Ink.Shape("Brass rim", plate, Vector2.zero, Vector2.one * 1.55f, Ink.Gold, -80, true);
                Ink.Shape("Enamel", plate, Vector2.zero, Vector2.one * 1.42f, new Color(.15f,.13f,.13f), -79, true);
                Sprite icon = Scene.UI.PassiveIcon(seal);
                if (icon) TrialArt(plate, name, icon, Vector2.zero, Vector2.one * 1.03f, -78, true);
            }
            else
            {
                string key = kind == StationKind.Altar ? "AltarPlate" : kind == StationKind.Copper ? "CopperPlate" : "ShieldPlate";
                TrialArt(plate, name, TrialSprite(key), Vector2.zero, Vector2.one * 1.95f, -79, true);
            }
            Ink.Shape("Nameplate", plate, Vector2.down * 1.10f, new Vector2(2.95f,.72f), new Color(.12f,.09f,.08f,.93f), -78);
            var label = new GameObject("Floor label").AddComponent<TextMesh>();
            label.transform.SetParent(plate,false);
            label.transform.SetPositionAndRotation(PaperWorld.Point(world + Vector2.down * 1.10f,.07f),
                Quaternion.Euler(PaperWorld.Pitch,0,0));
            label.font = Scene.UI.Element<UnityEngine.UI.Text>("Location").font;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 44;
            label.characterSize = .092f;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1,.93f,.74f);
            station.Label = label;
            label.text = name;
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = label.font.material;
            renderer.sortingOrder = -740;
            float width = renderer.localBounds.size.x;
            if (width > 2.78f) label.transform.localScale = Vector3.one * (2.78f / width);
        }

        string StationName(TrainingStation station)
        {
            switch (station.Kind)
            {
                case StationKind.Seal: return station.Seal.NameStringKey.Replace("인장 · ", "");
                case StationKind.Altar: return "성흔 강화";
                case StationKind.Copper: return "동전 +10,000";
                default: return Hero.TrainingDamageProtection ? "무적 켜짐" : "무적 꺼짐";
            }
        }

        TrainingStation NearbyTrainingStation()
        {
            if (!InTrainingRoom) return null;
            float best = 1.72f * 1.72f;
            TrainingStation nearest = null;
            foreach (var station in trainingStations)
            {
                float distance = (Pos - station.World).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = station;
            }
            return nearest;
        }

        string StationPrompt(TrainingStation station)
        {
            switch (station.Kind)
            {
                case StationKind.Seal:
                    Passives.TryGetPassive(station.Seal.PassiveID, out var owned);
                    return $"G · {StationName(station)} 획득  ·  보유 {owned?.Stack ?? 0}개";
                case StationKind.Altar: return "G · 성흔 획득 / 장착 / 강화";
                case StationKind.Copper: return $"G · 구리동전 +10,000  ·  보유 {Wallet.Copper:N0}";
                default: return Hero.TrainingDamageProtection ? "G · 무적 끄기  ·  받는 피해 0" : "G · 무적 켜기  ·  받는 피해 0";
            }
        }

        internal bool UseTrainingStation()
        {
            if (!Running) return false;
            var station = NearbyTrainingStation();
            if (station == null) return false;
            switch (station.Kind)
            {
                case StationKind.Seal:
                    var result = Passives.AcquirePassive(station.Seal, PassiveAcquisitionSource.Debug);
                    Tell(result.Success ? $"{StationName(station)} 획득 · 보유 {result.NewStack}개" : result.Error);
                    break;
                case StationKind.Altar:
                    Stigmas.Room.BeginTrainingVisit();
                    State = RunState.SigilChoice;
                    break;
                case StationKind.Copper:
                    Wallet.Add(CopperGrant);
                    Tell($"구리동전 +10,000 · 보유 {Wallet.Copper:N0}");
                    break;
                case StationKind.Protection:
                    Hero.TrainingDamageProtection = !Hero.TrainingDamageProtection;
                    station.Label.text = StationName(station);
                    Tell(station.Label.text);
                    break;
            }
            return true;
        }
    }
}
