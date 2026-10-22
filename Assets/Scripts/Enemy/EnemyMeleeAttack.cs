using UnityEngine;

namespace WesternLemegeton
{
    // 공격 준비, 재사용 대기, 실제 명중 판정을 담당하는 근접 공격 부품입니다.
    [DisallowMultipleComponent]
    public sealed class EnemyMeleeAttack : EnemyAttackModule
    {
        [Min(0)] public float Damage = 15;
        [Min(0)] public float TriggerRange = 1.7f;
        [Min(0)] public float HitRange = 1.75f;
        [Min(0)] public float WindupSeconds = .65f;
        [Min(0)] public float CooldownSeconds = 1.5f;
        [Min(0)] public float WarningRadius = 1.55f;

        float cooldown;
        float windup = -1;
        Enemy owner;
        public override bool Telegraphing => windup > 0;
        public override float AttackPower => Damage;
        public override float AttackRange => TriggerRange;

        public override void Initialize(Enemy enemy)
        {
            owner = enemy;
            windup = -1;
            cooldown = Random.Range(.8f, 2);
        }

        // true이면 공격 동작 중이므로 행동 제어 부품이 이동을 멈춥니다.
        public bool Tick(Dungeon game, float dt)
            => Tick(game, 1, dt);

        public override bool Tick(Dungeon game, float speedMultiplier, float dt)
        {
            if (windup >= 0)
            {
                windup -= dt;
                if (windup <= 0)
                {
                    owner.SetMeleeWarning(false, WarningRadius);
                    Vector2 position = transform.position;
                    Ink.Ring(position, WarningRadius, Ink.Red);
                    // 준비 시작 때가 아니라 공격이 나가는 순간에 거리를 확인합니다.
                    if (Vector2.Distance(position, game.Pos) < HitRange) game.Hero.Hurt(Damage);
                    windup = -1;
                    cooldown = CooldownSeconds;
                }
                return true;
            }

            cooldown -= dt;
            if (cooldown > 0 || Vector2.Distance(transform.position, game.Pos) >= TriggerRange) return false;
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
