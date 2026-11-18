using UnityEngine;

namespace WesternLemegeton
{
    public partial class Dungeon
    {
        enum TrialRoom { None, Gallery, Arena }
        TrialRoom trialRoom;
        static readonly Vector2 GalleryCenter = new Vector2(-180,-180);
        static readonly Vector2 SanctuaryCenter = new Vector2(-240,-180);
        static readonly Vector2 PortraitPoint = new Vector2(-3,0);
        static readonly Vector2 NorthGate = new Vector2(0,6.8f);
        Vector2 SouthGate => Vector2.down * (InJudicatorArena ? PaladinArea.ArenaHalfSize.y - 2 : 7);
        static readonly Vector2[] TrialRacks = { new Vector2(3,0), new Vector2(6,0), new Vector2(9,0) };
        GameObject trialRoot, trialGate;
        SpriteRenderer trialPortrait;
        TrialInterior trialInterior;
        Enemy trialBoss;
        internal PaladinEchoBarrier EchoBarrier => trialBoss is PaladinEncounter judge ? judge.EchoBarrier : null;
        bool trialComplete, retryTrial;
        int selection;
        BossTrialAudio trialAudio;
        [Header("보스 시험실 오디오")]
        [SerializeField] AudioClip trialMusicOverride;
        [SerializeField, Range(0,1)] float trialMusicVolume=.16f;
        [SerializeField, Range(0,1)] float trialEffectVolume=.23f;
        [System.Serializable]
        public struct TrialBossEntry
        {
            public Enemy Prefab;
            public Sprite Portrait;
            public string DisplayName;
        }
        [Header("보스 시험실 추가 등록")]
        [SerializeField] TrialBossEntry[] additionalTrialBosses = System.Array.Empty<TrialBossEntry>();
        readonly System.Collections.Generic.List<TrialBossEntry> trialBosses = new System.Collections.Generic.List<TrialBossEntry>();
        public bool InBossTrial => trialRoom != TrialRoom.None;
        public bool InTrialGallery => trialRoom == TrialRoom.Gallery;
        internal bool InJudicatorArena => trialRoom == TrialRoom.Arena && trialBosses.Count > 0 &&
            trialBosses[selection].Prefab is PaladinEncounter;
        bool AtBossSelector => InTrialGallery && (At(ArrowPoint(-1)) || At(ArrowPoint(1)));
        public Enemy TrialBoss => trialComplete ? null : trialBoss;
        internal PaladinEncounter Judicator => trialBoss as PaladinEncounter;
        public string TrialBossName => Judicator && Judicator.SecondPhase ? "심판받지 못한 자" :
            trialBosses.Count==0 ? "" : trialBosses[selection].DisplayName;
        public string TrialLocation => trialRoom == TrialRoom.Gallery ? "히르바 · 보스 시험실" : "보스 시험 · " + TrialBossName;
        Vector2 TrialCenter => trialRoom == TrialRoom.Gallery ? GalleryCenter : SanctuaryCenter;
        bool At(Vector2 point, float radius = 1.65f) => Vector2.Distance(LocalPos,point) <= radius;
        public bool NearTrialGate => trialGate && State == RunState.Town && !InsideHouse &&
            Vector2.Distance(Pos, trialGate.transform.position) < 2;
        public string TrialPrompt
        {
            get
            {
                if (!InBossTrial) return "F · 보스 시험실";
                if (trialRoom == TrialRoom.Arena) return At(SouthGate) || trialComplete ? "남쪽 포탈 · F 시험실로 돌아가기" : "";
                var station = NearbyTrainingStation();
                if (station != null) return StationPrompt(station);
                if (At(ArrowPoint(-1)) || At(ArrowPoint(1))) return "G · 보스 변경  /  " + TrialBossName;
                if (At(NorthGate,2)) return "F · " + TrialBossName + " 전투 입장";
                if (At(SouthGate)) return "F · 히르바로 돌아가기";
                for (int i=0; i<TrialRacks.Length; i++) if (At(TrialRacks[i],1.25f)) return "T · " + Player.GunNames[i] + " 착용 / 해제";
                return "서쪽 인장 · 동쪽 강화 / 동전 / 무적 · 북쪽 전투 포탈";
            }
        }

        static Vector2 ArrowPoint(int direction) => PortraitPoint + Vector2.right * direction * 2.25f;
        static Sprite TrialSprite(string key) => Resources.Load<Sprite>("Paladin/" + key);

        internal static SpriteRenderer TrialArt(Transform parent, string name, Sprite sprite, Vector2 point, Vector2 size, int order, bool flat)
        {
            var art = Ink.Shape(name,parent,point,size,Color.white,order);
            art.sprite = sprite;
            Vector2 bounds = sprite.bounds.size;
            art.transform.localScale = new Vector3(size.x/bounds.x,size.y/bounds.y,1);
            art.GetComponent<PaperCard>().Flat = flat;
            return art;
        }

