using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace WesternLemegeton
{
    public sealed class HauntedBullAttack : EnemyAttackModule
    {
        public enum BullState { Wild, Ride }
        public enum ActionState { Chase, Stomp, Lock, Charge, Stunned }
        public BullState State { get; private set; }
        public ActionState Action { get; private set; }
        public float RiderHealth { get; private set; }
        public Enemy Rider => rider;
        // 상태별 시작 거리, 접촉 반경, 예고 길이입니다. 예고 폭은 반경의 2배이며 길이는 돌진 종료 거리와 별개입니다.
        [Header("Wild Attack Range")]
        [FormerlySerializedAs("TriggerRange"), Min(0)]
        public float WildTriggerRange = 6;
        [FormerlySerializedAs("HitRadius"), Min(.01f)]
        public float WildHitRadius = 1.1f;
        [FormerlySerializedAs("WarningLength"), Min(.1f)]
        public float WildWarningLength = 12;
        [Header("Ride Attack Range")]
        [Min(0)]
        public float RideTriggerRange = 6;
        [Min(.01f)]
        public float RideHitRadius = 1.1f;
        [Min(.1f)]
        public float RideWarningLength = 12;
        // 공통 돌진 설정과 애니메이션 이벤트입니다. Wild는 충돌까지, Ride는 지정 거리까지 돌진합니다.
        [Header("Charge Behaviour")]
        [Min(0)] public float StompSeconds = 3;
        [Min(0)] public float LockSeconds = 1;
        [Min(0)] public float ChargeSpeed = 12;
        [Min(.1f)] public float RideDistance = 8;
        [Min(0)] public float Damage = 22;
        [Min(.01f)] public float BodyRadius = .5f;
        [Min(0)] public float KnockbackDistance = 1;
        [Min(0)] public float StunSeconds = 3;
        [Min(0)] public float ChargeCooldown = 2;
        public UnityEvent StompStarted = new UnityEvent();
        public UnityEvent ChargeStarted = new UnityEvent();
        public UnityEvent Stunned = new UnityEvent();
        Enemy owner, rider;
        CowboyAttack riderAttack;
        float timer, travelled, cooldown, shotTimer;
        Vector2 aim = Vector2.right;
        public Vector2 Aim => aim;
        public override bool Telegraphing => Action == ActionState.Stomp || Action == ActionState.Lock;
        public override float AttackPower => Damage;
        public override float AttackRange => Mathf.Max(0, State == BullState.Ride ? RideTriggerRange : WildTriggerRange);
        float CurrentHitRadius => Mathf.Max(.01f, State == BullState.Ride ? RideHitRadius : WildHitRadius);
        float CurrentWarningLength => Mathf.Max(.1f, State == BullState.Ride ? RideWarningLength : WildWarningLength);

        // 사망 이벤트를 연결하고 Wild 추적 상태에서 행동을 시작합니다.
        public override void Initialize(Enemy enemy)
        {
            if (owner) owner.Dying -= OnDeath;
            owner = enemy;
            owner.Dying += OnDeath;
            State = BullState.Wild;
            Action = ActionState.Chase;
            timer = cooldown = travelled = 0;
        }

        // 준비 → 방향 확정 → 돌진 → 기절/추적 순으로 진행하며, 공격 중 일반 이동을 막습니다.
        public override bool Tick(Dungeon game, float speedMultiplier, float dt)
        {
            if (!game.Running || owner.Dead) return true;
            if (Action == ActionState.Stunned)
            {
                timer -= dt;
                if (timer <= 0) { Action = ActionState.Chase; HideWarning(); }
                return true;
            }
            if (Action == ActionState.Stomp)
            {
                Vector2 delta = game.Pos - (Vector2)transform.position;
                if (delta.sqrMagnitude > .0001f) aim = delta.normalized;
                ShowLine();
                if (State == BullState.Ride && riderAttack)
                {
                    shotTimer -= dt;
                    if (shotTimer <= 0)
                    {
                        riderAttack.FireFrom(transform.position, game.Pos);
                        shotTimer = Mathf.Max(.05f, riderAttack.ShotCooldown);
                    }
                }
                timer -= dt;
                if (timer <= 0) { Action = ActionState.Lock; timer = LockSeconds; }
                return true;
            }
            if (Action == ActionState.Lock)
            {
                // Ride는 마지막 준비 시간에도 추적하고, Wild만 방향을 고정합니다.
                if (State == BullState.Ride)
                {
                    Vector2 delta = game.Pos - (Vector2)transform.position;
                    if (delta.sqrMagnitude > .0001f) aim = delta.normalized;
                }
                // Wild는 방향을 유지하되 Inspector에서 변경한 범위 크기는 반영합니다.
                ShowLine();
                timer -= dt;
                if (timer <= 0)
                {
                    Action = ActionState.Charge; travelled = 0; HideWarning(); ChargeStarted.Invoke();
                }
                return true;
            }
            if (Action == ActionState.Charge)
            {
                float distance = Mathf.Max(0, ChargeSpeed * speedMultiplier * dt);
                if (State == BullState.Ride) distance = Mathf.Min(distance, Mathf.Max(0, RideDistance - travelled));
                int steps = Mathf.Max(1, Mathf.CeilToInt(distance / .1f));
                for (int i = 0; i < steps; i++)
                {
                    Vector2 p = transform.position, next = p + aim * (distance / steps);
                    // 슬라이딩하지 않고 벽/장애물과의 첫 접촉에서 돌진을 끝냅니다.
                    if (Blocked(next, BodyRadius)) { Crash(game); return true; }
                    transform.position = next;
                    travelled += distance / steps;
                    if (Vector2.Distance(next, game.Pos) <= CurrentHitRadius) game.Hero.Hurt(Damage);
                }
                if (State == BullState.Ride && travelled >= RideDistance - .0001f)
                { Action = ActionState.Chase; cooldown = ChargeCooldown; }
                return true;
            }
            cooldown -= dt;
            if (cooldown <= 0 && Vector2.Distance(transform.position, game.Pos) <= AttackRange)
            {
                aim = ((Vector2)game.Pos - (Vector2)transform.position).normalized;
                if (aim.sqrMagnitude < .001f) aim = Vector2.right;
                Action = ActionState.Stomp; timer = StompSeconds; shotTimer = 0;
                ShowLine(); StompStarted.Invoke(); return true;
            }
            return false;
        }

        // 황소 돌진과 Cowboy 접근에서 공유하는 방 경계·장애물 접촉 검사입니다.
        public static bool Blocked(Vector2 position, float radius)
        {
            if ((Rules.ClampToRoom(position, radius) - position).sqrMagnitude > .000001f) return true;
            foreach (Rect rect in Dungeon.Obstacles)
                if (Rect.MinMaxRect(rect.xMin - radius, rect.yMin - radius, rect.xMax + radius, rect.yMax + radius).Contains(position)) return true;
            return false;
        }

        // Cowboy 체력을 별도로 보관하고 독립 적 목록에서 제외한 뒤 탑승 상태로 전환합니다.
        public bool Mount(CowboyAttack cowboy)
        {
            if (State != BullState.Wild || !owner || owner.Dead || !cowboy || cowboy.Owner.Dead) return false;
            rider = cowboy.Owner; riderAttack = cowboy; RiderHealth = rider.Hp;
            State = BullState.Ride;
            cowboy.EnterMounted(this);
            Dungeon.I.Enemies.Remove(rider);
            rider.transform.SetParent(transform, true);
            rider.gameObject.SetActive(false);
            if (Telegraphing) ShowLine();
            return true;
        }

        // 충돌 후 뒤로 밀리고 탑승자를 하차시킨 뒤 원형 기절 표시를 켭니다.
        void Crash(Dungeon game)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(KnockbackDistance / .1f));
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = (Vector2)transform.position - aim * (KnockbackDistance / steps);
                if (Blocked(next, BodyRadius)) break;
                transform.position = next;
            }
            ReleaseRider(game);
            Action = ActionState.Stunned; timer = StunSeconds;
            if (owner.AuthoredWarning) owner.AuthoredWarning.sprite = Ink.Disc;
            owner.SetMeleeWarning(true, 1); Stunned.Invoke();
        }

        // 저장한 체력으로 탑승자를 복원하고, 적 목록 순회 중 변경을 피하도록 등록을 예약합니다.
        void ReleaseRider(Dungeon game)
        {
            if (!rider) { State = BullState.Wild; return; }
            var released = rider; var attack = riderAttack;
            rider = null; riderAttack = null; State = BullState.Wild;
            released.transform.SetParent(game.Scene ? game.Scene.EnemyContainer : null, true);
            released.transform.position = Rules.OutsideCover((Vector2)transform.position - aim * .8f, .5f);
            released.Hp = RiderHealth;
            attack.LeaveMounted(StunSeconds);
            released.gameObject.SetActive(true);
            game.QueueEnemy(released); // 범위 공격 중 리스트 변경을 피하면서 웨이브 종료 전에 등록합니다.
        }

        void OnDeath(Enemy enemy) { if (Dungeon.I) ReleaseRider(Dungeon.I); }
        void ShowLine()
        {
            // 원형 스프라이트를 늘리면 타원이 되므로 예고할 때 사각형으로 전환합니다.
            if (owner.AuthoredWarning) owner.AuthoredWarning.sprite = Ink.Square;
            float length = CurrentWarningLength;
            owner.SetAttackWarning(true, (Vector2)transform.position + aim * length * .5f,
                new Vector3(length, CurrentHitRadius * 2, 1), Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        }
        // 취소·비활성화 시 표시를 숨기고, 파괴 시 소유자의 이벤트 구독을 해제합니다.
        void HideWarning() { if (owner) owner.SetMeleeWarning(false, 1); }
        public override void Cancel() { HideWarning(); Action = ActionState.Chase; cooldown = ChargeCooldown; }
        void OnDisable() => HideWarning();
        void OnDestroy() { if (owner) owner.Dying -= OnDeath; }
    }
}
