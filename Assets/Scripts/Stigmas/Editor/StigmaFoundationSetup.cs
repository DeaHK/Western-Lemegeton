using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WesternLemegeton.Stigmas.Editor
{
    // Explicit authoring command only. No InitializeOnLoad or automatic asset generation.
    public static class StigmaFoundationSetup
    {
        public const string DataRoot = "Assets/Resources/Stigmas";
        public const string DatabasePath = DataRoot + "/StigmaDatabase.asset";
        const string UiPath = "Assets/Editable/UI/GameUI.prefab";

        [MenuItem("Tools/Western Lemegeton/Stigma/Install Foundation")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring stigma assets.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var db = CreateMissingData();
            if (!db.Validate(out string error)) throw new InvalidOperationException(error);
            GameObject prefab = PrefabUtility.LoadPrefabContents(UiPath);
            try
            {
                StigmaUIAuthoring.Build(prefab.GetComponent<GameUIView>());
                PrefabUtility.SaveAsPrefabAsset(prefab, UiPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            // Opening a scene can unload assets referenced only by local variables.
            db = AssetDatabase.LoadAssetAtPath<StigmaDatabaseSO>(DatabasePath);
            Dungeon dungeon = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out Dungeon found)) dungeon = found;
            if (!dungeon) throw new InvalidOperationException("Main scene Dungeon not found.");
            var manager = dungeon.GetComponent<StigmaManager>();
            if (!manager) manager = dungeon.gameObject.AddComponent<StigmaManager>();
            manager.Configure(db);
            if (!dungeon.GetComponent<StigmaDebugMenu>()) dungeon.gameObject.AddComponent<StigmaDebugMenu>();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("STIGMA_FOUNDATION_INSTALLED: database, new management panel, Main bindings.");
        }

        public static StigmaDatabaseSO CreateMissingData()
        {
            Directory.CreateDirectory(DataRoot);
            AssetDatabase.Refresh();
            var rules = LoadOrCreate<StigmaRulesSO>("StigmaRules", out _);
            var ardor = Definition("ardor", "아르도르", "FireIcon", new[] {
                "처치 시 공격속도 +10% (3초). 재처치 시 지속시간 갱신.",
                "처치 시 공격속도 +15% (4초), 이동속도 +5%. 재처치 시 지속시간 갱신.",
                "처치 시 공격속도 +20% (5초), 이동속도 +10%. 재처치 시 지속시간 갱신."
            }, new[] {
                new[] { Effect("on_kill_attack_speed", "percent", 10, "seconds", 3) },
                new[] { Effect("on_kill_attack_speed", "percent", 15, "seconds", 4), Effect("move_percent", "percent", 5) },
                new[] { Effect("on_kill_attack_speed", "percent", 20, "seconds", 5), Effect("move_percent", "percent", 10) }
            });
            var ros = Definition("ros", "로스", "WaterIcon", new[] {
                "흔적이 붙은 적 이동속도 -20%.", "흔적이 붙은 적 이동속도 -30%.",
                "흔적이 붙은 적 이동속도 -35%. 감속된 적 받는 피해 +10% (중복/합산 불가)."
            }, new[] {
                new[] { Effect("marked_enemy_slow", "percent", 20) },
                new[] { Effect("marked_enemy_slow", "percent", 30) },
                new[] { Effect("marked_enemy_slow", "percent", 35), Effect("slowed_enemy_vulnerability", "percent", 10) }
            });
            var humus = Definition("humus", "우무스", "NatureIcon", new[] {
                "처치 시 가상체력 +3.", "처치 시 가상체력 +5.", "처치 시 가상체력 +8. 타격 시 약한 넉백 (세부 수치 미정)."
            }, new[] {
                new[] { Effect("on_kill_bonus_hp", "amount", 3) },
                new[] { Effect("on_kill_bonus_hp", "amount", 5) },
                new[] { Effect("on_kill_bonus_hp", "amount", 8), Effect("on_hit_knockback") }
            });
            bool createFlagra = !AssetDatabase.LoadAssetAtPath<StigmaDefinitionSO>(DataRoot + "/Stigma_flagra.asset");
            var flagra = Definition("flagra", "플라그라", "FireIcon", new[] {
                "처치 시 공속 +15% (5초). 원 샷 잔탄 연소. 재처치 시 지속시간 갱신.",
                "처치 시 공속 +25% (8초), 상시 이속 +10%. 원 샷 피해 강화.",
                "처치 시 공속 +40% (10초), 상시 이속 +15%. 강화 원 샷 재장전 완료 시 회피 초기화."
            }, new[] {
                new[] { Effect("on_kill_attack_speed", "percent",15,"seconds",5) },
                new[] { Effect("on_kill_attack_speed", "percent",25,"seconds",8), Effect("move_percent","percent",10),
                    Effect("one_shot_tier","ammo_0",1.1f,"ammo_1",1.6f,"ammo_2",2.1f,"ammo_3",2.6f,"ammo_4",3.3f,"ammo_5",4f,"ammo_6",4.5f) },
                new[] { Effect("on_kill_attack_speed", "percent",40,"seconds",10), Effect("move_percent","percent",15),
                    Effect("one_shot_tier","ammo_0",1.1f,"ammo_1",1.7f,"ammo_2",2.2f,"ammo_3",2.8f,"ammo_4",3.6f,"ammo_5",4.5f,"ammo_6",5f,"reset_dodge",1) }
            });
            if (createFlagra)
            {
                flagra.BaseDefinition = ardor;
                flagra.UseBaseCombination = true;
                flagra.OwnerRestriction = StigmaOwnerRestriction.PlayerOnly;
                flagra.UseOwnerLevel = true;
                flagra.CommonEffects = new[] { Effect("one_shot_ammo","range_multiplier",2,"empty_ammo_cooldown_reduction",.7f,
                    "ammo_0",1.1f,"ammo_1",1.5f,"ammo_2",2f,"ammo_3",2.4f,"ammo_4",3f,"ammo_5",3.6f,"ammo_6",4f) };
                flagra.Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Stigmas/Icons/FlagraIcon.png");
                EditorUtility.SetDirty(flagra);
            }
            var combinations = new[] {
                Combination("flood", "수해", ros, ros, "범람", 2,
                    new[] { "감속된 적 처치 시 회피 쿨타임 감소 (수치 미정).", "동일 보너스의 강화 수치 미정.", "동일 보너스의 강화 수치 미정." },
                    new[] { "주변 적 이동속도 -40% (4초). 범위 미정.", "범위·지속시간 소폭 증가 (수치 미정).", "범위 확대. 범위 내 적 받는 피해 +10% (중복/합산 불가)." },
                    new[] { Params("slow_percent",40,"seconds",4), Params(), Params("vulnerability_percent",10) }),
                Combination("bloom", "자연", humus, humus, "개화", 2,
                    new[] { "가상체력 보유 중 피격 경직 감소 (수치 미정).", "동일 보너스의 강화 수치 미정.", "동일 보너스의 강화 수치 미정." },
                    new[] { "가상체력 +15. 가상체력 보유 중 공격력 +10%.", "가상체력 +20. 가상체력 보유 중 공격력 +10%.", "처치 시 가상체력 소량 재충전 (수치 미정)." },
                    new[] { Params("bonus_hp",15,"attack_percent",10), Params("bonus_hp",20,"attack_percent",10), Params() }),
                Combination("burn_rush", "업화", ardor, ardor, "연소 질주", 3,
                    new[] { "연속 처치 시 공격속도 단계 상승 (수치 미정).", "동일 보너스의 강화 수치 미정.", "동일 보너스의 강화 수치 미정." },
                    new[] { "3초간 플레이어·까마귀 공속 +30%. 종료 후 1.5초간 이동속도 -15%.", "지속시간 4초. 종료 후 1.5초간 이동속도 -15%.", "지속시간 연장, 대가 지속시간 감소 (수치 미정)." },
                    new[] { Params("attack_speed_percent",30,"seconds",3,"penalty_slow_percent",15,"penalty_seconds",1.5f), Params("attack_speed_percent",30,"seconds",4,"penalty_slow_percent",15,"penalty_seconds",1.5f), Params() }),
                Combination("vapor", "증발", ros, ardor, "증기 사역마", 3,
                    new[] { "이동속도 +3%.", "이동속도 +5%.", "이동속도 +6%, 회피 쿨타임 -3%." },
                    new[] { "사역마 6초 소환. 공격력의 40%로 공격. 유지 중 플레이어 이동속도 -10%.", "사역마 유지시간 7초.", "사역마 2기. 소멸 시 작은 폭발 (세부 수치 미정)." },
                    new[] { Params("seconds",6,"attack_percent",40,"penalty_slow_percent",10), Params("seconds",7,"attack_percent",40,"penalty_slow_percent",10), Params("familiar_count",2) },
                    new[] { new[] { Effect("move_percent","percent",3) }, new[] { Effect("move_percent","percent",5) }, new[] { Effect("move_percent","percent",6),Effect("dash_cooldown_percent","percent",-3) } }),
                Combination("mire", "진흙", ros, humus, "늪 폭발", 4,
                    new[] { "받는 경직 시간 -5%.", "받는 경직 시간 -8%.", "받는 경직 시간 -10%, 최대 체력 +2%." },
                    new[] { "전방 폭발. 감속된 적에게 공격력 220%, 그 외 80%. 사용 후 3초간 이동속도 -30%.", "폭발 범위 증가 (수치 미정).", "범위 확대, 대가 지속시간 2초." },
                    new[] { Params("slowed_attack_percent",220,"attack_percent",80,"penalty_slow_percent",30,"penalty_seconds",3), Params(), Params("penalty_seconds",2) },
                    new[] { new[] { Effect("hit_stun_percent","percent",-5) }, new[] { Effect("hit_stun_percent","percent",-8) }, new[] { Effect("hit_stun_percent","percent",-10),Effect("max_hp_percent","percent",2) } }),
                Combination("inferno", "화재", ardor, humus, "대폭발", 5,
                    new[] { "공격력 +2%.", "공격력 +3%.", "공격력 +4%, 가상체력 획득량 +5%." },
                    new[] { "지정 위치에 공격력 350% 폭발. 최대 체력의 15%를 기본 체력에서 소모. 소모 후 기본 체력 1 이상 필요.", "폭발 범위 증가 (수치 미정).", "폭발 후 3초간 화염 장판 (피해 등 세부 수치 미정)." },
                    new[] { Params("attack_percent",350,"max_hp_cost_percent",15,"minimum_remaining_hp",1), Params(), Params("ground_seconds",3) },
                    new[] { new[] { Effect("attack_percent","percent",2) }, new[] { Effect("attack_percent","percent",3) }, new[] { Effect("attack_percent","percent",4),Effect("bonus_hp_gain_percent","percent",5) } })
            };
            var db = LoadOrCreate<StigmaDatabaseSO>("StigmaDatabase", out bool created);
            if (created) db.Rules = rules;
            foreach (var d in new[] { ardor, ros, humus, flagra }) if (!db.Definitions.Contains(d)) db.Definitions.Add(d);
            foreach (var c in combinations) if (!db.Combinations.Contains(c)) db.Combinations.Add(c);
            // Fill the previously data-only same-type bonuses once. Keep designer edits on later installs.
            foreach (var c in combinations)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (c.Levels[i].Bonuses.Length != 0) continue;
                    if (c.SkillModuleId == "flood") c.Levels[i].Bonuses = new[] { Effect("slowed_kill_dash_refund", "value", rules.FloodDashRefund[i]) };
                    if (c.SkillModuleId == "bloom") c.Levels[i].Bonuses = new[] { Effect("shield_stun_reduction", "value", rules.BloomStunReduction[i]) };
                    if (c.SkillModuleId == "burn_rush") c.Levels[i].Bonuses = new[] { Effect("kill_chain_speed", "value", rules.KillChainSpeedStep[i]) };
                }
                EditorUtility.SetDirty(c);
            }
            EditorUtility.SetDirty(rules);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }

        static StigmaDefinitionSO Definition(string id, string name, string icon, string[] descriptions, StigmaEffectDefinition[][] effects)
        {
            var d = LoadOrCreate<StigmaDefinitionSO>("Stigma_" + id, out bool created);
            if (!created) return d;
            d.Id = id; d.DisplayName = name; d.FallbackIconKey = icon; d.InitialChoice = true;
            for (int i = 0; i < 3; i++) d.Levels[i] = new StigmaLevelDefinition { Description = descriptions[i], Effects = effects[i] };
            EditorUtility.SetDirty(d);
            return d;
        }
        static StigmaCombinationSO Combination(string id, string name, StigmaDefinitionSO a, StigmaDefinitionSO b,
            string skill, int cost, string[] bonus, string[] descriptions, StigmaParameter[][] parameters, StigmaEffectDefinition[][] effects = null)
        {
            var c = LoadOrCreate<StigmaCombinationSO>("Combination_" + id, out bool created);
            if (!created) return c;
            c.Id = id; c.DisplayName = name; c.First = a; c.Second = b; c.SkillName = skill; c.SkillModuleId = id; c.ChargeCost = cost;
            for (int i = 0; i < 3; i++) c.Levels[i] = new StigmaCombinationLevel {
                BonusDescription = bonus[i], SkillDescription = descriptions[i], SkillParameters = parameters[i],
                Bonuses = effects == null ? Array.Empty<StigmaEffectDefinition>() : effects[i], HasUnspecifiedDesignValues = true
            };
            EditorUtility.SetDirty(c);
            return c;
        }
        public static StigmaParameter[] Params(params object[] pairs)
        {
            var values = new StigmaParameter[pairs.Length / 2];
            for (int i = 0; i < values.Length; i++) values[i] = new StigmaParameter { Key = (string)pairs[i * 2], Value = Convert.ToSingle(pairs[i * 2 + 1]) };
            return values;
        }
        static StigmaEffectDefinition Effect(string module, params object[] pairs) => new StigmaEffectDefinition { ModuleId = module, Parameters = Params(pairs) };
        static T LoadOrCreate<T>(string name, out bool created) where T : ScriptableObject
        {
            string path = DataRoot + "/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = !asset;
            if (!asset) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
            return asset;
        }
    }
}