        void PrepareTrialGate()
        {
            if (trialGate) return;
            var point = Rules.OutsideCover(Scene.Hirva.Center + new Vector2(-11.8f,0));
            trialGate = new GameObject("West Gate - Boss Trials");
            trialGate.transform.position = point;
            trialGate.transform.SetParent(Scene.Hirva.transform,true);
            DrawGate(trialGate.transform,Vector2.zero,Ink.Gold);
        }

        static void DrawGate(Transform parent, Vector2 pos, Color color)
        {
            var gate = TrialArt(parent,"Engraved portal",TrialSprite("Gate"),pos,new Vector2(3,2.8f),-45,true);
            gate.color = Color.Lerp(Color.white,color,.22f);
        }

        void ChangeTrialRoom(TrialRoom next)
        {
            if (trialBoss) trialBoss.gameObject.SetActive(false);
            DestroyTrackedEnemies();
            ClearTransient();
            if (trialRoot) { trialRoot.SetActive(false); Destroy(trialRoot); }
            trainingStations.Clear();
            trialBoss = null;
            trialRoom = next;
            trialComplete = retryTrial = false;
            ClearEnvironment();
            if (houseMarker) houseMarker.SetActive(false);
            IsTown = false;
            trialRoot = new GameObject(next == TrialRoom.Gallery ? "Boss Selection Gallery" : "Sanctuary Arena");
            trialRoot.transform.position = TrialCenter;
            Rules.RoomOrigin = TrialCenter;
            Obstacles = SolidObstacles = System.Array.Empty<Rect>();
            State = next == TrialRoom.Gallery ? RunState.Exit : RunState.Combat;
            Hero.ResetAfterPractice();
            PlaceAt(TrialCenter + new Vector2(0,-4.6f));
            trialInterior = trialRoot.AddComponent<TrialInterior>();
            Vector2 extent = InJudicatorArena ? PaladinArea.ArenaHalfSize : new Vector2(InTrialGallery ? TrainingHalfWidth : 15,9);
            trialInterior.Build(TrialCenter, InTrialGallery, extent, InJudicatorArena);
            DrawGate(trialRoot.transform,SouthGate,Ink.Cyan);
        }

        internal void OpenTrialGallery()
        {
            ChangeTrialRoom(TrialRoom.Gallery);
            if (trialAudio) trialAudio.StopMusic();
            trialBosses.Clear();
            var paladin = Resources.Load<GameObject>("Paladin/SanctuaryPaladin");
            RegisterTrialBoss(new TrialBossEntry { Prefab=paladin.GetComponent<PaladinEncounter>(),
                Portrait=TrialSprite("Portrait"), DisplayName="심판관" });
            foreach (var prefab in Scene.EnemyPrefabs)
                if (prefab && (prefab.Kind==3 || prefab.GetComponent<LoganBossAttack>()))
                    RegisterTrialBoss(new TrialBossEntry { Prefab=prefab });
            foreach (var entry in additionalTrialBosses) RegisterTrialBoss(entry);
            selection = Mathf.Clamp(selection,0,trialBosses.Count-1);
            var root = trialRoot.transform;
            DrawGate(root,NorthGate,Ink.Gold);
            Ink.Shape("Selection frame",root,PortraitPoint,new Vector2(3.4f,4.3f),Ink.Gold,-96);
            Ink.Shape("Profile field",root,PortraitPoint,new Vector2(3.2f,4.1f),new Color(.12f,.18f,.18f),-95);
            trialPortrait = Ink.Shape("Selected boss portrait",root,PortraitPoint,Vector2.one,Color.white,-94);
            for (int direction=-1; direction<=1; direction+=2)
            {
                var point = ArrowPoint(direction);
                for (int y=-1; y<=1; y+=2)
                    Ink.Shape("Selection arrow",root,point+new Vector2(0,y*.24f),new Vector2(.84f,.19f),
                        Ink.Gold,-90,false,-direction*y*38);
                var key=new GameObject("G - change boss").AddComponent<TextMesh>();
                key.transform.SetParent(root,false);
                key.transform.SetPositionAndRotation(PaperWorld.Point(GalleryCenter+point+Vector2.down*.94f,.05f),
                    Quaternion.Euler(90,0,0));
                key.anchor=TextAnchor.MiddleCenter;
                key.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                key.fontSize=36; key.characterSize=.20f; key.text="G";
                var ink=key.GetComponent<MeshRenderer>();
                ink.sharedMaterial=key.font.material; ink.sortingOrder=-890;
            }
            ShowTrialSelection();
            for (int i=0; i<TrialRacks.Length; i++)
            {
                var sprite = GunIcon((Player.Firearm)i);
                TrialArt(root,Player.GunNames[i],sprite,TrialRacks[i],new Vector2(1.8f,1.8f*sprite.rect.height/sprite.rect.width),-82,true);
            }
            BuildTrainingStations(root, GalleryCenter);
            Tell("서쪽에서 인장 획득 · 동쪽에서 강화 / 동전 / 무적 · 장치 앞에서 G");
        }

