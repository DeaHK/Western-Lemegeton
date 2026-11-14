using UnityEngine;

namespace WesternLemegeton
{
    public sealed partial class LoganBossAttack
    {
        [System.Serializable]
        public sealed class ShotgunSettings
        {
            [Range(0, 2)] public int CrowdControl = 1;
            [Tooltip("총 휘두르기 넉백·기절 전용 설정")]
            [Range(0, 2)] public int SwingCrowdControl = 2;
            [Min(0)] public float RecoverySeconds = .5f;
            [Min(0)] public float TriggerRange = 8;
            [Min(0)] public float AimSeconds = 1;
            [Min(0)] public float Damage = 10;
            [Range(0,360)] public float SwingAngle = 100;
            [Min(0)] public float SwingRange = 3;
            [Min(0)] public float SwingSeconds = .3f;
            [Tooltip("총 휘두르기 시작 시 실제 판정 범위를 표시하는 시간. 공격 선딜레이와는 별개입니다.")]
            [Min(0)] public float SwingRangeDisplaySeconds = .5f;
            [Min(0)] public float KnockbackDistance = 1;
            [Min(0)] public float StunSeconds = .5f;
            [Min(1)] public int PelletCount = 7;
            [Range(0,180)] public float SpreadAngle = 20;
            [Min(.01f)] public float BulletDistance = 8;
            [Min(.01f)] public float BulletSpeed = 12;
        }
        [System.Serializable]
        public sealed class WhipSettings
        {
            [Range(0, 2)] public int CrowdControl = 1;
            [Min(0)] public float RecoverySeconds = .5f;
            [Min(0)] public float WindupSeconds = 1;
            [Min(0)] public float Damage = 20;
            [Min(.01f)] public float HitInterval = .5f;
            [Min(.01f)] public float Duration = 3;
            [Tooltip("감속률. -0.5는 속도 1.5배, 0.5는 속도 0.5배입니다.")]
            [Range(-3,1)] public float SlowFraction = -.5f;
            [Min(0)] public float AttackRadius = 3;
            [Min(0)] public float Cooldown = 8;
        }
        [System.Serializable]
        public sealed class DynamiteSettings
        {
            [Range(0, 2)] public int CrowdControl = 1;
            [Min(0)] public float RecoverySeconds = .5f;
            [Min(0)] public float TriggerRange = 14;
            [Min(0)] public float WindupSeconds = 1;
            [Min(0)] public float Damage = 90;
            [Min(0)] public int AdditionalCount = 2;
            [Min(0)] public float ScatterRadius = 2;
            [Min(0)] public float FuseSeconds = 3;
            [Min(.01f)] public float FlightSpeed = 8;
            [Min(0)] public float ArcHeight = 2;
            [Min(.01f)] public float ExplosionRadius = 2;
            [Min(0)] public float Cooldown = 10;
        }
        public bool EnableShotgun = true, EnableWhip = true, EnableDynamite = true;
        [Tooltip("2페이즈 기본 이동 배율. 채찍 사용 중에는 채찍 배율과 곱해집니다.")]
        [Min(0)] public float PhaseTwoMoveMultiplier = 1.2f;
        public ShotgunSettings Shotgun = new ShotgunSettings();
        public WhipSettings Whip = new WhipSettings();
        public DynamiteSettings Dynamite = new DynamiteSettings();
        float whipCooldown, dynamiteCooldown;
        Vector2 swingDirection;
        bool swingHit;
        EnemyChaseMovement whipMovement;
        float savedStopDistance;
        EnemyKeepDistanceMovement whipKeepDistance;
        bool savedUseAttackRange;
        float savedApproachRange, savedApproachFactor, savedRetreatDistance;
        void BeginWhipMovement()
        {
            whipMovement=GetComponent<EnemyChaseMovement>();
            if(whipMovement) { savedStopDistance=whipMovement.StopDistance; whipMovement.StopDistance=0; }
            whipKeepDistance=GetComponent<EnemyKeepDistanceMovement>();
            if(whipKeepDistance)
            {
                savedUseAttackRange=whipKeepDistance.UseAttackRange;
                savedApproachRange=whipKeepDistance.ApproachRange;
                savedApproachFactor=whipKeepDistance.ApproachRangeFactor;
                savedRetreatDistance=whipKeepDistance.RetreatDistance;
                whipKeepDistance.UseAttackRange=false;
                whipKeepDistance.ApproachRange=0;
                whipKeepDistance.ApproachRangeFactor=1;
                whipKeepDistance.RetreatDistance=0;
            }
        }
        void RestoreWhipMovement()
        {
            if(whipMovement) whipMovement.StopDistance=savedStopDistance;
            whipMovement=null;
            if(whipKeepDistance)
            {
                whipKeepDistance.UseAttackRange=savedUseAttackRange;
                whipKeepDistance.ApproachRange=savedApproachRange;
                whipKeepDistance.ApproachRangeFactor=savedApproachFactor;
                whipKeepDistance.RetreatDistance=savedRetreatDistance;
            }
            whipKeepDistance=null;
        }
        public float WhipCooldownRemaining => whipCooldown;
        public float DynamiteCooldownRemaining => dynamiteCooldown;
        public float SkillMovementMultiplier => (Phase==2 ? Mathf.Max(0,PhaseTwoMoveMultiplier) : 1) *
            (ActivePattern == Pattern.Whip && Action == ActionState.Active ? Mathf.Max(0,1-Whip.SlowFraction) : 1);

