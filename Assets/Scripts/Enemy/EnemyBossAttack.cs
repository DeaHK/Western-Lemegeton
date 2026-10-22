using UnityEngine;

namespace WesternLemegeton
{
    // Brain에는 하나의 공격 부품으로 연결하고, 내부에서 켜진 패턴의 순서를 관리합니다.
    public sealed class EnemyBossAttack : EnemyAttackModule
    {
        public enum Pattern { None, Melee, Charge, Ranged }

        [System.Serializable]
        public sealed class MeleeSettings
        {
            [Min(0)] public float Damage = 15;
            [Min(0)] public float TriggerRange = 1.7f;
            [Min(0)] public float HitRange = 1.75f;
            [Min(0)] public float WindupSeconds = .65f;
            [Min(0)] public float CooldownSeconds = 1.5f;
            [Min(0)] public float WarningRadius = 1.55f;
        }

        [System.Serializable]
        public sealed class ChargeSettings
        {
            [Min(0)] public float Damage = 22;
            [Min(0)] public float TriggerRange = 8.5f;
            [Min(0)] public float WindupSeconds = .9f;
            [Min(0)] public float CooldownSeconds = 1.3f;
            [Min(0)] public float ChargeSeconds = .55f;
            [Min(0)] public float ChargeSpeed = 12;
            [Min(0)] public float HitRange = 1.15f;
            [Min(0)] public float BodyRadius = .5f;
            [Min(0)] public float WarningRadius = 2.5f;
        }

        [System.Serializable]
        public sealed class RangedSettings
        {
            [Min(0)] public float Damage = 17;
            [Min(0)] public float TriggerRange = 8.5f;
            [Min(0)] public float WindupSeconds = .9f;
            [Min(0)] public float CooldownSeconds = 1.3f;
            [Min(1)] public int ProjectileCount = 12;
            [Min(1)] public int LowHealthProjectileCount = 20;
            [Min(0)] public float ProjectileSpeed = 4.5f;
            [Min(0)] public float LowHealthProjectileSpeed = 6;
            [Range(0, 1)] public float LowHealthThreshold = .5f;
            [Min(0)] public float ProjectileLifetime = 5;
            public float RotationPerAttack = 13;
            [Min(0)] public float WarningRadius = 2.5f;
        }

        [Header("Enabled patterns: Ranged, Ranged, Charge, Melee")]
        public bool EnableMelee;
        public bool EnableCharge = true;
        public bool EnableRanged = true;
        public MeleeSettings Melee = new MeleeSettings();
        public ChargeSettings Charge = new ChargeSettings();
        public RangedSettings Ranged = new RangedSettings();

        public Vector2 InitialCooldownRange = new Vector2(.8f, 2);
        [Tooltip("Raven의 대상 우선순위 조회값입니다. 각 공격의 실제 피해량과 별개입니다.")]
        [Min(0)] public float ThreatPower = 25;
        [Tooltip("모든 공격을 껐을 때 일반 이동이 사용하는 기준 사거리입니다.")]
        [Min(0)] public float IdleApproachRange = 8.5f;

        static readonly Pattern[] Sequence = { Pattern.Ranged, Pattern.Ranged, Pattern.Charge, Pattern.Melee };
        Enemy owner;
        int cursor, activeSlot, cycle;
        Pattern activePattern;
        float cooldown, windup = -1, chargeTime;
        Vector2 aim;

        public override bool Telegraphing => windup > 0 && IsEnabled(activePattern);
        public override float AttackPower => EnableMelee || EnableCharge || EnableRanged ? ThreatPower : 0;
        public override float AttackRange
        {
            get
            {
                Pattern pattern = IsEnabled(activePattern) ? activePattern : NextPattern(out _);
                return RangeFor(pattern);
            }
        }

        public override void Initialize(Enemy enemy)
        {
            owner = enemy;
            cursor = activeSlot = cycle = 0;
            activePattern = Pattern.None;
            windup = -1;
            chargeTime = 0;
            cooldown = Random.Range(InitialCooldownRange.x, InitialCooldownRange.y);
        }

        bool IsEnabled(Pattern pattern) => pattern == Pattern.Melee ? EnableMelee :
            pattern == Pattern.Charge ? EnableCharge : pattern == Pattern.Ranged && EnableRanged;

