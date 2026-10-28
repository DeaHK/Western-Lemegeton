using UnityEngine;

namespace WesternLemegeton
{
    // RobeMan 전용 공격 부품. 이동/체력은 기존 공통 부품을 사용합니다.
    public sealed class RobeManAttack : EnemyAttackModule, ISlowMovementSkill
    {
        public enum Phase { Chase, MeleeWindup, MeleeRecovery, Aim, ThrowRecovery }
        // 독병 사용 조건과 근접·투척·독 위험구역의 성능을 기능별로 설정합니다.
        [Header("독병 사용 조건")]
        [Min(0)] public float DetectionRadius = 3;
        [Min(0)] public float RequiredStaySeconds = 5;
        [Header("근접 공격")]
        [Min(0)] public float MeleeRange = 1.7f;
        [Min(0)] public float MeleeHitRange = 1.75f;
        [Min(0)] public float MeleeDamage = 15;
        [Min(0)] public float MeleeWindup = .65f;
        [Min(0)] public float MeleeRecovery = .25f;
        [Min(0)] public float MeleeCooldown = 1.5f;
        [Header("조준 / 투척")]
        [Min(0)] public float AimDelay = 1;
        [Min(.01f)] public float FlightSeconds = .8f;
        [Min(0)] public float ArcHeight = 3;
        [Min(0)] public float ThrowRecovery = .4f;
        [Range(0, 1)] public float PoisonMoveMultiplier = .35f;
        [Header("독 위험구역 (임시 표시)")]
        [Min(.01f)] public float PoisonRadius = 4;
        [Min(.01f)] public float PoisonLifetime = 6;
        [Min(0)] public float PoisonDamage = 5;
        [Min(.01f)] public float PoisonInterval = 1;

        Enemy owner;
        float timer, meleeCooldown, stay;
        bool poisonReady;
        SpriteRenderer marker;
        public Phase CurrentPhase { get; private set; }
        public Vector2 LockedTarget { get; private set; }
        public float StaySeconds => stay;
        // 조준부터 투척 후딜까지 감속합니다. 독병 비행과 독 구역의 수명은 스킬 사용 시간과 별개입니다.
        public float SkillMovementMultiplier =>
            CurrentPhase == Phase.Aim || CurrentPhase == Phase.ThrowRecovery ? PoisonMoveMultiplier : 1;
        public override bool Telegraphing => CurrentPhase == Phase.Aim || CurrentPhase == Phase.MeleeWindup;
        public override float AttackPower => Mathf.Max(MeleeDamage, PoisonDamage);
        public override float AttackRange => MeleeRange;

        // 남은 조준 표시와 공격 상태를 정리하고 새 소유자에 연결합니다.
        public override void Initialize(Enemy enemy)
        {
            Cancel();
            owner = enemy;
            meleeCooldown = 0;
        }

        // 체류 조건을 누적하면서 근접 준비·후딜·고정 조준·투척 후 대기를 순서대로 처리합니다.
        public override bool Tick(Dungeon game, float speedMultiplier, float dt)
        {
            if (!game || !game.Running || !game.Hero || !owner || owner.Dead) return true;
            dt = Mathf.Max(0, dt);
            meleeCooldown = Mathf.Max(0, meleeCooldown - dt);
            // 근접 공격 중에도 체류 시간을 셉니다. 5초 달성은 공격 종료까지 기억합니다.
            if (CurrentPhase == Phase.Chase || CurrentPhase == Phase.MeleeWindup || CurrentPhase == Phase.MeleeRecovery)
            {
                if (Vector2.Distance(transform.position, game.Pos) <= DetectionRadius)
                {
                    stay += dt;
                    if (stay >= RequiredStaySeconds) poisonReady = true;
                }
                else if (!poisonReady) stay = 0;
            }

            // 타격 시점의 거리로 명중을 판정한 뒤 후딜이 끝나야 다음 공격으로 넘어갑니다.
            if (CurrentPhase == Phase.MeleeWindup)
            {
                timer -= dt;
                if (timer <= 0)
                {
                    owner.SetMeleeWarning(false, MeleeRange);
                    if (Vector2.Distance(transform.position, game.Pos) <= MeleeHitRange) game.Hero.Hurt(MeleeDamage);
                    CurrentPhase = Phase.MeleeRecovery;
                    timer = MeleeRecovery;
                    if (timer <= 0) FinishMelee(game);
                }
                return CurrentPhase != Phase.Aim;
            }
            if (CurrentPhase == Phase.MeleeRecovery)
            {
                timer -= dt;
                if (timer <= 0) FinishMelee(game);
                return CurrentPhase != Phase.Aim;
            }
            // 조준 목표는 고정한 채 감속 이동하고, 현재 위치에서 독병을 던진 뒤 후딜까지 감속을 유지합니다.
            if (CurrentPhase == Phase.Aim)
            {
                timer -= dt;
                if (timer <= 0)
                {
                    RobeManPoisonBottle.Spawn(transform.position, LockedTarget, FlightSeconds, ArcHeight,
                        PoisonRadius, PoisonLifetime, PoisonDamage, PoisonInterval, marker);
                    marker = null; // 조준 표시는 이제 독병이 관리합니다.
                    CurrentPhase = Phase.ThrowRecovery;
                    timer = ThrowRecovery;
                }
                return false; // 근접 공격은 재개하지 않고 공통 Brain을 통해 감속 이동합니다.
            }
            if (CurrentPhase == Phase.ThrowRecovery)
            {
                timer -= dt;
                if (timer <= 0) CurrentPhase = Phase.Chase;
                return false;
            }
            if (poisonReady)
            {
                BeginAim(game);
                return false;
            }
            if (meleeCooldown <= 0 && Vector2.Distance(transform.position, game.Pos) <= MeleeRange)
            {
                CurrentPhase = Phase.MeleeWindup;
                timer = MeleeWindup;
                owner.SetMeleeWarning(true, MeleeRange);
                return true;
            }
            return false; // Brain이 추적 이동 부품을 실행하도록 허용합니다.
        }

        // 근접 후딜이 끝나면 대기 중인 독병 조건을 우선 실행합니다.
        void FinishMelee(Dungeon game)
        {
            meleeCooldown = MeleeCooldown;
            CurrentPhase = Phase.Chase;
            if (poisonReady) BeginAim(game);
        }

        // 조준 시작 시 Hunter 위치를 고정하고 체류 조건을 소비해 예고 표시를 생성합니다.
        void BeginAim(Dungeon game)
        {
            LockedTarget = game.Pos; // 현재 위치를 한 번 복사합니다. 이후 Hunter를 따라가지 않습니다.
            CurrentPhase = Phase.Aim;
            timer = AimDelay;
            stay = 0;
            poisonReady = false;
            marker = RobeManPoisonZone.CreateCircle("RobeMan / Locked aim", LockedTarget,
                PoisonRadius, new Color(1, .65f, .1f, .25f));
        }

        // 취소 시 아직 던지지 않은 조준 표시만 정리합니다. 이미 생성된 독병과 독 구역은 독립적으로 유지됩니다.
        public override void Cancel()
        {
            CurrentPhase = Phase.Chase;
            stay = timer = 0;
            poisonReady = false;
            if (owner) owner.SetMeleeWarning(false, MeleeRange);
            if (marker) RobeManPoisonZone.Remove(marker.gameObject);
            marker = null;
        }
        void OnDisable() => Cancel();
    }
}
