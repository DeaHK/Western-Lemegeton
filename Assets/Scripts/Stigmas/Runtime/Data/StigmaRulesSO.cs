using UnityEngine;

namespace WesternLemegeton.Stigmas
{
    [CreateAssetMenu(menuName = "Western Lemegeton/Stigma/Rules")]
    public sealed class StigmaRulesSO : ScriptableObject
    {
        public const int MaximumLevel = 3;
        public const int VisibleSlots = 6;
        [Tooltip("1 = once per enemy; 0 = repeatable. Consumed only by successful charge grants.")]
        [Min(0)] public int MaxChargeGrantsPerEnemy = 1;
        [Range(0, 1)] public float MaximumSlowFraction = .5f;
        public KeyCode LinkSkillKey = KeyCode.G;
        public KeyCode DebugKey = KeyCode.F9;
        [Tooltip("Replacement is free at every level. Upgrade costs are separate.")]
        public int[] ReplacementCosts = { 0, 0, 0 };
        [Tooltip("Provisional copper costs: 1 -> 2, 2 -> 3.")]
        public int[] UpgradeCosts = { 400, 1000 };

        [Header("Provisional combat tuning — design values still unspecified")]
        [Min(0)] public int EnemyCopper = 50, BossCopper = 500;
        [Min(0)] public float KnockbackDistance = .35f;
        [Min(0)] public float BaseHitStunSeconds = .15f;
        public float[] FloodRadius = { 6, 7, 8 };
        public float[] FloodSeconds = { 4, 5, 5 };
        public float[] FloodDashRefund = { .3f, .4f, .5f };
        public float[] BloomStunReduction = { 10, 15, 20 };
        public float BloomKillRefill = 3;
        public float[] KillChainSpeedStep = { 3, 4, 5 };
        public int KillChainMaxStacks = 3;
        public float KillChainSeconds = 5;
        public float RushLevel3Seconds = 5, RushLevel3PenaltySeconds = 1;
        public float FamiliarInterval = 1, FamiliarRange = 14, FamiliarExplosionRadius = 3, FamiliarExplosionPercent = 60;
        public float[] MireRadius = { 4, 5, 6 };
        public float[] InfernoRadius = { 5, 6, 7 };
        public float InfernoTargetRange = 14, GroundTickSeconds = .5f, GroundAttackPercent = 30;
        public float LinkBaseAttack = 20, MireForwardDistance = 4;

        // Fixed gameplay contracts shared by authoring, runtime and debug adapters.
        public bool ChargeSurvivesRoomAndStageTravel => true;
        public bool ClearChargeOnDeath => true;
        public bool AddAttackSpeedAndAttackBonuses => true;
        public bool BonusHpCapUsesLargestApplicableGrant => true;
        public bool ReplacementClearsStigmaBuffsDebuffsBonusHpAndSummons => true;
        public bool PlayerSkillsAlsoLeaveMarks => true;
        public bool FamiliarKillsBelongToPlayer => true;
        public int ClampChargeAfterReplacement(int charge, int newCapacity) =>
            Mathf.Clamp(charge, 0, Mathf.Max(0, newCapacity));
    }
}
