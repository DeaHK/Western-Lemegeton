using UnityEngine;

namespace WesternLemegeton
{
    public sealed class EnemyChargeAttack : EnemyAttackModule
    {
        [Min(0)] public float Damage = 22;
        [Min(0)] public float TriggerRange = 6;
        [Min(0)] public float WindupSeconds = .85f;
        [Min(0)] public float CooldownSeconds = 2;
        [Min(0)] public float ChargeSeconds = .45f;
        [Min(0)] public float ChargeSpeed = 12;
        [Min(0)] public float HitRange = 1.15f;
        [Min(0)] public float BodyRadius = .5f;
        public Vector2 InitialCooldownRange = new Vector2(.8f, 2);
        public Vector3 WarningSize = new Vector3(6, 1, 1);
        public float WarningOffset = 2.8f;
        [Min(0)] public float TrailLength = 5.4f;

        Enemy owner;
        float cooldown, windup = -1, chargeTime;
        Vector2 aim;
        public override bool Telegraphing => windup > 0;
        public override float AttackPower => Damage;
        public override float AttackRange => TriggerRange;

        public override void Initialize(Enemy enemy)
        {
            owner = enemy;
            windup = -1;
            chargeTime = 0;
            cooldown = Random.Range(InitialCooldownRange.x, InitialCooldownRange.y);
        }

        public override bool Tick(Dungeon game, float speedMultiplier, float dt)
        {
            Vector2 position = transform.position;
            Vector2 delta = game.Pos - position;
            float distance = delta.magnitude;
            if (chargeTime > 0)
            {
                chargeTime -= dt;
                transform.position = Rules.ResolveObstacles(position,
                    position + aim * ChargeSpeed * speedMultiplier * dt, BodyRadius);
                // 기존처럼 이동 전 거리로 명중을 판정하고 돌진 중에는 대기시간을 줄이지 않습니다.
                if (distance < HitRange) game.Hero.Hurt(Damage);
                return true;
            }
            if (windup >= 0)
            {
                windup -= dt;
                if (windup <= 0)
                {
                    owner.SetAttackWarning(false, position, WarningSize, 0);
                    chargeTime = ChargeSeconds;
                    Ink.Line(position, position + aim * TrailLength, Ink.Red, .2f, .35f);
                    windup = -1;
                    cooldown = CooldownSeconds;
                }
                return true;
            }
            cooldown -= dt;
            if (cooldown > 0 || distance >= TriggerRange) return false;
            aim = delta.normalized;
            windup = WindupSeconds;
            owner.SetAttackWarning(true, position + aim * WarningOffset, WarningSize,
                Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            return true;
        }

        public override void Cancel()
        {
            windup = -1;
            chargeTime = 0;
            if (owner) owner.SetAttackWarning(false, transform.position, WarningSize, 0);
        }
        void OnDisable() => Cancel();
    }
}
