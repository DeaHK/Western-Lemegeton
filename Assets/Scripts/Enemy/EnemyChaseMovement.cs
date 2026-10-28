using UnityEngine;

namespace WesternLemegeton
{
    // 공격 방식과 관계없이 목표를 추적하는 이동 부품입니다.
    [DisallowMultipleComponent]
    public sealed class EnemyChaseMovement : EnemyMovementModule
    {
        [Min(0)] public float Speed = 2.2f;
        [Min(0)] public float StopDistance = 1.36f;
        [Min(0)] public float BodyRadius = .5f;
        [Min(0)] public float SeparationRadius = 1.183216f;
        [Min(0)] public float SeparationStrength = .7f;

        public override void Tick(Enemy owner, Dungeon game, Vector2 target, float speedMultiplier, float dt)
        {
            Vector2 position = transform.position;
            Vector2 delta = target - position;
            Vector2 movement = delta.magnitude > StopDistance ? delta.normalized : Vector2.zero;
            foreach (var other in game.Enemies)
            {
                if (!other || other == owner || other.Dead || other.AuxiliaryTarget) continue;
                Vector2 away = position - (Vector2)other.transform.position;
                if (away.sqrMagnitude < SeparationRadius * SeparationRadius && away.sqrMagnitude > .01f)
                    movement += away.normalized * SeparationStrength;
            }
            Vector2 next = position + Vector2.ClampMagnitude(movement, 1) * Speed * speedMultiplier * dt;
            transform.position = Rules.ResolveObstacles(position, next, BodyRadius);
        }
    }
}
