using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    // Describes registered execution modules using the same SO values as combat.
    public static class StigmaDescriptions
    {
        static string N(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        static string Signed(float value) => (value > 0 ? "+" : "") + N(value);
        static string Heading(string text) => "<color=#E0BD78><b>" + text + "</b></color>";

        public static string Bonuses(StigmaCombinationSO combination, int level, StigmaRulesSO rules)
        {
            var data = combination.Level(level);
            var lines = new List<string>();
            foreach (var effect in data.Bonuses)
            {
                float value = StigmaCombat.Parameter(effect.Parameters, "value");
                float percent = StigmaCombat.Parameter(effect.Parameters, "percent");
                switch (effect.ModuleId)
                {
                    case "slowed_kill_dash_refund": lines.Add($"감속된 적 처치 시 플레이어 회피 쿨타임 {N(value)}초 감소."); break;
                    case "shield_stun_reduction": lines.Add($"가상체력 보유 중 플레이어 피격 경직 {N(value)}% 감소."); break;
                    case "kill_chain_speed": lines.Add($"각자 처치 시 자신의 공속 +{N(value)}%, 최대 {rules.KillChainMaxStacks}중첩. {N(rules.KillChainSeconds)}초 내 재처치하면 갱신."); break;
                    case "move_percent": lines.Add($"플레이어·까마귀 이동속도 {Signed(percent)}%."); break;
                    case "attack_percent": lines.Add($"플레이어·까마귀 공격력 {Signed(percent)}%."); break;
                    case "dash_cooldown_percent": lines.Add($"플레이어 회피 쿨타임 {Signed(percent)}%."); break;
                    case "hit_stun_percent": lines.Add($"플레이어 피격 경직 {Signed(percent)}%."); break;
                    case "max_hp_percent": lines.Add($"플레이어 최대 체력 {Signed(percent)}%."); break;
                    case "bonus_hp_gain_percent": lines.Add($"가상체력 획득량 {Signed(percent)}%."); break;
                    default: return data.BonusDescription ?? "등록된 조합 보너스 설명이 없습니다.";
                }
            }
            return lines.Count > 0 ? string.Join("\n", lines) : data.BonusDescription ?? "없음";
        }

        public static string Skill(StigmaCombinationSO c, int level, StigmaRulesSO r)
        {
            int tier = Mathf.Clamp(level - 1, 0, 2);
            float P(string key, float fallback = 0) => c.SkillParameter(level, key, fallback);
            switch (c.SkillModuleId)
            {
                case "flood":
                    return $"주변 반경 {N(r.FloodRadius[tier])}의 적을 {N(r.FloodSeconds[tier])}초간 {N(P("slow_percent",40))}% 감속." +
                        (level == 3 ? " 받는 피해 +10% (중복 불가)." : "");
                case "bloom":
                    return $"가상체력 +{N(P("bonus_hp",15))}. 가상체력 보유 중 플레이어 공격력 +{N(P("attack_percent",10))}%." +
                        (level == 3 ? $" 처치 시 가상체력 +{N(r.BloomKillRefill)} 재충전." : "") + " 상한 초과 누적 불가.";
                case "burn_rush":
                    return $"{N(level == 3 ? r.RushLevel3Seconds : P("seconds",3))}초간 플레이어·까마귀 공속 +{N(P("attack_speed_percent",30))}%.";
                case "vapor":
                    return $"사역마 {Mathf.Clamp(Mathf.RoundToInt(P("familiar_count",1)),1,8)}기를 {N(P("seconds",6))}초 소환. 공격력의 {N(P("attack_percent",40))}%로 공격. 탐색 거리 {N(r.FamiliarRange)}." +
                        (level == 3 ? $" 소멸 시 반경 {N(r.FamiliarExplosionRadius)}에 {N(r.FamiliarExplosionPercent)}% 폭발." : "");
                case "mire":
                    return $"전방 반경 {N(r.MireRadius[tier])} 폭발. 감속된 적에게 공격력의 {N(P("slowed_attack_percent",220))}%, 그 외 {N(P("attack_percent",80))}% 피해.";
                case "inferno":
                    return $"지정 위치 반경 {N(r.InfernoRadius[tier])}에 공격력의 {N(P("attack_percent",350))}% 폭발. 사거리 {N(r.InfernoTargetRange)}." +
                        (level == 3 ? $" {N(P("ground_seconds",3))}초간 장판: {N(r.GroundTickSeconds)}초마다 {N(r.GroundAttackPercent)}% 피해." : "");
                default: return c.Level(level).SkillDescription ?? "등록된 연계스킬 설명이 없습니다.";
            }
        }

        public static string Price(StigmaCombinationSO c, int level, StigmaRulesSO r)
        {
            float P(string key, float fallback = 0) => c.SkillParameter(level, key, fallback);
            switch (c.SkillModuleId)
            {
                case "flood": case "bloom": return "없음";
                case "burn_rush": return $"종료 후 {N(level == 3 ? r.RushLevel3PenaltySeconds : P("penalty_seconds",1.5f))}초간 플레이어 이동속도 -{N(P("penalty_slow_percent",15))}%.";
                case "vapor": return $"소환 유지 중 플레이어 이동속도 -{N(P("penalty_slow_percent",10))}%.";
                case "mire": return $"사용 후 {N(P("penalty_seconds",3))}초간 플레이어 이동속도 -{N(P("penalty_slow_percent",30))}%.";
                case "inferno": return $"최대 체력의 {N(P("max_hp_cost_percent"))}%를 기본 체력에서 소모. 남는 체력 {N(P("minimum_remaining_hp",1))} 이상 필요. 가상체력은 소모하지 않음.";
                default: return "해당 연계스킬 설명을 확인하세요.";
            }
        }

        static void AppendCombination(StringBuilder text, StigmaCombinationSO combination, int level, StigmaRulesSO rules)
        {
            text.AppendLine(Heading("상시 조합 효과 · 표식 소모 없음"));
            text.AppendLine(Bonuses(combination, level, rules));
            text.AppendLine();
            text.AppendLine(Heading($"{rules.LinkSkillKey} · {combination.SkillName}"));
            text.AppendLine(Skill(combination, level, rules));
            text.AppendLine($"<color=#EAB4AB>대가: {Price(combination, level, rules)}</color>");
            text.AppendLine($"사용 시 표식 {combination.ChargeCost} 소모 / 보유 상한 {combination.ChargeCapacity}");
        }

        public static string Detail(StigmaDefinitionSO selected, int selectedLevel, StigmaEquipment mine, StigmaEquipment other,
            StigmaCombinationSO combination, StigmaRulesSO rules, string ownerName)
        {
            int ownLevel = Mathf.Clamp(selectedLevel, 1, StigmaRulesSO.MaximumLevel);
            int level = other.IsEquipped ? Mathf.Min(ownLevel, other.Level) : ownLevel;
            int effectLevel = selected.UseOwnerLevel ? ownLevel : level;
            bool equipped = mine.Definition == selected;
            var text = new StringBuilder();
            if (!other.IsEquipped)
                text.AppendLine("상대도 성흔을 장착해야 고유 효과·조합 효과·G 스킬이 활성화됩니다.\n");
            else if (combination)
            {
                text.AppendLine(Heading((equipped ? "현재 조합: " : "장착 시 조합: ") + combination.DisplayName + $" · Lv.{level}"));
                text.AppendLine($"{selected.DisplayName} + {other.Definition.DisplayName}");
                AppendCombination(text, combination, level, rules);
                text.AppendLine();
            }
            else text.AppendLine("이 성흔 쌍의 조합이 아직 등록되지 않았습니다.\n");
            text.AppendLine(Heading($"{ownerName} 고유 효과 · {selected.DisplayName} Lv.{effectLevel}"));
            if (selected.UseOwnerLevel) text.AppendLine("고유 효과는 장착 대상 레벨 적용 · 조합/G는 양쪽 중 낮은 레벨 적용.");
            text.AppendLine(selected.Level(effectLevel)?.Description ?? "설명 없음");
            string weaponDescription = StigmaWeaponEffects.Description(selected, effectLevel);
            if (!string.IsNullOrEmpty(weaponDescription)) { text.AppendLine(); text.AppendLine(weaponDescription); }
            if (selected.IsEnhanced)
                text.AppendLine($"강화 계보: {selected.BaseDefinition.DisplayName} → {selected.DisplayName}. 보유 중 하위 성흔 장착 불가.");
            text.AppendLine();
            if (effectLevel < StigmaRulesSO.MaximumLevel)
            {
                int next = effectLevel + 1;
                text.AppendLine(Heading($"다음 고유 효과 단계 · Lv.{next}"));
                text.AppendLine(selected.UseOwnerLevel ? $"{ownerName} 성흔을 Lv.{next}로 강화하면 적용됩니다." : $"양쪽 성흔이 모두 Lv.{next} 이상이어야 적용됩니다.");
                if (!selected.UseOwnerLevel && other.IsEquipped && ownLevel >= other.Level)
                    text.AppendLine("선택한 대상만 강화하면 현재 적용 단계는 오르지 않습니다.");
                text.AppendLine("고유 효과: " + (selected.Level(next)?.Description ?? "설명 없음"));
                string nextWeapon = StigmaWeaponEffects.Description(selected, next);
                if (!string.IsNullOrEmpty(nextWeapon)) text.AppendLine(nextWeapon);
            }
            else text.AppendLine(Heading("최대 고유 효과 단계 · Lv.3"));
            if (combination && level < StigmaRulesSO.MaximumLevel)
            {
                text.AppendLine();
                text.AppendLine(Heading($"다음 조합/G 단계 · Lv.{level + 1}"));
                text.AppendLine($"양쪽 성흔이 모두 Lv.{level + 1} 이상이어야 적용됩니다.");
                AppendCombination(text, combination, level + 1, rules);
            }
            return text.ToString().TrimEnd();
        }
    }
}