        Pattern NextPattern(out int slot)
        {
            for (int offset = 0; offset < Sequence.Length; offset++)
            {
                slot = (cursor + offset) % Sequence.Length;
                if (IsEnabled(Sequence[slot])) return Sequence[slot];
            }
            slot = 0;
            return Pattern.None;
        }

        float RangeFor(Pattern pattern) => pattern == Pattern.Melee ? Melee.TriggerRange :
            pattern == Pattern.Charge ? Charge.TriggerRange : pattern == Pattern.Ranged ? Ranged.TriggerRange : IdleApproachRange;
        float WindupFor(Pattern pattern) => pattern == Pattern.Melee ? Melee.WindupSeconds :
            pattern == Pattern.Charge ? Charge.WindupSeconds : Ranged.WindupSeconds;
        float CooldownFor(Pattern pattern) => pattern == Pattern.Melee ? Melee.CooldownSeconds :
            pattern == Pattern.Charge ? Charge.CooldownSeconds : Ranged.CooldownSeconds;
        float WarningFor(Pattern pattern) => pattern == Pattern.Melee ? Melee.WarningRadius :
            pattern == Pattern.Charge ? Charge.WarningRadius : Ranged.WarningRadius;

        public override bool Tick(Dungeon game, float speedMultiplier, float dt)
        {
            // 진행 중인 종류를 끄면 다음 갱신에서 준비·돌진·경고를 취소합니다.
            if (activePattern != Pattern.None && !IsEnabled(activePattern))
            {
                Cancel();
                return true;
            }
            Vector2 position = transform.position;
            Vector2 delta = game.Pos - position;
            float distance = delta.magnitude;
            if (chargeTime > 0)
            {
                chargeTime -= dt;
                transform.position = Rules.ResolveObstacles(position,
                    position + aim * Charge.ChargeSpeed * speedMultiplier * dt, Charge.BodyRadius);
                // 기존 보스처럼 이동 전 거리로 명중을 판정하고 돌진 중에는 대기시간을 줄이지 않습니다.
                if (distance < Charge.HitRange) game.Hero.Hurt(Charge.Damage);
                if (chargeTime <= 0) activePattern = Pattern.None;
                return true;
            }
            if (windup >= 0)
            {
                windup -= dt;
                if (windup <= 0)
                {
                    owner.SetMeleeWarning(false, WarningFor(activePattern));
                    cycle++;
                    cursor = (activeSlot + 1) % Sequence.Length;
                    cooldown = CooldownFor(activePattern);
                    Execute(position, game);
                    windup = -1;
                    if (chargeTime <= 0) activePattern = Pattern.None;
                }
                return true;
            }
            cooldown -= dt;
            Pattern next = NextPattern(out int slot);
            if (next == Pattern.None || cooldown > 0 || distance >= RangeFor(next)) return false;
            activePattern = next;
            activeSlot = slot;
            aim = delta.normalized;
            windup = WindupFor(next);
            owner.SetMeleeWarning(true, WarningFor(next));
            return true;
        }

        void Execute(Vector2 position, Dungeon game)
        {
            if (activePattern == Pattern.Charge)
            {
                chargeTime = Charge.ChargeSeconds;
                Ink.Ring(position, Charge.WarningRadius, Ink.Red);
            }
            else if (activePattern == Pattern.Melee)
            {
                Ink.Ring(position, Melee.WarningRadius, Ink.Red);
                if (Vector2.Distance(position, game.Pos) < Melee.HitRange) game.Hero.Hurt(Melee.Damage);
            }
            else if (activePattern == Pattern.Ranged)
            {
                // 강화 여부는 준비 시작이 아니라 실제 발사 시점에 판정합니다.
                bool lowHealth = owner.Hp < owner.MaxHp * Ranged.LowHealthThreshold;
                int count = Mathf.Max(1, lowHealth ? Ranged.LowHealthProjectileCount : Ranged.ProjectileCount);
                float speed = lowHealth ? Ranged.LowHealthProjectileSpeed : Ranged.ProjectileSpeed;
                for (int i = 0; i < count; i++)
                {
                    float angle = (i / (float)count * 360 + cycle * Ranged.RotationPerAttack) * Mathf.Deg2Rad;
                    Bullet.Spawn(position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                        Ranged.Damage, true, Ranged.ProjectileLifetime);
                }
            }
        }

        public override void Cancel()
        {
            windup = -1;
            chargeTime = 0;
            activePattern = Pattern.None;
            if (owner) owner.SetMeleeWarning(false, 0);
        }
        void OnDisable() => Cancel();
    }
}