        void RegisterTrialBoss(TrialBossEntry entry)
        {
            if (!entry.Prefab) return;
            if (string.IsNullOrWhiteSpace(entry.DisplayName)) entry.DisplayName=entry.Prefab.name;
            if (!entry.Portrait && entry.Prefab.AuthoredSprite) entry.Portrait=entry.Prefab.AuthoredSprite.sprite;
            if (!entry.Portrait) entry.Portrait=CombatArt.Get("Enemy");
            int index=trialBosses.FindIndex(item=>item.Prefab==entry.Prefab);
            if (index<0) trialBosses.Add(entry);
            else trialBosses[index]=entry;
        }

        void ShowTrialSelection()
        {
            Sprite portrait = trialBosses[selection].Portrait;
            trialPortrait.sprite = portrait;
            float scale = Mathf.Min(2.9f/portrait.bounds.size.x,3.8f/portrait.bounds.size.y);
            trialPortrait.transform.localScale = Vector3.one*scale;
        }

        internal void CycleTrialBoss(int direction)
        {
            selection = (selection+direction+trialBosses.Count)%trialBosses.Count;
            ShowTrialSelection();
            Tell(TrialBossName);
        }

        internal void StartTrialBattle()
        {
            ChangeTrialRoom(TrialRoom.Arena);
            var prefab = trialBosses[selection].Prefab;
            trialBoss = Instantiate(prefab, (Vector3)(TrialCenter + Vector2.up*2), Quaternion.identity);
            Enemies.Add(trialBoss);
            if (!trialAudio) trialAudio = gameObject.AddComponent<BossTrialAudio>();
            trialAudio.MusicVolume=trialMusicVolume;
            trialAudio.EffectVolume=trialEffectVolume;
            trialAudio.StartMusic(trialMusicOverride, trialBoss is PaladinEncounter);
            if (trialBoss is PaladinEncounter paladin)
            {
                SetSanctuaryDusk(0);
                paladin.Begin(this,trialAudio);
            }
            else trialBoss.Init(3,0);
            Tell(TrialBossName + " · 전투 시작");
        }

        internal void SetSanctuaryDusk(float amount)
        {
            if (trialInterior) trialInterior.SetDusk(amount);
        }

        internal void EndTrialBattle()
        {
            trialComplete = true;
            State = RunState.Exit;
            if (trialAudio) trialAudio.StopMusic();
            Tell("시험 완료 · 남쪽 포탈에서 F로 다시 선택");
        }

        bool HandleTrialInput()
        {
            if (!InBossTrial)
            {
                if (NearTrialGate && (Input.GetKeyDown(KeyCode.F) || Vector2.Distance(Pos,trialGate.transform.position) < .75f))
                { OpenTrialGallery(); return true; }
                return false;
            }
            if (retryTrial) { OpenTrialGallery(); Tell("다시 도전할 보스를 선택하세요"); return true; }
            if (trialRoom == TrialRoom.Arena)
            {
                RunTime += Time.deltaTime;
                if (!(trialBoss is PaladinEncounter))
                {
                    RemoveDefeatedEnemies();
                    if (!trialComplete && (!trialBoss || trialBoss.Dead)) EndTrialBattle();
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.G))
                {
                    int step = At(ArrowPoint(-1)) ? -1 : At(ArrowPoint(1)) ? 1 : 0;
                    if (step != 0) CycleTrialBoss(step);
                    else if (UseTrainingStation()) return true;
                }
                if (Input.GetKeyDown(KeyCode.T))
                    for (int i=0; i<TrialRacks.Length; i++)
                        if (At(TrialRacks[i],1.25f)) { Hero.SelectPracticeGun((Player.Firearm)i); break; }
            }
            bool walkThrough = trialRoom == TrialRoom.Gallery ? At(SouthGate,.72f) || At(NorthGate,.72f) : trialComplete && At(SouthGate,.72f);
            if (Input.GetKeyDown(KeyCode.F) || walkThrough) TravelTrial();
            return true;
        }

        bool TravelTrial()
        {
            if (NearTrialGate) { OpenTrialGallery(); return true; }
            if (trialRoom == TrialRoom.Gallery && At(NorthGate,2)) { StartTrialBattle(); return true; }
            if (!InBossTrial || !At(SouthGate,2)) return false;
            if (trialRoom == TrialRoom.Arena) OpenTrialGallery();
            else
            {
                EnterTown();
                PlaceAt(Rules.OutsideCover((Vector2)trialGate.transform.position + Vector2.right*2));
            }
            return true;
        }

        void ClearBossTrial()
        {
            if (InBossTrial) trainingStations.Clear();
            if (trialBoss) trialBoss.gameObject.SetActive(false);
            if (trialAudio) trialAudio.StopMusic();
            if (trialRoot) { trialRoot.SetActive(false); Destroy(trialRoot); }
            trialBoss = null;
            trialRoom = TrialRoom.None;
            trialComplete = retryTrial = false;
        }
    }
}

