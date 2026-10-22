using UnityEngine;

namespace WesternLemegeton
{
    public sealed class EnemyRangedAttack : EnemyAttackModule
    {
        [Min(0)] public float Damage = 13;
        [Min(0)] public float TriggerRange = 8;
        [Min(0)] public float WindupSeconds = .9f;
        [Min(0)] public float CooldownSeconds = 1.5f;
        [Min(0)] public float ProjectileSpeed = 6.5f;
        [Min(0)] public float ProjectileLifetime = 3;
        [Min(1)] public int ProjectileCount = 3;
        public float SpreadAngle = 16;
        [Min(0)] public float WarningRadius = 1;
        public Vector2 InitialCooldownRange = new Vector2(.8f, 2);

        Enemy owner;
        float cooldown, windup = -1;
        Vector2 aim;
        public override bool Telegraphing => windup > 0;
        public override float AttackPower => Damage;
        public override float AttackRange => TriggerRange;

        public override void Initialize(Enemy enemy)
        {
            owner = enemy;
            windup = -1;
            cooldown = Random.Range(InitialCooldownRange.x, InitialCooldownRange.y);
        }

        public override bool Tick(Dungeon game, float speedMultiplier, float dt)
        {
            if (windup >= 0)
            {
                windup -= dt;
                if (windup <= 0)
                {
                    owner.SetMeleeWarning(false, WarningRadius);
                    Vector2 position = transform.position;
                    for (int i = 0; i < ProjectileCount; i++)
                    {
                        float angle = (i - (ProjectileCount - 1) * .5f) * SpreadAngle;
                        Bullet.Spawn(position, Quaternion.Euler(0, 0, angle) * aim * ProjectileSpeed,
                            Damage, true, ProjectileLifetime);
                    }
                    windup = -1;
                    cooldown = CooldownSeconds;
                }
                return true;
            }
            cooldown -= dt;
            Vector2 delta = game.Pos - (Vector2)transform.position;
            if (cooldown > 0 || delta.magnitude >= TriggerRange) return false;
            // 발사 순간에 다시 조준하지 않고 준비 시작 시 방향을 고정합니다.
            aim = delta.normalized;
            windup = WindupSeconds;
            owner.SetMeleeWarning(true, WarningRadius);
            return true;
        }

        public override void Cancel()
        {
            windup = -1;
            if (owner) owner.SetMeleeWarning(false, WarningRadius);
        }
        void OnDisable() => Cancel();
    }
}