        bool WhipReady(Dungeon game) => EnableWhip && whipCooldown <= 0 && Vector2.Distance(transform.position,game.Pos) <= WhipRange;
        bool CanThrowAt(Vector2 target) => (!EnableWhip || whipCooldown >= 1) || Vector2.Distance(transform.position,target) > Dynamite.ExplosionRadius;
        bool DynamiteReady(Dungeon game) => EnableDynamite && dynamiteCooldown <= 0 &&
            Vector2.Distance(transform.position,game.Pos) <= Dynamite.TriggerRange && CanThrowAt(game.Pos);
        Pattern ReadySkill(Dungeon game) => WhipReady(game) ? Pattern.Whip : DynamiteReady(game) ? Pattern.Dynamite : Pattern.None;
        bool ShotgunInRange(Dungeon game) => Vector2.Distance(transform.position,game.Pos) <= Shotgun.TriggerRange;
        bool EnabledPhaseTwo(Pattern p) => p == Pattern.Shotgun ? EnableShotgun : p == Pattern.Whip ? EnableWhip : p == Pattern.Dynamite && EnableDynamite;

        public bool SignalShotgun() => RequestPhaseTwo(Pattern.Shotgun, false);
        public bool SignalWhip() => RequestPhaseTwo(Pattern.Whip, false);
        public bool SignalDynamite() => RequestPhaseTwo(Pattern.Dynamite, false);
        bool RequestPhaseTwo(Pattern pattern, bool automatic)
        {
            var game = Dungeon.I;
            if (!game || !game.Running || !owner || owner.Dead || Phase != 2 || transitioning || !EnabledPhaseTwo(pattern)) return false;
            if (dodging) return false;
            Pattern skill = ReadySkill(game);
            if (pattern != Pattern.Shotgun && skill != pattern) return false;
            if (pattern == Pattern.Shotgun && (skill != Pattern.None || Vector2.Distance(transform.position,game.Pos) > Shotgun.TriggerRange || (automatic && !ShotgunInRange(game)))) return false;
            if (ActivePattern != Pattern.None)
            {
                if (pattern == Pattern.Shotgun || ActivePattern != Pattern.Shotgun ||
                    (Action != ActionState.Aim && Action != ActionState.Swing)) return false;
                // 조준 시간과 경고를 먼저 정리하여 취소된 평타가 뒤늦게 발사되지 않게 합니다.
                CancelAttack();
            }
            ActivePattern = pattern; elapsed = nextShot = 0; swingHit = false;
            owner.SetMeleeWarning(false,0);
            if (pattern != Pattern.Shotgun) { Action = ActionState.Windup; return true; }
            Action = ActionState.Aim;
            swingDirection = (game.Pos-(Vector2)transform.position).normalized;
            if (swingDirection.sqrMagnitude < .001f) swingDirection=Vector2.right;
            if (Vector2.Distance(transform.position,game.Pos) <= WhipRange)
            {
                Action = ActionState.Swing;
                ShowShotgunSwingRange();
            }
            return true;
        }
        // 고정된 휘두르기 방향과 판정 반경으로 부채꼴의 양 끝과 바깥 호를 표시합니다.
        void ShowShotgunSwingRange()
        {
            float life = Mathf.Max(0, Shotgun.SwingRangeDisplaySeconds);
            float radius = Mathf.Max(0, Shotgun.SwingRange);
            if (life <= 0 || radius <= 0) return;
            Vector2 center = transform.position;
            float halfAngle = Mathf.Clamp(Shotgun.SwingAngle, 0, 360) * .5f;
            Vector2 previous = center + (Vector2)(Quaternion.Euler(0, 0, -halfAngle) * swingDirection) * radius;
            Ink.Line(center, previous, Ink.Gold, .06f, life);
            int segments = Mathf.Max(1, Mathf.CeilToInt(halfAngle * 2 / 5));
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                Vector2 next = center + (Vector2)(Quaternion.Euler(0, 0, angle) * swingDirection) * radius;
                Ink.Line(previous, next, Ink.Gold, .06f, life);
                previous = next;
            }
            Ink.Line(center, previous, Ink.Gold, .06f, life);
        }
        bool TickPhaseTwo(Dungeon game, float speedMultiplier, float dt)
        {
            whipCooldown = Mathf.Max(0,whipCooldown-dt);
            dynamiteCooldown = Mathf.Max(0,dynamiteCooldown-dt);
            if (ActivePattern != Pattern.None && !EnabledPhaseTwo(ActivePattern)) CancelAttack();
            if (ActivePattern == Pattern.Shotgun && (Action == ActionState.Aim || Action == ActionState.Swing))
            {
                var ready = ReadySkill(game);
                if (ready != Pattern.None && RequestPhaseTwo(ready,true)) return true;
            }
            if (ActivePattern == Pattern.None)
            {
                var next=ReadySkill(game);
                RequestPhaseTwo(next == Pattern.None ? Pattern.Shotgun : next,true);
                return ActivePattern != Pattern.None;
            }
            if (ActivePattern == Pattern.Shotgun)
            {
                if (Action == ActionState.Swing)
                {
                    elapsed += dt;
                    // 현재 부채꼴 안의 헌터를 한 번만 밀어냅니다. 총 휘두르기는 피해 없는 제어 공격입니다.
                    Vector2 delta=game.Pos-(Vector2)transform.position;
                    if (!swingHit && delta.magnitude<=Shotgun.SwingRange && Vector2.Angle(swingDirection,delta)<=Shotgun.SwingAngle*.5f)
                        swingHit=game.Hero.TryApplyImpactControl(delta,Shotgun.KnockbackDistance,Shotgun.StunSeconds,Shotgun.SwingCrowdControl);
                    float angle=Mathf.Lerp(-Shotgun.SwingAngle*.5f,Shotgun.SwingAngle*.5f,Mathf.Clamp01(elapsed/Mathf.Max(.01f,Shotgun.SwingSeconds)));
                    Vector2 direction=Quaternion.Euler(0,0,angle)*swingDirection;
                    Ink.Line(transform.position,(Vector2)transform.position+direction*Shotgun.SwingRange,Ink.Gold,.12f,.08f);
                    if(elapsed>=Shotgun.SwingSeconds) { Action=ActionState.Aim; elapsed=0; }
                    return true;
                }
                LockedAim=CombatBody.For(game.Hero).Center;
                elapsed+=dt;
                if(elapsed>=Shotgun.AimSeconds)
                {
                    // 이미 시작한 샷건은 조준과 발사를 마친 뒤 다음 패턴을 선택합니다.
                    int count=Mathf.Max(1,Shotgun.PelletCount);
                    for(int i=0;i<count;i++)
                    {
                        float angle=count==1?0:Mathf.Lerp(-Shotgun.SpreadAngle*.5f,Shotgun.SpreadAngle*.5f,i/(float)(count-1));
                        float speed=Mathf.Max(.01f,Shotgun.BulletSpeed);
                        FireGunBullet(LockedAim,speed,angle,Shotgun.Damage,Mathf.Max(.01f,Shotgun.BulletDistance)/speed,Shotgun.CrowdControl);
                    }
                    BeginRecovery();
                }
                return true;
            }
            if (ActivePattern == Pattern.Whip)
            {
                owner.SetMeleeWarning(true,Whip.AttackRadius);
                if(Action==ActionState.Windup)
                {
                    elapsed+=dt;
                    if(elapsed<Whip.WindupSeconds) return true;
                    Action=ActionState.Active; elapsed=nextShot=0;
                    BeginWhipMovement();
                }
                else elapsed+=dt;
                while(nextShot<Mathf.Max(.01f,Whip.Duration) && nextShot<=elapsed)
                {
                    if(Vector2.Distance(transform.position,game.Pos)<=Whip.AttackRadius) game.Hero.HurtOverTime(Whip.Damage,Whip.CrowdControl);
                    Ink.Ring(transform.position,Whip.AttackRadius,Ink.Red,.15f);
                    nextShot+=Mathf.Max(.01f,Whip.HitInterval);
                }
                if(elapsed>=Whip.Duration) { BeginRecovery(); return true; }
                // false를 반환하면 공통 Brain이 감속률을 반영한 속도로 추적 이동을 실행합니다.
                return false;
            }
            elapsed+=dt;
            if(elapsed>=Dynamite.WindupSeconds)
            {
                Vector2 target=game.Pos;
                if(CanThrowAt(target)) LoganDynamiteVolley.Spawn(transform.position,target,Dynamite);
                BeginRecovery();
            }
            return true;
        }
    }
}
