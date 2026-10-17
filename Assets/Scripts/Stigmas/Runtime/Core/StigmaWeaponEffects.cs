using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    public readonly struct StigmaShotProfile
    {
        public readonly float Range, DamageMultiplier, CooldownMultiplier;
        public readonly bool ForcesReload, ResetDodgeOnReload;
        public StigmaShotProfile(float range, float multiplier, bool reload, bool reset, float cooldownMultiplier = 1)
        { Range = range; DamageMultiplier = multiplier; ForcesReload = reload; ResetDodgeOnReload = reset; CooldownMultiplier = cooldownMultiplier; }
    }

    // Weapon-specific modules stay here; Player only consumes a resolved shot/reload profile.
    public static class StigmaWeaponEffects
    {
        public const string AmmoShot = "one_shot_ammo";
        static float P(StigmaEffectDefinition effect, string key, float fallback = 0) =>
            effect == null ? fallback : StigmaCombat.Parameter(effect.Parameters, key, fallback);

        public static StigmaShotProfile Resolve(StigmaLoadout state, int ammo, float baseRange)
        {
            var result = new StigmaShotProfile(baseRange, 1, false, false);
            if (!state.BothEquipped) return result;
            var definition = state.Get(StigmaOwner.Player).Definition;
            return Resolve(definition, state.EffectLevel(StigmaOwner.Player), ammo, baseRange);
        }

        public static StigmaShotProfile Resolve(StigmaDefinitionSO definition, int level, int ammo, float baseRange)
        {
            var common = definition.Effect(level, AmmoShot);
            if (common == null) return new StigmaShotProfile(baseRange, 1, false, false);
            var tier = definition.Effect(level, "one_shot_tier");
            string key = "ammo_" + Mathf.Clamp(ammo, 0, 6);
            float reduction = Mathf.Lerp(Mathf.Clamp01(P(common, "empty_ammo_cooldown_reduction")), 0, Mathf.Clamp(ammo, 0, 6) / 6f);
            return new StigmaShotProfile(baseRange * P(common, "range_multiplier", 1),
                P(tier, key, P(common, key, 1)), true, P(tier, "reset_dodge") > 0, 1 - reduction);
        }

        public static float ReloadRate(float attackSpeed) => Mathf.Max(.1f, attackSpeed);

        public static string Description(StigmaDefinitionSO definition, int level)
        {
            var common = definition.Effect(level, AmmoShot);
            if (common == null) return "";
            string N(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
            var multipliers = new List<string>();
            for (int ammo = 0; ammo <= 6; ammo++) multipliers.Add(N(Resolve(definition, level, ammo, 1).DamageMultiplier));
            return $"공통 추가 효과 · 잔탄 연소\n플레이어 원 샷 사거리 ×{N(P(common, "range_multiplier", 1))}.\n" +
                "잔탄 0~6발 피해 배율: " + string.Join(" / ", multipliers) + ".\n" +
                $"발사 직전 잔탄 0발: 원 샷 쿨타임 {N(P(common, "empty_ammo_cooldown_reduction") * 100)}% 감소. 6발: 감소 없음. 중간 잔탄은 선형 보정.\n" +
                "발사 시 잔탄을 모두 소모하고 새 재장전 시작. 이 재장전은 현재 플레이어 공속에 비례해 진행.\n" +
                (Resolve(definition, level, 0, 1).ResetDodgeOnReload ? "이 재장전 완료 시 회피 쿨타임 초기화.\n" : "") +
                "플레이어 장착 성흔의 효과 단계로 적용.";
        }
    }
}
